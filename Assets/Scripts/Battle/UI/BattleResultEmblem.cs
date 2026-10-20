using UnityEngine;
using UnityEngine.UI;

// An engraved crossed-blade seal, with broken blades on defeat.
public sealed class BattleResultEmblem : MaskableGraphic
{
    bool victory = true;

    public void SetVictory(bool value)
    {
        if (victory == value) return;
        victory = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float scale = Mathf.Min(rect.width, rect.height) / 100;
        Vector2[] diamond = { new Vector2(0, 47), new Vector2(47, 0), new Vector2(0, -47), new Vector2(-47, 0) };
        for (int i = 0; i < diamond.Length; i++)
        {
            Line(diamond[i], diamond[(i + 1) % 4], 1.6f, .8f);
            Line(diamond[i] * .88f, diamond[(i + 1) % 4] * .88f, 1f, .45f);
        }
        Sword(-.62f);
        Sword(.62f);

        void Sword(float angle)
        {
            Vector2 Rotate(Vector2 p) => new Vector2(p.x * Mathf.Cos(angle) - p.y * Mathf.Sin(angle),
                p.x * Mathf.Sin(angle) + p.y * Mathf.Cos(angle));
            void Blade(params Vector2[] points)
            {
                for (int i = 0; i < points.Length; i++) points[i] = Rotate(points[i]);
                Polygon(points, .9f);
            }
            if (victory)
                Blade(new Vector2(-3, -10), new Vector2(-4, 24), new Vector2(0, 35), new Vector2(4, 24), new Vector2(3, -10));
            else
            {
                Blade(new Vector2(-3, -10), new Vector2(-3.5f, 7), new Vector2(0, 4), new Vector2(3.5f, 9), new Vector2(3, -10));
                Blade(new Vector2(-3, 15), new Vector2(-4, 24), new Vector2(0, 35), new Vector2(4, 24), new Vector2(3, 16), new Vector2(0, 11));
            }
            Line(Rotate(new Vector2(-10, -13)), Rotate(new Vector2(10, -13)), 3, 1);
            Line(Rotate(new Vector2(0, -15)), Rotate(new Vector2(0, -28)), 4, .8f);
            Blade(new Vector2(-4, -29), new Vector2(0, -33), new Vector2(4, -29), new Vector2(0, -25));
        }
        void Polygon(Vector2[] points, float alpha)
        {
            int first = mesh.currentVertCount;
            foreach (var point in points) Add(point, alpha);
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(first, first + i, first + i + 1);
        }
        void Line(Vector2 a, Vector2 b, float width, float alpha)
        {
            var normal = new Vector2(a.y - b.y, b.x - a.x).normalized * width * .5f;
            Polygon(new[] { a - normal, a + normal, b + normal, b - normal }, alpha);
        }
        void Add(Vector2 point, float alpha)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = rect.center + point * scale;
            Color tint = color; tint.a *= alpha; vertex.color = tint;
            mesh.AddVert(vertex);
        }
    }
}
