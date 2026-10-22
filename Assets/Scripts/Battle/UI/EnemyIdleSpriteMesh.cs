using UnityEngine;

// A registered drawing can breathe, strike and recoil without changing ink or palette.
public sealed class EnemyIdleSpriteMesh : MonoBehaviour, IEnemySpritePose
{
    const int Grid = 32;
    Mesh mesh;
    MeshRenderer appearance;
    Sprite sprite;
    SpriteRenderer source;
    Vector3[] vertices;
    MaterialPropertyBlock properties;
    int lastTick = -1;
    Vector3[] transition;
    public int LastTick => lastTick;
    public Renderer Appearance => appearance;

    public Vector3 Point(Vector2 uv)
    {
        float x = Mathf.Clamp01(uv.x) * Grid, y = (1 - Mathf.Clamp01(uv.y)) * Grid;
        int left = Mathf.Min(Grid - 1, Mathf.FloorToInt(x)), top = Mathf.Min(Grid - 1, Mathf.FloorToInt(y));
        return Vector3.Lerp(Vector3.Lerp(vertices[top * (Grid + 1) + left], vertices[top * (Grid + 1) + left + 1], x - left),
            Vector3.Lerp(vertices[(top + 1) * (Grid + 1) + left], vertices[(top + 1) * (Grid + 1) + left + 1], x - left), y - top);
    }

    public void Initialize(Sprite drawing, SpriteRenderer renderer, EnemySpriteAnimationSet set = null)
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
        if (set != null && set.UsesArtworkMotion) ApplyArtwork(set, EnemyArtworkPose.Idle, 0, 1);
        else Apply(0f, 3.2f);
        Show(false);
    }

    public void Show(bool visible)
    {
        appearance.enabled = visible;
        source.enabled = !visible;
    }

    public void BeginPose() { transition = (Vector3[])vertices.Clone(); lastTick = -1; }

    public Vector3 ArtworkPoint(EnemySpriteAnimationSet set, EnemyArtworkPose pose, Vector2 point, float time)
    {
        var sample = EnemyArtworkMotion.Sample(set.artworkProfile, pose, point.x, point.y, time,
            set.idleCycleSeconds, set.attackContactFrame / (float)set.AttackFrameCount);
        var bounds = set.artworkBounds;
        return Local(new Vector2(Mathf.LerpUnclamped(bounds.x, bounds.z, sample.x),
            Mathf.LerpUnclamped(bounds.y, bounds.w, sample.y)));
    }

    Vector3 Local(Vector2 point) => new Vector3(
        (point.x * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit * (source.flipX ? -1 : 1),
        (point.y * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit * (source.flipY ? -1 : 1), 0);

    public void ApplyArtwork(EnemySpriteAnimationSet set, EnemyArtworkPose pose, float seconds, float duration)
    {
        EnsureProperties();
        int tick = Mathf.FloorToInt(Mathf.Max(0f, seconds) * set.framesPerSecond + .0001f);
        if (tick == lastTick) return;
        float quantized = tick / (float)set.framesPerSecond;
        float time = pose == EnemyArtworkPose.Idle ? quantized : Mathf.Clamp01(quantized / duration);
        float blend = Mathf.SmoothStep(0, 1, seconds / (pose == EnemyArtworkPose.Hurt ? .065f : .12f));
        var b = set.artworkBounds;
        for (int y = 0; y <= Grid; y++) for (int x = 0; x <= Grid; x++)
        {
            int i = y * (Grid + 1) + x;
            Vector2 point = new Vector2((x / (float)Grid - b.x) / (b.z - b.x),
                (1f - y / (float)Grid - b.y) / (b.w - b.y));
            Vector3 target = ArtworkPoint(set, pose, point, time);
            vertices[i] = transition != null ? Vector3.Lerp(transition[i], target, blend) : target;
        }
        mesh.SetVertices(vertices); mesh.RecalculateBounds(); lastTick = tick;
    }

    public void Apply(float seconds, float cycleSeconds)
    {
        EnsureProperties();
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

    void EnsureProperties()
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
    }

    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
