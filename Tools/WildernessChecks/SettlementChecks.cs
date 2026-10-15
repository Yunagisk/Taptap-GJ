using System;

internal static partial class Program
{
    static void CheckEveryOutcome()
    {
        var catalog = Catalog(); string catalogBefore = Save(catalog);
        foreach (var expected in ExpectedCases.All)
            for (int choice = 0; choice < 2; choice++)
                for (int outcome = 0; outcome < expected.Choices[choice].Outcomes.Length; outcome++)
                {
                    var fixture = Case(catalog, expected.Title);
                    var level = fixture.Level; var node = fixture.Node;
                    Force(catalog, level, node, choice, outcome);
                    var resources = Resources();
                    string inventoryBefore = Save(resources.inventory), rollsBefore = Save(node.rolls);
                    var want = expected.Choices[choice]; var branch = want.Outcomes[outcome];
                    string label = expected.Title + " " + choice + "/" + outcome;
                    Enter(level, node);
                    var result = WildernessEventRules.TryChoose(level, catalog, resources, Layout(), choice);
                    Check(result.success && node.resolved && node.chosenIndex == choice, label + " settles selected option");
                    int time = want.Time + (branch.Encounter ? 1 : 0);
                    Check(resources.world.time == time, label + " exact extra time including hidden battle");
                    Check(resources.world.sin == Math.Clamp(40 + time * 3 + branch.Sin, 0, 100), label + " natural and event sin");
                    Check(resources.player.hp == Math.Min(100, 50 + branch.Heal), label + " global healing");
                    Check(Save(resources.inventory) == inventoryBefore && resources.player.nextDirectHitReductionPercent == 7
                        && resources.player.nextAttackBonusPercent == 9, label + " item/attack/hit effects only text");
                    Check(resources.player.nextCampRecoveryBonusPercent == branch.Camp, label + " camp recovery effect");
                    Check(Save(node.rolls) == rollsBefore, label + " selecting never rerolls saved outcomes");
                    Check(level.history.Count == 1 && level.history[0] == result.message, label + " one durable receipt");
                    if (branch.Reward != "") Check(result.message.Contains(branch.Reward) && result.message.Contains("文字"), label + " item text visible");
                    if (branch.Hit != 0 || branch.Attack != 0) Check(result.message.Contains("15%") && result.message.Contains("文字"), label + " combat text visible");
                    Check(result.encounterPending == branch.Encounter && node.encounterPending == branch.Encounter,
                        label + " hidden battle only from stored branch");
                    string settled = Snapshot(level, resources);
                    Check(!WildernessEventRules.TryChoose(level, catalog, resources, Layout(), choice).success
                        && Snapshot(level, resources) == settled, label + " double click has no effects");
                    if (branch.Encounter)
                    {
                        Check(!string.IsNullOrEmpty(node.enemyId) && result.message.Contains("×1"), label + " exactly one fixed enemy");
                        WildernessEventRules.Leave(level);
                        Check(Snapshot(level, resources) == settled, label + " pending encounter cannot be dismissed");
                        var victoryLevel = Copy(level); var victoryResources = Copy(resources);
                        int coins = victoryResources.world.sinRemnant;
                        string playerBefore = Save(victoryResources.player);
                        int oldTime = victoryResources.world.time, oldSin = victoryResources.world.sin;
                        Check(WildernessEventRules.ResolveEncounter(victoryLevel, victoryResources, true).success,
                            label + " hidden victory settles");
                        Check(victoryResources.world.sinRemnant == coins + 1 && victoryResources.world.time == oldTime
                            && victoryResources.world.sin == oldSin && Save(victoryResources.player) == playerBefore,
                            label + " victory exactly one coin with no damage or extra time");
                        string victorySnapshot = Snapshot(victoryLevel, victoryResources);
                        Check(!WildernessEventRules.ResolveEncounter(victoryLevel, victoryResources, true).success
                            && Snapshot(victoryLevel, victoryResources) == victorySnapshot, label + " no duplicate victory payment");
                        var failedLevel = Copy(level); var failedResources = Copy(resources);
                        string failedResourcesBefore = Save(failedResources);
                        Check(WildernessEventRules.ResolveEncounter(failedLevel, failedResources, false).success
                            && Save(failedResources) == failedResourcesBefore, label + " text failure changes no resources");
                        string failedSnapshot = Snapshot(failedLevel, failedResources);
                        Check(!WildernessEventRules.ResolveEncounter(failedLevel, failedResources, true).success
                            && Snapshot(failedLevel, failedResources) == failedSnapshot, label + " failure cannot later grant a reward");
                    }
                    else Check(string.IsNullOrEmpty(node.enemyId) && !node.encounterResolved, label + " ordinary choice creates no enemy");
                    WildernessEventRules.Leave(level);
                    if (!branch.Encounter) Check(!WildernessEventRules.TryEnter(level, node.id), label + " completed node never prompts again");
                }
        Check(Save(catalog) == catalogBefore, "all 27 outcomes leave catalog unchanged");
    }

    static void CheckVisitAndPersistence()
    {
        var catalog = Catalog(); var fixture = Case(catalog, "棺下敲击");
        var level = fixture.Level; var node = fixture.Node; var resources = Resources();
        string initial = Snapshot(level, resources);
        Check(!WildernessEventRules.TryEnter(level, node.id) && Snapshot(level, resources) == initial,
            "entry requires actually occupying the location");
        Check(!WildernessEventRules.TryChoose(level, catalog, resources, Layout(), 0).success
            && Snapshot(level, resources) == initial, "choice before entry changes nothing");
        string fixedRolls = Save(level.nodes.ConvertAll(value => value.rolls));
        Enter(level, node); string opened = Snapshot(level, resources);
        Check(!WildernessEventRules.TryEnter(level, node.id) && Snapshot(level, resources) == opened, "opening twice keeps original active event");
        WildernessEventRules.Leave(level);
        Check(node.shown && node.abandoned && !node.resolved && node.chosenIndex == -1 && level.activeNodeId == null,
            "unanswered dismiss consumes only one-time prompt");
        Check(Save(resources) == Save(Resources()), "unanswered dismiss spends no option resources");
        string abandoned = Snapshot(level, resources);
        Check(!WildernessEventRules.TryEnter(level, node.id) && Snapshot(level, resources) == abandoned, "abandoned prompt cannot reopen");
        var loaded = Copy(level);
        Check(Save(loaded) == Save(level) && !WildernessEventRules.TryEnter(loaded, node.id), "JSON saves abandoned prompt history");
        Check(Save(level.nodes.ConvertAll(value => value.rolls)) == fixedRolls, "entry/dismiss/reload never reroll");
        WildernessEventRules.Leave(level);
        Check(Snapshot(level, resources) == abandoned, "repeated close is inert");
        var saved = Case(catalog, "被弃的药箱"); Force(catalog, saved.Level, saved.Node, 1, 1);
        var originalResources = Resources(); var restoredLevel = Copy(saved.Level); var restoredResources = Copy(originalResources);
        originalResources.world.stage = 3; restoredResources.world.stage = 3;
        Enter(saved.Level, saved.Node); Enter(restoredLevel, WildernessEventRules.FindNode(restoredLevel, saved.Node.id));
        Check(WildernessEventRules.TryChoose(saved.Level, catalog, originalResources, Layout(), 1).success
            && WildernessEventRules.TryChoose(restoredLevel, catalog, restoredResources, Layout(), 1).success,
            "saved result settles after global stage changes");
        Check(Snapshot(saved.Level, originalResources) == Snapshot(restoredLevel, restoredResources),
            "JSON continuation has identical result without regeneration");
        Check(catalog.enemyVariants.Find(value => value.id == saved.Node.enemyId).stage == 1,
            "enemy retains generation stage after world advances");
        var pendingReload = Copy(saved.Level); var pendingResources = Copy(originalResources);
        Check(WildernessEventRules.ResolveEncounter(pendingReload, pendingResources, true).success,
            "JSON pending encounter finishes without reroll");
        string complete = Snapshot(pendingReload, pendingResources);
        pendingReload = Copy(pendingReload); pendingResources = Copy(pendingResources);
        Check(!WildernessEventRules.ResolveEncounter(pendingReload, pendingResources, true).success
            && Snapshot(pendingReload, pendingResources) == complete, "reload cannot duplicate encounter reward");
    }
}
