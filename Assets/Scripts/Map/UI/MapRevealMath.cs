using UnityEngine;

public static class MapRevealMath
{
    public static float CompletionProgress(float elapsed, float duration, float completionPortion) =>
        Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration * completionPortion));

    public static float RoadProgress(float elapsed, float duration, float roadPortion, float startPortion = 0) =>
        EaseOutCubic(RoadPhase(elapsed, duration, roadPortion, startPortion));

    public static float RoadPhase(float elapsed, float duration, float roadPortion, float startPortion = 0) =>
        Mathf.Clamp01((elapsed - duration * startPortion)
            / Mathf.Max(0.001f, duration * (roadPortion - startPortion)));

    public static float NodeProgress(float elapsed, float duration, float roadPortion) =>
        EaseOutCubic(NodePhase(elapsed, duration, roadPortion));

    public static float NodePhase(float elapsed, float duration, float roadPortion) =>
        Mathf.Clamp01((elapsed - duration * roadPortion) / Mathf.Max(0.001f, duration * (1 - roadPortion)));

    public static float WetEnvelope(float phase) =>
        Smooth(phase / 0.08f) * (1 - Smooth((phase - 0.70f) / 0.30f));

    // Fast initial flow with a gentle, stationary finish. Clamp the clock here
    // so content stays fully hidden before its turn and fully visible afterward.
    public static float EaseOutCubic(float value)
    {
        float remaining = 1 - Mathf.Clamp01(value);
        return 1 - remaining * remaining * remaining;
    }

    public static float RoadOpacity(float distance, float length, float progress)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;
        float feather = Mathf.Max(0.1f, Mathf.Min(12, length * 0.2f));
        return Smooth((progress * (length + feather) - distance) / feather);
    }

    public static float WaterOpacity(Vector2 position, Rect bounds, Vector2 direction, float progress, int seed)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();
        float center = Vector2.Dot(bounds.center, direction);
        float radius = (Mathf.Abs(direction.x) * bounds.width + Mathf.Abs(direction.y) * bounds.height) * 0.5f;
        float coordinate = (Vector2.Dot(position, direction) - center + radius) / Mathf.Max(0.001f, 2 * radius);
        float warp = (MapRoadGeometry.Noise(position.x / 5, seed) * 0.65f
            + MapRoadGeometry.Noise(position.y / 7, seed + 431) * 0.35f - 0.5f) * 0.10f;
        float front = progress * 1.32f - 0.16f;
        return Smooth((front - coordinate - warp) / 0.12f + 0.5f);
    }

    public static float Smooth(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3 - 2 * t);
    }
}
