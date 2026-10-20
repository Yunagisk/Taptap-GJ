using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class DayStartSavePlayChecks : MonoBehaviour
{
    readonly List<string> checks = new List<string>();
    readonly string output = Path.GetFullPath("..");
    DayStartSaveStore slot;
    WorldMapTemplate template;
    MapLayout layout;
    int errors;
    [Serializable] sealed class Result { public bool passed; public string[] checks; public int runtimeErrors; public string error; }
    void Awake() { Application.logMessageReceived += Logged; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add(message); }
    IEnumerator Start()
    {
        var stack = new Stack<IEnumerator>(); stack.Push(Exercise());
        while (stack.Count > 0)
        {
            object next;
            try
            {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
            }
            catch (Exception error) { Finish(error); yield break; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
    }
    void Finish(Exception error)
    {
        Time.timeScale = 1;
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result {
            passed = error == null, checks = checks.ToArray(), runtimeErrors = errors, error = error?.ToString() }, true));
        UnityEditor.EditorApplication.Exit(error == null ? 0 : 1);
    }
    IEnumerator Wait(Func<bool> ready, string label)
    {
        float deadline = Time.realtimeSinceStartup + 35;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Check(ready(), label); yield return null;
    }
    void Call(object target, string name) => target.GetType().GetMethod(name,
        BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    GameState Load()
    {
        Check(slot.TryLoad(layout, out var state, out var error), "morning slot reads: " + error);
        return state;
    }
    GameState Fresh(int seed = 76543)
    {
        var state = NewRunFactory.Create(layout, "sword", seed);
        GameRoot.I.state = state;
        return state;
    }
    MapProgression Map(GameState state) => new MapProgression(layout, state.map, state.world, state.map.randomSeed);

    IEnumerator Exercise()
    {
        yield return null; yield return null;
        var menu = FindObjectOfType<GameFlowController>();
        template = menu.template; layout = template.Load();
        string folder = Path.Combine(output, "slot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        slot = new DayStartSaveStore(Path.Combine(folder, "day-start-save-v1.json"));
        GameRoot.I.SaveSlot = slot;
        menu.GetComponent<MainMenuOpening>()?.SkipOpening();
        Call(menu, "RefreshSaveAvailability");
        Check(!menu.continueButton.interactable && !slot.TryLoad(layout, out _, out _), "empty slot disables Continue");
        menu.BeginNewGame();
        yield return Wait(() => SceneManager.GetActiveScene().name == "Village", "New Game loads village");
        var initial = Load();
        Check(initial.world.time == 0 && initial.story.useVillageIntroduction && initial.player.hp == 100
            && initial.map.wildernessEvents == null,
            "new run writes a complete first-morning save before introduction");
        yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
        RulesChecks(folder);

        // Recreate the session host, so continuing depends on disk rather than an in-memory checkpoint.
        var morning = Fresh(); morning.world.time = 6; morning.player.hp = 72;
        morning.player.lanternCharges = 1; morning.inventory.items.Add(new ItemStack("bandage", 2));
        Check(GameRoot.I.SaveMorning(morning, layout), "save a village morning for a fresh session");
        Destroy(GameRoot.I.gameObject); yield return null;
        var newRoot = new GameObject("GameRoot").AddComponent<GameRoot>(); newRoot.SaveSlot = slot;
        yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
        menu = FindObjectOfType<GameFlowController>(); menu.GetComponent<MainMenuOpening>()?.SkipOpening();
        Check(menu.continueButton.interactable, "a recreated session enables real Continue from the one disk slot");
        Capture("menu-continue.png");
        newRoot.state.player.hp = 9;
        menu.continueButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.15f);
        Check(DayStartLoadTransition.IsBusy && newRoot.state.player.hp == 9,
            "load fades to black before replacing the current run");
        yield return Wait(() => SceneManager.GetActiveScene().name == "Village" && !DayStartLoadTransition.IsBusy,
            "Continue finishes loading the saved village morning");
        Check(newRoot.state.world.time == 6 && newRoot.state.player.hp == 72
            && ItemRules.Count(newRoot.state.inventory, "bandage") == 2 && newRoot.state.player.lanternCharges == 1,
            "Continue restores morning resources and time instead of healing or preserving afternoon progress");

        // The pause menu exercises a second read path and the installed parchment layout.
        morning = Fresh(); morning.world.time = 9; morning.player.hp = 63;
        string battleNode = layout.nodes.First(node => node.kind == MapNodeKind.Battle).id;
        morning.map.currentNodeId = battleNode;
        morning.map.visitedNodes.Add(battleNode); morning.map.clearedNodes.Add(battleNode);
        morning.map.revealedNodes.Add(battleNode); morning.story.slothHookSeen = true;
        Check(newRoot.SaveMorning(morning, layout), "save an outdoor morning");
        Check(DayStartLoadTransition.TryBegin(template, error => { throw new Exception(error); }), "begin map morning load");
        yield return Wait(() => SceneManager.GetActiveScene().name == "MainMap" && !DayStartLoadTransition.IsBusy,
            "outdoor morning restores the map scene");
        var mapView = FindObjectOfType<WorldMapSceneView>();
        yield return Wait(() => !mapView.IsRevealing, "map reveal finishes after load");
        var mapMenu = FindObjectOfType<GameFlowController>(); mapMenu.OpenPause();
        var reload = mapMenu.pauseOverlay.transform.Find("Window/Btn_LoadDay").GetComponent<Button>();
        Check(reload.interactable && reload.GetComponentInChildren<TMP_Text>().text == "回到今天早晨",
            "pause menu has an enabled morning reload button");
        Capture("pause-reload.png");
        newRoot.state.world.time = 10; newRoot.state.player.hp = 11;
        string savedBytes = File.ReadAllText(slot.Path);
        reload.onClick.Invoke();
        yield return Wait(() => !DayStartLoadTransition.IsBusy && GameRoot.I.state.player.hp == 63,
            "pause reload restores the saved morning");
        Check(GameRoot.I.state.world.time == 9 && File.ReadAllText(slot.Path) == savedBytes,
            "loading neither advances the clock nor overwrites the morning slot");

        // Reproduce the screenshot: show 1 HP, kill the actor, then keep the 0 readout after destruction.
        var run = Fresh(); run.player.hp = 72;
        Check(GameRoot.I.SaveMorning(run, layout), "save before a real combat defeat");
        var progression = Map(run);
        string neighbor = layout.nodes.First(node => node.kind == MapNodeKind.Battle
            && progression.CanMove(node.id)).id;
        Check(progression.TryMove(neighbor, out var request), "prepare real map battle request");
        newRoot.battleMapTemplate = template; newRoot.battleRequestId = request.requestId;
        newRoot.battleReturnScene = "MainMap";
        yield return SceneManager.LoadSceneAsync("CombatScene");
        yield return Wait(() => FindObjectOfType<BattleHudView>()?.Flow?.Player != null, "combat HUD binds the real player");
        var hud = FindObjectOfType<BattleHudView>(); var battle = hud.Flow;
        Time.timeScale = 0;
        battle.Player.TakeDamage(battle.Player.CurrentHP - 1);
        Check(hud.healthText.text == "1 / 100", "HP changes repaint immediately to 1");
        battle.Player.LoseRaw(1);
        Check(hud.healthText.text == "0 / 100" && hud.playerHealth.rectTransform.localScale.x == 0,
            "lethal status damage immediately paints 0 HP and an empty bar");
        Call(battle, "Defeat"); yield return null; yield return null;
        Check(battle.Player == null && hud.healthText.text == "0 / 100", "destroyed player no longer leaves a stale 1 HP readout");
        var result = battle.GetComponent<BattleResultUI>();
        var returnButton = (Button)typeof(BattleResultUI).GetField("returnButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(result);
        Check(returnButton.interactable && returnButton.GetComponentInChildren<TMP_Text>().text == "回到今天早晨",
            "defeat button loads an actual morning slot");
        Capture("defeat-zero-hp.png");
        returnButton.onClick.Invoke();
        yield return Wait(() => SceneManager.GetActiveScene().name == "Village" && !DayStartLoadTransition.IsBusy,
            "defeat reload fades into the saved village");
        Check(GameRoot.I.state.player.hp == 72 && GameRoot.I.state.world.time == 0
            && GameRoot.I.state.map.currentNodeId == layout.villageNodeId
            && string.IsNullOrEmpty(GameRoot.I.state.map.pendingRequestId) && GameRoot.I.battleRequestId == null,
            "defeat restores exact morning health, position and clock and clears stale combat requests");

        run = Fresh(); run.world.time = 2; progression = Map(run);
        Check(progression.TryMove(neighbor, out request), "reach an unfinished battle at dawn");
        Check(DayStartLoadTransition.TryBegin(template, error => { throw new Exception(error); }), "reload a dawn battle arrival");
        yield return Wait(() => SceneManager.GetActiveScene().name == "CombatScene" && !DayStartLoadTransition.IsBusy
            && FindObjectOfType<BattleHudView>()?.Flow?.Player != null,
            "loaded dawn battle request resumes through the real map transition");
        Check(GameRoot.I.state.world.time == 3 && GameRoot.I.battleRequestId == request.requestId,
            "resuming a dawn battle does not charge a second road or change the request");
    }

    void RulesChecks(string folder)
    {
        var root = GameRoot.I;
        var state = Fresh(); state.player.hp = 64; state.player.resolve = 2;
        state.inventory.items.Add(new ItemStack("holy_water", 1));
        state.player.ordinaryStatuses.Add(new StatusSnapshot { id = StatusId.Poison, stacks = 2 });
        state.world.time = 2;
        WorldResourceRules.SpendTime(layout, state.map, state.world, 1, true);
        string snapshot = JsonUtility.ToJson(Load());
        state.world.time = 4; state.world.sinRemnant += 20; state.player.hp = 0;
        state.inventory.items.Clear(); state.story.guideRewards.Add("afternoon-only");
        state.map.clearedNodes.Add("afternoon-only"); state.shop.availableWeaponIds.Clear();
        Check(JsonUtility.ToJson(Load()) == snapshot, "all player, inventory, map, shop, story, statuses and events survive later mutations");
        var firstRead = Load(); firstRead.player.hp = 1;
        Check(Load().player.hp == 64, "each read creates independent state");
        string bytes = File.ReadAllText(slot.Path);
        var preview = NewRunFactory.Create(layout, "axe", 999); preview.world.time = 2;
        WorldResourceRules.SpendTime(layout, preview.map, preview.world, 1, true);
        Check(File.ReadAllText(slot.Path) == bytes, "preview and independent rule worlds cannot overwrite the player's slot");
        state = Fresh(); state.world.time = 5; state.player.hp = 81;
        WorldResourceRules.SpendTime(layout, state.map, state.world, 1, true);
        Check(Load().world.time == 6 && Load().player.hp == 81 && Directory.GetFiles(folder, "*.json").Length == 1,
            "next dawn atomically replaces the only slot");
        bytes = File.ReadAllText(slot.Path);
        state.world.time = 7;
        Check(!slot.TrySave(state, layout, out _) && File.ReadAllText(slot.Path) == bytes,
            "afternoon writes are rejected without damaging the previous morning");
        File.WriteAllText(slot.Path, "{broken json");
        Check(!slot.TryLoad(layout, out _, out _) && ReferenceEquals(root.state, state),
            "corrupt JSON is rejected without replacing the current run");
        File.WriteAllText(slot.Path, bytes);
        File.WriteAllText(slot.Path + ".tmp", "incomplete write");
        Check(Load().world.time == 6, "an interrupted temporary write does not replace the valid slot");

        state = Fresh(); state.world.time = 2;
        var map = Map(state);
        string next = layout.nodes.First(node => node.kind == MapNodeKind.Battle && map.CanMove(node.id)).id;
        map.TryMove(next, out var request);
        var saved = Load();
        Check(saved.world.time == 3 && saved.map.pendingRequestId == request.requestId && Map(saved).GetPendingRequest() != null,
            "dawn arrival saves a resumable unfinished battle request");
        state.world.time = 5; map.TryCompleteNode(request.requestId); saved = Load();
        Check(saved.world.time == 6 && saved.map.clearedNodes.Contains(next) && string.IsNullOrEmpty(saved.map.pendingRequestId),
            "dawn victory saves completion rather than replaying an already won battle");

        state = Fresh(); state.world.time = 2; state.player.hp = 40; state.player.lanternCharges = 0;
        Check(VillageInnRules.TryRest(state, layout, 2, out _), "rest reaches next morning");
        saved = Load();
        Check(saved.world.time == 3 && saved.player.hp == 100 && saved.player.lanternCharges == 2
            && saved.shop.itemStockDay == state.shop.itemStockDay,
            "inn dawn saves after healing, lantern refill and shop refresh");

        state = Fresh(); state.world.time = 2; state.player.hp = 20;
        state.player.nextCampRecoveryBonusPercent = 20;
        string camp = layout.nodes.First(node => node.kind == MapNodeKind.Camp).id;
        state.map.currentNodeId = camp; state.map.pendingNodeId = camp; state.map.pendingRequestId = "save-camp";
        state.map.visitedNodes.Add(camp); state.map.revealedNodes.Add(camp);
        Check(Map(state).TryCompleteNode("save-camp"), "camp rest completes at dawn");
        WorldResourceRules.RecoverAtCamp(state.player); saved = Load();
        Check(saved.player.hp == state.player.hp && saved.player.nextCampRecoveryBonusPercent == 0
            && saved.map.clearedNodes.Contains(camp) && string.IsNullOrEmpty(saved.map.pendingRequestId),
            "camp dawn saves recovery, consumes its bonus and clears the completed camp request");

        state = Fresh();
        foreach (var node in layout.nodes)
        {
            if (!state.map.clearedNodes.Contains(node.id)) state.map.clearedNodes.Add(node.id);
            if (!state.map.visitedNodes.Contains(node.id)) state.map.visitedNodes.Add(node.id);
            if (!state.map.revealedNodes.Contains(node.id)) state.map.revealedNodes.Add(node.id);
        }
        map = Map(state); state.world.time = 2;
        MapTravelPlan route = null;
        foreach (var node in layout.nodes)
            if (map.TryPlanFastTravel(node.id, out var plan) && plan.NodeIds.Count >= 3) { route = plan; break; }
        Check(route != null && map.TryFastTravel(route), "fast travel traverses a multi-road route across dawn");
        saved = Load();
        // The last dawn may be later than the first for long routes.
        int dawnStep = (saved.world.time - 2);
        Check(saved.world.time % 3 == 0 && saved.map.currentNodeId == route.NodeIds[dawnStep]
            && saved.map.sinRollCount == dawnStep,
            "fast travel stores the exact road reached at dawn, including its random-roll position");

        state = Fresh(); state.world.time = 8;
        state.world.villageEvents.occurrences.Add(new VillageEventOccurrence {
            id = "save-test", definitionId = "v03", appearedTime = 6, expiresTime = 15 });
        var choice = VillageEventRules.TryChoose(state, layout, "save-test", 0);
        saved = Load();
        Check(choice.success && state.world.time == 10 && saved.world.time == 9
            && ItemRules.Count(saved.inventory, "holy_water") == ItemRules.Count(state.inventory, "holy_water")
            && saved.world.villageEvents.occurrences.Find(value => value.id == "save-test").resolved,
            "a two-unit tavern choice crossing dawn saves its reward and resolved choice together");

        var source = JsonUtility.FromJson<WildernessEventCatalog>(File.ReadAllText(
            Path.Combine(Application.dataPath, "Data/Wilderness/WildernessEvents.json")));
        var catalog = MapWildernessRules.BuildCatalog(source, layout);
        WildernessNodeState question = null;
        for (int seed = 1; seed <= 100 && question == null; seed++)
        {
            state = Fresh(seed); MapWildernessRules.EnsureInitialized(state, layout, catalog);
            question = state.map.wildernessEvents.nodes.FirstOrDefault(node =>
                WildernessEventRules.FindDefinition(catalog, node.templateId).choices[0].timeCost == 2);
        }
        Check(question != null, "find a production two-unit wilderness choice");
        state.world.time = 2; state.map.currentNodeId = question.id;
        state.map.wildernessEvents.currentNodeId = question.id;
        state.map.pendingNodeId = question.id; state.map.pendingRequestId = "save-event";
        Check(WildernessEventRules.TryEnter(state.map.wildernessEvents, question.id), "open seeded wilderness choice");
        var wildResult = WildernessEventRules.TryChoose(state.map.wildernessEvents, catalog, state, layout, 0);
        saved = Load();
        var savedQuestion = WildernessEventRules.FindNode(saved.map.wildernessEvents, question.id);
        Check(wildResult.success && saved.world.time == 3 && savedQuestion.resolved
            && savedQuestion.encounterPending == question.encounterPending
            && saved.player.hp == state.player.hp && saved.world.sinRemnant == state.world.sinRemnant
            && saved.map.wildernessEvents.history.Count == state.map.wildernessEvents.history.Count,
            "multi-unit wilderness choice saves coherent outcomes, resources, history and encounters at dawn");
        WildernessEventRules.ValidateLevel(saved.map.wildernessEvents, catalog);
        Check(true, "saved wilderness state passes production validation and keeps seeded rolls");
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
