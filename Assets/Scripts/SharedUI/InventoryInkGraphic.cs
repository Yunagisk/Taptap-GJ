using UnityEngine;
using UnityEngine.UI;

// Small uneven ink strokes over the paper; decorative graphics never receive input.
public sealed class InventoryInkGraphic : MaskableGraphic
{
    public enum Shape { Frame, Rule, Diamond, VerticalRule, OrnateFrame, Dot }
    public Shape shape;
    public float stroke = 1.3f;
    public int seed = 17;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        if (shape == Shape.Dot)
        {
            Add(r.center);
            const int segments = 32;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float radius = Mathf.Min(r.width, r.height) * .46f * (1 + .035f * Mathf.Sin(i * 2.3f + seed));
                Add(r.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                if (i > 0) mesh.AddTriangle(0, i, i + 1);
            }
            return;
        }
        if (shape == Shape.OrnateFrame)
        {
            if (r.width > 0 && r.height > 0) OrnateFrame();
            return;
        }
        if (shape == Shape.Rule) { Line(new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y), 0); return; }
        if (shape == Shape.VerticalRule) { Line(new Vector2(r.center.x, r.yMin), new Vector2(r.center.x, r.yMax), 0); return; }
        if (shape == Shape.Diamond)
        {
            Vector2[] p = { new Vector2(r.center.x, r.yMax), new Vector2(r.xMax, r.center.y),
                new Vector2(r.center.x, r.yMin), new Vector2(r.xMin, r.center.y) };
            for (int i=0;i<4;i++) Line(p[i],p[(i+1)%4],i);
            return;
        }
        Vector2[] corners = { new Vector2(r.xMin+1,r.yMin+1), new Vector2(r.xMax-1,r.yMin+1),
            new Vector2(r.xMax-1,r.yMax-1), new Vector2(r.xMin+1,r.yMax-1) };
        for (int i=0;i<4;i++) Line(corners[i],corners[(i+1)%4],i);

        void OrnateFrame()
        {
            float halfWidth = r.width * .5f, halfHeight = r.height * .5f;
            float unit = Mathf.Min(1, Mathf.Min(r.width / 228, r.height / 50));
            // Mirror the engraved scrolls at all four corners. Keep the strokes
            // inside the graphic's rect, with a quiet inset around the caption.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                Vector2 Point(float x, float y) => r.center + new Vector2(
                    sx * (halfWidth - x * unit), sy * (halfHeight - y * unit));
                void Edge(float ax, float ay, float bx, float by, int edge)
                    => Line(Point(ax, ay), Point(bx, by), edge);
                void Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
                {
                    Vector2 previous = Point(a.x, a.y);
                    for (int i = 1; i <= 18; i++)
                    {
                        float t = i / 18f, s = 1 - t;
                        Vector2 p = s*s*s*a + 3*s*s*t*b + 3*s*t*t*c + t*t*t*d;
                        Vector2 next = Point(p.x, p.y);
                        Line(previous, next, i); previous = next;
                    }
                }
                Edge(28, 5, halfWidth / unit, 5, 0);
                Edge(32, 8, halfWidth / unit, 8, 1);
                Edge(5, 20, 5, halfHeight / unit, 2);
                Edge(8, 22, 8, halfHeight / unit, 3);
                Curve(new Vector2(32, 5), new Vector2(18, 5), new Vector2(22, 1), new Vector2(11, 2));
                Curve(new Vector2(11, 2), new Vector2(0, 2), new Vector2(0, 16), new Vector2(9, 16));
                Curve(new Vector2(9, 16), new Vector2(17, 16), new Vector2(16, 7), new Vector2(10, 8));
                Curve(new Vector2(10, 8), new Vector2(7, 8), new Vector2(8, 12), new Vector2(11, 11));
                Curve(new Vector2(5, 22), new Vector2(5, 18), new Vector2(15, 20), new Vector2(21, 12));
                // A solid pointed leaf gives the delicate curls a visible black accent.
                Vector2[] leaf = { Point(17, 12), Point(21, 8), Point(27, 9), Point(23, 14) };
                int start = mesh.currentVertCount;
                foreach (Vector2 point in leaf) Add(point);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }

        void Line(Vector2 a, Vector2 b, int edge)
        {
            Vector2 delta=b-a, normal=new Vector2(-delta.y,delta.x).normalized;
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/18));
            for (int i=0;i<=steps;i++)
            {
                float t=(float)i/steps;
                float wave=Mathf.Sin(i*1.73f+seed+edge*3)*.22f*Mathf.Sin(t*Mathf.PI);
                Vector2 point=Vector2.Lerp(a,b,t)+normal*wave;
                float half=stroke*.5f*(.85f+.15f*Mathf.Sin(i*.81f+seed));
                Add(point-normal*half); Add(point+normal*half);
                if(i==0)continue;
                int n=mesh.currentVertCount-4;
                mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n+1,n+3,n+2);
            }
        }
        void Add(Vector2 point)
        {
            var vertex=UIVertex.simpleVert;vertex.position=point;vertex.color=color;mesh.AddVert(vertex);
        }
    }
}
