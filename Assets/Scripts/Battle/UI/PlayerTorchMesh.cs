using UnityEngine;

// Original torch drawing on a small deformable mesh. Only the flame moves;
// the shaft and grip remain rigid, so the hand never slides off the handle.
public sealed class PlayerTorchMesh : MonoBehaviour
{
    const int Columns = 24, Rows = 40;
    Mesh mesh;
    Vector3[] positions;
    Vector2[] sourcePixels;
    Sprite source;

    public void Initialize(Sprite sprite, SpriteRenderer appearance)
    {
        source = sprite;
        int count = (Columns + 1) * (Rows + 1);
        positions = new Vector3[count];
        sourcePixels = new Vector2[count];
        var uvs = new Vector2[count];
        var colors = new Color32[count];
        var triangles = new int[Columns * Rows * 6];
        Rect rect = sprite.textureRect;
        for (int y = 0; y <= Rows; y++) for (int x = 0; x <= Columns; x++)
        {
            int i = y * (Columns + 1) + x;
            float sx = x / (float)Columns, sy = y / (float)Rows;
            sourcePixels[i] = new Vector2(sx * sprite.rect.width, sy * sprite.rect.height);
            uvs[i] = new Vector2((rect.x + sx * rect.width) / sprite.texture.width,
                (rect.y + sy * rect.height) / sprite.texture.height);
            colors[i] = appearance.color;
        }
        int next = 0;
        for (int y = 0; y < Rows; y++) for (int x = 0; x < Columns; x++)
        {
            int a = y * (Columns + 1) + x, b = a + 1, c = a + Columns + 1;
            triangles[next++] = a; triangles[next++] = b; triangles[next++] = c;
            triangles[next++] = b; triangles[next++] = c + 1; triangles[next++] = c;
        }
        mesh = new Mesh { name = "TorchFlameMesh" };
        mesh.MarkDynamic();
        mesh.vertices = positions; mesh.uv = uvs; mesh.colors32 = colors; mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 48f);
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = appearance.sharedMaterial;
        renderer.sortingLayerID = appearance.sortingLayerID;
        renderer.sortingOrder = appearance.sortingOrder;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", sprite.texture);
        properties.SetColor("_Color", Color.white);
        properties.SetColor("_RendererColor", Color.white);
        properties.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
        renderer.SetPropertyBlock(properties);
        Apply(Time.time);
    }

    void LateUpdate() { if (mesh != null) Apply(Time.time); }

    void Apply(float time)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            Vector2 p = sourcePixels[i];
            float topY = source.rect.height - p.y;
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(655f, 360f, topY));
            float dx = (Mathf.Sin(time * 6.5f + topY * .012f) * 12f
                + Mathf.Sin(time * 11f + topY * .027f) * 3f) * weight;
            float dy = Mathf.Sin(time * 8f + p.x * .02f) * 5f * weight;
            positions[i] = new Vector3((p.x + dx - source.pivot.x) / source.pixelsPerUnit,
                (p.y + dy - source.pivot.y) / source.pixelsPerUnit, 0f);
        }
        mesh.SetVertices(positions);
    }

    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
