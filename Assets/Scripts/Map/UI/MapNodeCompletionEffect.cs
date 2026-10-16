using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Keep the original sprite and UVs. Clip the UI mesh into two complementary
// pieces, then gently separate and lean both intact halves outward.
[DisallowMultipleComponent]
public class MapNodeCompletionEffect : BaseMeshEffect
{
    readonly List<UIVertex> source = new List<UIVertex>();
    readonly List<UIVertex> previous = new List<UIVertex>();
    readonly List<UIVertex> pieces = new List<UIVertex>();
    readonly List<int> sides = new List<int>();
    Rect previousBounds;
    float progress;

    public void SetCompletion(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(progress, value)) return;
        progress = value;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || progress <= 0) return;
        source.Clear();
        mesh.GetUIVertexStream(source);
        Rect bounds = graphic.rectTransform.rect;
        bool changed = bounds != previousBounds || source.Count != previous.Count;
        for (int i = 0; i < source.Count && !changed; i++)
            changed = !source[i].Equals(previous[i]);
        if (changed)
        {
            MapNodeCrackMesh.Split(source, bounds, pieces, sides);
            previous.Clear();
            previous.AddRange(source);
            previousBounds = bounds;
        }
        mesh.Clear();
        float opacity = MapNodeCrackMesh.Opacity(progress);
        for (int i = 0; i < pieces.Count; i++)
        {
            var vertex = pieces[i];
            vertex.position = MapNodeCrackMesh.TransformPosition(vertex.position, bounds, sides[i], progress);
            var color = vertex.color;
            // Faded monochrome ink reads as inactive grey on the map's paper.
            // This also fades the Outline, which runs before this mesh effect.
            color.a = (byte)Mathf.RoundToInt(color.a * opacity);
            vertex.color = color;
            mesh.AddVert(vertex);
        }
        for (int i = 0; i + 2 < pieces.Count; i += 3) mesh.AddTriangle(i, i + 1, i + 2);
    }
}

public static class MapNodeCrackMesh
{
    static readonly float[] Heights = { 0, 0.20f, 0.38f, 0.55f, 0.72f, 0.89f, 1 };
    static readonly float[] Offsets = { 0.005f, -0.020f, 0.028f, -0.025f, 0.022f, -0.010f, 0.005f };

    public static float Gap(Rect bounds, float progress) =>
        Mathf.Clamp(Mathf.Min(bounds.width, bounds.height) * 0.055f, 2, 5) * MapRevealMath.EaseOutCubic(progress);

    public static float TiltDegrees(float progress) => 5 * MapRevealMath.EaseOutCubic(progress);

    public static Vector3 TransformPosition(Vector3 position, Rect bounds, int side, float progress)
    {
        // A low pivot lets the tops lean apart without pushing the bottom halves
        // back across the crack. UI anchors and road endpoints remain fixed.
        Vector2 pivot = new Vector2(bounds.center.x, bounds.yMin);
        Vector2 local = (Vector2)position - pivot;
        float angle = -side * TiltDegrees(progress) * Mathf.Deg2Rad;
        float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
        return new Vector3(pivot.x + local.x * cos - local.y * sin + side * Gap(bounds, progress) * 0.5f,
            pivot.y + local.x * sin + local.y * cos, position.z);
    }

    public static float Opacity(float progress) =>
        Mathf.Lerp(1, 0.44f, MapRevealMath.Smooth((progress - 0.15f) / 0.85f));

    public static void Split(IList<UIVertex> source, Rect bounds, List<UIVertex> pieces, List<int> sides)
    {
        pieces.Clear();
        sides.Clear();
        if (source.Count < 3) return;
        float bottom = bounds.yMin, top = bounds.yMax;
        foreach (var vertex in source)
        {
            bottom = Mathf.Min(bottom, vertex.position.y);
            top = Mathf.Max(top, vertex.position.y);
        }
        var strip = new List<UIVertex>(8);
        var buffer = new List<UIVertex>(8);
        var half = new List<UIVertex>(8);
        for (int segment = 0; segment < Heights.Length - 1; segment++)
        {
            float y0 = segment == 0 ? bottom - 1 : bounds.yMin + bounds.height * Heights[segment];
            float y1 = segment == Heights.Length - 2 ? top + 1 : bounds.yMin + bounds.height * Heights[segment + 1];
            float x0 = bounds.center.x + bounds.width * Offsets[segment];
            float x1 = bounds.center.x + bounds.width * Offsets[segment + 1];
            // On this strip the crooked seam is a straight line x = slope*y + intercept.
            float slope = (x1 - x0) / Mathf.Max(0.001f, y1 - y0);
            float intercept = x0 - slope * y0;
            for (int triangle = 0; triangle + 2 < source.Count; triangle += 3)
            {
                strip.Clear();
                strip.Add(source[triangle]);
                strip.Add(source[triangle + 1]);
                strip.Add(source[triangle + 2]);
                Clip(strip, buffer, new Vector2(0, -1), -y0);
                Clip(buffer, strip, Vector2.up, y1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Clip(strip, half, new Vector2(-side, side * slope), -side * intercept);
                    for (int corner = 1; corner + 1 < half.Count; corner++)
                    {
                        Vector3 a = half[corner].position - half[0].position;
                        Vector3 b = half[corner + 1].position - half[0].position;
                        if (Mathf.Abs(a.x * b.y - a.y * b.x) < 0.00001f) continue;
                        pieces.Add(half[0]);
                        pieces.Add(half[corner]);
                        pieces.Add(half[corner + 1]);
                        sides.Add(side);
                        sides.Add(side);
                        sides.Add(side);
                    }
                }
            }
        }
    }

    // Sutherland-Hodgman clipping against normal . position <= distance.
    static void Clip(List<UIVertex> input, List<UIVertex> output, Vector2 normal, float distance)
    {
        output.Clear();
        if (input.Count == 0) return;
        UIVertex previous = input[input.Count - 1];
        float previousDistance = Vector2.Dot(normal, previous.position) - distance;
        foreach (var current in input)
        {
            float currentDistance = Vector2.Dot(normal, current.position) - distance;
            if ((currentDistance <= 0) != (previousDistance <= 0))
                output.Add(Interpolate(previous, current, previousDistance / (previousDistance - currentDistance)));
            if (currentDistance <= 0) output.Add(current);
            previous = current;
            previousDistance = currentDistance;
        }
    }

    static UIVertex Interpolate(UIVertex a, UIVertex b, float t)
    {
        var vertex = a;
        vertex.position = Vector3.LerpUnclamped(a.position, b.position, t);
        vertex.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
        vertex.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t);
        vertex.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t);
        vertex.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t);
        vertex.normal = Vector3.LerpUnclamped(a.normal, b.normal, t);
        vertex.tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t);
        vertex.color = Color.LerpUnclamped(a.color, b.color, t);
        return vertex;
    }
}
