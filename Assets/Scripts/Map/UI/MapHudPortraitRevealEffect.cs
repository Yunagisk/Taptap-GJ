using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Reveal the open portrait over the hooded portrait. The shared outer ring is
// always drawn by the base Image, so its ink outline cannot flicker or move.
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class MapHudPortraitRevealEffect : BaseMeshEffect
{
    [Range(0.35f, 0.48f)] public float contentRadius = 0.445f;
    [Range(24, 96)] public int meshResolution = 64;
    public int noiseSeed = 3109;

    readonly List<UIVertex> source = new List<UIVertex>();
    float progress;

    public void SetReveal(float value)
    {
        value = Mathf.Clamp01(value);
        if (progress == value) return;
        progress = value;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive()) return;
        if (progress <= 0) { mesh.Clear(); return; }
        source.Clear();
        mesh.GetUIVertexStream(source);
        mesh.Clear();
        if (source.Count == 0) return;
        Vector2 uvMin = source[0].uv0, uvMax = uvMin;
        Vector2 min = source[0].position, max = min;
        foreach (var vertex in source)
        {
            uvMin = Vector2.Min(uvMin, vertex.uv0);
            uvMax = Vector2.Max(uvMax, vertex.uv0);
            min = Vector2.Min(min, vertex.position);
            max = Vector2.Max(max, vertex.position);
        }
        int grid = Mathf.Clamp(meshResolution, 24, 96);
        for (int row = 0; row <= grid; row++)
            for (int column = 0; column <= grid; column++)
            {
                Vector2 uv = new Vector2(column / (float)grid, row / (float)grid);
                var vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(Mathf.Lerp(min.x, max.x, uv.x), Mathf.Lerp(min.y, max.y, uv.y));
                vertex.uv0 = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, uv.x), Mathf.Lerp(uvMin.y, uvMax.y, uv.y));
                var color = source[0].color;
                color.a = (byte)Mathf.RoundToInt(color.a * MapHudPortraitBlendMath.Opacity(uv, progress, contentRadius, noiseSeed));
                vertex.color = color;
                mesh.AddVert(vertex);
                if (row == 0 || column == 0) continue;
                int v = row * (grid + 1) + column;
                mesh.AddTriangle(v - grid - 2, v - 1, v);
                mesh.AddTriangle(v - grid - 2, v, v - grid - 1);
            }
    }
}

public static class MapHudPortraitBlendMath
{
    public static float Opacity(Vector2 uv, float progress, float contentRadius = 0.445f, int seed = 3109)
    {
        if (progress <= 0) return 0;
        // Feather inside the disk; the external ring and transparent corners
        // always retain the hooded source's exact pixels.
        float circle = 1 - MapRevealMath.Smooth((Vector2.Distance(uv, Vector2.one * 0.5f)
            - contentRadius + 0.012f) / 0.012f);
        if (circle <= 0) return 0;
        if (progress >= 1) return circle;
        // Reveal the face-facing edge first, then wash back across the hood.
        // Normalized sampling makes the ink grain stable under canvas scaling.
        float ink = MapRevealMath.WaterOpacity(uv * 116, new Rect(0, 0, 116, 116),
            new Vector2(-1, 0.16f), progress, seed);
        return circle * ink;
    }
}
