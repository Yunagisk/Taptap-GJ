using System;
using System.Linq;
using System.Reflection;

internal static partial class Program
{
    // Inject a bounded random value into the private pure selector to verify every
    // percentage boundary without adding a testing API to production rules.
    static uint UndoLeft(uint value, int shift)
    {
        uint result = value;
        for (int i = 0; i < 32; i += shift) result = value ^ (result << shift);
        return result;
    }
    static uint UndoRight(uint value, int shift)
    {
        uint result = value;
        for (int i = 0; i < 32; i += shift) result = value ^ (result >> shift);
        return result;
    }
    static object RandomWithNext(int value)
    {
        var type = typeof(WildernessEventRules).GetNestedType("StableRandom", BindingFlags.NonPublic);
        var random = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { 123, 1 }, null);
        uint before = UndoLeft(UndoRight(UndoLeft((uint)value, 5), 17), 13);
        type.GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(random, before);
        return random;
    }
    static void CheckProbabilityBoundaries()
    {
        var catalog = Catalog();
        var select = typeof(WildernessEventRules).GetMethod("SelectOutcome", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var definition in catalog.templates)
            foreach (var choice in definition.choices)
                for (int roll = 0; roll < 100; roll++)
                {
                    int expected = 0, cumulative = choice.outcomes[0].weight;
                    while (roll >= cumulative) cumulative += choice.outcomes[++expected].weight;
                    int actual = (int)select.Invoke(null, new object[] { choice.outcomes, RandomWithNext(roll) });
                    Check(actual == expected, definition.title + " percentage boundary " + roll);
                }
        var selectDefinition = typeof(WildernessEventRules).GetMethod("SelectDefinition", BindingFlags.Static | BindingFlags.NonPublic);
        for (int roll = 0; roll < 19; roll++)
        {
            int expected = 0, cumulative = catalog.templates[0].weight;
            while (roll >= cumulative) cumulative += catalog.templates[++expected].weight;
            int actual = (int)selectDefinition.Invoke(null, new object[] { catalog.templates, RandomWithNext(roll) });
            Check(actual == expected, "template weighted boundary " + roll);
        }
    }
    static void CheckGeneration()
    {
        var catalog = Catalog(); string original = Save(catalog);
        var first = catalog.templates.ToDictionary(value => value.id, value => 0);
        var second = catalog.templates.ToDictionary(value => value.id,
            value => catalog.templates.ToDictionary(next => next.id, next => 0));
        var observations = catalog.templates.ToDictionary(value => value.id,
            value => value.choices.Select(choice => new int[choice.outcomes.Count]).ToArray());
        for (int seed = 0; seed < 10000; seed++)
        {
            var level = WildernessEventRules.Generate(catalog, seed, 1);
            Check(level.nodes.Count == 7 && level.nodes.Select(value => value.templateId).Distinct().Count() == 7,
                "weighted sampling removes full template seed " + seed);
            Check(level.nodes.Select(value => value.id).Distinct().Count() == 7
                && level.currentNodeId == catalog.graph.villageNodeId, "seven unique question locations seed " + seed);
            first[level.nodes[0].templateId]++;
            second[level.nodes[0].templateId][level.nodes[1].templateId]++;
            foreach (var node in level.nodes)
            {
                var definition = WildernessEventRules.FindDefinition(catalog, node.templateId);
                Check(node.rolls.Count == 2 && !node.shown && !node.resolved && !node.abandoned && node.chosenIndex == -1,
                    "saved rolls and untouched flags seed " + seed + " " + node.id);
                for (int choice = 0; choice < 2; choice++)
                {
                    var roll = node.rolls[choice];
                    Check(roll.outcomeIndex >= 0 && roll.outcomeIndex < definition.choices[choice].outcomes.Count,
                        "stored outcome is in range");
                    var outcome = definition.choices[choice].outcomes[roll.outcomeIndex];
                    Check(outcome.encounter
                        ? catalog.enemyVariants.Any(value => value.id == roll.enemyId && value.stage == 1)
                        : string.IsNullOrEmpty(roll.enemyId), "only hidden results save one stage enemy ID");
                    observations[node.templateId][choice][roll.outcomeIndex]++;
                }
            }
            if (seed < 40)
                Check(Save(level) == Save(WildernessEventRules.Generate(catalog, seed, 1)), "same seed exact level " + seed);
        }
        foreach (var a in catalog.templates)
        {
            double expected = 10000d * a.weight / 19;
            Check(Math.Abs(first[a.id] - expected) < 6 * Math.Sqrt(expected * (1 - a.weight / 19d)) + 5,
                a.title + " first draw follows weight / 19");
            foreach (var b in catalog.templates.Where(value => value.id != a.id))
            {
                double probability = b.weight / (19d - a.weight);
                double expectedSecond = first[a.id] * probability;
                Check(Math.Abs(second[a.id][b.id] - expectedSecond) < 6 * Math.Sqrt(expectedSecond * (1 - probability)) + 5,
                    "second draw removes first weight " + a.title + " -> " + b.title);
            }
            for (int choice = 0; choice < 2; choice++)
            {
                int total = observations[a.id][choice].Sum();
                foreach (int outcome in Enumerable.Range(0, a.choices[choice].outcomes.Count))
                {
                    double p = a.choices[choice].outcomes[outcome].weight / 100d;
                    Check(Math.Abs(observations[a.id][choice][outcome] - total * p) < 6 * Math.Sqrt(total * p * (1 - p)) + 5,
                        a.title + " generated probability " + choice + "/" + outcome);
                }
            }
        }
        for (int stage = 1; stage <= 3; stage++)
        {
            var level = WildernessEventRules.Generate(catalog, int.MinValue + stage, stage);
            Check(level.stage == stage && level.nodes.SelectMany(value => value.rolls).Where(value => value.enemyId != null)
                .All(value => catalog.enemyVariants.Any(enemy => enemy.id == value.enemyId && enemy.stage == stage)), "all saved variants at stage " + stage);
        }
        Check(Save(catalog) == original, "10,000 generations leave catalog unchanged");
    }
}
