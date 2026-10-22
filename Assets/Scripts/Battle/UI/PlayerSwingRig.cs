using System;
using Point = PlayerThrustRig.Point;

// Single-hand downward sweep: lift, accelerate through contact, follow through,
// and recover. The character and feet remain in place; no weapon is baked in.
public static class PlayerSwingRig
{
    public const float Duration = .9f, HitTime = .36f, ReleaseTime = .23f;

    public static PlayerThrustRig.Pose Sample(float seconds, float duration = Duration,
        float hitSeconds = HitTime, float idleSeconds = 0f, bool blendIdle = true)
    {
        duration = Math.Max(.001f, duration);
        float hit = Math.Max(.0001f, Math.Min(duration - .0001f, hitSeconds));
        float t = seconds <= hit ? HitTime * seconds / hit
            : HitTime + (Duration - HitTime) * (seconds - hit) / (duration - hit);
        t = Clamp01(t / Duration) * Duration;
        var ready = PlayerThrustRig.Sample(0f);
        float readyUpper = Angle(ready.upperArm), readyFore = Angle(ready.forearm);
        float readyWrist = Angle(ready.hand) - readyFore;
        float upper, fore, wrist, lean, wind, drive;
        if (t < ReleaseTime)
        {
            wind = Ease(t / ReleaseTime); drive = 0f;
            upper = Lerp(readyUpper, -100f, wind);
            fore = Lerp(readyFore, -65f, wind);
            wrist = Lerp(readyWrist, 45f, wind);
            lean = -8f * wind;
        }
        else if (t < HitTime)
        {
            float u = (t - ReleaseTime) / (HitTime - ReleaseTime);
            // The torso leads, then the elbow drives the torch through contact.
            // Only ten degrees of additional wrist articulation are needed.
            drive = Ease(u * 1.15f); wind = 1f - drive;
            upper = Lerp(-100f, -72f, Ease(u));
            fore = Lerp(-65f, 55f, u * u);
            wrist = Lerp(45f, 55f, u * u);
            lean = Lerp(-8f, 18f, drive);
        }
        else if (t <= .41f)
        {
            wind = 0f; drive = 1f;
            upper = -72f; fore = 55f; wrist = 55f; lean = 18f;
        }
        else if (t < .48f)
        {
            float u = Ease((t - .41f) / .07f);
            wind = 0f; drive = 1f;
            upper = Lerp(-72f, -43f, u);
            fore = Lerp(55f, 80f, u);
            wrist = Lerp(55f, 50f, u); lean = Lerp(18f, 19f, u);
        }
        else
        {
            float u = Ease((t - .48f) / .30f);
            wind = 0f; drive = 1f - u;
            upper = Lerp(-43f, readyUpper, u);
            fore = Lerp(80f, readyFore, u);
            wrist = Lerp(50f, readyWrist, u);
            lean = 19f * drive;
        }
        Point hip = new Point(-28f * wind + 58f * drive, 34f * wind + 22f * drive);
        float knee = 32f * wind + 24f * drive;
        float cloth = Ease((t - .25f) / .16f) * (1f - Ease((t - .52f) / .38f));
        float capeX = -70f * cloth, capeY = -24f * cloth;
        if (blendIdle)
        {
            var idle = PlayerThrustRig.SampleIdle(idleSeconds);
            float mix = 1f - Ease(t / .1f) * (1f - Ease((t - .77f) / .13f));
            upper = Lerp(upper, Angle(idle.upperArm), mix);
            fore = Lerp(fore, Angle(idle.forearm), mix);
            wrist = Lerp(wrist, Angle(idle.hand) - Angle(idle.forearm), mix);
            lean = Lerp(lean, Angle(idle.torso), mix);
            hip = Lerp(hip, idle.hipDelta, mix);
            knee = Lerp(knee, idle.kneeBend, mix);
            capeX = Lerp(capeX, idle.capeX, mix); capeY = Lerp(capeY, idle.capeY, mix);
        }
        return PlayerThrustRig.FromArmRotations(lean, hip, upper, fore, wrist, knee, capeX, capeY);
    }

    static float Angle(PlayerThrustRig.Affine m) => (float)(Math.Atan2(m.c, m.a) * 180.0 / Math.PI);
    static float Clamp01(float t) => Math.Max(0f, Math.Min(1f, t));
    static float Ease(float t) { t = Clamp01(t); return t * t * t * (t * (t * 6f - 15f) + 10f); }
    static float Lerp(float a, float b, float t) => a + (b - a) * t;
    static Point Lerp(Point a, Point b, float t) => a + (b - a) * t;
}
