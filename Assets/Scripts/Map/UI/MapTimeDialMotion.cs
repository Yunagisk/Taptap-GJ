using System;

// Presentation only. The world's integer clock remains owned by WorldResourceRules.
public sealed class MapTimeDialMotion
{
    public const double Duration = 0.5;
    static readonly string[] Labels = { "早晨", "下午", "晚上" };
    int time;
    bool initialized;
    double from, target, startedAt;

    public int Time => time;
    public static int Period(int worldTime) => Math.Max(0, worldTime) % 3;
    public static string FormatLabel(int worldTime)
    {
        int time = Math.Max(0, worldTime);
        return "第" + (time / 3 + 1) + "日·" + Labels[Period(time)];
    }

    public static double Rotation(int worldTime)
    {
        switch (Period(worldTime)) { case 0: return -45; case 1: return 45; default: return 180; }
    }

    public void Snap(int worldTime, double now)
    {
        time = Math.Max(0, worldTime);
        from = target = Rotation(time);
        startedAt = now - Duration;
        initialized = true;
    }

    public bool SetTime(int worldTime, double now)
    {
        worldTime = Math.Max(0, worldTime);
        if (!initialized || worldTime < time) { Snap(worldTime, now); return true; }
        if (worldTime == time) return false;
        double current = Angle(now);
        double step = Wrap(Rotation(worldTime) - Rotation(time));
        // Spending a whole day still visibly advances the disc, in half a second.
        if (step < 0.000001) step = 360;
        // Keep the old target ahead when another time change interrupts the motion.
        target += step;
        from = current;
        time = worldTime;
        startedAt = now;
        return true;
    }

    public double Angle(double now)
    {
        double t = Math.Max(0, Math.Min(1, (now - startedAt) / Duration));
        // Smooth acceleration and deceleration, with an exact half-second endpoint.
        double eased = t * t * (3 - 2 * t);
        double value = from + (target - from) * eased;
        if (t >= 1) return from = target = Wrap(target);
        return value;
    }

    public bool IsMoving(double now) => initialized && now - startedAt < Duration;
    public static double Wrap(double degrees) => (degrees % 360 + 360) % 360;
}
