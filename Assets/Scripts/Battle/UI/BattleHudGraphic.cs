using UnityEngine;
using UnityEngine.UI;

// Code-native ink frames, diamonds and wheel strokes remain crisp at any scale.
public sealed class BattleHudGraphic : MaskableGraphic
{
    public enum Shape { Panel, Diamond, Ellipse, Rule, Arrow }
    public Shape shape;
    public Color fill = new Color32(246, 242, 228, 245);
    public Color ink = new Color32(26, 25, 23, 255);
    public float stroke = 2;
    public bool highlighted;
    static readonly Color Gold = new Color32(237, 185, 53, 255);

    public void Highlight(bool value)
    { if (highlighted == value) return; highlighted = value; SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var r = rectTransform.rect;
        Color line = highlighted ? Gold : ink;
        if (shape == Shape.Rule)
        { Line(new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y), stroke, line); return; }
        if (shape == Shape.Ellipse)
        {
            for (int i = 0; i < 96; i++)
            {
                float a = i * Mathf.PI * 2 / 96, b = (i + 1) * Mathf.PI * 2 / 96;
                Color c = line; c.a *= Mathf.Sin(a) > 0 ? .28f : .7f;
                if (Mathf.Sin(a) > 0 && i % 4 == 0) continue;
                Line(r.center + new Vector2(Mathf.Cos(a) * r.width * .5f, Mathf.Sin(a) * r.height * .5f),
                    r.center + new Vector2(Mathf.Cos(b) * r.width * .5f, Mathf.Sin(b) * r.height * .5f), stroke, c);
            }
            return;
        }
        Vector2[] points;
        if (shape == Shape.Diamond)
            points = new[] { new Vector2(r.center.x, r.yMax), new Vector2(r.xMax, r.center.y), new Vector2(r.center.x, r.yMin), new Vector2(r.xMin, r.center.y) };
        else if (shape == Shape.Arrow)
            points = new[] { new Vector2(r.xMin + r.width * .3f, r.yMax), new Vector2(r.xMax - r.width * .3f, r.yMax),
                new Vector2(r.xMax - r.width * .3f, r.center.y), new Vector2(r.xMax, r.center.y),
                new Vector2(r.center.x, r.yMin), new Vector2(r.xMin, r.center.y), new Vector2(r.xMin + r.width * .3f, r.center.y) };
        else
        {
            float bevel = Mathf.Min(11, r.height * .18f);
            points = new[] { new Vector2(r.xMin + bevel, r.yMin), new Vector2(r.xMax - bevel, r.yMin),
                new Vector2(r.xMax, r.yMin + bevel), new Vector2(r.xMax, r.yMax - bevel),
                new Vector2(r.xMax - bevel, r.yMax), new Vector2(r.xMin + bevel, r.yMax),
                new Vector2(r.xMin, r.yMax - bevel), new Vector2(r.xMin, r.yMin + bevel) };
        }
        if (fill.a > 0)
        {
            int start = mesh.currentVertCount; Add(r.center, fill);
            foreach (var point in points) Add(point, fill);
            for (int i = 0; i < points.Length; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % points.Length + 1);
        }
        if (stroke <= 0) return;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Length];
            Line(a, b, stroke, line);
            if (shape == Shape.Panel || shape == Shape.Diamond)
                Line(Vector2.Lerp(a, r.center, .045f), Vector2.Lerp(b, r.center, .045f), .8f, line);
        }

        void Add(Vector2 p, Color c)
        { var v = UIVertex.simpleVert; v.position = p; v.color = c * color; mesh.AddVert(v); }
        void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 normal = new Vector2(a.y - b.y, b.x - a.x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            Add(a - normal, c); Add(a + normal, c); Add(b - normal, c); Add(b + normal, c);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start + 1, start + 3, start + 2);
        }
    }
}
