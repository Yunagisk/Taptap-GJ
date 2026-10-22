using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One material layer of the card, clipped against the same fracture polygons.
// Font UV channels and vertex colors survive the cut, keeping text recognizable.
public sealed class BattleEnemyCardFragment : MaskableGraphic
{
    sealed class Shard
    {
        public readonly List<UIVertex> triangles = new List<UIVertex>();
        public Vector2 center, velocity;
        public float spin;
    }
    readonly List<Shard> pieces = new List<Shard>();
    Texture texture;
    float progress;
    public override Texture mainTexture => texture != null ? texture : base.mainTexture;

    public void Bind(Graphic source, RectTransform card, Mesh mesh, Vector2[][] polygons)
    {
        raycastTarget = false;
        maskable = false;
        texture = source.mainTexture;
        material = source.materialForRendering;
        var positions = mesh.vertices;
        var colors = mesh.colors32;
        var normals = mesh.normals;
        var tangents = mesh.tangents;
        var uv = new List<Vector4>[4];
        for (int channel = 0; channel < uv.Length; channel++) { uv[channel] = new List<Vector4>(); mesh.GetUVs(channel, uv[channel]); }
        var vertices = new UIVertex[positions.Length];
        var tint = source.canvasRenderer.GetColor();
        for (int i = 0; i < positions.Length; i++)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = card.InverseTransformPoint(source.transform.TransformPoint(positions[i]));
            vertex.color = (colors.Length > i ? (Color)colors[i] : Color.white) * tint;
            if (normals.Length > i) vertex.normal = normals[i];
            if (tangents.Length > i) vertex.tangent = tangents[i];
            if (uv[0].Count > i) vertex.uv0 = uv[0][i];
            if (uv[1].Count > i) vertex.uv1 = uv[1][i];
            if (uv[2].Count > i) vertex.uv2 = uv[2][i];
            if (uv[3].Count > i) vertex.uv3 = uv[3][i];
            vertices[i] = vertex;
        }
        var indices = mesh.triangles;
        var scratchA = new List<UIVertex>(8);
        var scratchB = new List<UIVertex>(8);
        for (int shardIndex = 0; shardIndex < polygons.Length; shardIndex++)
        {
            var polygon = polygons[shardIndex];
            var piece = new Shard();
            foreach (var point in polygon) piece.center += point;
            piece.center /= polygon.Length;
            var radial = (piece.center - card.rect.center).normalized;
            piece.velocity = radial * (56 + shardIndex % 5 * 13) + new Vector2((shardIndex % 3 - 1) * 12, 12);
            piece.spin = (shardIndex % 2 == 0 ? 1 : -1) * (20 + shardIndex % 7 * 6);
            for (int t = 0; t < indices.Length; t += 3)
            {
                scratchA.Clear();
                scratchA.Add(vertices[indices[t]]); scratchA.Add(vertices[indices[t + 1]]); scratchA.Add(vertices[indices[t + 2]]);
                var input = scratchA; var output = scratchB;
                for (int edge = 0; edge < polygon.Length && input.Count > 0; edge++)
                {
                    output.Clear();
                    Vector2 a = polygon[edge], direction = polygon[(edge + 1) % polygon.Length] - a;
                    var previous = input[input.Count - 1];
                    float previousSide = Cross(direction, (Vector2)previous.position - a);
                    foreach (var current in input)
                    {
                        float side = Cross(direction, (Vector2)current.position - a);
                        if ((side >= 0) != (previousSide >= 0))
                            output.Add(Interpolate(previous, current, previousSide / (previousSide - side)));
                        if (side >= 0) output.Add(current);
                        previous = current; previousSide = side;
                    }
                    var swap = input; input = output; output = swap;
                }
                for (int v = 1; v + 1 < input.Count; v++)
                { piece.triangles.Add(input[0]); piece.triangles.Add(input[v]); piece.triangles.Add(input[v + 1]); }
            }
            if (piece.triangles.Count > 0) pieces.Add(piece);
        }
        SetVerticesDirty();
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static UIVertex Interpolate(UIVertex a, UIVertex b, float t)
    {
        a.position = Vector3.LerpUnclamped(a.position, b.position, t);
        a.color = Color32.Lerp(a.color, b.color, t);
        a.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
        a.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t);
        a.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t);
        a.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t);
        a.normal = Vector3.LerpUnclamped(a.normal, b.normal, t);
        a.tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t);
        return a;
    }

    public void Sample(float value) { progress = Mathf.Clamp01(value); SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float time = progress * BattleEnemyCardShatter.Duration;
        float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.22f, 1, progress));
        foreach (var piece in pieces)
        {
            float angle = piece.spin * time * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            Vector2 offset = piece.velocity * time + Vector2.down * (170 * time * time);
            int start = mesh.currentVertCount;
            foreach (var original in piece.triangles)
            {
                var vertex = original;
                Vector2 p = (Vector2)original.position - piece.center;
                vertex.position = piece.center + new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos) + offset;
                Color tint = vertex.color; tint.a *= fade; vertex.color = tint;
                mesh.AddVert(vertex);
            }
            for (int t = 0; t < piece.triangles.Count; t += 3) mesh.AddTriangle(start + t, start + t + 1, start + t + 2);
        }
    }
}
