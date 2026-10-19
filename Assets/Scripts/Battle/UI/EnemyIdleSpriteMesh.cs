using UnityEngine;

// Idle keeps a single registered drawing so feather outlines and feet cannot pop.
// Attack and hurt still use the authored SpriteRenderer sequences.
public sealed class EnemyIdleSpriteMesh : MonoBehaviour
{
    const int Grid = 32;
    Mesh mesh;
    MeshRenderer appearance;
    Sprite sprite;
    SpriteRenderer source;
    Vector3[] vertices;
    MaterialPropertyBlock properties;
    int lastTick = -1;
    public int LastTick => lastTick;

    public void Initialize(Sprite drawing, SpriteRenderer renderer)
    {
        sprite = drawing;
        source = renderer;
        vertices = new Vector3[(Grid + 1) * (Grid + 1)];
        var uv = new Vector2[vertices.Length];
        var colors = new Color32[vertices.Length];
        var triangles = new int[Grid * Grid * 6];
        Rect rect = sprite.textureRect;
        Vector2 trim = sprite.textureRectOffset;
        for (int y = 0; y <= Grid; y++) for (int x = 0; x <= Grid; x++)
        {
            int i = y * (Grid + 1) + x;
            uv[i] = new Vector2((rect.x - trim.x + x / (float)Grid * sprite.rect.width) / sprite.texture.width,
                (rect.y - trim.y + (1f - y / (float)Grid) * sprite.rect.height) / sprite.texture.height);
            colors[i] = new Color32(255, 255, 255, 255);
        }
        int next = 0;
        for (int y = 0; y < Grid; y++) for (int x = 0; x < Grid; x++)
        {
            int a = y * (Grid + 1) + x, b = a + 1, c = a + Grid + 1;
            triangles[next++] = a; triangles[next++] = b; triangles[next++] = c;
            triangles[next++] = b; triangles[next++] = c + 1; triangles[next++] = c;
        }
        mesh = new Mesh { name = "StableEnemyIdle30FPS" };
        mesh.MarkDynamic();
        mesh.vertices = vertices; mesh.uv = uv; mesh.colors32 = colors; mesh.triangles = triangles;
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        appearance = gameObject.AddComponent<MeshRenderer>();
        appearance.sharedMaterial = source.sharedMaterial;
        appearance.sortingLayerID = source.sortingLayerID;
        appearance.sortingOrder = source.sortingOrder;
        Apply(0f, 3.2f);
        Show(false);
    }

    public void Show(bool visible)
    {
        appearance.enabled = visible;
        source.enabled = !visible;
    }

    public void Apply(float seconds, float cycleSeconds)
    {
        if (properties == null)
        {
            properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", sprite.texture);
            properties.SetColor("_Color", source.color);
            properties.SetColor("_RendererColor", Color.white);
            properties.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
            appearance.SetPropertyBlock(properties);
        }
        int tick = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 30f + .00001f);
        if (tick == lastTick) return;
        for (int y = 0; y <= Grid; y++) for (int x = 0; x <= Grid; x++)
        {
            var point = EnemyIdleMotion.Sample(x / (float)Grid, y / (float)Grid, seconds, cycleSeconds);
            vertices[y * (Grid + 1) + x] = new Vector3(
                (point.x * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit * (source.flipX ? -1f : 1f),
                ((1f - point.y) * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit * (source.flipY ? -1f : 1f), 0f);
        }
        mesh.SetVertices(vertices);
        mesh.RecalculateBounds();
        lastTick = tick;
    }

    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
