using UnityEngine;

public interface IEnemySpritePose
{
    Renderer Appearance { get; }
    Vector3 Point(Vector2 uv);
}

// Each quad samples its original patch of the sprite's alpha. Only the actual
// corpse sheds grains; transparent space never emits particles or a rectangle.
public sealed class EnemyBlackAshDissolve : MonoBehaviour
{
    const int Grid = 120;
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    Renderer corpse;
    MeshRenderer dust;
    Mesh mesh;
    MaterialPropertyBlock bodyProperties, dustProperties;
    public float Progress { get; private set; }
    Sprite bodyDrawing;

    public void Initialize(SpriteRenderer source, Material material, IEnemySpritePose pose = null)
    {
        corpse = pose != null ? pose.Appearance : (Renderer)source;
        Sprite sprite = source.sprite;
        bodyDrawing = sprite;
        var root = new GameObject("BlackAshDust");
        root.transform.SetParent(source.transform, false);
        dust = root.AddComponent<MeshRenderer>();
        dust.sharedMaterial = material;
        dust.sortingLayerID = source.sortingLayerID;
        dust.sortingOrder = source.sortingOrder + 1;
        var vertices = new Vector3[Grid * Grid * 4];
        var uv = new Vector2[vertices.Length];
        var centers = new Vector2[vertices.Length];
        var origins = new Vector2[vertices.Length];
        var triangles = new int[Grid * Grid * 6];
        Vector2 size = sprite.rect.size / sprite.pixelsPerUnit;
        Vector2 pivot = sprite.pivot / sprite.rect.size;
        Rect rect = sprite.textureRect;
        Vector2 offset = sprite.textureRectOffset;
        var uvRect = new Vector4((rect.x - offset.x) / sprite.texture.width,
            (rect.y - offset.y) / sprite.texture.height,
            sprite.rect.width / sprite.texture.width, sprite.rect.height / sprite.texture.height);
        for (int y = 0; y < Grid; y++) for (int x = 0; x < Grid; x++)
        {
            int index = (y * Grid + x) * 4, triangle = (y * Grid + x) * 6;
            Vector2 center = new Vector2((x + .5f) / Grid, (y + .5f) / Grid);
            Vector2 origin = pose != null ? (Vector2)pose.Point(center) : (center - pivot) * size
                * new Vector2(source.flipX ? -1f : 1f, source.flipY ? -1f : 1f);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 point = new Vector2((x + (corner == 1 || corner == 2 ? 1f : 0f)) / Grid,
                    (y + (corner >= 2 ? 1f : 0f)) / Grid);
                vertices[index + corner] = pose != null ? pose.Point(point) : (Vector3)((point - pivot) * size
                    * new Vector2(source.flipX ? -1f : 1f, source.flipY ? -1f : 1f));
                uv[index + corner] = new Vector2(uvRect.x + point.x * uvRect.z, uvRect.y + point.y * uvRect.w);
                centers[index + corner] = center;
                origins[index + corner] = origin;
            }
            triangles[triangle] = index; triangles[triangle + 1] = index + 1; triangles[triangle + 2] = index + 2;
            triangles[triangle + 3] = index; triangles[triangle + 4] = index + 2; triangles[triangle + 5] = index + 3;
        }
        mesh = new Mesh { name = "EnemyBlackAshGrains" };
        mesh.vertices = vertices; mesh.uv = uv; mesh.uv2 = centers; mesh.uv3 = origins; mesh.triangles = triangles;
        // The shader moves grains upward and sideways beyond the original sprite bounds.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(size.x * 2f, size.y * 2f, 1f));
        mesh.UploadMeshData(true);
        root.AddComponent<MeshFilter>().sharedMesh = mesh;

        float artworkGamma = corpse.sharedMaterial != null && corpse.sharedMaterial.HasProperty("_ArtworkGamma")
            ? corpse.sharedMaterial.GetFloat("_ArtworkGamma") : 1f;
        corpse.sharedMaterial = material;
        bodyProperties = new MaterialPropertyBlock(); corpse.GetPropertyBlock(bodyProperties);
        bodyProperties.SetTexture("_MainTex", sprite.texture);
        bodyProperties.SetVector("_SpriteUVRect", uvRect);
        bodyProperties.SetVector("_Grid", new Vector4(Grid, Grid, 0, 0));
        bodyProperties.SetFloat("_AshPass", 0f);
        bodyProperties.SetFloat("_ArtworkGamma", artworkGamma);

        dustProperties = new MaterialPropertyBlock();
        dustProperties.SetTexture("_MainTex", sprite.texture);
        dustProperties.SetVector("_SpriteUVRect", uvRect);
        dustProperties.SetVector("_Grid", new Vector4(Grid, Grid, 0, 0));
        dustProperties.SetVector("_LocalSize", new Vector4(size.x, size.y, 0, 0));
        dustProperties.SetVector("_Pivot", new Vector4(pivot.x, pivot.y, 0, 0));
        dustProperties.SetVector("_Flip", new Vector4(source.flipX ? -1f : 1f, source.flipY ? -1f : 1f, 1, 1));
        dustProperties.SetColor("_Color", Color.white);
        dustProperties.SetColor("_RendererColor", Color.white);
        dustProperties.SetFloat("_Opacity", source.color.a);
        dustProperties.SetFloat("_AshPass", 1f);
        SetProgress(0f);
    }

    public void SetProgress(float value)
    {
        Progress = Mathf.Clamp01(value);
        bodyProperties.SetFloat(ProgressId, Progress); corpse.SetPropertyBlock(bodyProperties);
        dustProperties.SetFloat(ProgressId, Progress); dust.SetPropertyBlock(dustProperties);
    }

    // Authored hurt sequences can change drawings while the same ash clock keeps running.
    // Already emitted dust retains its original alpha sample rather than changing shape in flight.
    public void SetBodyDrawing(Sprite sprite)
    {
        if (sprite == null || sprite == bodyDrawing) return;
        bodyDrawing = sprite;
        Rect rect = sprite.textureRect; Vector2 offset = sprite.textureRectOffset;
        bodyProperties.SetTexture("_MainTex", sprite.texture);
        bodyProperties.SetVector("_SpriteUVRect", new Vector4((rect.x - offset.x) / sprite.texture.width,
            (rect.y - offset.y) / sprite.texture.height, sprite.rect.width / sprite.texture.width,
            sprite.rect.height / sprite.texture.height));
        corpse.SetPropertyBlock(bodyProperties);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
