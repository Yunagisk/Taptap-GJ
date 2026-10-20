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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DayStartSaveValidation
{
    const string Pending = "GlobalQA.DayStartSave";
    static readonly string Output = Path.GetFullPath("../save-load");
    static readonly List<string> checks = new List<string>(), errors = new List<string>();
    static bool finished;
    static WorldMapTemplate template;
    static MapLayout layout;
    [Serializable] sealed class Result { public bool passed; public string[] checks, errors; }
    static DayStartSaveValidation()
    {
        EditorApplication.playModeStateChanged += mode => {
            if (mode != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            var runner = new GameObject("DayStartSaveValidation"); Object.DontDestroyOnLoad(runner);
            runner.AddComponent<BattleSimulatorRound>().StartCoroutine(Guard(Exercise()));
        };
        Application.logMessageReceived += (text, trace, type) => {
            if (!finished && SessionState.GetBool(Pending, false)
                && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                errors.Add(text + "\n" + trace);
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project only");
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static IEnumerator Guard(IEnumerator exercise)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(exercise);
        while (stack.Count > 0)
        {
            object next;
            try { if (!stack.Peek().MoveNext()) { stack.Pop(); continue; } next = stack.Peek().Current; }
            catch (Exception error) { errors.Add(error.ToString()); Finish(); yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Finish();
    }
    static void Finish()
    {
        finished = true;
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonUtility.ToJson(new Result {
            passed = errors.Count == 0, checks = checks.ToArray(), errors = errors.ToArray() }, true));
        EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
    }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks.Add(text); }
    static IEnumerator Wait(Func<bool> predicate, string text)
    {
        float until = Time.realtimeSinceStartup + 30;
        while (!predicate() && Time.realtimeSinceStartup < until) yield return null;
        Check(predicate(), text);
    }
    static IEnumerator Load(string scene)
    {
        var operation = SceneManager.LoadSceneAsync(scene); while (!operation.isDone) yield return null;
        yield return null; yield return null;
    }
    static GameState Ready(int time = 0)
    {
        var state = NewRunFactory.Create(layout, "sword", 700123);
        state.story.stage = MainStoryStage.TutorialCompleted; state.story.guideCompleted = true;
        state.story.guideEquipmentReady = state.story.guideOutsideReady = true;
        state.story.slothHookSeen = true; state.world.time = time;
        state.player.hp = 68; state.player.resolve = 2; state.player.lanternCharges = 1;
        return state;
    }
    static GameState Saved()
    {
        Check(GameRoot.I.SaveSlot.TryLoad(layout, out var saved, out var error), "morning file reloads: " + error);
        return saved;
    }
    static void Install(GameState state) { GameRoot.I.state = state; GameRoot.I.villageMapTemplate = template; }
    static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static IEnumerator Exercise()
    {
        yield return null; yield return null;
        var root = GameRoot.I; var menu = Object.FindObjectOfType<GameFlowController>();
        template = menu.template; layout = template.Load();
        Check(root.SaveSlot.Path.StartsWith(Path.GetFullPath("..")), "native save path stays inside the isolated workspace");
        Check(!menu.continueButton.interactable && !File.Exists(root.SaveSlot.Path), "fresh process has no existing slot or continue action");
        var opening = Object.FindObjectOfType<MainMenuOpening>(); if (opening != null) opening.SkipOpening();
        yield return null; yield return null;

        var initial = NewRunFactory.CreateForIntroduction(layout, 700001);
        Check(root.SaveMorning(initial, layout), "new arrival can be saved on day one morning");
        Check(JsonUtility.ToJson(Saved()) == JsonUtility.ToJson(initial), "JSON roundtrip preserves the complete new-run state");
        var firstBytes = File.ReadAllBytes(root.SaveSlot.Path);
        var invalid = DayStartSaveStore.Copy(initial); invalid.world.time = 1;
        Check(!root.SaveSlot.TrySave(invalid, layout, out _)
            && firstBytes.SequenceEqual(File.ReadAllBytes(root.SaveSlot.Path)), "invalid afternoon save preserves the previous morning atomically");
        var corrupt = new DayStartSaveStore(Path.Combine(Output, "corrupt.json")); File.WriteAllText(corrupt.Path, "{bad json");
        Check(!corrupt.TryLoad(layout, out var bad, out _) && bad == null, "corrupt JSON is rejected without installing partial state");

        yield return Load("MainMenu"); menu = Object.FindObjectOfType<GameFlowController>();
        Check(menu.continueButton.interactable, "real menu discovers the morning file after a scene reload");
        var status = menu.mainPage.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "ContinueStatus");
        status.ForceMeshUpdate(); Check(!status.isTextOverflowing, "available-save status fits the menu text box");
        var previous = root.state; menu.continueButton.onClick.Invoke();
        Check(DayStartLoadTransition.IsBusy, "real continue button starts the persistent load veil");
        yield return new WaitForSecondsRealtime(.1f);
        Check(ReferenceEquals(root.state, previous) && Object.FindObjectOfType<DayStartLoadTransition>().GetComponent<CanvasGroup>().alpha > 0,
            "load fades toward black before replacing the active run");
        yield return Wait(() => SceneManager.GetActiveScene().name == "Village" && !DayStartLoadTransition.IsBusy,
            "continue reaches the saved arrival and completes its fade");
        Check(root.state.world.time == 0 && root.state.map.randomSeed == 700001
            && root.state.inventory.weapons.Count == 0 && Object.FindObjectOfType<VillageIntroController>().IsOpen,
            "continuing the first morning restores the mandatory arrival without invented equipment");

        var state = Ready(); Install(state); Check(root.SaveMorning(state, layout), "post-guide morning saves");
        firstBytes = File.ReadAllBytes(root.SaveSlot.Path);
        WorldResourceRules.SpendTime(layout, state.map, state.world, 1, true);
        Check(firstBytes.SequenceEqual(File.ReadAllBytes(root.SaveSlot.Path)), "afternoon does not overwrite the morning slot");
        WorldResourceRules.SpendTime(layout, state.map, state.world, 2, true);
        Check(Saved().world.time == 3 && state.world.time == 3, "crossing dawn writes the next morning exactly");
        var loaded = Saved(); loaded.player.hp = 1;
        Check(state.player.hp == 68 && Saved().player.hp == 68, "loaded state is independent of both the live run and the file");

        foreach (var reward in new[] { new[] { "v01", "1", "bandage" }, new[] { "v03", "0", "holy_water" }, new[] { "v06", "0", "resolve" } })
        {
            var definition = VillageEventRules.FindDefinition(reward[0]); int choice = int.Parse(reward[1]);
            state = Ready(3 - definition.choices[choice].timeCost); state.inventory.items.Clear(); Install(state);
            state.world.villageEvents = new VillageEventState { initialized = true, randomSeed = state.map.randomSeed,
                lastProcessedTime = state.world.time, occurrences = new List<VillageEventOccurrence> {
                    new VillageEventOccurrence { id = "dawn", definitionId = reward[0], expiresTime = 99 } } };
            Check(VillageEventRules.TryChoose(state, layout, "dawn", choice).success, "tavern reward resolves across dawn: " + reward[0]);
            loaded = Saved();
            Check(loaded.world.villageEvents.occurrences.Single(x => x.id == "dawn").resolved
                && loaded.world.villageEvents.rewardTexts.Count == 1 && loaded.world.sin == state.world.sin,
                "dawn receipt and event resolution are saved with the selected sin outcome");
            Check(reward[2] == "resolve" ? loaded.player.resolve == 3 : ItemRules.Count(loaded.inventory, reward[2]) == 1,
                "dawn snapshot contains the actual reward: " + reward[2]);
            string snapshot = JsonUtility.ToJson(loaded);
            Check(!VillageEventRules.TryChoose(loaded, layout, "dawn", choice).success
                && snapshot == JsonUtility.ToJson(loaded), "restored tavern settlement cannot grant again");
        }
        state = Ready(2); state.player.hp = 31; state.player.lanternCharges = 0; Install(state);
        Check(VillageInnRules.TryRest(state, layout, 2, out _), "inn crosses into a real morning");
        loaded = Saved();
        Check(loaded.world.time == 3 && loaded.player.hp == 100 && loaded.player.lanternCharges == 2,
            "inn saves recovered health and replenished lantern oil");

        foreach (var kind in new[] { MapNodeKind.Battle, MapNodeKind.Camp })
        {
            state = Ready(2); state.player.hp = 40; var node = layout.nodes.First(n => n.kind == kind);
            state.map.currentNodeId = state.map.pendingNodeId = node.id; state.map.pendingRequestId = "dawn-node"; Install(state);
            var map = new MapProgression(layout, state.map, state.world, state.map.randomSeed);
            Check(map.TryCompleteNode("dawn-node"), "node settlement crosses dawn: " + kind);
            if (kind == MapNodeKind.Camp) WorldResourceRules.RecoverAtCamp(state.player);
            loaded = Saved();
            Check(loaded.map.clearedNodes.Contains(node.id) && string.IsNullOrEmpty(loaded.map.pendingRequestId)
                && loaded.world.time == 3 && loaded.player.hp == state.player.hp,
                "dawn node snapshot contains completion and camp recovery: " + kind);
        }

        // A genuine enemy turn supplies the defeat; only the fixture's initial HP is shortened.
        var morning = Ready(3); ItemRules.TryGrant(morning, "bandage"); Install(morning);
        Check(root.SaveMorning(morning, layout), "death test stores its complete morning");
        state = DayStartSaveStore.Copy(morning); state.world.time = 4;
        var fight = layout.nodes.First(n => n.kind == MapNodeKind.Battle);
        state.map.currentNodeId = state.map.pendingNodeId = fight.id; state.map.pendingRequestId = "death-test";
        Install(state); root.battleMapTemplate = template; root.battleRequestId = "death-test";
        yield return Load("CombatScene");
        var battle = Object.FindObjectOfType<BattleFlowController>();
        yield return Wait(() => battle.CurrentState == BattleState.PlayerTurn, "combat begins for the death/load regression");
        battle.Player.TakeDamage(battle.Player.CurrentHP - 1);
        for (int turn = 0; turn < 12 && battle.CurrentState != BattleState.Defeat; turn++)
        {
            battle.PlayerDefend();
            yield return Wait(() => battle.CurrentState == BattleState.PlayerTurn || battle.CurrentState == BattleState.Defeat,
                "enemy turn completes while the fixture defends");
        }
        Check(battle.CurrentState == BattleState.Defeat, "enemy attack defeats the fixture through normal combat");
        var hud = Object.FindObjectOfType<BattleHudView>();
        Check(hud.playerHealth.rectTransform.localScale.x == 0 && hud.healthText.text.StartsWith("0 /"),
            "death immediately paints zero in the real battle HUD");
        var result = Object.FindObjectOfType<BattleResultUI>(); var returnButton = Field<Button>(result, "returnButton");
        Check(returnButton.interactable, "death overlay exposes the saved morning entry"); returnButton.onClick.Invoke();
        yield return Wait(() => SceneManager.GetActiveScene().name == "Village" && !DayStartLoadTransition.IsBusy,
            "defeat loads the morning through the real result button");
        Check(root.state.world.time == 3 && root.state.player.hp == 68 && root.state.player.resolve == 2
            && root.state.player.lanternCharges == 1 && ItemRules.Count(root.state.inventory, "bandage") == 1
            && string.IsNullOrEmpty(root.state.map.pendingRequestId), "death restores the complete morning instead of only refilling health");
        var outdoor = Ready(3); outdoor.map.currentNodeId = fight.id;
        outdoor.map.clearedNodes.Add(fight.id); outdoor.map.visitedNodes.Add(fight.id); outdoor.map.revealedNodes.Add(fight.id);
        Install(outdoor); Check(root.SaveMorning(outdoor, layout), "pause fixture stores a cleared outdoor morning");
        Check(DayStartLoadTransition.TryBegin(template, error => { throw new Exception(error); }), "outdoor morning load starts");
        yield return Wait(() => SceneManager.GetActiveScene().name == "MainMap" && !DayStartLoadTransition.IsBusy
            && !Object.FindObjectOfType<WorldMapSceneView>().IsRevealing, "outdoor morning reaches the map pause entry");
        var flow = Object.FindObjectOfType<GameFlowController>(); flow.OpenPause(); yield return null;
        var loadDay = flow.pauseOverlay.GetComponentsInChildren<Button>().Single(b => b.name == "Btn_LoadDay");
        Check(loadDay.interactable, "pause menu has a usable morning-load entry");
        var canvas = loadDay.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var center = loadDay.transform.TransformPoint(((RectTransform)loadDay.transform).rect.center);
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(camera, center) }, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == loadDay,
            "pause morning-load entry receives real pointer input");
        foreach (var button in flow.pauseOverlay.GetComponentsInChildren<Button>())
            if (button != loadDay) Check(!RectIn(button, loadDay.transform.parent).Overlaps(RectIn(loadDay, loadDay.transform.parent)),
                "load-day button does not overlap " + button.name);
        WorldResourceRules.SpendTime(layout, root.state.map, root.state.world, 1, false); Time.timeScale = 0;
        loadDay.onClick.Invoke();
        yield return Wait(() => SceneManager.GetActiveScene().name == "MainMap" && !DayStartLoadTransition.IsBusy,
            "paused morning load completes using unscaled time");
        Check(root.state.world.time == 3 && Time.timeScale == 1, "manual load restores morning time and releases the pause");
        Check(errors.Count == 0, "save/load integration reports no runtime errors");
    }
    static Rect RectIn(Button button, Transform space)
    {
        var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
        var points = corners.Select(p => space.InverseTransformPoint(p)).ToArray();
        return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
    }
}
#endif
