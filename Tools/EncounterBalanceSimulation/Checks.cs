using System;
using System.Linq;
using System.Reflection;

// Hosting stubs only. Tests execute the production selector, intent budget,
// encounter tables, attack presentation and data types.
namespace UnityEngine
{
    public class ScriptableObject { public string name = "test"; }
    public class Header : Attribute { public Header(string text) { } }
    public class SerializeField : Attribute { }
    public class Min : Attribute { public Min(float value) { } }
    public class CreateAssetMenu : Attribute { public string fileName, menuName; }
    public static class Mathf { public static int Max(int a, int b) => Math.Max(a, b); public static float Max(float a, float b) => Math.Max(a, b); }
}
public class Combatant { public bool IsDead; public void ApplyStatus(StatusId id, int count) { } }
public struct EnemyAttackInfo { public EnemyAttackInfo(Combatant attacker, Combatant target, int damage) { } }
public class DamageResistance { public class DamageRule { } }
public static class StatusRules { public static string DisplayName(StatusId id) => id.ToString(); }

static class Checks
{
    static int checks;
    static void Check(bool condition, string name)
    { checks++; if (!condition) throw new Exception(name); }
    static void Set(object instance, string field, object value) => instance.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    static EnemyAttackData Attack(int weight, EnemyActionKind kind = EnemyActionKind.Attack,
        EnemyAttackData followUp = null, bool status = false)
    {
        var action = new EnemyAttackData();
        Set(action, "weight", weight); Set(action, "kind", kind);
        Set(action, "followUp", followUp); Set(action, "applyStatus", status); Set(action, "statusStacks", status ? 2 : 0);
        return action;
    }
    static void Main()
    {
        var a = Attack(45); var b = Attack(25); var c = Attack(20); var d = Attack(10);
        int calls = 0;
        var selector = new WeightedEnemyActionSelector(new[] { a, b, c, d }, max => { calls++; return max - 1; });
        Check(selector.Prepare(action => action.Weight) == d, "last positive weight can be selected");
        for (int i = 0; i < 100; i++) Check(selector.PeekAction() == d && selector.SelectAction() == d, "intent stays locked");
        Check(calls == 1, "preview and execution never reroll");
        selector.OnActionCompleted();
        Check(selector.PeekAction() == null && selector.ActionsTaken == 1, "completion clears one intent");
        selector.Reset(); Check(selector.ActionsTaken == 0 && selector.LastAction == null, "reset clears battle history");

        var rng = new Random(20261020); int[] frequencies = new int[4];
        selector = new WeightedEnemyActionSelector(new[] { a, b, c, d }, rng.Next);
        for (int i = 0; i < 100000; i++)
        {
            var chosen = selector.Prepare(action => action.Weight);
            frequencies[Array.IndexOf(new[] { a, b, c, d }, chosen)]++;
            selector.OnActionCompleted();
        }
        for (int i = 0; i < 4; i++) Check(Math.Abs(frequencies[i] / 1000f - new[] { 45, 25, 20, 10 }[i]) < .6f, "weight distribution");

        var heavy = Attack(0); var charge = Attack(20, EnemyActionKind.Charge, heavy);
        selector = new WeightedEnemyActionSelector(new[] { charge }, max => 0);
        selector.Prepare(action => action.Weight); selector.OnActionCompleted();
        Check(selector.IsCharged && selector.Prepare(action => 0) == heavy, "charge guarantees a heavy without random selection");
        selector.CancelCharge(); selector.SkipAction();
        Check(!selector.IsCharged && selector.PeekAction() == null, "break skips and cancels charged heavy");

        var status = Attack(25, status: true);
        selector = new WeightedEnemyActionSelector(new[] { status, a }, max => 0);
        Check(selector.Prepare(action => action.Weight) == status, "status chosen");
        selector.OnActionCompleted();
        for (int turn = 0; turn < 2; turn++)
        {
            Check(selector.Prepare(action => action.HasStatus && selector.ActionsTaken < selector.StatusReadyAt ? 0 : action.Weight) == a,
                "two following actions cannot repeat the status");
            selector.OnActionCompleted();
        }
        Check(selector.Prepare(action => action.Weight) == status, "status available after cooldown");
        var budget = new EnemyIntentBudget();
        budget.Reserve(charge); Check(!budget.Allows(charge) && budget.Allows(a), "one charge per round");
        budget.Reserve(status); Check(!budget.Allows(status), "one planned cast per status per round");
        Check(charge.IntentAmountText == "蓄力" && d.IntentAmountText == "10", "intent presentation identifies charge");
        Check(Attack(10, EnemyActionKind.Guard).IntentAmountText == "防御", "guard is not displayed as zero damage");
        var noHit = Attack(10, status: true); Set(noHit, "damage", 0);
        Check(noHit.CompactIntentText == "0" && noHit.IntentDetailText.Contains(StatusRules.DisplayName(noHit.StatusOnHit)),
            "pure status intents collapse to zero damage but retain the rider in their detail text");
        Check(charge.CompactIntentText == "蓄力" && charge.IntentDetailText.Contains(heavy.CompactIntentText),
            "charge detail explains the following heavy damage");
        var cycle = new CycleActionSelector(new[] { a, charge, d });
        Check(cycle.PeekAction() == a && cycle.PeekAction() == a, "cycle preview does not advance");
        cycle.SelectAction(); cycle.OnActionCompleted();
        Check(cycle.SelectAction() == charge, "cycle reaches its charge entry");
        cycle.OnActionCompleted();
        Check(cycle.IsCharged && cycle.PeekAction() == heavy, "cycle inserts the heavy follow-up");
        cycle.SelectAction(); cycle.SkipAction();
        Check(!cycle.IsCharged && cycle.PeekAction() == d, "breaking heavy cancels it without skipping the following entry");
        cycle.SelectAction(); cycle.OnActionCompleted();
        Check(cycle.PeekAction() == a, "cycle resumes normally after interruption");
        cycle.SelectAction(); cycle.OnActionCompleted();
        cycle.SelectAction(); cycle.SkipAction();
        Check(!cycle.IsCharged && cycle.PeekAction() == d, "breaking charge never creates a heavy");
        cycle.Reset(); cycle.SelectAction(); cycle.OnActionCompleted();
        cycle.SelectAction(); cycle.OnActionCompleted();
        cycle.SelectAction(); cycle.OnActionCompleted();
        Check(cycle.PeekAction() == d && !cycle.IsCharged, "executed follow-up does not consume two cycle entries");
        cycle.Reset(); Check(cycle.PeekAction() == a, "cycle reset clears pending follow-up state");
        Set(a, "attackCount", 2);
        Check(a.CompactIntentText == "10×2" && a.IntentDetailText.Contains("2"), "multi-hit detail retains the number of hits");
        Set(a, "attackCount", 1);

        int triples = 0;
        for (int i = 0; i < 10000; i++)
        {
            var first = SmallMonsterEncounterRules.Choose(i, "first", 0);
            Check(first.Length == 2 && first.All(kind => kind == SmallMonsterKind.Rat), "first encounter is two rats");
            Check(SmallMonsterEncounterRules.Choose(i, "mid", 5).Length == 2, "early/mid encounters have two enemies");
            var late = SmallMonsterEncounterRules.Choose(i, "late", 6);
            Check(late.SequenceEqual(SmallMonsterEncounterRules.Choose(i, "late", 6)), "node roster is reproducible");
            if (late.Length == 3) triples++;
        }
        Check(triples > 3700 && triples < 4300, "late roster has approximately forty percent triples");
        Console.WriteLine($"PASS: {checks} small-monster AI, intent and encounter checks; weight counts={string.Join(',', frequencies)}");
    }
}
