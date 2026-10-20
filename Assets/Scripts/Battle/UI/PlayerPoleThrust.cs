using System;

// Additional forward commitment, in the authored 2K drawing's pixel coordinates.
public static class PlayerPoleThrust
{
    public static float Advance(float seconds, float duration, float release, float hit)
    {
        float t = (float)Math.Floor(Math.Max(0, seconds) * 30f + .0001f) / 30f;
        if (seconds >= duration) return 0;
        if (t <= release) return -70f * Smooth(0, release, t);
        if (t <= hit) return -70f + 370f * Smooth(release, hit, t);
        float hold = hit + (duration - hit) * .16f;
        return 300f * (1f - Smooth(hold, duration, t));
    }

    public static float Weight(float x, float y)
    {
        float upper = 1f - Smooth(1450, 1780, y);
        float frontStep = Smooth(800, 1120, x) * Smooth(1460, 1720, y) * .28f;
        return upper + frontStep * (1f - upper);
    }
    static float Smooth(float a, float b, float t)
    { t = Math.Max(0, Math.Min(1, (t - a) / (b - a))); return t * t * (3 - 2 * t); }
}
