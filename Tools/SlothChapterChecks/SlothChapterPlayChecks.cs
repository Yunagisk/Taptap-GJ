#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Starts at the already-verified guide's return report, then drives production village,
// map, boss, dialogue and ending UI. Health is lowered only to expedite win/defeat
// branches after genuine combat rounds; this is a flow check, not a balance simulation.
public sealed class SlothChapterPlayChecks : MonoBehaviour
{
    readonly string output = Path.GetFullPath("../sloth-chapter");
    readonly List<string> checks = new List<string>();
    readonly List<string> runtimeMessages = new List<string>();
    int errors;
    WorldMapTemplate template;
    GameState state;
    WorldMapController map;
    BattleFlowController battle;
    void Awake() { Directory.CreateDirectory(output); Application.logMessageReceived += Logged; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        errors++; runtimeMessages.Add(message + "\n" + stack);
    }
    void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAILED: " + name);
        checks.Add(name);
    }
    IEnumerator Start()
    {
        var work = new Stack<IEnumerator>(); work.Push(Exercise());
        while (work.Count > 0)
        {
            object next;
            try
            {
                if (!work.Peek().MoveNext()) { work.Pop(); continue; }
                next = work.Peek().Current;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "failure.txt"), error + "\n" + string.Join("\n", runtimeMessages));
                File.WriteAllText(Path.Combine(output, "partial-checks.json"), JsonUtility.ToJson(new Result {
                    passed = false, checks = checks.ToArray(), runtimeErrors = errors }, true));
                Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); yield break;
            }
            if (next is IEnumerator nested) work.Push(nested); else yield return next;
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result {
            passed = true, checks = checks.ToArray(), runtimeErrors = errors }, true));
        UnityEditor.EditorApplication.Exit(0);
    }
    [Serializable] sealed class Result { public bool passed; public string[] checks; public int runtimeErrors; }
    IEnumerator Wait(Func<bool> predicate, string name)
    {
        float deadline = Time.realtimeSinceStartup + 45;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Check(predicate(), name);
    }
    IEnumerator Load(string name)
    {
        var loading = SceneManager.LoadSceneAsync(name);
        while (!loading.isDone) yield return null;
        yield return null; yield return null;
    }
    GuideDialogueView OpenView() => FindObjectsOfType<GuideDialogueView>().FirstOrDefault(view => view.IsOpen);
    SlothChapterPresentation Chapter() => FindObjectOfType<SlothChapterPresentation>();
    IEnumerator Walk(GuideDialogueView view, Dictionary<string, int> choices = null)
    {
        Check(view != null && view.Manager != null, "authored presentation uses the real dialogue manager");
        var manager = view.Manager;
        for (int i = 0; i < 200 && view != null && view.IsOpen && manager.IsPlaying; i++)
        {
            if (manager.ui.CanChoose)
            {
                int index = choices != null && choices.TryGetValue(manager.CurrentNodeId, out var selected) ? selected : 0;
                Choose(manager, index);
            }
            else manager.Next();
            yield return null;
        }
        Check(view == null || !view.IsOpen || !manager.IsPlaying, "authored conversation reaches its completion");
    }
    IEnumerator ToNode(GuideDialogueView view, string node)
    {
        for (int i = 0; i < 150 && view.Manager.IsPlaying
            && (view.Manager.CurrentNodeId != node || view.Manager.HasChoice() && !view.Manager.ui.CanChoose); i++)
        {
            if (view.Manager.ui.CanChoose) Choose(view.Manager, 0); else view.Manager.Next();
            yield return null;
        }
        Check(view.Manager.IsPlaying && view.Manager.CurrentNodeId == node, "dialogue reaches " + node);
    }
    static void Choose(DialogueManager manager, int index)
    {
        var choices = manager.ui.choiceParent.GetComponentsInChildren<Button>(false);
        if (index < 0 || index >= choices.Length) throw new Exception("No dialogue choice " + index);
        Submit(choices[index]);
    }
    static void Submit(Button button)
    {
        EventSystem.current.SetSelectedGameObject(button.gameObject);
        ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }
    IEnumerator Exercise()
    {
        yield return null; yield return null;
        if (GameRoot.I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        template = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldMapTemplate>(
            "Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.asset");
        Check(template != null, "production map is available");
        PrepareGuideReturn();
        yield return Load("Village");
        var village = FindObjectOfType<VillageScreen>();
        Check(village.Introduction != null && village.Introduction.IsOpen,
            "completed tutorial returns through the existing guide narration");
        yield return Walk(OpenView());
        village.Introduction.OpenMayorHouse(); yield return null;
        yield return Walk(OpenView());
        yield return Wait(() => Chapter() != null && Chapter().ActiveDialogue != null
            && Chapter().ActiveDialogue.dialogueId == "sloth_hook", "finishing the guide automatically opens the patient's appeal");
        yield return null; // Let newly activated Canvas graphics register before pointer probing.
        Check(state.story.guideCompleted && SlothChapterPresentation.IsBlocking,
            "guide completion releases its mechanics while chapter dialogue owns interaction");
        CheckDialoguePresentation(OpenView());
        Capture("01-village-appeal.png");
        string snapshot = JsonUtility.ToJson(state);
        village.OpenInn(); village.OpenShop(); village.OpenBag(); village.LeaveToMap();
        Check(!village.IsLoading && !village.panelRoot.activeSelf && snapshot == JsonUtility.ToJson(state),
            "appeal blocks village actions and changes no resources");
        yield return Walk(OpenView()); yield return null;
        Check(state.story.slothHookSeen && !SlothChapterPresentation.IsBlocking && village.CanUseFacilities,
            "appeal completes once and returns to ordinary village services");
        var chapter = Chapter();
        Check(chapter.InvestigationButton != null && chapter.InvestigationButton.gameObject.activeInHierarchy,
            "investigation has a visible village entrance");
        Submit(chapter.InvestigationButton); yield return null;
        Check(Chapter().ActiveDialogue.dialogueId == "sloth_investigation", "village entrance opens the authored investigation");
        yield return Walk(OpenView(), new Dictionary<string, int> { { "questions", 2 } }); yield return null;
        Check(state.story.slothInvestigated && !state.story.slothEndingSeen,
            "investigation records progress without finishing the chapter");
        village.OpenBag(); yield return new WaitForSecondsRealtime(.8f);
        Check(village.panelBag.activeInHierarchy, "backpack can be opened between chapter conversations");
        Check(SlothChapterPresentation.PlayInvestigation(village.transform),
            "a chapter conversation can take over while the backpack is open");
        yield return new WaitForSecondsRealtime(.8f);
        Check(!village.panelRoot.activeSelf && OpenView() != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(OpenView().Manager.transform),
            "closing parchment cannot steal the chapter dialogue's keyboard focus");
        yield return Walk(OpenView(), new Dictionary<string, int> { { "questions", 2 } });

        // Exercise the spare ending from a real entered boss request.
        yield return OpenBoss(false);
        var view = OpenView();
        yield return ToNode(view, "debate2");
        string debateState = JsonUtility.ToJson(state);
        Choose(view.Manager, 1); yield return null;
        yield return ToNode(view, "debate2");
        Check(debateState == JsonUtility.ToJson(state),
            "wrong second-round response returns to the same round without resources or progress changes");
        CheckDialoguePresentation(view);
        Capture("02-sloth-confrontation.png");
        yield return Walk(view);
        yield return Wait(() => SceneManager.GetActiveScene().name == "CombatScene", "finished confrontation enters actual combat");
        yield return null; yield return null;
        yield return CheckBossCombat(true);
        yield return WinBoss(1);
        yield return ReturnAndFinish(true);

        // A completed save must reopen its final screen, without playing or settling again.
        string completed = JsonUtility.ToJson(state);
        yield return Load("Village");
        yield return Wait(() => Chapter() != null && Chapter().IsEndingOpen, "reloading a completed village restores its ending screen");
        Check(JsonUtility.ToJson(state) == completed, "reloading a completed run cannot repeat rewards or story settlement");
        Submit(Chapter().ReturnToMenuButton);
        yield return Wait(() => SceneManager.GetActiveScene().name == "MainMenu", "ending offers a working return to main menu");

        // The other choice closes a separate run without changing the first ending's semantics.
        PrepareFinishedGuide(811);
        yield return OpenBoss(true);
        yield return Walk(OpenView());
        yield return Wait(() => SceneManager.GetActiveScene().name == "CombatScene", "direct exploration can reach Sloth without optional investigation");
        yield return null; yield return null;
        yield return CheckBossCombat(false);
        yield return WinBoss(0);
        yield return ReturnAndFinish(false);

        // Losing loads the saved morning and never unlocks the ending.
        PrepareFinishedGuide(812);
        yield return OpenBoss(true);
        yield return Walk(OpenView());
        yield return Wait(() => SceneManager.GetActiveScene().name == "CombatScene", "third run reaches boss for defeat coverage");
        yield return null; yield return null;
        battle = FindObjectOfType<BattleFlowController>();
        battle.Player.TakeDamage(battle.Player.CurrentHP - 1);
        for (int turns = 0; turns < 8 && battle.CurrentState != BattleState.Defeat; turns++)
        {
            battle.PlayerDefend();
            yield return Wait(() => battle.CurrentState == BattleState.PlayerTurn || battle.CurrentState == BattleState.Defeat,
                "actual boss move finishes its defeat-test turn");
        }
        Check(battle.CurrentState == BattleState.Defeat && state.story.slothFate == SlothFate.Undecided,
            "real boss attack defeats the player without choosing Theodore's fate");
        FindObjectOfType<BattleResultUI>().ReturnToWorldMap();
        yield return Wait(() => SceneManager.GetActiveScene().name == "Village" && !DayStartLoadTransition.IsBusy,
            "boss defeat loads its saved village morning");
        yield return null; yield return null;
        state = GameRoot.I.state;
        Check(state.player.hp == state.player.maxHp && !state.map.defeatedBossNodes.Contains(state.story.slothNodeId)
            && !state.story.slothEndingSeen && state.story.slothFate == SlothFate.Undecided
            && state.world.time == 0 && state.map.randomSeed == 812
            && string.IsNullOrEmpty(state.map.pendingRequestId), "reload restores the complete morning and leaves Sloth undefeated and retryable");
        Check(!Chapter().IsEndingOpen, "defeat does not show the thank-you ending");
        Check(errors == 0, "production chapter flow reports no runtime errors");
    }
    void PrepareGuideReturn()
    {
        state = NewRunFactory.Create(template.Load(), "sword", 810);
        state.story.useVillageIntroduction = true;
        state.story.stage = MainStoryStage.TutorialCompleted;
        state.story.guideEquipmentReady = true; state.story.guideOutsideReady = true;
        state.story.guideSecondWeaponId = "hammer";
        state.story.guideLessons = GuideLesson.Switch | GuideLesson.Lantern | GuideLesson.Resolve | GuideLesson.Break | GuideLesson.Items;
        state.inventory.weapons.Add("hammer");
        EquipmentRules.TryEquip(state, 2, "hammer");
        if (!state.map.clearedNodes.Contains(state.story.tutorialNodeId)) state.map.clearedNodes.Add(state.story.tutorialNodeId);
        SetRoot();
    }
    void PrepareFinishedGuide(int seed)
    {
        state = NewRunFactory.Create(template.Load(), "sword", seed);
        state.story.stage = MainStoryStage.TutorialCompleted; state.story.guideCompleted = true;
        state.story.slothHookSeen = true;
        SetRoot();
        Check(GameRoot.I.SaveMorning(state, template.Load()), "fresh fixture stores its actual morning before entering the boss");
    }
    void SetRoot()
    {
        GameRoot.I.state = state; GameRoot.I.pendingMapReveal = null;
        GameRoot.I.battleMapTemplate = null; GameRoot.I.battleRequestId = null; GameRoot.I.battleReturnScene = null;
        GameRoot.I.villageMapTemplate = template; GameRoot.I.villageReturnScene = GameFlowController.MapScene;
        GameRoot.I.enterVillageOnLoad = false;
    }
    IEnumerator OpenBoss(bool fresh)
    {
        var layout = template.Load();
        var boss = layout.regions.First(region => region.bossKey == "sloth");
        state.story.slothNodeId = boss.bossNodeId;
        string origin = layout.nodes.First(node => node.id == boss.bossNodeId).neighbors[0];
        // Set up a legitimately traversable cleared return path; entry itself is always the real map action.
        foreach (var node in layout.nodes.Where(node => node.kind != MapNodeKind.Boss))
        {
            Add(state.map.clearedNodes, node.id); Add(state.map.visitedNodes, node.id); Add(state.map.revealedNodes, node.id);
        }
        Add(state.map.spawnedBossNodes, boss.bossNodeId); Add(state.map.revealedNodes, boss.bossNodeId);
        state.map.currentNodeId = origin; state.map.checkpointNodeId = layout.villageNodeId;
        state.map.pendingNodeId = null; state.map.pendingRequestId = null; state.world.time = 0;
        state.player.hp = state.player.maxHp; state.player.resolve = 3;
        foreach (string spawned in state.map.spawnedBossNodes) GameRoot.I.mapBossIntroductions.MarkShown(state.map, spawned);
        yield return Load("MainMap");
        map = FindObjectOfType<WorldMapController>();
        yield return Wait(() => !MapSceneTransition.IsBusy && !map.GetComponent<WorldMapSceneView>().IsRevealing,
            "map is ready for actual Sloth entry");
        map.MoveToNode(boss.bossNodeId);
        yield return Wait(() => OpenView() != null && Chapter() != null && Chapter().ActiveDialogue != null
            && Chapter().ActiveDialogue.dialogueId == "sloth_confrontation", "Sloth node opens confrontation instead of placeholder completion");
        yield return Wait(() => !MapSceneTransition.IsBusy, "map transition finishes before interacting with the confrontation");
        CheckDialoguePresentation(OpenView());
        Check(state.world.time == 1 && state.map.pendingNodeId == boss.bossNodeId
            && !state.map.defeatedBossNodes.Contains(boss.bossNodeId), "entering Sloth pays one movement and preserves an unresolved boss request");
        string snapshot = JsonUtility.ToJson(state);
        map.CompleteNode(state.map.pendingRequestId);
        Check(JsonUtility.ToJson(state) == snapshot, "placeholder completion cannot bypass a real boss");
        FindObjectOfType<GameFlowController>().HandleBack();
        Check(SlothChapterPresentation.IsBlocking && OpenView().IsOpen && JsonUtility.ToJson(state) == snapshot,
            "map Escape handler cannot expose gameplay behind an active chapter conversation");
    }
    IEnumerator CheckBossCombat(bool exerciseTurn)
    {
        battle = FindObjectOfType<BattleFlowController>();
        Check(battle != null && battle.Guide == null && battle.Enemies.Count == 1,
            "real boss encounter contains one Sloth and no tutorial controller");
        var enemy = battle.Enemies[0];
        var animation = enemy.GetComponent<EnemySpriteAnimation>();
        Check(animation != null && animation.animations != null && animation.animations.name == "SlothAnimations",
            "combat uses existing Sloth animation assets");
        Check(enemy.CurrentHP == 300 && enemy.Definition == null && enemy.DisplayName == "懒惰"
            && enemy.UpcomingAction != null && battle.GetComponent<SmallMonsterEncounterSetup>().IsSlothEncounter,
            "Sloth retains its existing 300 HP and authored action cycle");
        Check(battle.Player.MaxDetermination == 3 && state.story.slothConfronted,
            "combat preserves the three-point resolve rule and completed confrontation");
        if (!exerciseTurn) yield break;
        Capture("03-sloth-battle.png");
        int time = state.world.time, hp = enemy.CurrentHP;
        battle.PlayerAttack(); battle.AttackTarget(enemy);
        yield return Wait(() => battle.CurrentState == BattleState.PlayerTurn || battle.CurrentState == BattleState.Defeat,
            "player attack and existing Sloth enemy action complete");
        Check(battle.CurrentState == BattleState.PlayerTurn && enemy.CurrentHP < hp && state.world.time == time,
            "actual attack damages Sloth without charging world time per action");
        int resolve = battle.Player.CurrentDetermination;
        Check(battle.CanUseWeaponSkill, "existing sword skill is available in the real boss encounter");
        battle.PlayerSkill();
        if (battle.CurrentState == BattleState.SelectingTarget) battle.AttackTarget(enemy);
        yield return Wait(() => battle.CurrentState == BattleState.PlayerTurn || battle.CurrentState == BattleState.Defeat,
            "existing weapon skill completes its boss combat round");
        Check(battle.CurrentState == BattleState.PlayerTurn && battle.Player.CurrentDetermination < resolve
            && state.world.time == time, "weapon skill spends resolve and keeps the established battle-time rule");
    }
    IEnumerator WinBoss(int choice)
    {
        var enemy = battle.Enemies[0];
        // Avoid claiming balance validation: expedite remaining HP, then finish with the real attack pipeline.
        enemy.TakeDamage(enemy.CurrentHP - 1);
        battle.PlayerAttack(); battle.AttackTarget(enemy);
        yield return Wait(() => battle.CurrentState == BattleState.Victory, "real attack completes the Sloth victory state");
        yield return Wait(() => FindObjectsOfType<Button>().Any(button => button.gameObject.activeInHierarchy
            && button.name == "ReturnButton" && button.interactable), "Sloth defeat animation finishes before the result button appears");
        Submit(FindObjectsOfType<Button>().Single(button => button.gameObject.activeInHierarchy && button.name == "ReturnButton"));
        yield return Wait(() => Chapter() != null && Chapter().ActiveDialogue != null
            && Chapter().ActiveDialogue.dialogueId == "sloth_after_battle", "boss result continues into Theodore's judgement");
        var view = OpenView(); yield return ToNode(view, "judgement");
        CheckDialoguePresentation(view);
        Check(state.story.slothFate == SlothFate.Undecided && !state.story.slothEndingSeen,
            "victory waits for fate selection and does not end the game outside the village");
        Capture(choice == 1 ? "04-theodore-choice.png" : "07-theodore-choice-kill.png");
        FindObjectOfType<BattleResultUI>().ReturnToWorldMap();
        yield return null;
        Check(SceneManager.GetActiveScene().name == "CombatScene" && view.IsOpen,
            "direct return cannot skip the unresolved post-battle decision");
        yield return Walk(view, new Dictionary<string, int> { { "judgement", choice } });
        var expected = choice == 1 ? SlothFate.Spared : SlothFate.Killed;
        Check(state.story.slothFate == expected && !state.story.slothEndingSeen,
            "selected fate is saved while the chapter still requires returning home");
        yield return Wait(() => SceneManager.GetActiveScene().name == "MainMap"
            || FindObjectsOfType<Button>().Any(button => button.gameObject.activeInHierarchy && button.name == "ReturnButton"),
            "completed judgement exposes the normal map return");
        if (SceneManager.GetActiveScene().name == "CombatScene") FindObjectOfType<BattleResultUI>().ReturnToWorldMap();
        yield return Wait(() => SceneManager.GetActiveScene().name == "MainMap", "victory returns to the actual map");
        yield return null; yield return null;
        map = FindObjectOfType<WorldMapController>();
        Check(state.map.defeatedBossNodes.Contains(state.story.slothNodeId)
            && state.story.stage == MainStoryStage.SlothDefeated && state.world.time == 2,
            "Sloth settlement clears once and spends exactly one battle time");
        Check(!state.story.slothEndingSeen, "victory on the world map has not ended the game");
    }
    IEnumerator ReturnAndFinish(bool spared)
    {
        yield return Wait(() => !MapSceneTransition.IsBusy && !map.GetComponent<WorldMapSceneView>().IsRevealing,
            "post-victory map reveal completes");
        string villageId = map.Progression.Layout.villageNodeId;
        map.MoveToNode(villageId);
        Check(map.IsFastTravelConfirmationOpen, "cleared route home uses existing fast travel confirmation");
        map.ConfirmFastTravel(); yield return null;
        yield return Wait(() => !MapSceneTransition.IsBusy, "return route animation finishes");
        if (SceneManager.GetActiveScene().name == "MainMap")
        {
            map.MoveToNode(villageId); yield return null;
            if (map.IsVillageEntryConfirmationOpen) map.ConfirmVillageEntry();
        }
        yield return Wait(() => SceneManager.GetActiveScene().name == "Village", "real village entry completes the homeward trip");
        yield return null; yield return null;
        string expected = spared ? "sloth_return_spared" : "sloth_return_killed";
        yield return Wait(() => Chapter() != null && Chapter().ActiveDialogue != null
            && Chapter().ActiveDialogue.dialogueId == expected, "return automatically plays the matching village epilogue");
        Check(!state.story.slothEndingSeen, "chapter completion waits for the village epilogue's final line");
        Capture(spared ? "05-village-return-spared.png" : "08-village-return-killed.png");
        yield return Walk(OpenView()); yield return null;
        yield return Wait(() => Chapter() != null && Chapter().IsEndingOpen, "village epilogue opens thank-you screen");
        Check(state.story.slothEndingSeen && state.story.stage == MainStoryStage.CommissionTurnedIn,
            "chapter is complete only after returning home and finishing its epilogue");
        Check(FindObjectsOfType<TMP_Text>().Any(text => text.gameObject.activeInHierarchy && text.text.Contains("感谢游玩")),
            "final screen thanks the player");
        Check(FindObjectsOfType<TMP_Text>().Any(text => text.gameObject.activeInHierarchy && text.text.Contains("敬请期待")),
            "final screen promises future updates without starting Wrath");
        string snapshot = JsonUtility.ToJson(state);
        var village = FindObjectOfType<VillageScreen>();
        FindObjectOfType<GameFlowController>()?.HandleBack();
        village.OpenShop(); village.OpenBag(); village.OpenInn(); village.OpenTavern(); village.LeaveToMap();
        yield return null;
        village.ClosePanels(); village.CloseConflict(); village.ConfirmInn(); village.ConfirmBuy();
        Check(Chapter().IsEndingOpen && SlothChapterPresentation.IsBlocking && !village.IsLoading
            && !village.panelRoot.activeSelf && snapshot == JsonUtility.ToJson(state),
            "ending stays modal and stale village callbacks cannot reopen gameplay");
        Check(PointerTarget(Chapter().ReturnToMenuButton), "thank-you menu button receives real pointer input");
        Capture(spared ? "06-thanks-for-playing.png" : "09-thanks-killed.png");
    }
    static void Add(List<string> values, string value) { if (!values.Contains(value)) values.Add(value); }
    void CheckDialoguePresentation(GuideDialogueView view)
    {
        Canvas.ForceUpdateCanvases();
        var canvas = view.transform.Find("GuideCanvas").GetComponent<Canvas>();
        Check(canvas.isRootCanvas, "chapter conversation has an independent root canvas");
        var corners = new Vector3[4];
        canvas.GetComponent<RectTransform>().GetWorldCorners(corners);
        var bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        var top = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        Check(top.x - bottom.x >= Screen.width * .95f && top.y - bottom.y >= Screen.height * .95f,
            "chapter dialogue canvas covers the whole game viewport");
        var target = view.Manager.ui.CanChoose
            ? view.Manager.ui.choiceParent.GetComponentsInChildren<Button>(false)[0] : view.Manager.ui.continueButton;
        Check(PointerTarget(target), "visible dialogue control receives real pointer input");
    }
    static bool PointerTarget(Button button)
    {
        var rect = button.GetComponent<RectTransform>();
        var data = new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        bool correct = hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button;
        if (!correct)
        {
            string PathOf(Transform value) => value.parent == null ? value.name : PathOf(value.parent) + "/" + value.name;
            File.WriteAllText(Path.GetFullPath("../sloth-chapter/pointer-detail.txt"),
                "Target: " + PathOf(button.transform) + "\nPosition: " + data.position
                + "\nRect: " + rect.rect + "\nGraphic: " + button.targetGraphic
                + "\nScreen: " + Screen.width + "x" + Screen.height
                + "\nCanvas: " + button.GetComponentInParent<Canvas>().renderMode
                + "\nRaycasters: " + FindObjectsOfType<GraphicRaycaster>().Length
                + "\nGraphic enabled/raycast: " + button.targetGraphic.enabled + "/" + button.targetGraphic.raycastTarget
                + "\nHits:\n"
                + string.Join("\n", hits.Select(hit => PathOf(hit.gameObject.transform) + " sorting " + hit.sortingOrder)));
        }
        return correct;
    }
    void Capture(string name)
    {
        var camera = Camera.main;
        var canvases = FindObjectsOfType<Canvas>().Where(canvas => canvas.isRootCanvas).ToArray();
        var modes = canvases.Select(canvas => canvas.renderMode).ToArray();
        var cameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        var distances = canvases.Select(canvas => canvas.planeDistance).ToArray();
        var target = new RenderTexture(1920, 1080, 24); target.Create();
        var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        try
        {
            foreach (var canvas in canvases)
            { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++)
            { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
            camera.targetTexture = oldTarget; RenderTexture.active = previous;
            Destroy(texture); target.Release(); Destroy(target); Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
