using System;

// Input state is separate from equipment. Only Release returns a commit choice.
public sealed class BattleWeaponWheelGesture
{
    public bool Dragging { get; private set; }
    public float Cursor { get; private set; }
    float origin, start;
    public void Begin(float x, int selected, int count)
    {
        Dragging = count > 0; origin = x; start = selected; Cursor = selected;
    }
    public void Drag(float x, float step)
    {
        if (Dragging) Cursor = start - (x - origin) / Math.Max(1, step);
    }
    public int Release(bool canSwitch, int count)
    {
        if (!Dragging) return -1;
        Dragging = false;
        return canSwitch && count > 0 ? BattleWeaponWheelMath.CenteredIndex(Cursor, count) : -1;
    }
    public void Cancel() { Dragging = false; }
}

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
