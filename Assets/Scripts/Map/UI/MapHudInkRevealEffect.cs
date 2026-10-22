using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MapHudInkRevealEffect : BaseMeshEffect
{
    readonly List<UIVertex> source = new List<UIVertex>();
    readonly List<UIVertex> previous = new List<UIVertex>();
    readonly List<UIVertex> samples = new List<UIVertex>();
    readonly List<int> sampleTriangles = new List<int>();
    readonly List<UIVertex> vertices = new List<UIVertex>();
    readonly List<int> triangles = new List<int>();
    MapHudInkRevealGroup owner;

    public void Initialize(MapHudInkRevealGroup group) { owner = group; Invalidate(); }
    public void Invalidate() { if (graphic != null) graphic.SetVerticesDirty(); }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || owner == null || !owner.isActiveAndEnabled || owner.State.progress >= 1) return;
        if (owner.State.progress <= 0) { mesh.Clear(); return; }
        source.Clear();
        mesh.GetUIVertexStream(source);
        bool changed = source.Count != previous.Count;
        for (int i = 0; i < source.Count && !changed; i++)
            changed = source[i].position != previous[i].position || source[i].uv0 != previous[i].uv0
                || !source[i].color.Equals(previous[i].color);
        if (changed)
        {
            MapRevealMesh.Subdivide(source, samples, sampleTriangles);
            previous.Clear();
            previous.AddRange(source);
        }
        MapHudInkRevealMesh.Apply(samples, sampleTriangles, graphic.rectTransform.rect,
            MapUITransform.RelativeMatrix(transform, owner.transform), owner.State, vertices, triangles);
        mesh.Clear();
        foreach (var vertex in vertices) mesh.AddVert(vertex);
        for (int i = 0; i < triangles.Count; i += 3) mesh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }
}

public static class MapHudInkRevealMesh
{
    public static void Apply(IList<UIVertex> samples, IList<int> indices, Rect localBounds, Matrix4x4 toWing,
        MapHudInkRevealState state, List<UIVertex> vertices, List<int> triangles)
    {
        vertices.Clear();
        triangles.Clear();
        if (state.progress <= 0) return;
        float wet = MapRevealMath.WetEnvelope(state.progress);
        // Expand the existing texture slightly at the wet front. The bleed is
        // confined to the artwork, so it never paints a rectangle over the map.
        if (wet > 0 && samples.Count * 2 < 65000) AddPass(true);
        AddPass(false);

        void AddPass(bool bleed)
        {
            int first = vertices.Count;
            foreach (var original in samples)
            {
                var vertex = original;
                float opacity = state.Opacity(toWing.MultiplyPoint3x4(vertex.position));
                if (bleed)
                {
                    float edge = 4 * opacity * (1 - opacity);
                    opacity = edge * wet * 0.48f;
                    Vector2 relative = (Vector2)vertex.position - localBounds.center;
                    vertex.position += new Vector3(relative.x / Mathf.Max(0.001f, localBounds.width) * 3.4f,
                        relative.y / Mathf.Max(0.001f, localBounds.height) * 3.4f);
                }
                var color = vertex.color;
                color.a = (byte)Mathf.RoundToInt(color.a * opacity);
                vertex.color = color;
                vertices.Add(vertex);
            }
            foreach (int index in indices) triangles.Add(first + index);
        }
    }
}
