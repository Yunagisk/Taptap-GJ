using System;
namespace UnityEngine { } // Selector only imports this namespace; tests use no Unity runtime.
public class EnemyAttackData { }
static class Program
{
    static int checks;
    static void Check(bool ok, string name)
    { checks++; if (!ok) throw new Exception(name); }
    static void Main()
    {
        foreach (var end in new[] { (600f, 240f), (-300f, 180f), (0f, 500f), (0f, 0f) })
        {
            BattleTargetingCurve.Evaluate(0, 0, end.Item1, end.Item2, 0, out float x0, out float y0);
            BattleTargetingCurve.Evaluate(0, 0, end.Item1, end.Item2, 1, out float x1, out float y1);
            Check(x0 == 0 && y0 == 0 && x1 == end.Item1 && y1 == end.Item2, "targeting curve meets its exact source and arrow tip");
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                BattleTargetingCurve.Evaluate(0, 0, end.Item1, end.Item2, t, out float x, out float y);
                Check(!float.IsNaN(x) && !float.IsInfinity(y), "targeting curve remains finite even for zero distance");
                Check(Math.Abs(x - end.Item1 * t) < .001 && y >= end.Item2 * t - .001,
                    "curve advances toward its target and bows above the chord");
            }
        }
        for (int count = 1; count <= 4; count++)
        for (int slot = 0; slot < count; slot++)
        {
            BattleWeaponWheelMath.Pose(slot, slot, count, out float x, out float y, out float depth);
            Check(Math.Abs(x) < .001 && Math.Abs(y + 35) < .001 && depth == 1, "confirmed weapon occupies front center");
            float cursor = BattleWeaponWheelMath.NearestCursor(-30.1f, slot, count);
            Check(BattleWeaponWheelMath.CenteredIndex(cursor, count) == slot && Math.Abs(cursor + 30.1f) <= count * .5f + .001f, "snap takes nearest cyclic route");
        }
        var a = new EnemyAttackData(); var b = new EnemyAttackData();
        var cycle = new CycleActionSelector(new[] { null, a, null, b });
        for (int i = 0; i < 100; i++) Check(cycle.PeekAction() == a, "repainting intent does not advance cycle");
        cycle.OnActionCompleted(); Check(cycle.PeekAction() == a, "peek does not select an action");
        Check(cycle.SelectAction() == a, "execution agrees with preview");
        cycle.PeekAction(); cycle.OnActionCompleted(); Check(cycle.PeekAction() == b, "peek during action preserves completion");
        Check(cycle.SelectAction() == b, "skip uses same cycle"); cycle.OnActionCompleted();
        Check(cycle.PeekAction() == a, "cycle wraps across null entries");
        cycle.Reset(); Check(cycle.PeekAction() == a, "battle restart resets intent");
        Check(new CycleActionSelector(null).PeekAction() == null, "null action list");
        Check(new CycleActionSelector(new EnemyAttackData[] { null }).PeekAction() == null, "empty action cycle");
        Console.WriteLine($"PASS: {checks} battle UI interaction and intent checks");
    }
}
