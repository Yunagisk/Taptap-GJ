using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

// Runs production pure rules and real-map content integration without Unity, GameRoot or scene loads.
internal static partial class Program
{
    static readonly JsonSerializerOptions Json = JsonOptions();
    static int checks;
    static string catalogPath = Path.Combine(AppContext.BaseDirectory, "WildernessEvents.json");
    static string layoutPath = Path.Combine(AppContext.BaseDirectory, "CenteredThreeSinWhitebox.json");
    static WildernessEventCatalog Catalog() => JsonSerializer.Deserialize<WildernessEventCatalog>(File.ReadAllText(catalogPath), Json);
    static MapLayout Layout() => JsonSerializer.Deserialize<MapLayout>(File.ReadAllText(layoutPath), Json);
    static string Save<T>(T value) => JsonSerializer.Serialize(value, Json);
    static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(Save(value), Json);
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        checks++;
    }
    static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, name);
    }
    static GameState Resources(int seed = 2468)
    {
        var result = new GameState();
        new MapProgression(Layout(), result.map, result.world, seed);
        result.player.hp = 50;
        result.player.nextDirectHitReductionPercent = 7;
        result.player.nextAttackBonusPercent = 9;
        result.inventory.items.Add(new ItemStack("existing-item", 3));
        return result;
    }
    static WildernessEventDefinition Definition(WildernessEventCatalog catalog, string title)
        => catalog.templates.Single(value => value.title == title);
    static (WildernessLevelState Level, WildernessNodeState Node) Case(WildernessEventCatalog catalog, string title, int stage = 1)
    {
        var definition = Definition(catalog, title);
        for (int seed = 0; seed < 1000; seed++)
        {
            var level = WildernessEventRules.Generate(catalog, seed, stage);
            var node = level.nodes.Find(value => value.templateId == definition.id);
            if (node != null) return (level, node);
        }
        throw new Exception("No generated fixture contains " + title);
    }
    static void Enter(WildernessLevelState level, WildernessNodeState node)
    {
        level.currentNodeId = node.id;
        Check(WildernessEventRules.TryEnter(level, node.id), "first entry opens " + node.id);
    }
    static void Force(WildernessEventCatalog catalog, WildernessLevelState level, WildernessNodeState node, int choice, int outcome)
    {
        var effect = WildernessEventRules.FindDefinition(catalog, node.templateId).choices[choice].outcomes[outcome];
        node.rolls[choice].outcomeIndex = outcome;
        node.rolls[choice].enemyId = effect.encounter
            ? catalog.enemyVariants.First(value => value.stage == level.stage).id : null;
    }
    static string Snapshot(WildernessLevelState level, GameState resources) => Save(level) + "\n" + Save(resources);
    static void Main(string[] args)
    {
        if (args.Length > 0) catalogPath = Path.GetFullPath(args[0]);
        if (args.Length > 1) layoutPath = Path.GetFullPath(args[1]);
        CheckCatalog();
        CheckProbabilityBoundaries();
        CheckGeneration();
        CheckEveryOutcome();
        CheckVisitAndPersistence();
        CheckResourceBoundaries();
        CheckBridges();
        CheckInvalidTransactions();
        int previewChecks = checks;
        CheckMapIntegration();
        Console.WriteLine($"Map integration: {checks - previewChecks} assertions across both production maps.");
        Console.WriteLine($"PASS: {checks} wilderness assertions; 10 templates / 20 choices / 27 outcomes; 10,000 fixed seeds.");
    }
    static void CheckCatalog()
    {
        var catalog = Catalog();
        string original = Save(catalog);
        WildernessEventRules.ValidateCatalog(catalog);
        Check(Save(catalog) == original, "catalog validation is read-only");
        Check(catalog.templates.Count == 10 && catalog.templates.Sum(value => value.weight) == 19,
            "ten templates and total weight nineteen");
        Check(catalog.templates.Sum(value => value.choices.Count) == 20, "twenty choices");
        Check(catalog.templates.Sum(value => value.choices.Sum(choice => choice.outcomes.Count)) == 27, "twenty-seven result branches");
        foreach (var expected in ExpectedCases.All)
        {
            var actual = Definition(catalog, expected.Title);
            Check(actual.weight == expected.Weight, expected.Title + " template weight");
            Check(actual.choices.Count == expected.Choices.Length, expected.Title + " choice count");
            for (int c = 0; c < expected.Choices.Length; c++)
            {
                var want = expected.Choices[c]; var have = actual.choices[c];
                string label = expected.Title + " option " + c;
                Check(have.timeCost == want.Time, label + " extra time");
                Check(have.outcomes.Count == want.Outcomes.Length, label + " branch count");
                Check(have.outcomes.Sum(value => value.weight) == 100, label + " probability sums to 100");
                for (int o = 0; o < want.Outcomes.Length; o++)
                {
                    var w = want.Outcomes[o]; var h = have.outcomes[o];
                    string branch = label + " outcome " + o;
                    Check(h.weight == w.Percent, branch + " exact probability");
                    Check(h.sinDelta == w.Sin && h.healAmount == w.Heal, branch + " sin and healing");
                    Check(h.encounter == w.Encounter && h.repairBridge == w.Bridge, branch + " encounter and bridge");
                    Check(h.campRecoveryBonusPercent == w.Camp, branch + " camp recovery bonus");
                    Check(string.IsNullOrEmpty(w.Reward) ? string.IsNullOrEmpty(h.rewardText)
                        : h.rewardText != null && h.rewardText.Contains(w.Reward) && h.rewardText.Contains("1"), branch + " text reward");
                    string effect = (h.effectText ?? "").Replace('−', '-');
                    Check(w.Hit == 0 || effect.Contains("受击") && effect.Contains("15%"), branch + " reduction text");
                    Check(w.Attack == 0 || effect.Contains("攻击") && effect.Contains("15%"), branch + " attack text");
                    Check(w.Hit != 0 || w.Attack != 0 || string.IsNullOrEmpty(effect), branch + " no unintended effect");
                }
            }
        }
        Check(catalog.graph.nodes.Count == 8, "one village and seven standalone question locations");
        Check(catalog.graph.repairableRoads.Count > 0, "configured missing bridge");
        foreach (int stage in new[] { 1, 2, 3 })
            Check(catalog.enemyVariants.Any(value => value.stage == stage), "stage variant " + stage);
        Check(Save(catalog) == original, "queries leave catalog unchanged");
    }
}

