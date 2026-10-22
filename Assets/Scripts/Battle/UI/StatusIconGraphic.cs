using UnityEngine;
using UnityEngine.UI;

// Texture-free status signs, kept legible at twenty canvas units.
public sealed class StatusIconGraphic : MaskableGraphic
{
    public StatusId Status;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var center = rectTransform.rect.center;
        float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .45f;
        void Add(Vector2 p) { var v = UIVertex.simpleVert; v.position = center + p * scale; v.color = color; mesh.AddVert(v); }
        void Poly(params Vector2[] points)
        {
            int start = mesh.currentVertCount; foreach (var p in points) Add(p);
            for (int i = 1; i + 1 < points.Length; i++) mesh.AddTriangle(start, start + i, start + i + 1);
        }
        void Line(Vector2 a, Vector2 b)
        { var n = new Vector2(a.y - b.y, b.x - a.x).normalized * .1f; Poly(a - n, a + n, b + n, b - n); }
        void Path(params Vector2[] p) { for (int i = 1; i < p.Length; i++) Line(p[i - 1], p[i]); }
        switch (Status)
        {
            case StatusId.Poison:
                Poly(new Vector2(0, 1), new Vector2(.64f, -.08f), new Vector2(.6f, -.64f), new Vector2(0, -.98f), new Vector2(-.6f, -.64f), new Vector2(-.64f, -.08f)); break;
            case StatusId.Ignite:
                Poly(new Vector2(-.74f, -.2f), new Vector2(-.36f, .65f), new Vector2(-.14f, .15f), new Vector2(.28f, 1), new Vector2(.7f, -.15f), new Vector2(.52f, -.8f), new Vector2(0, -1), new Vector2(-.6f, -.7f)); break;
            case StatusId.Regen:
                Line(new Vector2(-.85f, 0), new Vector2(.85f, 0)); Line(new Vector2(0, -.85f), new Vector2(0, .85f)); break;
            case StatusId.Steadfast:
                Path(new Vector2(-.8f, .8f), new Vector2(.8f, .8f), new Vector2(.6f, -.35f), new Vector2(0, -1), new Vector2(-.6f, -.35f), new Vector2(-.8f, .8f)); break;
            case StatusId.Haste:
                for (int i = 0; i < 2; i++) { float x = -.8f + i * .8f; Path(new Vector2(x, .8f), new Vector2(x + .6f, 0), new Vector2(x, -.8f)); } break;
            case StatusId.Sunder:
                Path(new Vector2(-.1f, .85f), new Vector2(-.8f, .6f), new Vector2(-.55f, -.55f), new Vector2(-.14f, -.95f));
                Path(new Vector2(.3f, .85f), new Vector2(.85f, .55f), new Vector2(.64f, -.55f), new Vector2(.2f, -.75f));
                Path(new Vector2(.2f, .9f), new Vector2(-.1f, .1f), new Vector2(.22f, -.12f), new Vector2(-.14f, -1)); break;
            case StatusId.Confuse:
                Path(new Vector2(-.55f, .55f), new Vector2(-.3f, .9f), new Vector2(.38f, .85f), new Vector2(.6f, .4f), new Vector2(0, -.1f), new Vector2(0, -.4f));
                Line(new Vector2(0, -.72f), new Vector2(0, -.95f)); break;
            default:
                Line(new Vector2(0, .85f), new Vector2(0, -.65f));
                Path(new Vector2(-.65f, -.1f), new Vector2(0, -.85f), new Vector2(.65f, -.1f));
                if (Status == StatusId.Weak) Line(new Vector2(-.7f, .8f), new Vector2(.7f, .8f)); break;
        }
    }
}
