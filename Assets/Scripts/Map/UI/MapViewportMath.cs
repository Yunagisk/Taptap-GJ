using UnityEngine;

public static class MapViewportMath
{
    public static float BaseScale(Vector2 viewport, Vector2 mapSize) =>
        Mathf.Max(Mathf.Min(viewport.x / 1920, viewport.y / 1080),
            Mathf.Max(viewport.x / mapSize.x, viewport.y / mapSize.y));

    public static float MinimumZoom(Vector2 viewport, Vector2 mapSize, float baseScale) =>
        Mathf.Min(1, Mathf.Max(viewport.x / mapSize.x, viewport.y / mapSize.y) / Mathf.Max(0.001f, baseScale));

    public static Vector2 ClampCenter(Vector2 center, Vector2 mapSize, Vector2 visibleSize)
    {
        Vector2 limit = Vector2.Max(Vector2.zero, (mapSize - visibleSize) * 0.5f);
        return new Vector2(Mathf.Clamp(center.x, -limit.x, limit.x), Mathf.Clamp(center.y, -limit.y, limit.y));
    }

    public static Vector2 Frame(Vector2 center, Rect focus, Vector2 visibleSize, Vector2 mapSize, Vector2 padding)
    {
        Vector2 half = Vector2.Max(Vector2.one, visibleSize * 0.5f - padding);
        if (focus.width > half.x * 2) center.x = focus.center.x;
        else if (focus.xMax > center.x + half.x) center.x = focus.xMax - half.x;
        else if (focus.xMin < center.x - half.x) center.x = focus.xMin + half.x;
        if (focus.height > half.y * 2) center.y = focus.center.y;
        else if (focus.yMax > center.y + half.y) center.y = focus.yMax - half.y;
        else if (focus.yMin < center.y - half.y) center.y = focus.yMin + half.y;
        return ClampCenter(center, mapSize, visibleSize);
    }

    public static Vector2 ZoomAt(Vector2 center, Vector2 pointer, float oldScale, float newScale) =>
        center + pointer / Mathf.Max(0.001f, oldScale) - pointer / Mathf.Max(0.001f, newScale);
}
