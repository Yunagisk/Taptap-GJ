using System;

static class Program
{
    static int count;
    static void Check(bool value, string label)
    { count++; if (!value) throw new InvalidOperationException(label); }
    static void Main()
    {
        const float duration = 1.2f, release = .4f, hit = 17f / 30;
        Check(PlayerPoleThrust.Advance(0, duration, release, hit) == 0, "starts in the existing ready pose");
        Check(Math.Abs(PlayerPoleThrust.Advance(release, duration, release, hit) + 70) < .001f, "distinct backward preparation");
        Check(Math.Abs(PlayerPoleThrust.Advance(hit, duration, release, hit) - 300) < .001f, "forward commitment peaks at impact");
        Check(PlayerPoleThrust.Advance(duration, duration, release, hit) == 0
            && PlayerPoleThrust.Advance(2, duration, release, hit) == 0, "recovery ends in the exact initial position");
        Check(PlayerPoleThrust.Weight(500, 1800) == 0, "rear foot stays planted");
        Check(Math.Abs(PlayerPoleThrust.Weight(1180, 1800) - .28f) < .001f, "front foot takes only a small supporting step");
        foreach (float x in new[] { 370f, 480f, 690f, 1030f, 1230f })
            Check(PlayerPoleThrust.Weight(x, 1340) == 1, "both hands and weapon receive identical forward movement");
        bool continuous = true, forward = true, recovery = true;
        float previous = PlayerPoleThrust.Advance(0, duration, release, hit);
        for (int tick = 1; tick <= 36; tick++)
        {
            float time = tick / 30f, value = PlayerPoleThrust.Advance(time, duration, release, hit);
            continuous &= Math.Abs(value - previous) < 112;
            if (time > release && time <= hit) forward &= value >= previous;
            if (time > hit) recovery &= value <= previous + .0001f;
            previous = value;
        }
        Check(continuous && forward && recovery, "one continuous strike followed by monotonic recovery");
        Check(PlayerPoleThrust.Advance(.501f, duration, release, hit) == PlayerPoleThrust.Advance(.52f, duration, release, hit),
            "weapon and body share the same 30 Hz pose samples");
        Console.WriteLine($"PASS: {count} long-pole movement checks");
    }
}
