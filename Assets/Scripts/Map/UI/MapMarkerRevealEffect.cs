using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MapMarkerRevealEffect : BaseMeshEffect
{
    [Min(1)] public float crossLength = 1;
    [Range(0.5f, 1)] public float crossWidth = 1;

    readonly List<UIVertex> source = new List<UIVertex>();
    readonly List<UIVertex> vertices = new List<UIVertex>();
    readonly List<int> triangles = new List<int>();
    float progress = 1;
    float crossOffsetY;
    int seed;

    public void SetCrossOffset(float value)
    {
        if (Mathf.Approximately(crossOffsetY, value)) return;
        crossOffsetY = value;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public void SetReveal(float phase, int noiseSeed)
    {
        progress = Mathf.Clamp01(phase);
        seed = noiseSeed;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive()) return;
        source.Clear();
        mesh.GetUIVertexStream(source);
        mesh.Clear();
        if (source.Count == 0 || progress <= 0) return;
        Vector2 uvMin = source[0].uv0, uvMax = uvMin;
        foreach (var vertex in source)
        {
            uvMin = Vector2.Min(uvMin, vertex.uv0);
            uvMax = Vector2.Max(uvMax, vertex.uv0);
        }
        MapMarkerRevealMesh.Build(graphic.rectTransform.rect, Rect.MinMaxRect(uvMin.x, uvMin.y, uvMax.x, uvMax.y),
            source[0].color, progress, crossLength, crossWidth, seed, vertices, triangles, crossOffsetY);
        foreach (var vertex in vertices) mesh.AddVert(vertex);
        for (int i = 0; i < triangles.Count; i += 3) mesh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }
}

public static class MapMarkerRevealMesh
{
    static readonly Rect[] ArcParts = {
        Rect.MinMaxRect(0, 0, 1, MapMarkerRevealMath.CrossBase),
        Rect.MinMaxRect(0, MapMarkerRevealMath.CrossBase, MapMarkerRevealMath.StemLeft, MapMarkerRevealMath.CrossShoulder),
        Rect.MinMaxRect(MapMarkerRevealMath.StemRight, MapMarkerRevealMath.CrossBase, 1, MapMarkerRevealMath.CrossShoulder)
    };
    static readonly Rect[] CrossParts = {
        Rect.MinMaxRect(0, MapMarkerRevealMath.CrossShoulder, 1, 1),
        Rect.MinMaxRect(MapMarkerRevealMath.StemLeft, MapMarkerRevealMath.CrossBase, MapMarkerRevealMath.StemRight, MapMarkerRevealMath.CrossShoulder)
    };

    public static void Build(Rect bounds, Rect uvRect, Color32 color, float phase, float crossLength, float crossWidth,
        int seed, List<UIVertex> vertices, List<int> triangles, float crossOffsetY = 0)
    {
        vertices.Clear();
        triangles.Clear();
        if (phase <= 0 || bounds.width <= 0 || bounds.height <= 0) return;
        // Once the ink settles, each texture region is a single quad again.
        int density = phase >= 0.94f ? 1
            : Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(bounds.width, bounds.height) / 2), 64, 112);
        float wet = MapRevealMath.WetEnvelope(phase);
        if (wet > 0)
        {
            // Only the arcs get a wet bleed and an expanding ink echo. Leave
            // the transparent center clear so the encounter icon stays readable.
            AddParts(ArcParts, false, 1.065f, wet * 0.34f, bounds, uvRect, color, phase,
                crossLength, crossWidth, seed, density, vertices, triangles);
            AddParts(ArcParts, false, Mathf.Lerp(1.02f, 1.24f, MapRevealMath.EaseOutCubic(phase)), wet * 0.24f,
                bounds, uvRect, color, phase, crossLength, crossWidth, seed, density, vertices, triangles);
        }
        AddParts(ArcParts, false, 1, 1, bounds, uvRect, color, phase, crossLength, crossWidth, seed, density, vertices, triangles);
        AddParts(CrossParts, true, 1, 1, bounds, uvRect, color, phase, crossLength, crossWidth, seed, density, vertices, triangles, crossOffsetY);
    }

    static void AddParts(Rect[] parts, bool cross, float expansion, float strength, Rect bounds, Rect uvRect,
        Color32 color, float phase, float length, float width, int seed, int density, List<UIVertex> vertices, List<int> triangles,
        float crossOffsetY = 0)
    {
        foreach (Rect part in parts)
        {
            int columns = Mathf.Max(1, Mathf.CeilToInt(part.width * density));
            int rows = Mathf.Max(1, Mathf.CeilToInt(part.height * density));
            int first = vertices.Count;
            for (int row = 0; row <= rows; row++)
                for (int column = 0; column <= columns; column++)
                {
                    Vector2 uv = new Vector2(Mathf.Lerp(part.xMin, part.xMax, column / (float)columns),
                        Mathf.Lerp(part.yMin, part.yMax, row / (float)rows));
                    var vertex = UIVertex.simpleVert;
                    vertex.position = MapMarkerRevealMath.Position(uv, bounds, cross, length, width, expansion, crossOffsetY);
                    vertex.uv0 = new Vector2(Mathf.Lerp(uvRect.xMin, uvRect.xMax, uv.x), Mathf.Lerp(uvRect.yMin, uvRect.yMax, uv.y));
                    float opacity = cross ? MapMarkerRevealMath.CrossOpacity(uv, phase) : MapMarkerRevealMath.ArcOpacity(uv, phase, seed);
                    var tint = color;
                    tint.a = (byte)Mathf.RoundToInt(color.a * opacity * strength);
                    vertex.color = tint;
                    vertices.Add(vertex);
                    if (row == 0 || column == 0) continue;
                    int v = first + row * (columns + 1) + column;
                    triangles.Add(v - columns - 2); triangles.Add(v - 1); triangles.Add(v);
                    triangles.Add(v - columns - 2); triangles.Add(v); triangles.Add(v - columns - 1);
                }
        }
    }
}
