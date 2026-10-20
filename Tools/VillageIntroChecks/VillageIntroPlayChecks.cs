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

public sealed class VillageIntroPlayChecks : MonoBehaviour
{
    readonly List<string> checks = new List<string>();
    readonly string output = Path.GetFullPath("..");
    int errors;
    VillageScreen screen;
    VillageIntroController intro;
    DialogueManager manager;
    DialogueUI ui;
    void Awake() { Application.logMessageReceived += Logged; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    void Check(bool ok, string message)
    { if (!ok) throw new Exception("FAILED: " + message); checks.Add(message); }

    IEnumerator Start()
    {
        var work = new Stack<IEnumerator>();
        work.Push(Exercise());
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
                File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
                Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); yield break;
            }
            if (next is IEnumerator nested) work.Push(nested);
            else yield return next;
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result {
            passed = true, checks = checks.ToArray(), runtimeErrors = errors }, true));
        UnityEditor.EditorApplication.Exit(0);
    }
    [Serializable] sealed class Result { public bool passed; public string[] checks; public int runtimeErrors; }

    IEnumerator Exercise()
    {
        yield return null; yield return null;
        var menu = FindObjectOfType<GameFlowController>();
        Check(menu != null && menu.isMainMenu, "production main menu is loaded");
        menu.BeginNewGame();
        float loadingDeadline = Time.realtimeSinceStartup + 30;
        while (SceneManager.GetActiveScene().name != "Village" && Time.realtimeSinceStartup < loadingDeadline)
            yield return null;
        Check(SceneManager.GetActiveScene().name == "Village", "new game finishes loading the production village");
        yield return null; yield return null;
        Bind();
        Check(intro != null && intro.IsOpen && manager.IsPlaying, "new game enters the mandatory arrival without weapon selection");
        var state = GameRoot.I.state;
        Check(state.inventory.weapons.Count == 0 && state.inventory.equippedWeaponIds.All(string.IsNullOrEmpty)
            && string.IsNullOrEmpty(state.player.currentWeaponId) && state.player.lanternCharges == 0,
            "new arrival has not received weapons or lantern oil");
        Check(intro.Background.name == "01_VillageArrival_Sketch_v3", "arrival uses the selected 1920x1080 sketch");
        Check(ui.contentText.text.Contains("宴席"), "opening prose is from the approved script");
        Capture("01-arrival.png");
        screen.OpenBag(); screen.OpenInn(); screen.OpenShop(); screen.OpenTavern(); screen.LeaveToMap();
        Check(!screen.panelRoot.activeSelf && !screen.IsLoading, "dialogue blocks direct village actions and scene exit");
        intro.TogglePause(); yield return null;
        Check(intro.IsPaused && !ui.gameObject.activeInHierarchy && manager.IsPlaying, "Escape pause preserves the current conversation");
        intro.TogglePause(); yield return null;
        Check(!intro.IsPaused && ui.gameObject.activeInHierarchy, "resume restores the conversation");
        for (int i = 0; i < 30 && !ui.CanChoose; i++) { manager.Next(); yield return null; }
        Check(ui.CanChoose && Choices().Length == 2 && ui.speakerText.text == "妇人", "street scene presents the mother's two responses");
        Check(intro.Background.name == "02_VillageStreet_Sketch_v3", "arrival completion switches to street artwork");
        Capture("02-mother-choices.png");
        Submit(0); yield return null;
        Check(ui.contentText.text.Contains("别无二致"), "language response plays its own feedback");
        manager.Next(); yield return null;
        Check(ui.speakerText.text == "妇人" && ui.contentText.text == "……听不懂最好！",
            "language branch plays the mother's new retort before merging");
        Capture("06-mother-retort.png");
        for (int i = 0; i < 40 && !ui.CanChoose; i++) { manager.Next(); yield return null; }
        Check(Choices().Length == 1, "woodcutter's unclear speech waits for the player's question");
        Submit(0); yield return null;
        for (int i = 0; i < 20 && !ui.CanChoose; i++) { manager.Next(); yield return null; }
        Check(Choices().Length == 2 && ui.contentText.text.Contains("斗篷"), "identity challenge presents its two responses");
        string snapshot = JsonUtility.ToJson(state);
        Submit(1); yield return null;
        Check(ui.contentText.text.Contains("幽默"), "tailor response plays the rebuke");
        for (int i = 0; i < 10 && !ui.CanChoose; i++) { manager.Next(); yield return null; }
        Check(Choices().Length == 2 && ui.contentText.text.Contains("斗篷"), "tailor branch returns to the same identity question");
        Check(JsonUtility.ToJson(state) == snapshot && VillageIntroRules.IsPending(state),
            "joke loop does not finish the introduction or change resources");
        Capture("03-identity-question.png");
        Submit(0); yield return null;
        bool sawName = false;
        for (int i = 0; i < 30 && !ui.CanChoose; i++)
        {
            if (ui.speakerText.text == "冈格") { sawName = true; Capture("07-gangge-introduction.png"); }
            manager.Next(); yield return null;
        }
        Check(sawName && Choices().Length == 1, "correct identity reveals Gangge and reaches the city question");
        Submit(0); yield return null;
        bool sawUnlockNotice = false;
        for (int i = 0; i < 20 && intro.IsOpen && !ui.contentText.text.StartsWith("至少现在"); i++)
        {
            if (ui.contentText.text.StartsWith("解锁：埃尔南多城区地图")) sawUnlockNotice = true;
            manager.Next(); yield return null;
        }
        Check(sawUnlockNotice && ui.contentText.text == "至少现在，你总算知道自己该往哪里走了。"
            && VillageIntroRules.IsPending(state), "updated unlock notice precedes the closing narration without ending early");
        manager.Next(); yield return null;
        Check(!intro.IsOpen && VillageIntroRules.IsAwaitingMayor(state), "only the final completed dialogue unlocks the village");
        Check(MainStoryRules.GoalText(state).Contains("村长家") && intro.MayorButton.gameObject.activeInHierarchy,
            "village objective and mayor house entrance are visible");
        Canvas.ForceUpdateCanvases();
        var goalCorners = new Vector3[4];
        var statusCorners = new Vector3[4];
        intro.MayorButton.transform.parent.Find("Objective").GetComponent<RectTransform>().GetWorldCorners(goalCorners);
        screen.UICanvas.GetComponentsInChildren<Button>(true).Single(button => button.name == "Btn_Leave")
            .GetComponent<RectTransform>().GetWorldCorners(statusCorners);
        Check(goalCorners[2].y < statusCorners[0].y - 16, "new objective stays below the village navigation buttons");
        Check(state.world.time == 0 && state.inventory.items.Count == 0 && state.inventory.weapons.Count == 0
            && state.player.lanternCharges == 0, "opening dialogue grants no equipment and spends no world time");
        Capture("04-village-objective.png");
        screen.OpenInn(); screen.OpenShop(); screen.OpenTavern(); screen.LeaveToMap();
        Check(!screen.panelRoot.activeSelf && !screen.IsLoading, "unrelated facilities remain gated until later onboarding");
        screen.OpenBag(); yield return null;
        Check(screen.panelRoot.activeSelf && screen.panelBag.activeInHierarchy, "backpack opens after the first scene");
        screen.ClosePanels(); yield return new WaitForSecondsRealtime(.8f);
        intro.OpenMayorHouse(); yield return null;
        Check(intro.IsOpen && intro.Background.name == "03_MayorHouse_Sketch_v3", "mayor marker opens the next location approach");
        Capture("05-mayor-approach.png");
        yield return WalkDialogue();
        Check(VillageIntroRules.NeedsBlacksmith(state) && !VillageIntroRules.CanLeaveVillage(state),
            "mayor commission leads to the blacksmith and keeps the city gate locked");
        Check(ItemRules.Count(state.inventory, ItemTable.Bandage) == 1 && ItemRules.Count(state.inventory, ItemTable.HolyWater) == 1
            && state.inventory.weapons.Count == 0, "mayor grants one capped supply package without an early weapon");
        string beforeRepeat = JsonUtility.ToJson(state);
        VillageIntroRules.ApplyReward(state, "guide_mayor_commission:supplies", GuideReward.Supplies);
        Check(JsonUtility.ToJson(state) == beforeRepeat, "repeated mayor supply callback cannot duplicate the package");
        intro.OpenBlacksmith(); yield return null;
        Capture("08-blacksmith.png");
        yield return WalkDialogue(new Dictionary<string, int> { { "question", 1 }, { "types", 0 }, { "choose", 1 } });
        Check(state.story.guideEquipmentReady && state.inventory.weapons.SequenceEqual(new[] { "sword", "hammer" })
            && state.player.lanternCharges == 2 && state.inventory.equippedWeaponIds[2] == "hammer",
            "self-selected second weapon, sword and full lantern are granted and equipped");
        Check(VillageIntroRules.CanLeaveVillage(state) && !screen.CanUseFacilities,
            "equipment opens the city gate while trade and inn remain locked");
        int outsideTime = state.world.time;
        screen.LeaveToMap();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "MainMap", "leave city for the real world map");
        yield return null; yield return null;
        var map = FindObjectOfType<WorldMapController>();
        yield return WaitUntil(() => map.Guide != null && map.Guide.IsOpen, "outside narration opens before node movement");
        manager = map.Guide.View.Manager; ui = manager.ui;
        Capture("09-outside.png"); yield return WalkDialogue();
        Check(state.story.guideOutsideReady && state.world.time == outsideTime, "outside narration unlocks movement without charging time");
        string wrong = map.Progression.GetNeighbors(map.Progression.Layout.villageNodeId)
            .First(id => id != state.story.tutorialNodeId);
        map.MoveToNode(wrong);
        Check(state.world.time == outsideTime && state.map.currentNodeId == map.Progression.Layout.villageNodeId,
            "guide prevents leaving through an unrelated node without spending time");
        map.MoveToNode(state.story.tutorialNodeId);
        yield return WaitUntil(() => map.Guide.IsOpen && !MapSceneTransition.IsBusy, "move to old mill and open encounter prose");
        manager = map.Guide.View.Manager; ui = manager.ui;
        Check(state.world.time == outsideTime + 1, "walking to the old mill costs exactly one unit");
        yield return WalkDialogue();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "CombatScene", "encounter prose enters the real combat scene");
        yield return null; yield return null;
        var battle = FindObjectOfType<BattleFlowController>();
        Check(battle.Guide != null && battle.Enemies.Count == 1 && battle.Enemies[0].Definition.displayName == "旧磨坊怨影",
            "guide uses its authored single enemy instead of the random opening roster");
        manager = battle.Guide.View.Manager; ui = manager.ui;
        Capture("10-switch-tutorial.png"); yield return WalkDialogue();
        Capture("15-switch-objective.png");
        battle.PlayerAttack();
        Check(battle.CurrentState == BattleState.PlayerTurn, "first combat action waits for a real weapon switch");
        int roundBefore = battle.RoundNumber;
        FindObjectOfType<BattleWeaponController>().SwitchWeapon(2); yield return null;
        Check(battle.Guide.Step == GuideBattleStep.Lantern && battle.RoundNumber == roundBefore
            && state.world.time == outsideTime + 1, "weapon switching advances teaching without a turn or time cost");
        manager = battle.Guide.View.Manager; ui = manager.ui;
        Capture("11-lantern-tutorial.png"); yield return WalkDialogue();
        var enemy = battle.Enemies[0];
        Check(battle.PlayerUseItem(ItemTable.Lantern).ok, "lantern can be selected after switching");
        battle.AttackTarget(enemy);
        yield return WaitUntil(() => battle.CurrentState == BattleState.PlayerTurn && battle.Guide.View.IsOpen, "lantern action reaches resolve teaching");
        Check(state.player.lanternCharges == 1 && enemy.GetComponent<DamageResistance>().UnknownWeaknessCount == 0,
            "actual lantern action consumes one charge and discovers the enemy weaknesses");
        manager = battle.Guide.View.Manager; ui = manager.ui; yield return WalkDialogue();
        // Fail after learning the lantern. Discovery and completed lessons must survive the normal revival path.
        battle.Player.TakeDamage(battle.Player.CurrentHP - 1);
        battle.PlayerDefend();
        yield return WaitUntil(() => battle.CurrentState == BattleState.Defeat, "guide defeat uses the real enemy turn");
        Check((state.story.guideLessons & GuideLesson.Lantern) != 0 && battle.Player.CurrentDetermination > 0,
            "actual lantern and defense lessons are recorded before defeat");
        FindObjectOfType<BattleResultUI>().ReturnToWorldMap();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Village", "guide defeat revives at the village checkpoint");
        yield return null; yield return null; Bind();
        Check(!intro.IsOpen && state.player.hp == state.player.maxHp && state.player.lanternCharges == 2
            && state.inventory.weapons.Count == 2, "revival retains equipment and restores enough oil to retry safely");
        screen.LeaveToMap();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "MainMap", "retry leaves the village without replaying arrival");
        yield return null; yield return null;
        map = FindObjectOfType<WorldMapController>();
        yield return WaitUntil(() => !map.GetComponent<WorldMapSceneView>().IsRevealing, "retry map is ready");
        Capture("14-map-route.png");
        Check(map.Guide.TargetLabel != null && map.Guide.TargetLabel.gameObject.activeInHierarchy
            && map.Guide.TargetLabel.text.Contains("旧磨坊小路"), "the tutorial destination has a visible label on the real map node");
        map.MoveToNode(state.story.tutorialNodeId);
        yield return WaitUntil(() => map.Guide.IsOpen && !MapSceneTransition.IsBusy, "retry reaches the pending encounter");
        manager = map.Guide.View.Manager; ui = manager.ui; yield return WalkDialogue();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "CombatScene", "retry enters real combat");
        yield return null; yield return null;
        battle = FindObjectOfType<BattleFlowController>(); enemy = battle.Enemies[0];
        outsideTime++;
        Check(battle.Guide.Step == GuideBattleStep.Break && enemy.GetComponent<DamageResistance>().UnknownWeaknessCount == 0,
            "retry resumes the unfinished lesson and does not demand an impossible repeated lantern reveal");
        manager = battle.Guide.View.Manager; ui = manager.ui; yield return WalkDialogue();
        for (int action = 0; action < 6 && battle.Guide.Step == GuideBattleStep.Break; action++)
        {
            battle.PlayerAttack(); battle.AttackTarget(enemy);
            yield return WaitUntil(() => battle.CurrentState == BattleState.PlayerTurn, "weakness attack completes its full turn");
        }
        Check(battle.Guide.Step == GuideBattleStep.Items && enemy.Break.ProtectedThroughRound > 0,
            "real weakness hits and break advance to item teaching");
        manager = battle.Guide.View.Manager; ui = manager.ui;
        Capture("12-items-tutorial.png"); yield return WalkDialogue();
        Check(battle.Guide.Step == GuideBattleStep.Free, "item teaching releases normal combat actions");
        int hurtHp = battle.Player.CurrentHP;
        int peakHp = hurtHp;
        Action<Combatant> healing = actor => peakHp = Math.Max(peakHp, actor.CurrentHP);
        battle.Player.OnHpChanged += healing;
        Check(battle.PlayerUseItem(ItemTable.Bandage).ok, "initial bandage can be used in live combat");
        yield return WaitUntil(() => battle.CurrentState == BattleState.PlayerTurn, "bandage uses one combat action");
        battle.Player.OnHpChanged -= healing;
        Check(ItemRules.Count(state.inventory, ItemTable.Bandage) == 0 && peakHp > hurtHp,
            "bandage consumes the supply and really heals");
        if (battle.CanUseWeaponSkill)
        {
            int resolve = battle.Player.CurrentDetermination;
            battle.PlayerSkill();
            if (battle.CurrentState == BattleState.SelectingTarget) battle.AttackTarget(enemy);
            yield return WaitUntil(() => battle.CurrentState == BattleState.PlayerTurn || battle.CurrentState == BattleState.Victory,
                "weapon skill completes its action");
            Check(battle.Player.CurrentDetermination < resolve, "weapon skill spends the accumulated determination");
        }
        for (int action = 0; action < 20 && battle.CurrentState != BattleState.Victory; action++)
        {
            battle.PlayerAttack(); battle.AttackTarget(enemy);
            yield return WaitUntil(() => battle.CurrentState == BattleState.PlayerTurn || battle.CurrentState == BattleState.Victory,
                "free combat action resolves");
        }
        Check(battle.CurrentState == BattleState.Victory && state.world.time == outsideTime + 1,
            "winning combat does not charge time per action");
        FindObjectOfType<BattleResultUI>().ReturnToWorldMap();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "MainMap", "victory returns to world map");
        yield return null; yield return null;
        map = FindObjectOfType<WorldMapController>();
        Check(VillageIntroRules.NeedsReport(state) && state.world.time == outsideTime + 2,
            "battle settlement costs one unit and requires the return report");
        yield return WaitUntil(() => !map.GetComponent<WorldMapSceneView>().IsRevealing && !MapSceneTransition.IsBusy,
            "return route waits for map discovery to finish");
        string village = map.Progression.Layout.villageNodeId;
        map.MoveToNode(village);
        if (map.IsFastTravelConfirmationOpen) { map.ConfirmFastTravel(); yield return null; map.MoveToNode(village); }
        else if (map.IsVillageEntryConfirmationOpen) map.ConfirmVillageEntry();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Village", "return through the map to the city");
        yield return null; yield return null; Bind();
        yield return WaitUntil(() => !MapSceneTransition.IsBusy, "village narration is visible after the entry fade");
        Check(intro.IsOpen && intro.ActiveDialogue.dialogueId == "guide_return", "first return plays the revised city narration");
        yield return WalkDialogue();
        intro.OpenMayorHouse(); yield return null;
        Capture("13-report.png"); yield return WalkDialogue();
        Check(state.story.guideCompleted && screen.CanUseFacilities && !VillageIntroRules.IsRestricted(state)
            && MainStoryRules.GoalText(state).Contains("懒惰"), "report completes the guide and releases services and the sloth main story");
        intro.OpenBlacksmith(); yield return null;
        Check(screen.panelShop.activeInHierarchy, "blacksmith changes from starter rewards to real trade after the guide");
        screen.ClosePanels(); screen.OpenInn(); yield return null;
        Check(screen.panelInn.activeInHierarchy, "real inn opens after completing the guide");
        screen.ClosePanels();
        screen.OpenTavern(); yield return null;
        Check(screen.panelEvent.activeInHierarchy, "tavern and ordinary city events unlock after the guide");
        screen.ClosePanels();
        string completedSnapshot = JsonUtility.ToJson(state);
        var loading = SceneManager.LoadSceneAsync("Village");
        while (!loading.isDone) yield return null;
        yield return null; yield return null; Bind();
        Check(intro != null && !intro.IsOpen && JsonUtility.ToJson(state) == completedSnapshot,
            "reentering after the guide never replays it or grants more equipment");
        // Exercise the mother's other response in a fresh run, and cancellation before the real ending.
        var template = GameRoot.I.villageMapTemplate;
        GameRoot.I.state = NewRunFactory.CreateForIntroduction(template.Load(), 341);
        loading = SceneManager.LoadSceneAsync("Village");
        while (!loading.isDone) yield return null;
        yield return null; yield return null; Bind();
        for (int i = 0; i < 30 && !ui.CanChoose; i++) { manager.Next(); yield return null; }
        Submit(1); yield return null;
        Check(ui.speakerText.text == "猎魔人" && ui.contentText.text.Contains("投宿"), "second mother response shows the protagonist's line");
        manager.Next(); yield return null;
        Check(ui.contentText.text.Contains("低低地笑了一声"), "second mother response has distinct narrated feedback");
        manager.CancelDialogue(); yield return null;
        Check(VillageIntroRules.IsPending(GameRoot.I.state), "cancelling before completion does not unlock the village");
        var legacy = NewRunFactory.Create(template.Load(), "axe", 342);
        Check(!legacy.story.useVillageIntroduction && legacy.inventory.weapons.SequenceEqual(new[] { "axe" }),
            "legacy/debug weapon runs retain their existing setup");
        Check(errors == 0, "native production scene playthrough has no runtime errors");
    }

    void Bind()
    {
        screen = FindObjectOfType<VillageScreen>(); intro = screen != null ? screen.Introduction : null;
        manager = intro != null ? intro.Manager : null; ui = manager != null ? manager.ui : null;
    }
    IEnumerator WalkDialogue(Dictionary<string, int> choices = null)
    {
        for (int i = 0; i < 250 && manager.IsPlaying; i++)
        {
            if (ui.CanChoose)
            {
                int choice = choices != null && choices.TryGetValue(manager.CurrentNodeId, out int selected) ? selected : 0;
                Submit(choice);
            }
            else manager.Next();
            yield return null;
        }
        Check(!manager.IsPlaying, "authored conversation reaches its actual ending");
    }
    IEnumerator WaitUntil(Func<bool> condition, string message)
    {
        float deadline = Time.realtimeSinceStartup + 40;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Check(condition(), message);
    }
    Button[] Choices() => ui.choiceParent.GetComponentsInChildren<Button>(false);
    void Submit(int index)
    {
        var button = Choices()[index];
        EventSystem.current.SetSelectedGameObject(button.gameObject);
        ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }
    void Capture(string name)
    {
        var camera = Camera.main;
        var canvases = FindObjectsOfType<Canvas>().Where(value => value.isRootCanvas).ToArray();
        var modes = canvases.Select(value => value.renderMode).ToArray();
        var cameras = canvases.Select(value => value.worldCamera).ToArray();
        var distances = canvases.Select(value => value.planeDistance).ToArray();
        var target = new RenderTexture(1920, 1080, 24); target.Create();
        var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        try
        {
            foreach (var canvas in canvases)
            { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
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
