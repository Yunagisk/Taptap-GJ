using System;
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

[InitializeOnLoad]
public static class BattleTargetingValidation
{
    const string Pending = "BattleTargetingValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static bool finished;
    static bool actionCaptured;
    static int stage = 5, attackedRound, targetHpBefore, expectedSwordDamage;
    static EnemyCombatant attackedEnemy;
    static BattleHudEnemyView survivingRow;
    static bool shatterCaptured;
    static Sprite survivingPortrait;
    static int survivingHp;
    static double deadline;
    [Serializable] sealed class Result { public bool passed; public string[] checks; public string error; }
    static string Output
    {
        get { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-targetingResult"); return i < 0 ? "targeting-result.json" : args[i + 1]; }
    }

    static BattleTargetingValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Logged;
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated validation project.");
        EditorSceneManager.OpenScene("Assets/Scenes/CombatScene.unity");
        SetGameViewSize(1600, 900);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    static void SetGameViewSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance").GetValue(null);
        var getGroup = sizesType.GetMethod("GetGroup");
        var groupKind = Enum.Parse(getGroup.GetParameters()[0].ParameterType, "Standalone");
        var group = getGroup.Invoke(sizes, new[] { groupKind });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var constructor = sizeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .First(c => c.GetParameters().Length == 4);
        var fixedKind = Enum.ToObject(constructor.GetParameters()[0].ParameterType, 1);
        var size = constructor.Invoke(new object[] { fixedKind, width, height, "Battle UI validation" });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
    }
    static void Changed(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        deadline = EditorApplication.timeSinceStartup + 160;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (finished) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Targeting validation timed out.");
            var flow = UnityEngine.Object.FindObjectOfType<BattleFlowController>();
            var hud = UnityEngine.Object.FindObjectOfType<BattleHudView>();
            if (flow == null || hud == null || flow.RoundNumber == 0 || hud.TargetingArrow == null) return;
            var arrow = hud.TargetingArrow;
            var setup = UnityEngine.Object.FindObjectOfType<SmallMonsterEncounterSetup>();
            if (setup != null && !setup.GroundAligned) return;
            if (stage == 5)
            {
                if (PlayerStatusNativeChecks.Step(flow, hud, name => Capture(name, hud, Vector2.zero), Require)) stage = 0;
                return;
            }
            if (stage == 0)
            {
                Canvas.ForceUpdateCanvases();
                ValidateBackground();
                ValidateGround(flow, hud);
                ValidateOutlines(flow);
                Require(hud.EnemyRows.All(row => row.frame.fill.maxColorComponent < .3f),
                    "enemy HUD cards use the dark brown palette");
                SetIntent(hud.EnemyRows[0].Enemy, "RatBasic");
                SetIntent(hud.EnemyRows[1].Enemy, "RatStatus");
                foreach (var row in hud.EnemyRows) row.Refresh();
                arrow.UpdatePointer(new Vector2(Screen.width * .32f, Screen.height * .55f));
                Require(hud.EnemyRows.All(row => !NameTag(row, hud).activeSelf), "world enemy names are hidden while idle");
                Require(hud.EnemyRows.All(row => !row.IntentExpanded), "all intent details are collapsed while idle");
                var secondIntent = hud.transform.Find("WorldTag_2/Intent");
                var poisonIntent = AssetDatabase.LoadAssetAtPath<EnemyAttackData>("Assets/Data/EnemyAttacks/SmallMonsters/RatStatus.asset");
                Require(secondIntent.Find("Amount").GetComponent<TMP_Text>().text == poisonIntent.CompactIntentText
                    && ((RectTransform)secondIntent).rect.height == 34, "poison intent idles as one compact damage row");
                arrow.UpdatePointer(ScreenOf(hud.EnemyRows[0]));
                Require(NameTag(hud.EnemyRows[0], hud).activeSelf && !NameTag(hud.EnemyRows[1], hud).activeSelf
                    && !arrow.IsVisible, "ordinary hover reveals only the inspected enemy name without opening an attack bow");
                Require(hud.EnemyRows[0].IntentExpanded && !hud.EnemyRows[1].IntentExpanded,
                    "hovering one monster expands only its own intent details");
                arrow.UpdatePointer(ScreenOf(hud.EnemyRows[1]));
                Require(secondIntent.Find("Details/SpecialEffect").GetComponent<TMP_Text>().text.Contains("中毒"),
                    "expanded poison intent explains the damage and status rider");
                Capture("intent-hover.png", hud, ScreenOf(hud.EnemyRows[1]));
                arrow.UpdatePointer(new Vector2(Screen.width * .32f, Screen.height * .55f));
                Require(hud.EnemyRows.All(row => !row.IntentExpanded), "leaving a monster collapses its details again");
                arrow.UpdatePointer(ScreenOf((RectTransform)secondIntent, hud));
                Require(arrow.HoveredEnemy == hud.EnemyRows[1].Enemy && hud.EnemyRows[1].IntentExpanded,
                    "hovering the fixed intent pill reveals its matching monster's details");
                arrow.UpdatePointer(new Vector2(Screen.width * .32f, Screen.height * .55f));
                Require(!hud.prompt.gameObject.activeSelf, "removed instruction line stays hidden");
                var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
                Require(Mathf.Abs(scaler.referenceResolution.x - 1600f / .75f) < .01f
                    && Mathf.Abs(scaler.referenceResolution.y - 900f / .75f) < .01f, "all battle HUD elements use seventy-five percent of their original size");
                foreach (var row in hud.EnemyRows)
                {
                    Require(row.TryGetTargetBounds(out var placement), "formation has measurable painted bounds");
                    var right = Camera.main.WorldToViewportPoint(placement.max);
                    Require(right.x <= .981f, "right-aligned monsters remain fully inside the camera");
                }
                var a = hud.EnemyRows[0].Enemy.GetComponent<DamageResistance>();
                var b = hud.EnemyRows[1].Enemy.GetComponent<DamageResistance>();
                a.ObserveDamageType(PlayerDamageType.Pierce);
                Require(b.IsWeaknessKnown(PlayerDamageType.Pierce) && !b.IsWeaknessKnown(PlayerDamageType.Blunt),
                    "discovering one rat's pierce weakness shares that weakness but keeps untested blunt hidden");
                foreach (var row in hud.EnemyRows) row.Refresh();
                Capture("shared-rat-weakness.png", hud, Vector2.zero);
                var outlines = UnityEngine.Object.FindObjectsOfType<BattleActorOutline>();
                foreach (var outline in outlines) outline.enabled = false;
                Capture("outline-off.png", hud, Vector2.zero);
                foreach (var outline in outlines) { outline.enabled = true; outline.Apply(); }
                Capture("outlined-battle.png", hud, Vector2.zero);
                CompareOutlineFrames();
                Require(b.RevealWeaknesses() == 1 && a.UnknownWeaknessCount == 0,
                    "lantern reveal on the second rat updates both rats' remaining weaknesses");
                Require(!arrow.IsVisible && !arrow.IsTargeting, "arrow is hidden before choosing an action");
                int resolve = flow.Player.CurrentDetermination, hp = flow.Player.CurrentHP;
                hud.attack.onClick.Invoke();
                Require(flow.CurrentState == BattleState.SelectingTarget && arrow.IsVisible, "clicking Attack immediately opens the targeting bow");
                Require(hud.EnemyRows.All(row => NameTag(row, hud).activeSelf), "choosing an attack reveals all living target names");
                Require(hud.EnemyRows.All(row => row.IntentExpanded), "preparing an attack expands every living monster's intent");
                Require(!arrow.Graphic.raycastTarget, "arrow geometry never intercepts pointer input");
                arrow.UpdatePointer(ScreenOf((RectTransform)hud.attack.transform, hud));
                Require(arrow.IsVisible && arrow.HoveredEnemy == null, "cursor on Attack keeps a free neutral pointer without choosing a monster");
                Capture("attack-open.png", hud, ScreenOf((RectTransform)hud.attack.transform, hud));

                var first = hud.EnemyRows[0]; var second = hud.EnemyRows[1];
                arrow.UpdatePointer(ScreenOf(first));
                Require(arrow.HoveredEnemy == first.Enemy && hud.SelectedEnemy == first.Enemy && first.frame.highlighted,
                    "hovering painted monster bounds selects and highlights that monster: " + PointerDetails(first, hud));
                arrow.UpdatePointer(ScreenOf(second));
                Require(arrow.HoveredEnemy == second.Enemy && hud.SelectedEnemy == second.Enemy
                    && second.frame.highlighted && !first.frame.highlighted, "moving to the other monster changes the target immediately");
                Require(flow.Player.CurrentDetermination == resolve && flow.Player.CurrentHP == hp
                    && flow.Enemies.All(e => e.CurrentHP == e.MaxHP), "hovering does not spend resources or attack");
                Vector2 p = ScreenOf(second) + Vector2.left * 20;
                arrow.UpdatePointer(p);
                Vector2 oldTip = arrow.Graphic.Tip;
                p += Vector2.right * 40; arrow.UpdatePointer(p);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(hud.Root, p, null, out var expectedTip);
                Require(arrow.HoveredEnemy == second.Enemy && arrow.Graphic.HasTarget
                    && Vector2.Distance(arrow.Graphic.Tip, expectedTip) < .1f && Vector2.Distance(oldTip, arrow.Graphic.Tip) > 20,
                    "arrow follows two distinct cursor positions inside one target while retaining its valid-target glow");
                first.Enemy.GetComponent<EnemyTargetSelector>().SendMessage("OnMouseDown");
                Require(flow.CurrentState == BattleState.SelectingTarget, "legacy collider callbacks cannot race or override the arrow's selected target");
                Capture("attack-target-two.png", hud, p);

                arrow.UpdatePointer(new Vector2(Screen.width * .32f, Screen.height * .55f));
                Require(arrow.HoveredEnemy == null && arrow.IsVisible && !arrow.ConfirmHoveredTarget(), "empty arena space keeps targeting open without confirming");
                Require(!arrow.Graphic.HasTarget && hud.EnemyRows.All(row => !row.frame.highlighted),
                    "moving out of a monster's range removes the glow without confirming");
                arrow.UpdatePointer(ScreenOf((RectTransform)second.transform, hud));
                Require(arrow.HoveredEnemy == second.Enemy, "enemy HUD cards target the matching world monster");
                hud.attack.onClick.Invoke();
                Require(flow.CurrentState == BattleState.PlayerTurn && !arrow.IsVisible, "clicking Attack again cancels the bow");
                Require(hud.EnemyRows.All(row => !row.IntentExpanded), "canceling attack selection returns all intents to their compact state");
                hud.attack.onClick.Invoke(); arrow.CancelSelection();
                Require(flow.CurrentState == BattleState.PlayerTurn && !arrow.IsVisible
                    && flow.Player.CurrentDetermination == resolve, "right-click cancellation path costs no action or resolve");
                hud.attack.onClick.Invoke(); flow.CancelTargetSelection();
                Require(!arrow.IsVisible, "flow cancellation used by Escape hides the arrow immediately");
                hud.items.onClick.Invoke();
                Require(hud.popup.activeSelf && !arrow.IsVisible, "item popup does not display an attack bow");
                hud.closePopup.onClick.Invoke();

                // Reproduce the reported stale six-HP rat card, then kill it
                // while game time is paused: HUD effects must use unscaled time.
                first.Enemy.TakeDamage(first.Enemy.CurrentHP - 6);
                first.Refresh();
                Capture("card-before-death.png", hud, Vector2.zero);
                survivingRow = second;
                survivingHp = second.Enemy.CurrentHP;
                survivingPortrait = second.portrait.sprite;
                Time.timeScale = 0;
                first.Enemy.TakeDamage(6);
                Require(first.IsDeparting && first.healthText.text == "0 / " + first.Enemy.MaxHP && first.health.rectTransform.localScale.x == 0,
                    "death notification immediately clears the stale six-HP text and fill");
                Require(!first.select.interactable && !first.GetComponent<CanvasGroup>().blocksRaycasts
                    && first.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(graphic => !graphic.raycastTarget),
                    "dying card immediately releases all pointer input");
                Require(!NameTag(first, hud).transform.parent.gameObject.activeSelf,
                    "death immediately hides the monster name, target marker and intent");
                first.Refresh(); first.Refresh();
                Require(UnityEngine.Object.FindObjectsOfType<BattleEnemyCardShatter>().Length == 1,
                    "repeated HUD refreshes cannot start duplicate death effects");
                stage = 3;
                return;
            }
            if (stage == 3)
            {
                var effect = UnityEngine.Object.FindObjectOfType<BattleEnemyCardShatter>();
                if (effect != null && !shatterCaptured && effect.Progress > .24f)
                {
                    Capture("card-shattering.png", hud, Vector2.zero);
                    var pieces = effect.GetComponentsInChildren<BattleEnemyCardFragment>();
                    Require(pieces.Length > 5 && pieces.Sum(layer => layer.canvasRenderer.GetMesh().vertexCount) > 300,
                        "native canvas renders fractured portrait, text, health bar and frame meshes");
                    Require(pieces.All(layer => !layer.raycastTarget), "flying card fragments never block pointer input");
                    shatterCaptured = true;
                }
                if (hud.EnemyRows.Count != 1 || effect != null || survivingRow.IsMoving) return;
                Require(shatterCaptured, "death effect remains visible long enough to show its fractured card");
                Require(Time.timeScale == 0, "death effect and card reflow complete while gameplay is paused");
                Require(((RectTransform)survivingRow.transform).anchoredPosition == Vector2.zero,
                    "second rat smoothly fills the first card's empty slot");
                Require(survivingRow.enemyName.text == "老鼠 2" && survivingRow.Enemy.CurrentHP == survivingHp
                    && survivingRow.portrait.sprite == survivingPortrait,
                    "moving the card preserves monster identity, health and portrait");
                Require(hud.transform.Find("WorldTag_1") == null && NameTag(survivingRow, hud).transform.parent.gameObject.activeSelf,
                    "removed cards release their world tags without renumbering surviving tags");
                arrow.UpdatePointer(ScreenOf((RectTransform)survivingRow.transform, hud));
                Require(arrow.HoveredEnemy == survivingRow.Enemy, "moved card still targets the matching living monster");
                Capture("card-compacted.png", hud, Vector2.zero);
                Time.timeScale = 1;
                var root = GameRoot.I;
                root.battleMapTemplate = AssetDatabase.LoadAssetAtPath<WorldMapTemplate>("Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.asset");
                root.state.map.clearedNodes = root.battleMapTemplate.Load().nodes.Where(n => n.kind == MapNodeKind.Battle).Take(6).Select(n => n.id).ToList();
                root.state.map.pendingNodeId = "targeting-native";
                for (int seed = 0; ; seed++)
                    if (SmallMonsterEncounterRules.Choose(seed, root.state.map.pendingNodeId, 6).SequenceEqual(
                        new[] { SmallMonsterKind.Rat, SmallMonsterKind.Crow, SmallMonsterKind.Wraith }))
                    { root.state.map.randomSeed = seed; break; }
                stage = 1;
                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CombatScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
            if (stage == 1)
            {
                if (flow.Enemies.Count != 3 || flow.CurrentState != BattleState.PlayerTurn) return;
                ValidateGround(flow, hud);
                ValidateOutlines(flow);
                SetIntent(hud.EnemyRows[0].Enemy, "RatBasic");
                SetIntent(hud.EnemyRows[1].Enemy, "CrowStatus");
                SetIntent(hud.EnemyRows[2].Enemy, "WraithBasic");
                foreach (var row in hud.EnemyRows) row.Refresh();
                Require(flow.Enemies[0].GetComponent<DamageResistance>().UnknownWeaknessCount == 0,
                    "new encounter retains discoveries for the same species in the current run");
                Require(flow.Enemies[1].GetComponent<DamageResistance>().UnknownWeaknessCount > 0
                    && flow.Enemies[2].GetComponent<DamageResistance>().UnknownWeaknessCount > 0,
                    "rat discovery never reveals crow or wraith weaknesses");
                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(GameRoot.I.state));
                Require(restored.monsterWeaknesses.Find(entry => entry.monsterId == "Rat").observedTypes.Count == 2,
                    "species discoveries survive serialization with the run state");
                Require(new GameState().monsterWeaknesses.Count == 0, "starting a fresh run clears species discoveries");
                foreach (var enemy in flow.Enemies) enemy.GetComponent<DamageResistance>().RevealWeaknesses();
                hud.attack.onClick.Invoke();
                foreach (var row in hud.EnemyRows)
                {
                    arrow.UpdatePointer(ScreenOf(row));
                    Require(arrow.HoveredEnemy == row.Enemy, "all three species can be targeted on their painted bodies");
                }
                var ghost = hud.EnemyRows.Last();
                Capture("attack-target-three.png", hud, ScreenOf(ghost));
                Capture("attack-target-three-1280.png", hud, ScreenOf(ghost), 1280, 720);
                CaptureIconGuide(hud, ScreenOf(ghost));
                arrow.CancelSelection();
                flow.Player.AddDetermination(1);
                int beforeSkill = flow.Player.CurrentDetermination;
                hud.skill.onClick.Invoke();
                Require(flow.IsSelectingSkill && arrow.SourceButton == (RectTransform)hud.skill.transform,
                    "single-target skill uses its own action button as the bow origin");
                arrow.UpdatePointer(ScreenOf(ghost));
                Capture("skill-target.png", hud, ScreenOf(ghost));
                arrow.CancelSelection();
                Require(flow.Player.CurrentDetermination == beforeSkill, "canceling a skill target does not spend resolve");

                hud.attack.onClick.Invoke();
                var deadRow = hud.EnemyRows[0]; var oldPoint = ScreenOf(deadRow);
                deadRow.Enemy.TakeDamage(deadRow.Enemy.CurrentHP);
                arrow.UpdatePointer(oldPoint);
                Require(arrow.HoveredEnemy == null, "dead monsters are excluded from targeting");
                attackedEnemy = hud.EnemyRows[1].Enemy;
                targetHpBefore = attackedEnemy.CurrentHP;
                expectedSwordDamage = flow.Player.DamageComponents.Sum(part => part.damage);
                attackedRound = flow.RoundNumber;
                arrow.UpdatePointer(ScreenOf(hud.EnemyRows[1]));
                Require(arrow.ConfirmHoveredTarget(), "confirming the hovered monster commits the production attack action");
                Require(!arrow.IsVisible && !arrow.ConfirmHoveredTarget(), "committed attack hides the bow and cannot confirm twice");
                stage = 2; Time.timeScale = 10;
            }
            if (stage == 2)
            {
                Require(!arrow.IsVisible, "bow stays hidden while actions and enemy turns resolve");
                if (!actionCaptured && (flow.CurrentState == BattleState.ExecutingAction || flow.CurrentState == BattleState.EnemyTurn))
                {
                    ValidateOutlines(flow);
                    Capture("outlined-action.png", hud, Vector2.zero);
                    actionCaptured = true;
                }
                if (flow.CurrentState != BattleState.PlayerTurn || flow.RoundNumber <= attackedRound) return;
                Require(attackedEnemy != null && attackedEnemy.CurrentHP == targetHpBefore - expectedSwordDamage,
                    "one confirmed sword attack deals exactly one configured hit to the selected monster");
                Require(flow.RoundNumber == attackedRound + 1, "one confirmation advances one round");
                if (UnityEngine.Object.FindObjectOfType<BattleEnemyCardShatter>() != null
                    || hud.EnemyRows.Any(row => row.IsMoving)) return;
                Require(hud.EnemyRows.Count == 2 && ((RectTransform)hud.EnemyRows[0].transform).anchoredPosition == Vector2.zero,
                    "removing a dead rat during combat also compacts a three-monster encounter");
                // Several enemies can die to the same area attack or status tick.
                foreach (var row in hud.EnemyRows.ToArray()) row.Enemy.TakeDamage(row.Enemy.CurrentHP);
                Require(hud.EnemyRows.All(row => row.IsDeparting)
                    && UnityEngine.Object.FindObjectsOfType<BattleEnemyCardShatter>().Length == 2,
                    "simultaneous deaths each create exactly one independent shatter effect");
                stage = 4;
                Time.timeScale = 0;
                return;
            }
            if (stage == 4)
            {
                if (hud.EnemyRows.Count > 0 || UnityEngine.Object.FindObjectOfType<BattleEnemyCardShatter>() != null) return;
                Require(!hud.enemyContainer.GetComponentsInChildren<BattleHudEnemyView>().Any(),
                    "last enemy death leaves no active or stale enemy cards");
                Require(!hud.transform.Cast<Transform>().Any(child => child.name.StartsWith("WorldTag_")),
                    "all dead enemies release their separate world labels and intents");
                Capture("all-cards-removed.png", hud, Vector2.zero);
                Finish(null);
            }
        }
        catch (Exception e) { Finish(e); }
    }

    static Vector2 ScreenOf(BattleHudEnemyView row)
    {
        Require(row.TryGetTargetBounds(out var bounds), "monster exposes its painted bounds for targeting");
        return Camera.main.WorldToScreenPoint(bounds.center);
    }
    static GameObject NameTag(BattleHudEnemyView row, BattleHudView hud)
    {
        return hud.transform.Find("WorldTag_" + row.name.Substring("Enemy_".Length) + "/Name").gameObject;
    }
    static void ValidateOutlines(BattleFlowController flow)
    {
        var actors = new List<Combatant> { flow.Player }; actors.AddRange(flow.Enemies);
        foreach (var actor in actors)
        {
            if (actor == null || actor.IsDead) continue;
            var outline = actor.GetComponent<BattleActorOutline>();
            Require(outline != null, "actor has its own animation-aware outline binding");
            outline.Apply();
            var visible = actor.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy);
            Require(visible.Any(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "Taptap/Battle Actor Outline"),
                "visible actor artwork uses the outline shader");
            foreach (var renderer in visible)
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "Taptap/Battle Actor Outline")
                    Require(!ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), "outline shader compiles for the current graphics device");
        }
    }
    static void CompareOutlineFrames()
    {
        var before = new Texture2D(2, 2); var after = new Texture2D(2, 2);
        try
        {
            string root = Path.GetDirectoryName(Output);
            before.LoadImage(File.ReadAllBytes(Path.Combine(root, "outline-off.png")));
            after.LoadImage(File.ReadAllBytes(Path.Combine(root, "outlined-battle.png")));
            var a = before.GetPixels32(); var b = after.GetPixels32();
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 12) changed++;
            Require(changed > 500 && changed < a.Length / 12,
                "outline adds visible narrow silhouette edges while preserving the artwork and background interiors: " + changed + " pixels");
        }
        finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
    }
    static void ValidateGround(BattleFlowController flow, BattleHudView hud)
    {
        Require(flow.Player.GetComponent<PlayerAttackAnimation>().TryGetBodyVisual(out var art, out var local),
            "player exposes registered painted boot bounds");
        float foot = art.transform.TransformPoint(new Vector3(local.center.x, local.min.y, 0)).y;
        foreach (var row in hud.EnemyRows)
        {
            Require(row.TryGetTargetBounds(out var bounds) && Mathf.Abs(bounds.min.y - foot) < .025f,
                "player and " + row.Enemy.Definition.kind + " share the same visible ground baseline");
            var intent = hud.transform.Find("WorldTag_" + (hud.EnemyRows.ToList().IndexOf(row) + 1) + "/Intent");
            Require(intent.GetComponentInChildren<EnemyIntentIconGraphic>() != null,
                "compact intent places a reusable damage icon before its amount");
        }
    }
    static void SetIntent(EnemyCombatant enemy, string asset)
    {
        var action = AssetDatabase.LoadAssetAtPath<EnemyAttackData>("Assets/Data/EnemyAttacks/SmallMonsters/" + asset + ".asset");
        var selector = (WeightedEnemyActionSelector)typeof(EnemyCombatant).GetField("weighted", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(enemy);
        selector.Reset(); selector.Prepare(candidate => candidate == action ? 1 : 0);
        Require(enemy.UpcomingAction == action, "preview uses the configured " + asset + " intent");
    }
    static void CaptureIconGuide(BattleHudView hud, Vector2 pointer)
    {
        var guide = new GameObject("IconGuide", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic));
        guide.transform.SetParent(hud.transform, false);
        var panel = (RectTransform)guide.transform;
        panel.sizeDelta = new Vector2(600, 96); panel.anchoredPosition = new Vector2(0, 225);
        guide.GetComponent<BattleHudGraphic>().raycastTarget = false;
        string[] assets = { "RatBasic", "WraithBasic", "RatStatus", "WraithStatus" };
        string[] labels = { "啃咬", "撕裂", "毒素", "咒术" };
        for (int i = 0; i < assets.Length; i++)
        {
            var root = new GameObject(labels[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(EnemyIntentIconGraphic));
            root.transform.SetParent(guide.transform, false);
            var icon = root.GetComponent<EnemyIntentIconGraphic>(); icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(40, 40);
            icon.rectTransform.anchoredPosition = new Vector2(-225 + i * 150, 14);
            icon.SetIntent(AssetDatabase.LoadAssetAtPath<EnemyAttackData>("Assets/Data/EnemyAttacks/SmallMonsters/" + assets[i] + ".asset"));
            var label = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(guide.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>(); text.font = hud.prompt.font; text.fontSize = 19;
            text.color = new Color32(227, 214, 188, 255); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.text = labels[i]; text.rectTransform.sizeDelta = new Vector2(100, 28);
            text.rectTransform.anchoredPosition = new Vector2(-225 + i * 150, -25);
        }
        Capture("four-damage-icons.png", hud, pointer);
        foreach (var icon in guide.GetComponentsInChildren<EnemyIntentIconGraphic>())
            Require(icon.canvasRenderer.GetMesh().vertexCount > 6, "native canvas renders the " + icon.DamageType + " silhouette");
        UnityEngine.Object.DestroyImmediate(guide);
    }
    static void ValidateBackground()
    {
        var background = UnityEngine.Object.FindObjectOfType<BattleBackground>();
        Require(background != null, "combat scene includes the supplied battle background");
        var appearance = background.GetComponent<SpriteRenderer>();
        Require(appearance.sprite != null && appearance.sprite.texture.width == 1920
            && appearance.sprite.texture.height == 1080, "background uses the original nineteen-twenty by ten-eighty attachment");
        Require(appearance.sortingOrder < 0 && background.transform.position.z > 0,
            "background renders behind the actors and never intercepts UI input");
        var camera = Camera.main;
        try
        {
            foreach (float aspect in new[] { 16f / 9, 4f / 3, 21f / 9 })
            {
                camera.aspect = aspect; background.FitToCamera();
                float halfHeight = camera.orthographicSize, halfWidth = halfHeight * aspect;
                var center = camera.transform.position; var bounds = appearance.bounds;
                Require(bounds.min.x <= center.x - halfWidth + .001f && bounds.max.x >= center.x + halfWidth - .001f
                    && bounds.min.y <= center.y - halfHeight + .001f && bounds.max.y >= center.y + halfHeight - .001f,
                    "background fills the complete camera at aspect " + aspect);
                Require(Mathf.Abs(background.transform.localScale.x - background.transform.localScale.y) < .0001f,
                    "camera fitting preserves the background artwork's proportions");
                Require(Mathf.Lerp(bounds.min.y, bounds.max.y, background.groundLine) >= background.groundHeight - .001f,
                    "painted ground stays beneath the monsters' feet");
            }
        }
        finally { camera.ResetAspect(); background.FitToCamera(); }
        Require(!background.gameObject.scene.GetRootGameObjects().Any(o => o.name == "Ground" && o.activeSelf),
            "old white placeholder ground is disabled");
    }
    static Vector2 ScreenOf(RectTransform rect, BattleHudView hud)
    {
        var canvas = hud.GetComponent<Canvas>();
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
    }
    static string PointerDetails(BattleHudEnemyView row, BattleHudView hud)
    {
        var point = ScreenOf(row);
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        return "screen=" + Screen.width + "x" + Screen.height + ", point=" + point
            + ", hovered=" + hud.TargetingArrow.HoveredEnemy + ", selected=" + hud.SelectedEnemy
            + ", highlighted=" + row.frame.highlighted + ", hits=" + string.Join(",", hits.Select(h => h.gameObject.name));
    }
    static void Capture(string name, BattleHudView hud, Vector2 pointer, int width = 1600, int height = 900)
    {
        var camera = Camera.main; var canvas = hud.GetComponent<Canvas>();
        var previousMode = canvas.renderMode; var previousCamera = canvas.worldCamera;
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        var hovered = hud.TargetingArrow.HoveredEnemy;
        Vector2 aimOffset = hovered != null ? pointer - ScreenOf(hud.EnemyRows.First(row => row.Enemy == hovered)) : Vector2.zero;
        var render = new RenderTexture(width, height, 24); render.Create();
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = render;
            UnityEngine.Object.FindObjectOfType<BattleBackground>()?.FitToCamera();
            foreach (var row in hud.EnemyRows) row.Refresh();
            foreach (var label in hud.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            foreach (var row in hud.EnemyRows) row.SendMessage("LateUpdate");
            // Keep the screenshot's pointer tied to the selected world target
            // when rendering another size; camera pixel coordinates change.
            if (hud.TargetingArrow.HoveredEnemy != null)
            {
                var row = hud.EnemyRows.First(r => r.Enemy == hud.TargetingArrow.HoveredEnemy);
                pointer = ScreenOf(row) + new Vector2(aimOffset.x * width / Screen.width, aimOffset.y * height / Screen.height);
            }
            else pointer = ScreenOf(hud.TargetingArrow.SourceButton, hud);
            hud.TargetingArrow.UpdatePointer(pointer);
            Canvas.ForceUpdateCanvases();
            if (hud.TargetingArrow.IsVisible)
            {
                var geometry = hud.TargetingArrow.Graphic.canvasRenderer.GetMesh();
                Require(geometry.vertexCount > 60, "native canvas builds the dashed curve and arrowhead mesh");
            }
            camera.Render(); RenderTexture.active = render;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            var sample = pixels.GetPixel(width / 2, height * 7 / 10);
            Require(sample.r > sample.b, "native render shows the brown painted background instead of the blue camera clear color");
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Output), name), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            canvas.renderMode = previousMode; canvas.worldCamera = previousCamera;
            UnityEngine.Object.FindObjectOfType<BattleBackground>()?.FitToCamera();
            Canvas.ForceUpdateCanvases();
            UnityEngine.Object.DestroyImmediate(pixels); render.Release(); UnityEngine.Object.DestroyImmediate(render);
        }
    }
    static void Require(bool condition, string name)
    { if (!condition) throw new Exception(name); checks.Add(name); }
    static void Logged(string message, string stack, LogType type)
    {
        if (!finished && SessionState.GetBool(Pending, false)
            && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
            Finish(new Exception(message + "\n" + stack));
    }
    static void Finish(Exception error)
    {
        if (finished) return;
        finished = true; SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = error == null, checks = checks.ToArray(), error = error?.ToString() }, true));
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
