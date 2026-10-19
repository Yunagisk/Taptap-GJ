using System;

static class Program
{
    static int checks;
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }

    static void Main()
    {
        var clock = new EnemyAttackAnimationClock(18, 30, 9);
        Check(Math.Abs(clock.Duration - .6f) < .0001f, "18 drawings last .6 seconds");
        Check(Math.Abs(clock.ContactTime - .3f) < .0001f, "contact is the tenth drawing");
        Check(!clock.At(.299f).DeliverHit, "wind-up never deals damage");
        var contact = clock.At(.3f);
        Check(contact.DeliverHit && contact.Frame == 9 && !contact.Complete, "contact is visible when damage is delivered");
        Check(!clock.At(.3f).DeliverHit, "sampling the contact twice cannot double-hit");
        Check(!clock.At(.59f).Complete && clock.At(.6f).Complete, "turn waits for recovery");

        foreach (float hitch in new[] { .31f, .6f, 1f, 10f })
        {
            var slow = new EnemyAttackAnimationClock(18, 30, 9);
            slow.At(0f);
            var sample = slow.At(hitch);
            Check(sample.DeliverHit && sample.Frame == 9 && !sample.Complete,
                "slow frame shows contact before completing");
            Check(Math.Abs(sample.Time - .3f) < .0001f, "slow frame uses contact motion pose");
            Check(!slow.At(hitch).DeliverHit, "slow frame delivers exactly one hit");
        }
        foreach (float step in new[] { 1f / 30, 1f / 60, .1f, .25f, .7f })
        {
            var playback = new EnemyAttackAnimationClock(18, 30, 9);
            int hits = 0;
            bool finished = false;
            for (int i = 0; i < 120 && !finished; i++)
            {
                var sample = playback.At(i * step);
                if (sample.DeliverHit) hits++;
                Check(sample.Frame >= 0 && sample.Frame < 18, "frame stays inside sequence");
                finished = sample.Complete;
            }
            Check(finished && hits == 1, "frame-rate variation preserves a single hit and completion");
        }
        var invalid = new EnemyAttackAnimationClock(0, 0, 100);
        Check(invalid.At(-1).DeliverHit && invalid.Duration == 1f, "invalid metadata remains finite");
        Console.WriteLine($"PASS: {checks} enemy attack contact, hitch and recovery checks");
    }
}
