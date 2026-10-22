using System;
using System.Collections.Generic;
using UnityEngine;

// Geometry only: route appearance never changes the map's adjacency or progression.
public static class MapRoadGeometry
{
    public static int Seed(string a, string b)
    {
        if (string.CompareOrdinal(a, b) > 0) { string swap = a; a = b; b = swap; }
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in a ?? "") hash = (hash ^ c) * 16777619;
            hash = (hash ^ '|') * 16777619;
            foreach (char c in b ?? "") hash = (hash ^ c) * 16777619;
            return (int)hash;
        }
    }

    public static Rect Expand(Rect rect, float padding) => Rect.MinMaxRect(
        rect.xMin - padding, rect.yMin - padding, rect.xMax + padding, rect.yMax + padding);

    // Smooth, deterministic noise, with no dependency on Unity's global random state.
    public static float Noise(float position, int seed)
    {
        int cell = (int)Math.Floor(position);
        float t = position - cell;
        t = t * t * (3 - 2 * t);
        return Mathf.Lerp(Hash(cell, seed), Hash(cell + 1, seed), t);
    }

    static float Hash(int value, int seed)
    {
        unchecked
        {
            uint hash = (uint)(value ^ seed);
            hash = (hash ^ (hash >> 16)) * 0x7feb352d;
            hash = (hash ^ (hash >> 15)) * 0x846ca68b;
            return (hash ^ (hash >> 16)) / (float)uint.MaxValue;
        }
    }

    public static bool Build(Rect from, Rect to, Rect? village, bool fromVillage, bool toVillage,
        int seed, float bendAmount, Vector2 controlOffsets, List<Vector2> points)
    {
        points.Clear();
        Vector2 start = from.center, end = to.center;
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length < 1) return false;
        float bend = Mathf.Clamp(length * 0.19f, 12, 46) * Mathf.Max(0, bendAmount);
        float sign = (seed & 1) == 0 ? 1 : -1;
        bool doubleBend = (seed & 2) != 0;

        // Prefer a subtle C or S. If it crosses the village, try the other side,
        // then progressively wider single bends. Validate the sampled segments too.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float direction = (attempt & 1) == 0 ? sign : -sign;
            float amplitude = bend * (attempt < 2 ? 1 : 1.3f * (float)Math.Pow(1.55, attempt / 2 - 1));
            float first = direction * amplitude + controlOffsets.x;
            float second = direction * amplitude * (attempt < 2 && doubleBend ? -0.8f : 0.75f) + controlOffsets.y;
            if (TryCurve(from, to, village, fromVillage, toVillage, first, second, points)) return true;
        }
        // Overlapping nodes may make a route impossible. Do not draw through the village.
        points.Clear();
        return false;
    }

    public static bool TryCurve(Rect from, Rect to, Rect? village, bool fromVillage, bool toVillage,
        float first, float second, List<Vector2> points)
    {
        points.Clear();
        Vector2 start = from.center, end = to.center, delta = end - start;
        float length = delta.magnitude;
        if (length < 1) return false;
        Vector2 normal = new Vector2(-delta.y, delta.x) / length;
        Vector2 c1 = start + delta * 0.31f + normal * first;
        Vector2 c2 = start + delta * 0.71f + normal * second;
        int steps = Mathf.Clamp(Mathf.CeilToInt((length + Mathf.Abs(first) + Mathf.Abs(second)) / 2.5f), 24, 640);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1 - t;
            points.Add(u * u * u * start + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * end);
        }
        Trim(points, fromVillage && village.HasValue ? village.Value : Expand(from, 2),
            toVillage && village.HasValue ? village.Value : Expand(to, 2), fromVillage, toVillage);
        return points.Count >= 2 && (!village.HasValue || IsClear(points, village.Value));
    }

    public static float SegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        Vector2 ab = b - a, cd = d - c;
        float ac = Cross(ab, c - a), ad = Cross(ab, d - a);
        float ca = Cross(cd, a - c), cb = Cross(cd, b - c);
        if (ac * ad <= 0 && ca * cb <= 0
            && Mathf.Max(a.x, b.x) >= Mathf.Min(c.x, d.x) && Mathf.Max(c.x, d.x) >= Mathf.Min(a.x, b.x)
            && Mathf.Max(a.y, b.y) >= Mathf.Min(c.y, d.y) && Mathf.Max(c.y, d.y) >= Mathf.Min(a.y, b.y)) return 0;
        return Mathf.Min(Mathf.Min(PointDistanceSquared(a, c, d), PointDistanceSquared(b, c, d)),
            Mathf.Min(PointDistanceSquared(c, a, b), PointDistanceSquared(d, a, b)));
    }

    public static bool IsClearOfRectangle(IList<Vector2> points, Rect rect)
    {
        Vector2 lowerLeft = new Vector2(rect.xMin, rect.yMin), upperRight = new Vector2(rect.xMax, rect.yMax);
        Vector2 upperLeft = new Vector2(rect.xMin, rect.yMax), lowerRight = new Vector2(rect.xMax, rect.yMin);
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = points[i - 1], b = points[i];
            if (rect.Contains(a) || rect.Contains(b)
                || SegmentDistanceSquared(a, b, lowerLeft, upperLeft) < 0.000001f
                || SegmentDistanceSquared(a, b, upperLeft, upperRight) < 0.000001f
                || SegmentDistanceSquared(a, b, upperRight, lowerRight) < 0.000001f
                || SegmentDistanceSquared(a, b, lowerRight, lowerLeft) < 0.000001f) return false;
        }
        return true;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static float PointDistanceSquared(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        float t = delta.sqrMagnitude < 0.000001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude);
        return (p - a - t * delta).sqrMagnitude;
    }

    static void Trim(List<Vector2> points, Rect from, Rect to, bool fromEllipse, bool toEllipse)
    {
        int first = 0, last = points.Count - 1;
        while (first < points.Count && Contains(from, points[first], fromEllipse)) first++;
        while (last >= first && Contains(to, points[last], toEllipse)) last--;
        if (first > last) { points.Clear(); return; }
        Vector2 start = first > 0 ? Boundary(points[first - 1], points[first], from, fromEllipse) : points[first];
        Vector2 end = last < points.Count - 1 ? Boundary(points[last + 1], points[last], to, toEllipse) : points[last];
        if (last < points.Count - 1) points.RemoveRange(last + 1, points.Count - last - 1);
        if (first > 0) points.RemoveRange(0, first);
        points.Insert(0, start);
        points.Add(end);
    }

    static bool Contains(Rect rect, Vector2 point, bool ellipse)
    {
        if (!ellipse) return rect.Contains(point);
        Vector2 q = Normalize(point, rect);
        return q.sqrMagnitude < 1;
    }

    static Vector2 Normalize(Vector2 point, Rect ellipse) => new Vector2(
        (point.x - ellipse.center.x) / Mathf.Max(0.01f, ellipse.width * 0.5f),
        (point.y - ellipse.center.y) / Mathf.Max(0.01f, ellipse.height * 0.5f));

    static Vector2 Boundary(Vector2 inside, Vector2 outside, Rect rect, bool ellipse)
    {
        Vector2 delta = outside - inside;
        if (ellipse)
        {
            Vector2 a = Normalize(inside, rect), d = Normalize(outside, rect) - a;
            float aa = d.sqrMagnitude, bb = 2 * Vector2.Dot(a, d), cc = a.sqrMagnitude - 1;
            float exit = aa < 0.000001f ? 0 : (-bb + Mathf.Sqrt(Mathf.Max(0, bb * bb - 4 * aa * cc))) / (2 * aa);
            return inside + delta * Mathf.Clamp01(exit);
        }
        float t = 1;
        if (delta.x > 0) t = Mathf.Min(t, (rect.xMax - inside.x) / delta.x);
        if (delta.x < 0) t = Mathf.Min(t, (rect.xMin - inside.x) / delta.x);
        if (delta.y > 0) t = Mathf.Min(t, (rect.yMax - inside.y) / delta.y);
        if (delta.y < 0) t = Mathf.Min(t, (rect.yMin - inside.y) / delta.y);
        return inside + delta * Mathf.Clamp01(t);
    }

    public static bool IsClear(IList<Vector2> points, Rect obstacle)
    {
        // A route attached to the village may touch the padded boundary, not its interior.
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = Normalize(points[i - 1], obstacle), b = Normalize(points[i], obstacle);
            Vector2 delta = b - a;
            float t = delta.sqrMagnitude < 0.000001f ? 0 : Mathf.Clamp01(-Vector2.Dot(a, delta) / delta.sqrMagnitude);
            if ((a + delta * t).sqrMagnitude < 0.9999f) return false;
        }
        return true;
    }
}
