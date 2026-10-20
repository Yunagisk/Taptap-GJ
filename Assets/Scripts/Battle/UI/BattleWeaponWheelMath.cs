using System;

public static class BattleWeaponWheelMath
{
    public static int CenteredIndex(float cursor, int count) => Wrap((int)Math.Round(cursor, MidpointRounding.AwayFromZero), count);
    public static int Wrap(int index, int count) => count <= 0 ? -1 : (index % count + count) % count;
    public static float NearestCursor(float from, int target, int count)
    {
        if (count <= 0) return 0;
        return target + (float)Math.Round((from - target) / count) * count;
    }
    public static void Pose(int index, float cursor, int count, out float x, out float y, out float depth)
    {
        double angle = count > 1 ? (index - cursor) * Math.PI * 2 / count : 0;
        x = (float)Math.Sin(angle) * 108;
        y = -(float)Math.Cos(angle) * 35;
        depth = ((float)Math.Cos(angle) + 1) * .5f;
    }
}
