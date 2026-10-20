using System;

// A quadratic bow above the straight chord, in HUD-local coordinates.
public static class BattleTargetingCurve
{
    public static void Evaluate(float startX, float startY, float endX, float endY,
        float t, out float x, out float y)
    {
        t = Math.Max(0, Math.Min(1, t));
        float dx = endX - startX, dy = endY - startY;
        float distance = (float)Math.Sqrt(dx * dx + dy * dy);
        float lift = distance < .001f ? 0 : Math.Min(220, Math.Max(45, distance * .28f));
        x = startX + dx * t;
        y = startY + dy * t + 2 * lift * t * (1 - t);
    }
}
