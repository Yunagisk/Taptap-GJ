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
        foreach (float hit in new[] { 9f / 18, 9f / 21, 12f / 27, 18f / 36 })
        {
            bool bounded = true, forward = true, back = true, gentle = true;
            float previous = EnemyAttackMotion.Advance(0, hit);
            for (int tick = 1; tick <= 300; tick++)
            {
                float t = tick / 300f, p = EnemyAttackMotion.Advance(t, hit);
                bounded &= p >= -.00001f && p <= 1.00001f;
                forward &= t > hit || p >= previous - .00001f;
                back &= t <= hit + .004f || p <= previous + .00001f;
                gentle &= Math.Abs(p - previous) < .03f;
                previous = p;
            }
            Check(bounded && forward && back && gentle, "monster attack has one bounded, continuous advance and return");
            Check(Math.Abs(EnemyAttackMotion.Advance(hit, hit) - 1) < .00001f
                && EnemyAttackMotion.Advance(.96f, hit) == 0, "contact peaks once and settles before idle");
        }
        bool startsUnchanged = true, movesDown = true, staysContinuous = true, endsStill = true, feetGrounded = true;
        for (int x = 0; x <= 10; x++) for (int y = 0; y <= 10; y++)
        {
            float u = x / 10f, v = y / 10f;
            var previous = EnemyDeathMotion.Sample(u, v, 0f);
            startsUnchanged &= Math.Abs(previous.x - u) < .00001f && Math.Abs(previous.y - v) < .00001f;
            for (int tick = 1; tick <= 120; tick++)
            {
                var point = EnemyDeathMotion.Sample(u, v, tick / 120f);
                movesDown &= point.y <= previous.y + .00001f && point.y >= -.00001f;
                staysContinuous &= Math.Abs(point.y - previous.y) < .02f && Math.Abs(point.x - previous.x) < .01f;
                if (y == 0) feetGrounded &= Math.Abs(point.y) < .00001f;
                previous = point;
            }
            var end = EnemyDeathMotion.Sample(u, v, 4f);
            endsStill &= Math.Abs(end.x - previous.x) < .00001f && Math.Abs(end.y - previous.y) < .00001f;
        }
        Check(startsUnchanged, "death starts from the exact current drawing");
        Check(movesDown, "all sampled death points descend without upward twitches");
        Check(staysContinuous, "collapse remains continuous over 120 time samples");
        Check(endsStill, "completed death does not rebound or oscillate");
        Check(feetGrounded, "the ground contact line stays fixed");
        foreach (var profile in new[] { EnemyArtworkProfile.Rat, EnemyArtworkProfile.Wraith, EnemyArtworkProfile.Sloth })
        {
            bool exact = true, down = true, stable = true, continuous = true, periodic = true;
            for (int x = 0; x <= 10; x++) for (int y = 0; y <= 10; y++)
            {
                float u = x / 10f, v = y / 10f;
                var prev = EnemyArtworkMotion.Sample(profile, EnemyArtworkPose.Death, u, v, 0);
                exact &= Math.Abs(prev.x - u) < .00001f && Math.Abs(prev.y - v) < .00001f;
                for (int tick = 1; tick <= 120; tick++)
                {
                    var p = EnemyArtworkMotion.Sample(profile, EnemyArtworkPose.Death, u, v, tick / 120f);
                    down &= p.y <= prev.y + .00001f && p.y >= -.00001f;
                    continuous &= Math.Abs(p.x - prev.x) < .02f && Math.Abs(p.y - prev.y) < .02f;
                    prev = p;
                }
                var end = EnemyArtworkMotion.Sample(profile, EnemyArtworkPose.Death, u, v, 5);
                stable &= Math.Abs(end.x - prev.x) < .00001f && Math.Abs(end.y - prev.y) < .00001f;
                // Rat tail uses a half-frequency wave, hence two breathing cycles per complete loop.
                var a = EnemyArtworkMotion.Sample(profile, EnemyArtworkPose.Idle, u, v, 0, 3);
                var b = EnemyArtworkMotion.Sample(profile, EnemyArtworkPose.Idle, u, v, 6, 3);
                periodic &= Math.Abs(a.x - b.x) < .00001f && Math.Abs(a.y - b.y) < .00001f;
                foreach (var pose in new[] { EnemyArtworkPose.Attack, EnemyArtworkPose.Hurt })
                {
                    var start = EnemyArtworkMotion.Sample(profile, pose, u, v, 0);
                    var finish = EnemyArtworkMotion.Sample(profile, pose, u, v, 1);
                    exact &= Math.Abs(start.x - u) < .00001f && Math.Abs(start.y - v) < .00001f
                        && Math.Abs(finish.x - u) < .00001f && Math.Abs(finish.y - v) < .00001f;
                }
            }
            Check(exact, profile + " starts death exactly and recovers attacks/hurt to the original pose");
            Check(down && continuous, profile + " falls continuously without rebounding or crossing the floor");
            Check(stable, profile + " holds its final corpse pose");
            Check(periodic, profile + " idle loops without a seam");
            var lower = EnemyArtworkMotion.Sample(profile, EnemyArtworkPose.Death, .5f, .1f, 1);
            var upper = EnemyArtworkMotion.Sample(profile, EnemyArtworkPose.Death, .5f, .8f, 1);
            Check(lower.y > 0 && upper.y > lower.y + .15f,
                profile + " corpse retains positive silhouette area instead of sinking below the floor");
        }
        foreach (var pose in new[] { EnemyArtworkPose.Idle, EnemyArtworkPose.Attack, EnemyArtworkPose.Hurt, EnemyArtworkPose.Death })
        {
            bool planted = true, positiveArea = true;
            for (int tick = 0; tick <= 144; tick++)
            {
                float time = pose == EnemyArtworkPose.Idle ? tick / 30f : tick / 144f;
                for (int x = 0; x <= 32; x++)
                {
                    var foot = EnemyArtworkMotion.Sample(EnemyArtworkProfile.Sloth, pose, x / 32f, 0, time, 4.8f, .5f);
                    planted &= Math.Abs(foot.y) < .00001f;
                }
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    var a = EnemyArtworkMotion.Sample(EnemyArtworkProfile.Sloth, pose, x / 32f, y / 32f, time, 4.8f, .5f);
                    var b = EnemyArtworkMotion.Sample(EnemyArtworkProfile.Sloth, pose, (x + 1) / 32f, y / 32f, time, 4.8f, .5f);
                    var c = EnemyArtworkMotion.Sample(EnemyArtworkProfile.Sloth, pose, x / 32f, (y + 1) / 32f, time, 4.8f, .5f);
                    positiveArea &= (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) > .000001f;
                }
            }
            Check(planted, "Sloth " + pose + " keeps the ground contact line fixed");
            Check(positiveArea, "Sloth " + pose + " never folds or inverts the artwork mesh");
        }
        int changingTicks = 0;
        var last = EnemyArtworkMotion.Sample(EnemyArtworkProfile.Sloth, EnemyArtworkPose.Idle, .55f, .75f, 0, 4.8f);
        for (int tick = 1; tick < 144; tick++)
        {
            var next = EnemyArtworkMotion.Sample(EnemyArtworkProfile.Sloth, EnemyArtworkPose.Idle, .55f, .75f, tick / 30f, 4.8f);
            if (Math.Abs(next.x - last.x) + Math.Abs(next.y - last.y) > .000001f) changingTicks++;
            last = next;
        }
        Check(changingTicks >= 140, "Sloth idle has continuous unique 30 FPS poses instead of repeating seven drawings");
        Console.WriteLine($"PASS: {checks} attack timing and monster motion checks");
    }
}
