using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityEngine;

static class Program
{
    static int checks;
    static readonly Dictionary<string, WeaponData> Weapons = new();
    static string Root;
    static void Check(bool ok, string name)
    { checks++; if (!ok) throw new Exception(name); }
    static void Eq(int actual, int expected, string name) => Check(actual == expected, $"{name}: {actual} != {expected}");
    static void Set(object obj, string field, object value)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info != null) { info.SetValue(obj, value); return; }
        }
        throw new Exception("Missing field: " + field);
    }
    static string Field(string text, string key) => Regex.Match(text, "^  " + key + @": (.+)$", RegexOptions.Multiline).Groups[1].Value.Trim();
    static int Number(string text, string key) => int.Parse(Field(text, key));
    static DamageComponent[] Parts(string text, int indent)
    {
        string spaces = new(' ', indent);
        return Regex.Matches(text, "^" + spaces + @"- damageType: (\d+)\r?\n" + spaces + @"  damage: (\d+)", RegexOptions.Multiline)
            .Select(m => new DamageComponent((PlayerDamageType)int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))).ToArray();
    }
    static void LoadAssets()
    {
        string library = File.ReadAllText(Path.Combine(Root, "Assets/Data/Weapons/WeaponLibrary.asset"));
        int[] totals = { 10, 13, 10, 8, 11, 10, 11, 8, 8, 11, 10, 10 };
        int[] prices = { 4, 8, 8, 8, 4, 8, 4, 8, 4, 12, 12, 12 };
        int[] costs = { 2, 3, 2, 3, 2, 3, 2, 2, 2, 3, 3, 3 };
        int[] masks = { 1, 1, 1, 1, 2, 2, 4, 4, 8, 3, 9, 12 };
        for (int i = 1; i <= 12; i++)
        {
            string weaponPath = Path.Combine(Root, $"Assets/Data/Weapons/KBW{i:00}.asset");
            string raw = File.ReadAllText(weaponPath).Replace("\r\n", "\n");
            string skillPath = Path.Combine(Root, $"Assets/Data/Skills/KBS{i:00}.asset");
            string data = File.ReadAllText(skillPath).Replace("\r\n", "\n");
            string guid = Regex.Match(File.ReadAllText(skillPath + ".meta"), @"guid: (\w+)").Groups[1].Value;
            Check(Field(raw, "skill").Contains(guid), "skill GUID links to the expected asset " + i);
            string weaponGuid = Regex.Match(File.ReadAllText(weaponPath + ".meta"), @"guid: (\w+)").Groups[1].Value;
            Eq(Regex.Matches(library, weaponGuid).Count, 1, "weapon is referenced once " + i);
            var skill = new SkillData();
            Set(skill, "skillName", JsonSerializer.Deserialize<string>(Field(data, "skillName")));
            Set(skill, "skillDescription", JsonSerializer.Deserialize<string>(Field(data, "skillDescription")));
            Set(skill, "determinationCost", Number(data, "determinationCost"));
            var stageText = data.Split("  stages:\n")[1].Split("  statusEffects:")[0];
            var stages = Regex.Matches(stageText, @"^  - target: (\d+)\r?\n    hitCount: (\d+)\r?\n    damageComponents:\r?\n((?:    - damageType: \d+\r?\n      damage: \d+\r?\n)+)", RegexOptions.Multiline)
                .Select(m => new SkillStage { target = (SkillTarget)int.Parse(m.Groups[1].Value), hitCount = int.Parse(m.Groups[2].Value), damageComponents = Parts(m.Groups[3].Value, 4) }).ToArray();
            Set(skill, "stages", stages);
            var effects = Regex.Matches(data, @"^  - applyToSelf: (\d+)\r?\n    status: (\d+)\r?\n    stacks: (\d+)\r?\n    requiredDamageType: (\d+)", RegexOptions.Multiline)
                .Select(m => new SkillStatusEffect { applyToSelf = m.Groups[1].Value == "1", status = (StatusId)int.Parse(m.Groups[2].Value), stacks = int.Parse(m.Groups[3].Value), requiredDamageType = (PlayerDamageType)int.Parse(m.Groups[4].Value) }).ToArray();
            Set(skill, "statusEffects", effects);
            Set(skill, "ignoreResistance", Number(data, "ignoreResistance") == 1);
            Set(skill, "extraBreakOnWeakness", Number(data, "extraBreakOnWeakness"));
            Set(skill, "extraBreakDamageType", (PlayerDamageType)Number(data, "extraBreakDamageType"));
            Set(skill, "brokenTargetMultiplier", float.Parse(Field(data, "brokenTargetMultiplier"), System.Globalization.CultureInfo.InvariantCulture));
            var weapon = new WeaponData();
            string id = Field(raw, "weaponId");
            Set(weapon, "weaponId", id);
            Set(weapon, "weaponName", JsonSerializer.Deserialize<string>(Field(raw, "weaponName")));
            Set(weapon, "damageComponents", Parts(raw, 2).ToList());
            Set(weapon, "attackCount", Number(raw, "attackCount"));
            Set(weapon, "price", Number(raw, "price"));
            Set(weapon, "skill", skill);
            Weapons.Add(id, weapon);
            Check(skill.IsConfigured, "configured skill " + id);
            Eq(skill.DeterminationCost, costs[i - 1], "skill cost " + id);
            Eq(weapon.Price, prices[i - 1], "weapon price " + id);
            Eq(weapon.DamageComponents.Sum(p => p.damage) * weapon.AttackCount, totals[i - 1], "basic damage " + id);
            Eq(weapon.DamageComponents.Aggregate(0, (mask, p) => mask | 1 << (int)p.damageType), masks[i - 1], "basic types " + id);
        }
    }
    static PlayerCombatant Player(string id, bool animated = false)
    {
        GameRoot.I = null;
        var player = new GameObject("Player").AddComponent<PlayerCombatant>();
        Set(player, "maxHp", 200); player.Initialize(); player.AddDetermination(4); player.EquipWeapon(Weapons[id]);
        if (animated) player.gameObject.AddComponent<PlayerAttackAnimation>();
        return player;
    }
    static EnemyCombatant Enemy(int hp = 200, int threshold = 99,
        params (PlayerDamageType type, DamageReaction relation)[] rules)
    {
        var enemy = new GameObject("Enemy").AddComponent<EnemyCombatant>();
        Set(enemy, "maxHp", hp); enemy.Initialize(); enemy.Break = new BreakGauge(threshold);
        var resistance = enemy.gameObject.AddComponent<DamageResistance>();
        Set(resistance, "damageRules", rules.Select(r => new DamageResistance.DamageRule { damageType = r.type, damageReaction = r.relation }).ToList());
        return enemy;
    }
    static void Run(IEnumerator routine)
    {
        int limit = 0;
        while (routine.MoveNext())
        {
            if (++limit > 1000) throw new Exception("coroutine did not finish");
            if (routine.Current is IEnumerator nested) Run(nested);
        }
    }
    static void Cast(PlayerCombatant player, EnemyCombatant primary, params EnemyCombatant[] enemies)
        => Run(new SkillAction(enemies.Length == 0 ? new[] { primary } : enemies).Execute(player, primary));
    static void NormalSkills()
    {
        string[] ids = { "sword", "greatsword", "spear", "dual_swords", "axe", "sickle", "hammer", "flail", "torch", "poleaxe", "flame_tongue", "heaven_fire" };
        int[] damage = { 24, 29, 18, 16, 10, 14, 19, 16, 11, 11, 16, 14 };
        int[] secondary = { 0, 0, 0, 0, 10, 0, 0, 0, 0, 5, 0, 0 };
        for (int i = 0; i < ids.Length; i++)
        {
            var player = Player(ids[i], true); var target = Enemy(); var other = Enemy();
            Cast(player, target, target, other);
            Eq(200 - target.CurrentHP, damage[i], "skill primary damage " + ids[i]);
            Eq(200 - other.CurrentHP, secondary[i], "skill secondary damage " + ids[i]);
            Eq(player.CurrentDetermination, 3 - Weapons[ids[i]].Skill.DeterminationCost, "one payment, no resolve gain " + ids[i]);
            Eq(player.GetComponent<PlayerAttackAnimation>().Plays, Weapons[ids[i]].Skill.Stages.Sum(s => s.hitCount), "ordinary animation per skill segment " + ids[i]);
        }
        var p = Player("flail"); var e = Enemy(); Cast(p, e); Eq(e.Statuses.Stacks(StatusId.Weak), 1, "flail rider applied once for both hits");
        p = Player("greatsword"); e = Enemy(); Cast(p, e); Eq(e.Statuses.Stacks(StatusId.Weak), 1, "greatsword weak after damage");
        p = Player("hammer"); e = Enemy(); Cast(p, e); Eq(e.Statuses.Stacks(StatusId.Sunder), 2, "hammer sunder after damage");
        p = Player("torch"); e = Enemy(); Cast(p, e); Eq(e.Statuses.Stacks(StatusId.Ignite), 2, "torch ignite after damage");
    }
    static void ImmunityAndBreak()
    {
        var p = Player("sword"); var e = Enemy(200, 99, (PlayerDamageType.Pierce, DamageReaction.Resistant));
        Cast(p, e); Eq(e.CurrentHP, 176, "sword bypasses resistance");
        p = Player("sword"); e = Enemy(200, 99, (PlayerDamageType.Pierce, DamageReaction.Immune));
        Cast(p, e); Eq(e.CurrentHP, 200, "sword cannot bypass immunity");
        foreach (var item in new[] { ("greatsword", PlayerDamageType.Pierce, StatusId.Weak), ("hammer", PlayerDamageType.Blunt, StatusId.Sunder), ("flail", PlayerDamageType.Blunt, StatusId.Weak), ("torch", PlayerDamageType.Fire, StatusId.Ignite) })
        {
            p = Player(item.Item1); e = Enemy(200, 99, (item.Item2, DamageReaction.Immune)); Cast(p, e);
            Eq(e.CurrentHP, 200, "immune damage " + item.Item1); Eq(e.Statuses.Stacks(item.Item3), 0, "immune blocks rider " + item.Item1);
        }
        p = Player("heaven_fire"); e = Enemy(200, 99, (PlayerDamageType.Blunt, DamageReaction.Immune)); Cast(p, e);
        Eq(e.CurrentHP, 194, "heaven fire partial immunity damage"); Eq(e.Statuses.Stacks(StatusId.Sunder), 0, "blunt immune blocks sunder"); Eq(e.Statuses.Stacks(StatusId.Ignite), 1, "blunt immunity allows fire ignite");
        p = Player("heaven_fire"); e = Enemy(200, 99, (PlayerDamageType.Fire, DamageReaction.Immune)); Cast(p, e);
        Eq(e.CurrentHP, 192, "heaven fire other component damage"); Eq(e.Statuses.Stacks(StatusId.Sunder), 1, "fire immunity allows sunder"); Eq(e.Statuses.Stacks(StatusId.Ignite), 0, "fire immune blocks ignite");
        p = Player("flame_tongue"); e = Enemy(200, 99, (PlayerDamageType.Pierce, DamageReaction.Immune), (PlayerDamageType.Fire, DamageReaction.Immune)); Cast(p, e);
        Eq(e.CurrentHP, 200, "fully immune mixed segment"); Eq(p.Statuses.Stacks(StatusId.Steadfast), 1, "immunity does not block self buff");
        p = Player("flame_tongue"); e = Enemy(200, 99, (PlayerDamageType.Pierce, DamageReaction.Weak), (PlayerDamageType.Fire, DamageReaction.Weak)); Cast(p, e);
        Eq(e.Break.Progress, 1, "two mixed weaknesses give one break increment");
        p = Player("spear"); e = Enemy(200, 3, (PlayerDamageType.Pierce, DamageReaction.Weak)); Cast(p, e);
        Eq(e.Break.Progress, 2, "spear adds normal plus extra break");
        p = Player("spear"); e = Enemy(200, 3, (PlayerDamageType.Pierce, DamageReaction.Weak)); e.Break.TryAdd(3, 1); Cast(p, e);
        Eq(e.Break.Progress, 0, "protected spear target cannot accumulate break");
        p = Player("dual_swords"); e = Enemy(200, 3, (PlayerDamageType.Pierce, DamageReaction.Weak)); Cast(p, e);
        Check(e.Break.SkipPending && e.Break.Progress == 0, "dual sword breaks on third hit and protection blocks fourth");
    }
    static void Boundaries()
    {
        var p = Player("sickle"); var e = Enemy(200, 2, (PlayerDamageType.Slash, DamageReaction.Weak)); e.Break.TryAdd(2, 1); Cast(p, e);
        Eq(e.CurrentHP, 182, "scythe prebroken snapshot, each segment floors 7x1.3 to 9");
        p = Player("sickle"); e = Enemy(200, 1, (PlayerDamageType.Slash, DamageReaction.Weak)); Cast(p, e);
        Eq(e.CurrentHP, 186, "breaking mid-skill does not add scythe bonus");
        p = Player("sickle"); e = Enemy(200, 1); e.Break.TryAdd(1, 1); e.Break.ConsumeSkip(); e.BattleRound = 2; Cast(p, e);
        Eq(e.CurrentHP, 182, "scythe can exploit break on next player turn after skipped enemy action");
        p = Player("sickle"); e = Enemy(200, 1); e.Break.TryAdd(1, 1); e.Break.ConsumeSkip(); e.BattleRound = 3; Cast(p, e);
        Eq(e.CurrentHP, 186, "expired break protection grants no scythe bonus");
        p = Player("dual_swords", true); e = Enemy(7); var other = Enemy(); Cast(p, e, e, other);
        Eq(p.GetComponent<PlayerAttackAnimation>().Plays, 2, "lethal multi-hit stops remaining segments"); Eq(other.CurrentHP, 200, "multi-hit does not retarget");
        p = Player("poleaxe", true); e = Enemy(6); other = Enemy(); Cast(p, e, e, other);
        Eq(other.CurrentHP, 195, "poleaxe sweep continues after primary dies"); Eq(p.GetComponent<PlayerAttackAnimation>().Plays, 2, "poleaxe uses two attack phases");
        p = Player("dual_swords"); e = Enemy(); p.ApplyStatus(StatusId.Haste, 1); Cast(p, e); Eq(e.CurrentHP, 184, "haste does not add skill segments");
        p = Player("dual_swords", true); e = Enemy(); p.ApplyStatus(StatusId.Haste, 1); Run(new PlayerAttackAction().Execute(p, e)); Eq(e.CurrentHP, 188, "haste still adds basic segment");
        p = Player("sword"); e = Enemy(); p.SpendDetermination(2); Cast(p, e); Eq(e.CurrentHP, 200, "insufficient resolve prevents damage"); Eq(p.CurrentDetermination, 1, "insufficient resolve remains unchanged");
        p = Player("sword"); e = Enemy(); p.ApplyStatus(StatusId.Exhaust, 1); Cast(p, e); Eq(e.CurrentHP, 200, "exhaust prevents skill"); Eq(p.CurrentDetermination, 3, "exhaust costs nothing");
        p = Player("axe"); Run(new SkillAction(Array.Empty<EnemyCombatant>()).Execute(p, null)); Eq(p.CurrentDetermination, 3, "no targets costs nothing");
        p = Player("sword"); e = Enemy(); other = Enemy(); Cast(p, e, other); Eq(p.CurrentDetermination, 3, "foreign primary costs nothing");
        p = Player("axe"); e = Enemy(); other = Enemy(0); Cast(p, null, e, null, e, other); Eq(e.CurrentHP, 190, "aoe ignores dead/null/duplicate entries");
        p = Player("sword"); e = Enemy(); GameRoot.I = new GameObject().AddComponent<GameRoot>(); GameRoot.I.state.player.nextAttackBonusPercent = 50; Cast(p, e);
        Eq(e.CurrentHP, 164, "next attack bonus applies to skill"); Eq(GameRoot.I.state.player.nextAttackBonusPercent, 0, "skill consumes bonus once");
        GameRoot.I = null;
    }
    static BattleFlowController Flow(PlayerCombatant player, params EnemyCombatant[] enemies)
    {
        var flow = new GameObject("Flow").AddComponent<BattleFlowController>();
        Set(flow, "player", player); Set(flow, "enemies", enemies.ToList());
        Set(flow, "currentState", BattleState.PlayerTurn); Set(flow, "round", 1);
        Set(flow, "roundText", new TMPro.TMP_Text()); Set(flow, "turnManager", new TurnManager());
        return flow;
    }
    static void Targeting()
    {
        var p = Player("sword"); var e = Enemy(); var flow = Flow(p, e);
        flow.PlayerSkill(); Check(flow.IsSelectingSkill, "skill enters target selection"); Eq(p.CurrentDetermination, 3, "selection costs nothing");
        flow.CancelTargetSelection(); Check(flow.CurrentState == BattleState.PlayerTurn, "cancel restores turn"); Eq(p.CurrentDetermination, 3, "cancel costs nothing");
        flow.PlayerSkill(); flow.AttackTarget(e); Check(flow.CurrentState == BattleState.ExecutingAction, "confirmation immediately locks action");
        flow.AttackTarget(e); flow.PlayerSkill(); Eq(flow.Pending.Count, 1, "double click cannot queue duplicate cast");
        foreach (var pending in flow.Pending.ToArray()) Run(pending);
        Eq(e.CurrentHP, 176, "flow executes skill damage"); Eq(p.CurrentDetermination, 1, "flow pays once"); Check(flow.CurrentState == BattleState.PlayerTurn && flow.RoundNumber == 2, "skill advances through enemy turn");
        p = Player("axe"); e = Enemy(); var other = Enemy(); flow = Flow(p, e, other); flow.PlayerSkill();
        Check(flow.CurrentState == BattleState.ExecutingAction && !flow.IsSelectingSkill, "aoe casts without target selection");
        foreach (var pending in flow.Pending.ToArray()) Run(pending);
        Eq(e.CurrentHP, 190, "flow aoe primary"); Eq(other.CurrentHP, 190, "flow aoe secondary");
        p = Player("sword"); e = Enemy(); flow = Flow(p, e); p.SpendDetermination(3); flow.PlayerSkill();
        Check(flow.CurrentState == BattleState.PlayerTurn && flow.Pending.Count == 0, "insufficient resolve does not consume turn");
        p = Player("sword"); e = Enemy(); flow = Flow(p, e); p.ApplyStatus(StatusId.Exhaust, 1); Check(!flow.CanUseWeaponSkill, "exhaust disables skill button eligibility");
        flow.PlayerAttack(); Check(!flow.IsSelectingSkill, "ordinary attack retains its own selection mode");
    }
    static void SharedWeaknesses()
    {
        var body = Enemy(100, 2, (PlayerDamageType.Pierce, DamageReaction.Weak), (PlayerDamageType.Blunt, DamageReaction.Weak));
        var a = body.GetComponent<DamageResistance>();
        var b = Enemy(100, 2, (PlayerDamageType.Pierce, DamageReaction.Weak), (PlayerDamageType.Blunt, DamageReaction.Weak)).GetComponent<DamageResistance>();
        var unrelated = Enemy(100, 2, (PlayerDamageType.Pierce, DamageReaction.Weak)).GetComponent<DamageResistance>();
        var record = new List<PlayerDamageType>();
        a.ShareDiscovery(record); b.ShareDiscovery(record);
        Run(new PlayerAttackAction().Execute(Player("sword"), body));
        Check(a.IsWeaknessKnown(PlayerDamageType.Pierce) && b.IsWeaknessKnown(PlayerDamageType.Pierce), "one species shares a discovered damage type immediately");
        Check(!b.IsWeaknessKnown(PlayerDamageType.Blunt) && b.UnknownWeaknessCount == 1, "a hit reveals only its own damage type");
        Check(!unrelated.IsWeaknessKnown(PlayerDamageType.Pierce), "other species with the same weakness stay undiscovered");
        a.ObserveDamageType(PlayerDamageType.Pierce); Eq(record.Count, 1, "repeated attacks do not duplicate discovery entries");
        Eq(b.RevealWeaknesses(), 1, "lantern reveals only remaining shared weaknesses");
        Check(a.IsWeaknessKnown(PlayerDamageType.Blunt) && a.UnknownWeaknessCount == 0, "lantern discovery propagates back to the first monster");
        Eq(a.RevealWeaknesses(), 0, "already known species cannot consume another reveal");
        b.SetStageRules(new[] { new DamageResistance.DamageRule { damageType = PlayerDamageType.Fire, damageReaction = DamageReaction.Weak } });
        Check(!b.IsWeaknessKnown(PlayerDamageType.Fire) && a.IsWeaknessKnown(PlayerDamageType.Pierce), "phase reset detaches without erasing other monsters or the run record");
        a.ResetDiscovery(); Check(!a.IsWeaknessKnown(PlayerDamageType.Pierce) && record.Count == 2, "battle reset leaves saved discoveries intact");
        a.ShareDiscovery(record); Check(a.UnknownWeaknessCount == 0, "rebinding a new encounter restores species discoveries");
    }

    static void Main(string[] args)
    {
        Root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
        LoadAssets(); NormalSkills(); ImmunityAndBreak(); Boundaries(); Targeting(); SharedWeaknesses();
        var capped = Player("sword");
        Eq(capped.MaxDetermination, 3, "standalone combat shares the three-point default cap");
        Eq(capped.AddDetermination(99), 0, "three-point cap rejects excess determination");
        GameRoot.I = new GameObject().AddComponent<GameRoot>();
        GameRoot.I.state.player.maxResolve = 4; GameRoot.I.state.player.resolve = 4;
        capped.Initialize();
        Eq(capped.MaxDetermination, 4, "explicit run cap is preserved instead of silently reduced");
        Eq(capped.CurrentDetermination, 4, "explicit four-point run restores all four points");
        Console.WriteLine($"PASS: {checks} weapon asset, skill damage, immunity, break, animation, resolve and targeting checks.");
    }
}
