using UnityEngine;

public static class MapMarkerRevealMath
{
    // The exported PNG contains a cross above four separate circular arcs.
    // These boundaries pass through transparent pixels, so the source artwork
    // can be animated in parts without cutting or repainting the texture.
    public const float CrossBase = 0.65f;
    public const float CrossShoulder = 0.835f;
    public const float StemLeft = 0.46f;
    public const float StemRight = 0.55f;

    public static bool IsCross(Vector2 uv) => uv.y >= CrossShoulder
        || uv.y >= CrossBase && uv.x >= StemLeft && uv.x <= StemRight;

    public static Vector2 Position(Vector2 uv, Rect bounds, bool cross, float length, float width, float radialScale = 1,
        float crossOffsetY = 0)
    {
        if (!cross) return bounds.center + Vector2.Scale(uv - Vector2.one * 0.5f, bounds.size) * radialScale;
        // A village stretches the ring into an ellipse. Give the cross its own
        // scale so its horizontal stroke does not widen with the village.
        float basis = Mathf.Min(bounds.width, bounds.height);
        return bounds.center + new Vector2((uv.x - 0.5f) * basis * width,
            (CrossBase - 0.5f) * bounds.height + (uv.y - CrossBase) * basis * length + crossOffsetY);
    }

    public static float CrossOpacity(Vector2 uv, float phase)
    {
        if (phase <= 0) return 0;
        if (phase >= 0.94f) return 1;
        float stroke = MapRevealMath.EaseOutCubic(phase / 0.94f);
        float stem = 1 - MapRevealMath.Smooth((Mathf.Abs(uv.x - 0.5f) - 0.023f) / 0.012f);
        float bar = 1 - MapRevealMath.Smooth((Mathf.Abs(uv.y - 0.872f) - 0.025f) / 0.025f);
        // Write downwards and left to right on the same clock. A shared feather
        // and ink-settling envelope let both strokes finish together.
        float down = Wipe((0.977f - uv.y) / (0.977f - 0.665f), stroke, 0.06f);
        float across = Wipe((uv.x - 0.414f) / (0.590f - 0.414f), stroke, 0.06f);
        float settled = Mathf.Lerp(0.86f, 1, MapRevealMath.Smooth(phase / 0.94f));
        return Mathf.Max(stem * down, bar * across) * settled;
    }

    public static float ArcOpacity(Vector2 uv, float phase, int seed)
    {
        if (phase <= 0) return 0;
        if (phase >= 0.94f) return 1;
        Vector2 point = uv - Vector2.one * 0.5f;
        float quarter = Mathf.Repeat(Mathf.Atan2(point.y, point.x), Mathf.PI * 0.5f);
        float along = Mathf.Abs(quarter - Mathf.PI * 0.25f) / (Mathf.PI * 0.25f);
        float warp = (MapRoadGeometry.Noise(uv.x * 37, seed) * 0.6f
            + MapRoadGeometry.Noise(uv.y * 43, seed + 719) * 0.4f - 0.5f) * 0.18f;
        // All four arcs bloom from their middles at once, with uneven, feathered
        // edges that spread faster at first and settle into the original ink.
        float front = MapRevealMath.EaseOutCubic(phase / 0.94f) * 1.24f - 0.12f;
        return MapRevealMath.Smooth((front - along - warp) / 0.20f + 0.5f);
    }

    static float Wipe(float coordinate, float progress, float feather)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;
        return MapRevealMath.Smooth((progress * (1 + 2 * feather) - feather - coordinate) / feather + 0.5f);
    }
}
