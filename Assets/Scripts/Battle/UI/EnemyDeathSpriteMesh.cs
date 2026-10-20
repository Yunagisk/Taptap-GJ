using UnityEngine;

// Keeps the exact visible drawing throughout death, including the current idle pose.
// The final geometry is also used by the ash effect so its silhouette cannot jump.
public sealed class EnemyDeathSpriteMesh : MonoBehaviour, IEnemySpritePose
{
    const int Grid = 32;
    SpriteRenderer source;
    MeshRenderer appearance;
    Mesh mesh;
    Vector3[] initial, vertices;
    Rect bounds;
    Vector2 size, pivot;
    Vector3 landingOffset;
    float progress = -1f;
    EnemyArtworkProfile profile;
    public Renderer Appearance => appearance;
    public Sprite Drawing { get; private set; }
    public float Progress => Mathf.Max(0f, progress);

    public void Initialize(SpriteRenderer renderer, Vector3[] visibleVertices, float groundY, Vector4 paintedBounds,
        EnemyArtworkProfile artworkProfile = EnemyArtworkProfile.Crow)
    {
        profile = artworkProfile;
        source = renderer; Drawing = renderer.sprite;
        size = Drawing.rect.size / Drawing.pixelsPerUnit;
        pivot = Drawing.pivot / Drawing.rect.size;
        bounds = Rect.MinMaxRect(paintedBounds.x, paintedBounds.y, paintedBounds.z, paintedBounds.w);
        initial = new Vector3[(Grid + 1) * (Grid + 1)]; vertices = new Vector3[initial.Length];
        var uv = new Vector2[initial.Length]; var colors = new Color32[initial.Length];
        var triangles = new int[Grid * Grid * 6];
        Rect rect = Drawing.textureRect; Vector2 trim = Drawing.textureRectOffset;
        bool copyVisible = visibleVertices != null && visibleVertices.Length == initial.Length;
        for (int y = 0; y <= Grid; y++) for (int x = 0; x <= Grid; x++)
        {
            int i = y * (Grid + 1) + x;
            Vector2 point = new Vector2(x / (float)Grid, 1f - y / (float)Grid);
            initial[i] = copyVisible ? visibleVertices[i] : Local(point);
            uv[i] = new Vector2((rect.x - trim.x + point.x * Drawing.rect.width) / Drawing.texture.width,
                (rect.y - trim.y + point.y * Drawing.rect.height) / Drawing.texture.height);
            colors[i] = new Color32(255, 255, 255, 255);
        }
        int next = 0;
        for (int y = 0; y < Grid; y++) for (int x = 0; x < Grid; x++)
        {
            int a = y * (Grid + 1) + x, b = a + 1, c = a + Grid + 1;
            triangles[next++] = a; triangles[next++] = b; triangles[next++] = c;
            triangles[next++] = b; triangles[next++] = c + 1; triangles[next++] = c;
        }
        float currentGroundY = source.transform.TransformPoint(Local(new Vector2(pivot.x, bounds.yMin))).y;
        landingOffset = source.transform.InverseTransformVector(Vector3.up * (groundY - currentGroundY));
        mesh = new Mesh { name = "StableEnemyDeath" }; mesh.MarkDynamic();
        mesh.vertices = initial; mesh.uv = uv; mesh.colors32 = colors; mesh.triangles = triangles;
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        appearance = gameObject.AddComponent<MeshRenderer>(); appearance.sharedMaterial = renderer.sharedMaterial;
        appearance.sortingLayerID = renderer.sortingLayerID; appearance.sortingOrder = renderer.sortingOrder;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", Drawing.texture);
        properties.SetColor("_Color", renderer.color);
        properties.SetColor("_RendererColor", Color.white);
        properties.SetVector("_Flip", new Vector4(1, 1, 1, 1));
        appearance.SetPropertyBlock(properties);
        renderer.enabled = false;
        Apply(0f);
    }

    Vector3 Local(Vector2 point)
    {
        Vector2 position = (point - pivot) * size;
        return new Vector3(position.x * (source.flipX ? -1f : 1f), position.y * (source.flipY ? -1f : 1f), 0f);
    }

    public Vector3 Point(Vector2 uv)
    {
        float x = Mathf.Clamp01(uv.x) * Grid, y = (1f - Mathf.Clamp01(uv.y)) * Grid;
        int left = Mathf.Min(Grid - 1, Mathf.FloorToInt(x)), top = Mathf.Min(Grid - 1, Mathf.FloorToInt(y));
        Vector3 start = Vector3.Lerp(Vector3.Lerp(initial[top * (Grid + 1) + left], initial[top * (Grid + 1) + left + 1], x - left),
            Vector3.Lerp(initial[(top + 1) * (Grid + 1) + left], initial[(top + 1) * (Grid + 1) + left + 1], x - left), y - top);
        Vector2 normalized = new Vector2(start.x * (source.flipX ? -1f : 1f) / size.x + pivot.x,
            start.y * (source.flipY ? -1f : 1f) / size.y + pivot.y);
        float u = (normalized.x - bounds.xMin) / bounds.width, v = (normalized.y - bounds.yMin) / bounds.height;
        Vector2 pose;
        if (profile == EnemyArtworkProfile.Crow)
        {
            var crow = EnemyDeathMotion.Sample(u, v, Progress); pose = new Vector2(crow.x, crow.y);
        }
        else
        {
            var artwork = EnemyArtworkMotion.Sample(profile, EnemyArtworkPose.Death, u, v, Progress);
            pose = new Vector2(artwork.x, artwork.y);
        }
        Vector3 point = Local(new Vector2(bounds.xMin + pose.x * bounds.width, bounds.yMin + pose.y * bounds.height));
        return point + landingOffset * EnemyDeathMotion.Smooth(0f, 1f, Progress);
    }

    public void Apply(float value)
    {
        value = Mathf.Clamp01(value); if (value == progress) return; progress = value;
        for (int y = 0; y <= Grid; y++) for (int x = 0; x <= Grid; x++)
            vertices[y * (Grid + 1) + x] = Point(new Vector2(x / (float)Grid, 1f - y / (float)Grid));
        mesh.vertices = vertices; mesh.RecalculateBounds();
    }

    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
