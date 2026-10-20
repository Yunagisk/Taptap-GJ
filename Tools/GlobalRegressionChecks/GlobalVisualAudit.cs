#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GlobalVisualAudit
{
    const string Pending = "GlobalVisualAudit.Pending";
    static GlobalVisualAudit()
    {
        EditorApplication.playModeStateChanged += change => {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            var go = new GameObject("GlobalVisualAudit");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<GlobalVisualAuditRunner>();
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated audit only");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
}

public sealed class GlobalVisualAuditRunner : MonoBehaviour
{
    readonly string output = Path.GetFullPath("../global-visual");
    readonly List<string> checks = new List<string>();
    readonly List<string> errors = new List<string>();
    readonly List<TextCandidate> candidates = new List<TextCandidate>();
    readonly List<string> observations = new List<string>();
    [Serializable] public sealed class TextCandidate { public string screen, path, text, reason, overflowMode; public float fontPixels; public bool insideScroll; }
    [Serializable] public sealed class Result { public bool passed; public string[] checks, errors, observations; public TextCandidate[] textCandidates; }
    void Awake() { Directory.CreateDirectory(output); Application.logMessageReceived += Logged; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add(message); }
    IEnumerator Start()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Exercise());
        while (stack.Count > 0)
        {
            object next;
            try { if (!stack.Peek().MoveNext()) { stack.Pop(); continue; } next = stack.Peek().Current; }
            catch (Exception e) { errors.Add(e.ToString()); Finish(false); yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Finish(errors.Count == 0);
    }
    void Finish(bool ok)
    {
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result {
            passed = ok, checks = checks.ToArray(), errors = errors.ToArray(),
            observations = observations.ToArray(), textCandidates = candidates.ToArray() }, true));
        EditorApplication.Exit(ok ? 0 : 1);
    }
    IEnumerator Exercise()
    {
        yield return null; yield return null;
        var opening = FindObjectOfType<MainMenuOpening>();
        if (opening != null) { opening.SkipOpening(); yield return null; yield return null; }
        var flow = FindObjectOfType<GameFlowController>();
        var template = flow.template;
        Check(template != null && flow.isMainMenu, "installed main menu loads");
        observations.Add("Main menu continue interactable=" + flow.continueButton.interactable
            + "; weapon selection page active=" + flow.weaponPage.activeInHierarchy);
        foreach (var size in Sizes())
        {
            yield return Size(size.x, size.y);
            CaptureAndScan("menu-" + size.x + "x" + size.y);
            CheckReadableText(flow.mainPage.transform);
            flow.OpenSettings(); yield return null;
            CaptureAndScan("settings-" + size.x + "x" + size.y);
            flow.CloseSettings();
        }
        GameState state = null;
        for (int seed = 0; seed < 1000; seed++)
        {
            var fresh = NewRunFactory.Create(template.Load(), "sword", seed);
            if (!fresh.world.villageEvents.occurrences.Any(v => v.definitionId == "v01")) continue;
            state = fresh; break;
        }
        Check(state != null, "village reward event has an ordinary generated seed");
        state.story.useVillageIntroduction = true; state.story.guideCompleted = true;
        state.story.guideEquipmentReady = state.story.guideOutsideReady = true;
        state.story.stage = MainStoryStage.TutorialCompleted; state.story.slothHookSeen = true;
        state.player.hp = 55; state.player.lanternCharges = 1;
        state.world.sinRemnant = 50;
        state.inventory.weapons.AddRange(new[] { "axe", "hammer", "torch" });
        for (int i = 0; i < 4; i++) EquipmentRules.TryEquip(state, i, new[] { "sword", "axe", "hammer", "torch" }[i]);
        GameRoot.I.state = state; GameRoot.I.villageMapTemplate = template;
        var loading = SceneManager.LoadSceneAsync("Village"); while (!loading.isDone) yield return null;
        yield return null; yield return null;
        var village = FindObjectOfType<VillageScreen>();
        Check(village != null && village.CanUseFacilities, "post-guide village facilities have usable entries");
        foreach (var size in Sizes())
        {
            yield return Size(size.x, size.y);
            village.ClosePanels(); yield return new WaitForSecondsRealtime(.8f);
            CaptureAndScan("village-" + size.x + "x" + size.y);
            CheckNavigation(village);
            village.OpenShop(); yield return null; yield return null;
            CaptureAndScan("shop-first-" + size.x + "x" + size.y);
            village.Shop.ChangePage(1); yield return null;
            CaptureAndScan("shop-second-" + size.x + "x" + size.y);
            village.Shop.SetCategory(true); yield return null;
            CaptureAndScan("shop-items-" + size.x + "x" + size.y);
            village.ClosePanels(); village.OpenInn(); yield return null;
            CaptureAndScan("inn-" + size.x + "x" + size.y);
            village.ClosePanels(); village.OpenTavern(); yield return null; yield return null;
            var tavern = FindObjectOfType<TavernEventBrowserUI>();
            var occurrence = state.world.villageEvents.occurrences.First(v => v.definitionId == "v01");
            tavern.SelectTimedEvent(occurrence.id); yield return null;
            CaptureAndScan("tavern-" + size.x + "x" + size.y);
            CheckReadableText(tavern.transform);
            // Supply appeared-event fixtures to check every choice's layout;
            // generation and earliest-day rules have separate deterministic tests.
            for (int number = 1; number <= 13; number++)
            {
                string id = "v" + number.ToString("00");
                var entry = state.world.villageEvents.occurrences.FirstOrDefault(e => e.definitionId == id);
                if (entry == null)
                {
                    entry = new VillageEventOccurrence { id = "layout:" + id, definitionId = id,
                        appearedTime = state.world.time, expiresTime = state.world.time + 99 };
                    state.world.villageEvents.occurrences.Add(entry);
                }
                tavern.SelectTimedEvent(entry.id); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                CheckReadableText(tavern.transform);
                if (number == 3 || number == 6 || number == 13)
                    CaptureAndScan("tavern-" + id + "-" + size.x + "x" + size.y);
            }
            village.ClosePanels(); village.OpenBag(); yield return new WaitForSecondsRealtime(.9f);
            CaptureAndScan("bag-" + size.x + "x" + size.y);
            CheckReadableText(village.panelBag.transform);
            village.ClosePanels(); yield return new WaitForSecondsRealtime(.8f);
        }
        // Execute a configured reward through the actual timed-event controls.
        village.OpenTavern(); yield return null; yield return null;
        var browser = FindObjectOfType<TavernEventBrowserUI>();
        var rewardEvent = state.world.villageEvents.occurrences.First(v => v.definitionId == "v01");
        browser.SelectTimedEvent(rewardEvent.id); yield return null;
        int count = ItemRules.Count(state.inventory, ItemTable.Bandage);
        state.world.sin = 99;
        browser.ChooseTimedEvent(1); yield return null;
        observations.Add("v01 choice1 resolved=" + rewardEvent.resolved
            + "; bandage before=" + count + ",after=" + ItemRules.Count(state.inventory, ItemTable.Bandage)
            + "; text reward history=" + string.Join(";", state.world.villageEvents.rewardTexts));
        Check(rewardEvent.resolved, "generated tavern event settles through real controls");
        Check(ItemRules.Count(state.inventory, ItemTable.Bandage) == count + 1, "tavern reward actually enters inventory");
        Check(state.world.sin == 100 && !state.map.spawnedBossNodes.Contains("r2_boss"), "chapter threshold does not generate a hidden boss or reset sin");
        var map = new MapProgression(template.Load(), state.map, state.world, state.map.randomSeed);
        observations.Add("Threshold crossing: r2_boss spawned=" + state.map.spawnedBossNodes.Contains("r2_boss")
            + "; available=" + map.IsBossAvailable("r2_boss") + "; visible=" + map.IsVisible("r2_boss"));
        CaptureAndScan("tavern-reward-settled");
        var fullReward = state.world.villageEvents.occurrences.First(e => e.definitionId == "v04");
        browser.SelectTimedEvent(fullReward.id); yield return null;
        Check(browser.choiceLabels[0].text.Contains("携带已满"), "full reward is disclosed before spending time");
        int receiptCount = state.world.villageEvents.rewardTexts.Count;
        browser.ChooseTimedEvent(0); yield return null;
        Check(ItemRules.Count(state.inventory, ItemTable.Bandage) == 1
            && browser.story.messageText.text.Contains("未领取奖励")
            && state.world.villageEvents.rewardTexts.Count == receiptCount + 1,
            "real tavern controls respect carrying limits and show an honest receipt");
        CaptureAndScan("tavern-full-reward");
        village.ClosePanels();
        // Confirm every active weapon is represented by a category picker.
        foreach (var weapon in WeaponTable.All)
            if (!state.inventory.weapons.Contains(weapon.id)) state.inventory.weapons.Add(weapon.id);
        village.OpenBag(); yield return new WaitForSecondsRealtime(.9f);
        var equipment = village.panelBag.GetComponentInChildren<EquipmentPanelView>(true);
        var represented = new HashSet<string>();
        foreach (var size in Sizes())
        {
            yield return Size(size.x, size.y);
            for (int category = 0; category < 4; category++)
            {
                equipment.inventoryLayout.categorySlots[category].card.button.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                foreach (var row in equipment.inventoryLayout.weaponContent.GetComponentsInChildren<InventoryEntryView>())
                    represented.Add(row.name.Replace("Weapon_", ""));
                CaptureAndScan("bag-category-" + category + "-" + size.x + "x" + size.y);
                CheckReadableText(equipment.transform);
                equipment.inventoryLayout.closePickerButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
            }
        }
        Check(WeaponTable.All.All(w => represented.Contains(w.id)), "all active weapon assets have category and equipment UI entries");
        // Exercise available and corrupt disk-save entries, including pause, at each size.
        var morning = NewRunFactory.Create(template.Load(), "sword", 22101);
        morning.story.stage = MainStoryStage.TutorialCompleted; morning.story.guideCompleted = true;
        morning.story.slothHookSeen = true;
        Check(GameRoot.I.SaveMorning(morning, template.Load()), "layout fixture writes a valid morning slot");
        loading = SceneManager.LoadSceneAsync("MainMenu"); while (!loading.isDone) yield return null;
        yield return null; yield return null;
        flow = FindObjectOfType<GameFlowController>();
        flow.GetComponent<MainMenuOpening>().SkipOpening(); yield return null; yield return null;
        Check(flow.continueButton.interactable, "saved game has an active menu entry");
        foreach (var size in Sizes())
        {
            yield return Size(size.x, size.y);
            CaptureAndScan("menu-with-save-" + size.x + "x" + size.y);
            CheckReadableText(flow.mainPage.transform);
        }
        flow.continueButton.onClick.Invoke();
        float loadDeadline = Time.realtimeSinceStartup + 30;
        while ((DayStartLoadTransition.IsBusy || SceneManager.GetActiveScene().name != "Village")
            && Time.realtimeSinceStartup < loadDeadline) yield return null;
        Check(!DayStartLoadTransition.IsBusy && SceneManager.GetActiveScene().name == "Village", "saved morning reaches the real village");
        yield return null; yield return null;
        var outdoor = DayStartSaveStore.Copy(morning);
        string outdoorNode = template.Load().nodes.First(n => n.kind == MapNodeKind.Battle).id;
        outdoor.map.currentNodeId = outdoorNode; outdoor.map.clearedNodes.Add(outdoorNode);
        outdoor.map.visitedNodes.Add(outdoorNode); outdoor.map.revealedNodes.Add(outdoorNode);
        Check(GameRoot.I.SaveMorning(outdoor, template.Load()), "pause layout stores an outdoor morning");
        Check(DayStartLoadTransition.TryBegin(template, error => { throw new Exception(error); }), "pause layout loads the outdoor morning");
        loadDeadline = Time.realtimeSinceStartup + 30;
        while ((DayStartLoadTransition.IsBusy || SceneManager.GetActiveScene().name != "MainMap"
            || FindObjectOfType<WorldMapSceneView>().IsRevealing) && Time.realtimeSinceStartup < loadDeadline) yield return null;
        Check(!DayStartLoadTransition.IsBusy && SceneManager.GetActiveScene().name == "MainMap", "outdoor morning reaches its pause menu");
        flow = FindObjectOfType<GameFlowController>();
        foreach (var size in Sizes())
        {
            yield return Size(size.x, size.y); flow.OpenPause(); yield return null;
            CaptureAndScan("pause-with-save-" + size.x + "x" + size.y, flow.pauseOverlay.transform);
            CheckReadableText(flow.pauseOverlay.transform);
            flow.ClosePause();
        }
        File.WriteAllText(GameRoot.I.SaveSlot.Path, "{invalid json");
        loading = SceneManager.LoadSceneAsync("MainMenu"); while (!loading.isDone) yield return null;
        yield return null; yield return null;
        flow = FindObjectOfType<GameFlowController>(); flow.GetComponent<MainMenuOpening>().SkipOpening();
        yield return null; yield return null;
        Check(!flow.continueButton.interactable, "corrupt slot disables continue without installing partial state");
        foreach (var size in Sizes())
        {
            yield return Size(size.x, size.y);
            CaptureAndScan("menu-corrupt-save-" + size.x + "x" + size.y);
            CheckReadableText(flow.mainPage.transform);
        }
    }
    static Vector2Int[] Sizes() => new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(2560,1080) };
    void CheckNavigation(VillageScreen village)
    {
        var investigation = village.SlothChapter.InvestigationButton;
        Check(investigation != null && investigation.isActiveAndEnabled, "chapter investigation has an active village entry");
        foreach (var button in FindObjectsOfType<Button>())
        {
            if (button == investigation || !button.IsInteractable()) continue;
            Check(!ScreenRect(investigation).Overlaps(ScreenRect(button)), "investigation does not overlap " + button.name);
        }
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = ScreenRect(investigation).center }, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == investigation,
            "investigation entry receives real pointer input");
    }
    static Rect ScreenRect(Button button)
    {
        var canvas=button.GetComponentInParent<Canvas>();
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners=new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
        var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(camera,p)).ToArray();
        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    void CheckReadableText(Transform root)
    {
        foreach (var label in root.GetComponentsInChildren<TMP_Text>())
        {
            if (string.IsNullOrWhiteSpace(label.text)) continue;
            var canvas=label.GetComponentInParent<Canvas>();
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float pixels=Vector2.Distance(RectTransformUtility.WorldToScreenPoint(camera,label.transform.position),
                RectTransformUtility.WorldToScreenPoint(camera,label.transform.TransformPoint(Vector3.up*label.fontSize)));
            Check(pixels>=12, label.name+" keeps a readable screen font size");
            var scroll=label.GetComponentInParent<ScrollRect>();
            Check(!label.isTextOverflowing || scroll != null && scroll.content==label.rectTransform,
                label.name+" fits its text box or exposes a scrollable description");
        }
    }
    IEnumerator Size(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var method = sizesType.GetMethod("GetGroup");
        var group = method.Invoke(sizes, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var constructor = sizeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).First(c => c.GetParameters().Length == 4);
        var size = constructor.Invoke(new[] { Enum.ToObject(constructor.GetParameters()[0].ParameterType, 1), (object)width, height, "Global audit" });
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[] { size });
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
            .SetValue(view,(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1);
        yield return null; yield return null; yield return new WaitForSecondsRealtime(.2f);
        Canvas.ForceUpdateCanvases();
        Check(Screen.width == width && Screen.height == height, "actual game view resolution " + width + "x" + height);
    }
    void CaptureAndScan(string name, Transform textRoot = null)
    {
        int width = Screen.width, height = Screen.height;
        var camera = Camera.main;
        var canvases = FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var distances = canvases.Select(c => c.planeDistance).ToArray();
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = new RenderTexture(width,height,24); target.Create();
        var pixels = new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());
            foreach (var label in FindObjectsOfType<TMP_Text>())
            {
                if (textRoot != null && !label.transform.IsChildOf(textRoot)) continue;
                if (string.IsNullOrWhiteSpace(label.text) || label.color.a < .1f || label.canvasRenderer.GetInheritedAlpha() < .5f) continue;
                label.ForceMeshUpdate();
                bool scroll = label.GetComponentInParent<ScrollRect>() != null;
                var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(camera,p)).ToArray();
                bool offscreen = points.Min(p => p.x) < -2 || points.Max(p => p.x) > width+2 || points.Min(p => p.y) < -2 || points.Max(p => p.y) > height+2;
                string reason = (label.isTextOverflowing ? "text-overflow;" : "") + (offscreen ? "rect-offscreen;" : "");
                var fontBase = RectTransformUtility.WorldToScreenPoint(camera, label.transform.TransformPoint(Vector3.zero));
                var fontTop = RectTransformUtility.WorldToScreenPoint(camera, label.transform.TransformPoint(Vector3.up * label.fontSize));
                float fontPixels = Vector2.Distance(fontBase, fontTop);
                if (fontPixels < 12) reason += "font-under-12px;";
                if (reason.Length == 0) continue;
                string path = label.name; for(var t=label.transform.parent;t!=null;t=t.parent) path=t.name+"/"+path;
                candidates.Add(new TextCandidate { screen=name,path=path,text=label.text,reason=reason,insideScroll=scroll,fontPixels=fontPixels,overflowMode=label.overflowMode.ToString() });
            }
            checks.Add("captured and scanned installed screen " + name);
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++) { canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i]; }
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            Destroy(pixels);target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
