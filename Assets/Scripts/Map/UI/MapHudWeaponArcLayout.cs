using System;
using UnityEngine;

// One geometric definition drives the textured stroke, frames and weapon slots.
public static class MapHudWeaponArcLayout
{
    public const int Capacity = 4;
    public const float CenterX = 176, CenterY = 128, Radius = 100;
    public const float PortraitRadius = 58;
    public const float AngleStep = 40, FrameRadius = 32, InnerRadius = 24;
    public const float EndPadding = 14, StrokeWidth = 7;
    public const float BrushRadius = Radius;

    public static Vector2 PortraitPosition => new Vector2(CenterX - PortraitRadius, CenterY - PortraitRadius);

    // The authoring rectangle includes empty space. Fit the rendered stroke
    // and the current frames rather than reserving that whole rectangle.
    public static Rect ContentBounds(int count)
    {
        count = Mathf.Clamp(count, 0, Capacity);
        float start = Angle(0, Capacity) - EndPadding;
        float end = Angle(Capacity - 1, Capacity) + EndPadding;
        int segments = (int)Math.Ceiling(end - start);
        Vector2 min = Point(start, BrushRadius), max = min;
        for (int i = 0; i <= segments; i++)
        {
            float angle = start + (end - start) * i / segments;
            float taper = Math.Min(1, Math.Min(angle - start, end - angle) / EndPadding);
            float halfWidth = StrokeWidth * .5f * (float)Math.Sqrt(Math.Max(0, taper));
            Include(Point(angle, BrushRadius - halfWidth));
            Include(Point(angle, BrushRadius + halfWidth));
        }
        for (int i = 0; i < count; i++)
        {
            var center = Position(i, count);
            Include(center - Vector2.one * FrameRadius);
            Include(center + Vector2.one * FrameRadius);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);

        void Include(Vector2 point) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
    }

    // Omit the brush under each filled frame. The complete supporting circle
    // and stroke endpoints stay fixed for every equipment count.
    public static bool StrokeCovered(float angle, int count)
    {
        double halfAngle = 2 * Math.Asin((FrameRadius - 1) / (2 * Radius)) * 180 / Math.PI;
        for (int slot = 0; slot < count; slot++)
            if (Math.Abs(angle - Angle(slot, count)) < halfAngle) return true;
        return false;
    }

    public static float Angle(int index, int count)
    {
        if (count < 1 || count > Capacity) throw new ArgumentOutOfRangeException(nameof(count));
        if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        return 180 - (count - 1) * AngleStep * .5f + index * AngleStep;
    }

    public static Vector2 Position(int index, int count) => Point(Angle(index, count), Radius);

    public static Vector2 Point(float degrees, float radius)
    {
        double radians = degrees * Math.PI / 180;
        return new Vector2(CenterX + (float)Math.Cos(radians) * radius,
            CenterY + (float)Math.Sin(radians) * radius);
    }
}
