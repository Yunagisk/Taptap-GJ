using System;
using System.Linq;

internal static partial class Program
{
    static void CheckResourceBoundaries()
    {
        var catalog = Catalog();
        var heal = Case(catalog, "被弃的药箱"); var resources = Resources(); resources.player.hp = 94;
        Enter(heal.Level, heal.Node);
        Check(WildernessEventRules.TryChoose(heal.Level, catalog, resources, Layout(), 0).success
            && resources.player.hp == 100, "healing clamps at shared maximum HP");
        var full = Case(catalog, "被弃的药箱"); resources = Resources(); resources.player.hp = 100;
        Enter(full.Level, full.Node);
        Check(WildernessEventRules.TryChoose(full.Level, catalog, resources, Layout(), 0).success
            && resources.player.hp == 100, "full health is preserved");
        var floor = Case(catalog, "夜半送葬"); resources = Resources(); resources.world.sin = 0;
        Enter(floor.Level, floor.Node);
        Check(WildernessEventRules.TryChoose(floor.Level, catalog, resources, Layout(), 0).success
            && resources.world.sin == 0, "sin never falls below zero");
        var natural = Case(catalog, "被弃的药箱"); resources = Resources(); resources.world.sin = 99;
        int bosses = resources.map.spawnedBossNodes.Count;
        Enter(natural.Level, natural.Node);
        Check(WildernessEventRules.TryChoose(natural.Level, catalog, resources, Layout(), 0).success
            && resources.world.sin == 0 && resources.map.spawnedBossNodes.Count == bosses + 1,
            "outdoor extra time preserves natural three-sin growth and boss spawn-reset");
        var moral = Case(catalog, "夜半送葬"); resources = Resources(); resources.world.sin = 90;
        Enter(moral.Level, moral.Node);
        Check(WildernessEventRules.TryChoose(moral.Level, catalog, resources, Layout(), 1).success
            && resources.world.time == 0 && resources.world.sin == 0 && resources.map.spawnedBossNodes.Count == bosses + 1,
            "zero-time event can reach threshold without inventing movement time");
        var capped = Case(catalog, "夜半送葬"); resources = Resources(); resources.world.sin = 95;
        resources.map.spawnedBossNodes = Layout().regions.Select(value => value.bossNodeId).ToList();
        Enter(capped.Level, capped.Node);
        Check(WildernessEventRules.TryChoose(capped.Level, catalog, resources, Layout(), 1).success
            && resources.world.sin == 100 && resources.map.spawnedBossNodes.Count == 3, "sin caps at 100 after all bosses spawn");
        foreach (int existing in new[] { 0, 5, 20, 30 })
        {
            var camp = Case(catalog, "最后一封家书"); resources = Resources(); resources.player.nextCampRecoveryBonusPercent = existing;
            Enter(camp.Level, camp.Node);
            Check(WildernessEventRules.TryChoose(camp.Level, catalog, resources, Layout(), 0).success
                && resources.player.nextCampRecoveryBonusPercent == Math.Max(existing, 20), "camp bonus keeps maximum " + existing);
            if (existing == 0)
            {
                Check(WorldResourceRules.RecoverAtCamp(resources.player) == 30 && resources.player.nextCampRecoveryBonusPercent == 0,
                    "next real recovery consumes camp bonus once");
                resources.player.hp = 50;
                Check(WorldResourceRules.RecoverAtCamp(resources.player) == 25, "second camp recovery has no leftover bonus");
            }
        }
    }
    static void CheckInvalidTransactions()
    {
        var catalog = Catalog();
        void Attempt(string name, Action<WildernessLevelState, WildernessNodeState, GameState, MapLayout, WildernessEventCatalog> corrupt,
            int selected = 0)
        {
            var data = Copy(catalog); var fixture = Case(data, "棺下敲击");
            Force(data, fixture.Level, fixture.Node, 0, 1);
            var resources = Resources(); var layout = Layout(); Enter(fixture.Level, fixture.Node);
            corrupt(fixture.Level, fixture.Node, resources, layout, data);
            string before = Snapshot(fixture.Level, resources), sourceBefore = Save(data) + Save(layout);
            Check(!WildernessEventRules.TryChoose(fixture.Level, data, resources, layout, selected).success
                && Snapshot(fixture.Level, resources) == before && Save(data) + Save(layout) == sourceBefore,
                "invalid transaction is atomic: " + name);
        }
        Attempt("negative choice", (_, _, _, _, _) => { }, -1);
        Attempt("out of range choice", (_, _, _, _, _) => { }, 2);
        Attempt("invalid stored outcome", (_, n, _, _, _) => n.rolls[0].outcomeIndex = 99);
        Attempt("invalid fixed enemy", (_, n, _, _, _) => n.rolls[0].enemyId = "unknown-enemy");
        Attempt("missing player", (_, _, r, _, _) => r.player = null);
        Attempt("missing world", (_, _, r, _, _) => r.world = null);
        Attempt("missing map", (_, _, r, _, _) => r.map = null);
        Attempt("time overflow", (_, _, r, _, _) => r.world.time = int.MaxValue);
        Attempt("sin-roll overflow", (_, _, r, _, _) => r.map.sinRollCount = int.MaxValue);
        Attempt("invalid sin", (_, _, r, _, _) => r.world.sin = 101);
        Attempt("invalid health", (_, _, r, _, _) => r.player.hp = -1);
        Attempt("negative coins", (_, _, r, _, _) => r.world.sinRemnant = -1);
        Attempt("stale location", (l, _, _, _, _) => l.currentNodeId = "village");
        Attempt("null rolls", (_, n, _, _, _) => n.rolls = null);
        Attempt("invalid layout", (_, _, _, m, _) => m.nodes.Clear());
        Attempt("invalid catalog", (_, _, _, _, c) => c.templates[0].choices[0].timeCost = -1);
        Attempt("duplicate template", (l, n, _, _, _) => l.nodes.First(value => value.id != n.id).templateId = n.templateId);
        Attempt("duplicate node ID", (l, n, _, _, _) => l.nodes.First(value => value.id != n.id).id = n.id);
        Attempt("missing seventh question", (l, _, _, _, _) => l.nodes.RemoveAt(l.nodes.Count - 1));
        Attempt("invalid local stage", (l, _, _, _, _) => l.stage = 0);
        Attempt("chosen before resolved", (_, n, _, _, _) => n.chosenIndex = 0);
        Attempt("active but not shown", (_, n, _, _, _) => n.shown = false);
        Attempt("abandoned but still active", (_, n, _, _, _) => n.abandoned = true);
        Attempt("pending before choice", (_, n, _, _, _) => n.encounterPending = true);
        Attempt("encounter resolved before choice", (_, n, _, _, _) => n.encounterResolved = true);
        Attempt("other option has corrupt roll", (_, n, _, _, _) => n.rolls[1].outcomeIndex = 999);
        Attempt("forged repaired road", (l, _, _, _, _) => l.repairedRoads.Add(new WildernessRoad { from = "field01", to = "ghost" }));
        var pending = Case(catalog, "棺下敲击"); Force(catalog, pending.Level, pending.Node, 0, 1);
        var resources = Resources(); Enter(pending.Level, pending.Node);
        Check(WildernessEventRules.TryChoose(pending.Level, catalog, resources, Layout(), 0).success, "reward overflow fixture starts");
        resources.world.sinRemnant = int.MaxValue;
        string beforeReward = Snapshot(pending.Level, resources);
        Check(!WildernessEventRules.ResolveEncounter(pending.Level, resources, true).success
            && Snapshot(pending.Level, resources) == beforeReward, "coin overflow cannot partly finish encounter");
        Check(WildernessEventRules.ResolveEncounter(pending.Level, resources, false).success,
            "coin overflow still permits no-reward text failure");
    }
}

