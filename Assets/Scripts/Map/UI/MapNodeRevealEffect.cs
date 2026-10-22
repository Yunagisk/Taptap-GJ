using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Applied after Outline. Subdivision lets the normal UI material display a soft,
// irregular directional front, including on the outline, without new textures.
[DisallowMultipleComponent]
public class MapNodeRevealEffect : BaseMeshEffect
{
    readonly List<UIVertex> source = new List<UIVertex>();
    readonly List<UIVertex> previous = new List<UIVertex>();
    readonly List<UIVertex> samples = new List<UIVertex>();
    readonly List<int> triangles = new List<int>();
    float progress = 1;
    Vector2 direction = Vector2.right;
    int seed;
    MapInkRevealGraphic inkHalo;
    bool inkStarted;

    public void SetReveal(float value, Vector2 incomingDirection, int noiseSeed)
    {
        progress = Mathf.Clamp01(value);
        direction = incomingDirection;
        seed = noiseSeed;
        if (progress <= 0)
        {
            inkStarted = false;
            if (inkHalo != null) inkHalo.Stop();
        }
        else if (progress < 1 && !inkStarted)
        {
            if (inkHalo == null)
            {
                var halo = new GameObject(name + " InkReveal", typeof(RectTransform), typeof(CanvasRenderer), typeof(MapInkRevealGraphic));
                halo.layer = gameObject.layer;
                inkHalo = halo.GetComponent<MapInkRevealGraphic>();
            }
            inkHalo.Play(graphic.rectTransform, direction, seed);
            inkStarted = true;
        }
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public void CompleteReveal()
    {
        // Restore the static badge while its independently timed ripples settle.
        progress = 1;
        enabled = false;
    }

    protected override void OnDisable()
    {
        if (inkHalo != null && (progress < 1 || !gameObject.activeInHierarchy)) inkHalo.Stop();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        if (inkHalo != null) Destroy(inkHalo.gameObject);
        base.OnDestroy();
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || progress >= 1) return;
        if (progress <= 0) { mesh.Clear(); return; }
        source.Clear();
        mesh.GetUIVertexStream(source);
        bool changed = source.Count != previous.Count;
        for (int i = 0; i < source.Count && !changed; i++)
            changed = source[i].position != previous[i].position || source[i].uv0 != previous[i].uv0
                || !source[i].color.Equals(previous[i].color);
        if (changed)
        {
            MapRevealMesh.Subdivide(source, samples, triangles);
            previous.Clear();
            previous.AddRange(source);
        }
        mesh.Clear();
        Rect bounds = MapRoadGeometry.Expand(graphic.rectTransform.rect, 3);
        foreach (var original in samples)
        {
            UIVertex vertex = original;
            float opacity = MapRevealMath.WaterOpacity(vertex.position, bounds, direction, progress, seed);
            var color = vertex.color;
            color.a = (byte)Mathf.RoundToInt(color.a * opacity);
            vertex.color = color;
            mesh.AddVert(vertex);
        }
        for (int i = 0; i < triangles.Count; i += 3) mesh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }
}

public static class MapRevealMesh
{
    public static void Subdivide(IList<UIVertex> source, List<UIVertex> vertices, List<int> triangles)
    {
        vertices.Clear();
        triangles.Clear();
        for (int triangle = 0; triangle + 2 < source.Count; triangle += 3)
        {
            UIVertex a = source[triangle], b = source[triangle + 1], c = source[triangle + 2];
            float longest = Mathf.Max(Vector3.Distance(a.position, b.position),
                Mathf.Max(Vector3.Distance(b.position, c.position), Vector3.Distance(c.position, a.position)));
            int steps = Mathf.Clamp(Mathf.CeilToInt(longest / 4), 1, 32);
            int first = vertices.Count;
            for (int row = 0; row <= steps; row++)
                for (int col = 0; col <= steps - row; col++)
                {
                    float wb = row / (float)steps, wc = col / (float)steps, wa = 1 - wb - wc;
                    UIVertex vertex = a;
                    vertex.position = a.position * wa + b.position * wb + c.position * wc;
                    vertex.uv0 = a.uv0 * wa + b.uv0 * wb + c.uv0 * wc;
                    vertex.uv1 = a.uv1 * wa + b.uv1 * wb + c.uv1 * wc;
                    vertex.color = (Color)a.color * wa + (Color)b.color * wb + (Color)c.color * wc;
                    vertices.Add(vertex);
                }
            for (int row = 0; row < steps; row++)
            {
                int current = first + row * (steps + 1) - row * (row - 1) / 2;
                int next = current + steps - row + 1;
                for (int col = 0; col < steps - row; col++)
                {
                    triangles.Add(current + col);
                    triangles.Add(next + col);
                    triangles.Add(current + col + 1);
                    if (col >= steps - row - 1) continue;
                    triangles.Add(current + col + 1);
                    triangles.Add(next + col);
                    triangles.Add(next + col + 1);
                }
            }
        }
    }
}
