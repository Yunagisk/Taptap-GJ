using UnityEngine;
using UnityEngine.UI;

// Sample one complete raster disc. Clip its lower half in geometry, rotate the
// inner UVs as one layer, and keep the engraved toothed rim fixed on the screen.
public sealed class MapTimeDialGraphic : MaskableGraphic
{
    public Sprite artwork;
    public Vector2 textureCenter = new Vector2(0.5f, 0.5f);
    [Range(0.1f, 0.5f)] public float textureRadius = 0.48f;
    [Range(0.6f, 0.95f)] public float movingRadius = 0.84f;
    [SerializeField] float rotation = -45;
    const int Segments = 128;
    readonly Vector2[] clipped = new Vector2[4];
    public float Rotation => rotation;
    public override Texture mainTexture => artwork != null ? artwork.texture : base.mainTexture;

    public void SetRotation(float degrees)
    {
        degrees = Mathf.Repeat(degrees, 360);
        if (Mathf.Abs(Mathf.DeltaAngle(rotation, degrees)) < 0.0001f) return;
        rotation = degrees;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        float radius = Mathf.Min(r.width * 0.5f, r.height);
        if (radius <= 0) return;
        Vector2 center = new Vector2(r.center.x, r.yMin);
        float inner = Mathf.Clamp(movingRadius, 0.6f, 0.95f);
        Add(center, textureCenter);
        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * Mathf.PI / Segments;
            Vector2 direction = Direction(angle);
            Vector2 sample = Direction(angle - rotation * Mathf.Deg2Rad);
            Add(center + direction * (radius * inner), textureCenter + sample * (textureRadius * inner));
            if (i > 0) mesh.AddTriangle(0, i, i + 1);
        }
        int ringStart = mesh.currentVertCount;
        for (int i = 0; i <= Segments; i++)
        {
            Vector2 direction = Direction(i * Mathf.PI / Segments);
            Add(center + direction * (radius * inner), textureCenter + direction * (textureRadius * inner));
            Add(center + direction * radius, textureCenter + direction * textureRadius);
            if (i == 0) continue;
            int v = ringStart + i * 2;
            mesh.AddTriangle(v - 2, v - 1, v);
            mesh.AddTriangle(v - 1, v + 1, v);
        }

        // The raster contains no baked moon. Draw its exact circular silhouette
        // here, with the same orbit/rotation as the inner disc, clipped at horizon.
        Vector2 moonCenter = center + MapTimeCrescent.Rotate(Vector2.down,
            rotation) * (radius * MapTimeCrescent.Orbit);
        float moonRadius = radius * MapTimeCrescent.Size;
        if (moonCenter.y + moonRadius >= center.y)
        {
            Color ink = color * new Color(0.24f, 0.23f, 0.21f, 1);
            for (int i = 0; i < MapTimeCrescent.Steps; i++)
            {
                float t = (float)i / MapTimeCrescent.Steps, next = (i + 1f) / MapTimeCrescent.Steps;
                Vector2 a = MoonPoint(MapTimeCrescent.Outer(t)), b = MoonPoint(MapTimeCrescent.Inner(t));
                Vector2 c = MoonPoint(MapTimeCrescent.Outer(next)), d = MoonPoint(MapTimeCrescent.Inner(next));
                Triangle(a, b, c, color); Triangle(b, d, c, color);
            }
            for (int i = 0; i < MapTimeCrescent.Steps; i++)
            {
                float t = (float)i / MapTimeCrescent.Steps, next = (i + 1f) / MapTimeCrescent.Steps;
                Stroke(MoonPoint(MapTimeCrescent.Outer(t)), MoonPoint(MapTimeCrescent.Outer(next)), ink);
                Stroke(MoonPoint(MapTimeCrescent.Inner(t)), MoonPoint(MapTimeCrescent.Inner(next)), ink);
            }
        }

        Vector2 MoonPoint(Vector2 point) => moonCenter + MapTimeCrescent.Rotate(point * moonRadius, rotation + 180);

        void Stroke(Vector2 a, Vector2 b, Color tint)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (radius * 0.002f);
            Triangle(a - normal, a + normal, b - normal, tint);
            Triangle(a + normal, b + normal, b - normal, tint);
        }

        void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            if (Mathf.Abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x)) < 0.000001f) return;
            int count = 0;
            Vector2 previous = c;
            for (int i = 0; i < 3; i++)
            {
                Vector2 current = i == 0 ? a : i == 1 ? b : c;
                bool inside = current.y >= center.y, wasInside = previous.y >= center.y;
                if (inside != wasInside)
                    clipped[count++] = Vector2.Lerp(previous, current, (center.y - previous.y) / (current.y - previous.y));
                if (inside) clipped[count++] = current;
                previous = current;
            }
            if (count < 3) return;
            // Sample plain daylight paper, giving the native curve the same hue
            // as the sun and stars without another texture or material.
            Vector2 paper = textureCenter + new Vector2(0.03f, 0.13f);
            for (int i = 1; i < count - 1; i++)
            {
                int start = mesh.currentVertCount;
                MoonVertex(clipped[0]); MoonVertex(clipped[i]); MoonVertex(clipped[i+1]);
                mesh.AddTriangle(start, start+1, start+2);
            }
            void MoonVertex(Vector2 point)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = point; vertex.uv0 = paper; vertex.color = tint;
                mesh.AddVert(vertex);
            }
        }

        void Add(Vector2 position, Vector2 uv)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.uv0 = uv;
            vertex.color = color;
            mesh.AddVert(vertex);
        }
    }

    static Vector2 Direction(float radians) => new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
}
