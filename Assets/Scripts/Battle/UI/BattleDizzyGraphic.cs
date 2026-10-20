using UnityEngine;
using UnityEngine.UI;

// Three ink-outlined stars orbit above a broken enemy in place of its intent.
public sealed class BattleDizzyGraphic : MaskableGraphic
{
    float phase;
    static readonly Color Ink = new Color32(40, 33, 24, 255);
    static readonly Color Gold = new Color32(255, 211, 86, 255);

    protected override void OnEnable() { base.OnEnable(); phase = 0f; }
    void Update() { phase += Time.unscaledDeltaTime * 2.8f; SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Vector2 center = rectTransform.rect.center;
        float radius = rectTransform.rect.width * .33f;
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64, b = (i + 1) * Mathf.PI * 2f / 64;
            Line(center + Orbit(a), center + Orbit(b), 2f, new Color32(232, 203, 131, 145));
        }
        for (int i = 0; i < 3; i++)
        {
            float angle = phase + i * Mathf.PI * 2f / 3;
            Vector2 position = center + Orbit(angle);
            float size = Mathf.Lerp(7f, 10f, (Mathf.Sin(angle) + 1f) * .5f);
            Star(position, size + 2f, Ink);
            Star(position, size, Gold);
        }

        Vector2 Orbit(float angle) => new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * 10f);
        void Add(Vector2 p, Color tint)
        { var vertex = UIVertex.simpleVert; vertex.position = p; vertex.color = tint * color; mesh.AddVert(vertex); }
        void Star(Vector2 position, float radiusValue, Color tint)
        {
            int start = mesh.currentVertCount;
            Add(position, tint);
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * .5f + i * Mathf.PI / 5f - phase * .2f;
                float length = radiusValue * (i % 2 == 0 ? 1f : .43f);
                Add(position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length, tint);
            }
            for (int i = 0; i < 10; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % 10 + 1);
        }
        void Line(Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 normal = new Vector2(a.y - b.y, b.x - a.x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            Add(a - normal, tint); Add(a + normal, tint); Add(b - normal, tint); Add(b + normal, tint);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start + 1, start + 3, start + 2);
        }
    }
}
