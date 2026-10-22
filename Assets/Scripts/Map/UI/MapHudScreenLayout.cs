using UnityEngine;

public static class MapHudScreenLayout
{
    public struct Pose
    {
        public Vector2 position;
        public float scale;
    }

    public static Rect InSpace(Rect bounds, Matrix4x4 matrix)
    {
        Vector2 min = matrix.MultiplyPoint3x4(new Vector3(bounds.xMin, bounds.yMin));
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 point = matrix.MultiplyPoint3x4(new Vector3(i == 1 ? bounds.xMin : bounds.xMax,
                i == 3 ? bounds.yMin : bounds.yMax));
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // Fit the complete visible HUD directly against the left and bottom edges.
    // Its transparent authoring rectangle may extend outside the screen.
    public static Pose Fit(Vector2 screen, Rect expandedBounds, float requestedScale, float edgeMargin,
        Vector2 offset = default)
    {
        float margin = Mathf.Min(Mathf.Max(0, edgeMargin), Mathf.Max(0, Mathf.Min(screen.x, screen.y) * 0.25f));
        float scale = Mathf.Max(0, Mathf.Min(requestedScale,
            (screen.x - 2 * margin) / Mathf.Max(0.001f, expandedBounds.width),
            (screen.y - 2 * margin) / Mathf.Max(0.001f, expandedBounds.height)));
        Vector2 minimum = Vector2.one * margin - expandedBounds.min * scale;
        Vector2 maximum = screen - Vector2.one * margin - expandedBounds.max * scale;
        return new Pose
        {
            position = Vector2.Max(minimum, Vector2.Min(maximum, minimum + offset)),
            scale = scale
        };
    }
}
