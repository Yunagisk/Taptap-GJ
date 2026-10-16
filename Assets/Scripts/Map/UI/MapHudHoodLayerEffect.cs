using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class MapHudHoodLayerEffect : BaseMeshEffect
{
    public enum Layer { Disk, Head, BackCloth, FrontCloth }
    public Layer layer;
    [Range(24, 80)] public int meshResolution = 64;
    [Range(.35f, .48f)] public float contentRadius = .445f;
    readonly List<UIVertex> source = new List<UIVertex>();
    readonly Vector2[] cage = new Vector2[MapHudHoodMotion.CageSize];
    readonly Vector3[] headClipPlanes = new Vector3[MapHudHoodMotion.HeadPlaneCount];
    float[] weights;
    float progress = -1;
    int cachedGrid;

    public void SetPose(float value)
    {
        value = Mathf.Clamp01(value);
        if (value == progress) return;
        CachePose(value);
        if (graphic != null) graphic.SetVerticesDirty();
    }

    // Also used by the first mesh build. Only update local pose data here:
    // requesting another rebuild from ModifyMesh is rejected by Unity's UI.
    void CachePose(float value)
    {
        progress = value;
        for (int i = 0; i < cage.Length; i++) cage[i] = MapHudHoodMotion.CagePoint(value, i);
        MapHudHoodMotion.WriteHeadClipPlanes(value, headClipPlanes);
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive()) return;
        if (progress < 0) CachePose(0);
        float opacity = layer == Layer.FrontCloth ? MapHudHoodMotion.FrontOpacity(progress) : 1;
        if (opacity <= 0) { mesh.Clear(); return; }
        source.Clear();
        mesh.GetUIVertexStream(source);
        mesh.Clear();
        if (source.Count == 0) return;
        Vector2 uvMin = source[0].uv0, uvMax = uvMin;
        Vector2 min = source[0].position, max = min;
        foreach (var vertex in source)
        {
            uvMin = Vector2.Min(uvMin, vertex.uv0); uvMax = Vector2.Max(uvMax, vertex.uv0);
            min = Vector2.Min(min, vertex.position); max = Vector2.Max(max, vertex.position);
        }
        int grid = Mathf.Clamp(meshResolution, 24, 80);
        bool cloth = layer == Layer.BackCloth || layer == Layer.FrontCloth;
        if (cloth && cachedGrid != grid)
        {
            weights = new float[(grid + 1) * (grid + 1) * MapHudHoodMotion.CageSize];
            for (int row = 0; row <= grid; row++)
                for (int column = 0; column <= grid; column++)
                    MapHudHoodMotion.WriteWeights(new Vector2(column / (float)grid, row / (float)grid),
                        weights, (row * (grid + 1) + column) * MapHudHoodMotion.CageSize);
            cachedGrid = grid;
        }
        for (int row = 0; row <= grid; row++)
            for (int column = 0; column <= grid; column++)
            {
                int index = row * (grid + 1) + column;
                Vector2 uv = new Vector2(column / (float)grid, row / (float)grid);
                Vector2 position = cloth ? MapHudHoodMotion.Deform(weights, index * MapHudHoodMotion.CageSize, cage) : uv;
                if (cloth) position = MapHudHoodMotion.FitCloth(uv, position, progress);
                var vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(Mathf.LerpUnclamped(min.x, max.x, position.x), Mathf.LerpUnclamped(min.y, max.y, position.y));
                Vector2 sample = layer == Layer.Disk ? new Vector2(.5f, .5f) : uv;
                vertex.uv0 = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, sample.x), Mathf.Lerp(uvMin.y, uvMax.y, sample.y));
                Color32 color = layer == Layer.Disk ? new Color32(0, 0, 0, source[0].color.a) : source[0].color;
                float headMask = layer == Layer.Head ? MapHudHoodMotion.HeadOpacity(uv, headClipPlanes) : 1;
                float clothMask = layer == Layer.FrontCloth ? MapHudHoodMotion.FrontOpacity(uv, progress) : 1;
                color.a = (byte)Mathf.RoundToInt(color.a * opacity * headMask * clothMask * MapHudHoodMotion.DiskOpacity(position, contentRadius));
                vertex.color = color;
                mesh.AddVert(vertex);
                if (row == 0 || column == 0) continue;
                int v = index;
                mesh.AddTriangle(v - grid - 2, v - 1, v);
                mesh.AddTriangle(v - grid - 2, v, v - grid - 1);
            }
    }
}
