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
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SmallMonsterValidation
{
    const string Pending = "SmallMonsterValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static double deadline;
    static int stage, turns, remnantBefore;
    static bool finished;
    static readonly List<BossBattleResult> bossBattles = new List<BossBattleResult>();
    static BattleFlowController flow;
    static string Output
    {
        get
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-monsterResult");
            return i >= 0 ? args[i + 1] : "native-result.json";
        }
    }
    [Serializable] sealed class Result { public bool passed; public string[] checks; public string error; public int completedRounds; public BossBattleResult[] bossBattles; }
    [Serializable] sealed class BossBattleResult { public string weapon; public int startingHp, remainingHp, rounds, breaks; }

    static SmallMonsterValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Logged;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated validation project.");
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CombatScene.unity");
            foreach (var kind in new[] { "Rat", "Crow", "Wraith" })
            {
                var data = AssetDatabase.LoadAssetAtPath<SmallMonsterData>("Assets/Data/SmallMonsters/" + kind + ".asset");
                Require(data != null && data.actions.Length == 4 && data.actions.All(a => a != null), kind + " definition and four actions resolve");
                Require(data.maxHp == (kind == "Wraith" ? 44 : 36), kind + " balanced HP");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + kind + ".prefab");
                Require(prefab != null && prefab.GetComponent<EnemyCombatant>().Definition == data, kind + " prefab uses shared balance data");
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, kind + " has no missing scripts");
            }
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Finish(e); }
    }

    static void Changed(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        deadline = EditorApplication.timeSinceStartup + 110;
        stage = turns = 0;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (finished) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Native encounter validation timed out.");
            flow = UnityEngine.Object.FindObjectOfType<BattleFlowController>();
            var hud = UnityEngine.Object.FindObjectOfType<BattleHudView>();
            if (flow == null || hud == null || flow.RoundNumber == 0) return;
            if (stage == 0)
            {
                Require(flow.Enemies.Count == 2 && flow.Enemies.All(e => e.Definition.kind == SmallMonsterKind.Rat), "production CombatScene starts a two-rat encounter");
                Require(flow.Enemies.All(e => e.CurrentHP == 36 && e.UpcomingAction != null), "health and locked intents initialized before player action");
                foreach (var enemy in flow.Enemies)
                {
                    var intent = enemy.UpcomingAction;
                    for (int i = 0; i < 100; i++) Require(enemy.UpcomingAction == intent, "HUD reads do not reroll intent");
                    enemy.GetComponent<DamageResistance>().RevealWeaknesses();
                }
                RuntimeRules();
                ValidateBossBalance();
                ValidateWeaknessLayout(hud, false);
                Capture("two-monsters.png", hud);
                ValidateWeaknessLayout(hud, true);
                Capture("weakness-four-icons.png", hud);
                foreach (var enemy in flow.Enemies)
                { var r = enemy.GetComponent<DamageResistance>(); r.SetStageRules(enemy.Definition.resistances); r.RevealWeaknesses(); }

                // Select a real late-game node/seed whose production table yields
                // three enemies, then reload the actual scene through its Awake.
                var root = GameRoot.I;
                root.state.player.hp = 100;
                root.state.player.ordinaryStatuses.Clear();
                root.battleMapTemplate = AssetDatabase.LoadAssetAtPath<WorldMapTemplate>("Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.asset");
                Require(root.battleMapTemplate != null, "production map template resolves");
                root.state.map.clearedNodes = root.battleMapTemplate.Load().nodes.Where(n => n.kind == MapNodeKind.Battle).Take(6).Select(n => n.id).ToList();
                Require(root.state.map.clearedNodes.Count == 6, "map supports six completed ordinary battles");
                root.state.map.pendingNodeId = "native-late";
                for (int seed = 0; ; seed++)
                    if (SmallMonsterEncounterRules.Choose(seed, "native-late", 6).SequenceEqual(
                        new[] { SmallMonsterKind.Rat, SmallMonsterKind.Crow, SmallMonsterKind.Wraith }))
                    { root.state.map.randomSeed = seed; break; }
                stage = 1;
                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CombatScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
            if (stage == 1)
            {
                if (flow.Enemies.Count != 3 || flow.RoundNumber != 1) return;
                Require(flow.Enemies.Count == 3, "late map encounter spawns three production prefabs");
                foreach (var enemy in flow.Enemies) enemy.GetComponent<DamageResistance>().RevealWeaknesses();
                ValidateWeaknessLayout(hud, false);
                Capture("three-monsters.png", hud);
                remnantBefore = GameRoot.I.state.world.sinRemnant;
                stage = 2;
                Time.timeScale = 15;
            }
            if (stage == 2)
            {
                if (flow.CurrentState == BattleState.Defeat) throw new Exception("Full-health hunter lost the native smoke encounter.");
                if (flow.CurrentState == BattleState.Victory)
                {
                    Require(flow.Enemies.All(e => e == null || e.IsDead), "real player actions finish a three-monster encounter");
                    Require(flow.Player.CurrentHP > 0 && flow.Player.CurrentHP <= 100, "native battle preserves hunter HP bounds");
                    Require(flow.Player.Statuses.DebuffCount == 0, "victory clears ordinary debuffs");
                    Require(GameRoot.I.state.world.sinRemnant == remnantBefore + 3, "each of three defeated monsters grants remnant exactly once");
                    Finish(null); return;
                }
                if (flow.CurrentState != BattleState.PlayerTurn) return;
                if (++turns > 30) throw new Exception("Native encounter did not finish within 30 player actions.");
                var target = flow.Enemies.First(e => e != null && !e.IsDead);
                var skill = flow.Player.CurrentWeapon.Skill;
                if (flow.CanUseWeaponSkill)
                { flow.PlayerSkill(); if (skill.RequiresPrimaryTarget) flow.AttackTarget(target); }
                else { flow.PlayerAttack(); flow.AttackTarget(target); }
            }
        }
        catch (Exception e) { Finish(e); }
    }

    static void RuntimeRules()
    {
        var player = flow.Player;
        var copies = new List<GameObject>();
        try
        {
            EnemyCombatant Spawn(string name)
            {
                var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + name + ".prefab"));
                copies.Add(actor); actor.transform.position = Vector3.one * 100;
                actor.GetComponent<EnemySpriteAnimation>().enabled = false;
                var enemy = actor.GetComponent<EnemyCombatant>(); enemy.Initialize(); return enemy;
            }
            var rat = Spawn("Rat"); var crow = Spawn("Crow"); var wraith = Spawn("Wraith");
            Require(crow.GetComponent<DamageResistance>().GetDamageReaction(PlayerDamageType.Pierce) == DamageReaction.Normal,
                "crow no longer resists starter piercing weapons");
            rat.StartGuard();
            Require(rat.TakeDamage(8) == 4 && rat.LoseRaw(2) == 2, "enemy guard halves direct damage and does not block raw status loss");
            rat.Initialize();
            ForceAction(rat, rat.Definition.actions[2]); Drain(rat.TakeTurn(player));
            Require(rat.IsCharging, "charge spends a turn and creates a heavy follow-up");
            rat.PrepareIntent(player, new EnemyIntentBudget());
            Require(rat.UpcomingAction.Damage == 4, "charge locks the configured four-damage heavy");
            int before = player.CurrentHP;
            rat.Break.TryAdd(2, rat.BattleRound); Drain(rat.TakeTurn(player));
            Require(!rat.IsCharging && player.CurrentHP == before, "breaking a charged monster cancels its heavy and skips damage");

            rat.Initialize(); ForceAction(rat, rat.Definition.actions[1]); Drain(rat.TakeTurn(player));
            Require(player.CurrentHP == before && player.Statuses.Stacks(StatusId.Poison) == 1, "rat special applies poison one without direct damage");
            StatusSettlement.SettleOwnTurn(new CombatantStatusHost(player), 1);
            Require(player.CurrentHP == before - 1 && player.Statuses.Stacks(StatusId.Poison) == 0, "poison one settles once and expires");
            ForceAction(crow, crow.Definition.actions[1]); Drain(crow.TakeTurn(player));
            Require(player.Statuses.Stacks(StatusId.Sunder) == 1, "crow applies one sunder layer");
            ForceAction(wraith, wraith.Definition.actions[1]); Drain(wraith.TakeTurn(player));
            Require(player.Statuses.Stacks(StatusId.Exhaust) == 1 && player.AddDetermination(1) == 0,
                "wraith fatigue blocks resolve gains");

            rat.Initialize();
            var sword = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/KBW01.asset");
            DamageSystem.DealDamage(new PlayerAttackInfo(player, rat, sword.DamageComponents));
            DamageSystem.DealDamage(new PlayerAttackInfo(player, rat, sword.Skill.Stages[0].damageComponents));
            Require(rat.CurrentHP == 2 && rat.Break.SkipPending, "starter sword basic plus skill breaks a living rat instead of killing before break");

            crow.Initialize();
            var axe = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/KBW05.asset");
            DamageSystem.DealDamage(new PlayerAttackInfo(player, crow, axe.DamageComponents));
            DamageSystem.DealDamage(new PlayerAttackInfo(player, crow, axe.Skill.Stages[0].damageComponents));
            Require(crow.CurrentHP == 15 && crow.Break.SkipPending, "slash counter reaches crow break while the target survives");

            wraith.Initialize();
            var torch = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/KBW09.asset");
            DamageSystem.DealDamage(new PlayerAttackInfo(player, wraith, torch.DamageComponents));
            DamageSystem.DealDamage(new PlayerAttackInfo(player, wraith, torch.Skill.Stages[0].damageComponents));
            Require(wraith.CurrentHP == 25 && wraith.Break.SkipPending, "fire counter reaches wraith break while the target survives");
        }
        finally
        {
            foreach (var actor in copies) UnityEngine.Object.DestroyImmediate(actor);
            player.Statuses.ClearOrdinary();
            Set(player, "currentHp", 100); GameRoot.I.state.player.hp = 100;
        }
    }

    // Execute the production player actions, enemy cycle, status clocks, damage
    // rounding and break rules. No simulator health bonus, healing or buffs.
    static void ValidateBossBalance()
    {
        var root = GameRoot.I;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Sloth.prefab");
        var simulator = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleSimulator/Sloth.prefab");
        Require(prefab != null && simulator != null, "both Sloth prefabs import");
        foreach (var source in new[] { prefab, simulator })
        {
            var actor = UnityEngine.Object.Instantiate(source);
            try
            {
                var enemy = actor.GetComponent<EnemyCombatant>(); enemy.Initialize();
                Require(enemy.MaxHP == 300 && enemy.Break.Threshold == 3 && enemy.SinRemnantReward == 10,
                    source.name + " has 300 HP, threshold three and boss reward ten");
                var resistance = actor.GetComponent<DamageResistance>();
                Require(resistance.GetDamageReaction(PlayerDamageType.Pierce) == DamageReaction.Weak
                    && resistance.GetDamageReaction(PlayerDamageType.Fire) == DamageReaction.Weak,
                    source.name + " has real piercing and fire weaknesses");
                var moves = (List<EnemyAttackData>)typeof(EnemyCombatant).GetField("attackDatas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);
                Require(moves.Count == 4 && moves.All(a => a != null) && moves[2].Kind == EnemyActionKind.Charge
                    && moves[2].FollowUp.Damage == 12 && moves[3].Kind == EnemyActionKind.Guard,
                    source.name + " resolves named attacks, fatigue, charge/heavy and guard");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }
        for (int i = 1; i <= 13; i++)
        {
            int weaponNumber = i == 13 ? 1 : i;
            int startingHp = i == 13 ? 60 : 100;
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/Data/Weapons/KBW{weaponNumber:00}.asset");
            var hunterObject = new GameObject("BalanceHunter");
            var bossObject = UnityEngine.Object.Instantiate(prefab);
            try
            {
                root.state.player.hp = startingHp; root.state.player.maxHp = 100;
                root.state.player.resolve = 1; root.state.player.maxResolve = 3;
                root.state.player.nextAttackBonusPercent = root.state.player.nextDirectHitReductionPercent = 0;
                root.state.player.ordinaryStatuses.Clear();
                var hunter = hunterObject.AddComponent<PlayerCombatant>(); hunter.Initialize(); hunter.EquipWeapon(weapon);
                var boss = bossObject.GetComponent<EnemyCombatant>();
                bossObject.GetComponent<EnemySpriteAnimation>().enabled = false;
                boss.Initialize(); int round = 0, breaks = 0;
                for (round = 1; round <= 100 && !boss.IsDead && !hunter.IsDead; round++)
                {
                    hunter.BattleRound = boss.BattleRound = round;
                    var intent = boss.UpcomingAction;
                    bool skill = !hunter.Statuses.Has(StatusId.Exhaust) && hunter.CurrentDetermination >= weapon.Skill.DeterminationCost
                        && weapon.WeaponId != "axe";
                    var parts = skill ? weapon.Skill.Stages.SelectMany(s => s.damageComponents)
                        : weapon.DamageComponents;
                    bool weak = parts.Any(p => p.damage > 0 && boss.GetComponent<DamageResistance>().GetDamageReaction(p.damageType) == DamageReaction.Weak);
                    int hits = skill ? weapon.Skill.Stages.Sum(s => s.hitCount) : hunter.AttackCount;
                    bool canBreak = weak && !boss.Break.IsProtected(round) && boss.Break.Progress + hits >= boss.Break.Threshold;
                    // Counter a visible heavy with break when possible; otherwise defend.
                    if (intent.Kind == EnemyActionKind.Attack && intent.Damage >= 10 && !canBreak)
                        Drain(new DefendAction().Execute(hunter, null));
                    else if (skill)
                        Drain(new SkillAction(new[] { boss }).Execute(hunter, boss));
                    else
                        Drain(new PlayerAttackAction().Execute(hunter, boss));
                    if (boss.IsDead) break;
                    if (boss.Break.SkipPending) breaks++;
                    StatusSettlement.SettleOwnTurn(new CombatantStatusHost(hunter), round);
                    Drain(boss.TakeTurn(hunter));
                    hunter.Statuses.TickDuration(round); boss.Statuses.TickDuration(round);
                    hunter.EndDefense();
                }
                Require(boss.IsDead && !hunter.IsDead, weapon.WeaponId + " defeats 300-HP Sloth from " + startingHp + " HP without healing");
                bossBattles.Add(new BossBattleResult { weapon = weapon.WeaponId, startingHp = startingHp,
                    remainingHp = hunter.CurrentHP, rounds = round, breaks = breaks });
            }
            finally
            {
                if (bossObject != null) UnityEngine.Object.DestroyImmediate(bossObject);
                if (hunterObject != null) UnityEngine.Object.DestroyImmediate(hunterObject);
            }
        }
        root.state.player.hp = 100; root.state.player.resolve = 1; root.state.player.ordinaryStatuses.Clear();
    }

    static void ForceAction(EnemyCombatant enemy, EnemyAttackData action)
    {
        var selector = new WeightedEnemyActionSelector(new[] { action }, maximum => 0);
        selector.Prepare(a => 1);
        Set(enemy, "weighted", selector); Set(enemy, "actionSelector", selector);
    }
    static void Set(object instance, string name, object value)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); if (field != null) { field.SetValue(instance, value); return; } }
        throw new MissingFieldException(name);
    }
    static void Drain(IEnumerator routine)
    {
        int steps = 0;
        while (routine.MoveNext())
        { if (++steps > 1000) throw new Exception("Unexpected animated wait in deterministic runtime check."); if (routine.Current is IEnumerator nested) Drain(nested); }
    }

    static void ValidateWeaknessLayout(BattleHudView hud, bool fourIcons)
    {
        foreach (var row in hud.GetComponentsInChildren<BattleHudEnemyView>())
        {
            if (fourIcons)
            {
                var all = new List<DamageResistance.DamageRule>();
                foreach (PlayerDamageType type in Enum.GetValues(typeof(PlayerDamageType)))
                    all.Add(new DamageResistance.DamageRule { damageType = type, damageReaction = DamageReaction.Weak });
                var enemyField = typeof(BattleHudEnemyView).GetField("enemy", BindingFlags.NonPublic | BindingFlags.Instance);
                var enemy = (EnemyCombatant)enemyField.GetValue(row);
                var resistance = enemy.GetComponent<DamageResistance>();
                resistance.SetStageRules(all); resistance.RevealWeaknesses();
            }
            row.Refresh();
            var label = row.transform.Find("KnownWeakness").GetComponent<TMP_Text>();
            label.ForceMeshUpdate();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(row.transform, label.rectTransform);
            int visible = 0;
            foreach (var icon in row.weaknesses)
            {
                if (!icon.transform.parent.gameObject.activeSelf) continue;
                visible++;
                var iconBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(row.transform, (RectTransform)icon.transform.parent);
                Require(iconBounds.min.x >= bounds.max.x + 1, "revealed weakness icon clears the four-character title");
                Require(iconBounds.max.x <= ((RectTransform)row.transform).rect.xMax - 8, "weakness icons remain inside enemy card");
            }
            if (fourIcons) Require(visible == 4, "all four revealed weaknesses fit together");
            Require(!label.isTextOverflowing, "weakness title fits without clipping");
        }
    }

    static void Capture(string name, BattleHudView hud)
    {
        var camera = Camera.main;
        var canvas = hud.GetComponent<Canvas>();
        var previousMode = canvas.renderMode; var previousCamera = canvas.worldCamera;
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        var render = new RenderTexture(1600, 900, 24); render.Create();
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = render;
            foreach (var label in hud.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            foreach (var row in hud.GetComponentsInChildren<BattleHudEnemyView>()) row.SendMessage("LateUpdate");
            camera.Render(); RenderTexture.active = render;
            pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Output), name), pixels.EncodeToPNG());
            Require(pixels.GetPixels32().Count(p => p.r > 130 && p.g > 100 && p.r > p.b * 1.15f) > 1000, "native HUD preview renders dark warm panels and gold text");
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            canvas.renderMode = previousMode; canvas.worldCamera = previousCamera;
            UnityEngine.Object.DestroyImmediate(pixels); render.Release(); UnityEngine.Object.DestroyImmediate(render);
        }
    }

    static void Require(bool condition, string name)
    { if (!condition) throw new Exception(name); checks.Add(name); }
    static void Logged(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Pending, false) || finished) return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Finish(new Exception(message + "\n" + stack));
    }
    static void Finish(Exception error)
    {
        if (finished) return;
        finished = true; SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = error == null, checks = checks.ToArray(), error = error?.ToString(), completedRounds = turns, bossBattles = bossBattles.ToArray() }, true));
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
