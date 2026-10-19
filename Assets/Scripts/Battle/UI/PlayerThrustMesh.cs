using UnityEngine;

// Two immutable drawings, continuously posed at the display's render rate.
// Geometry, UVs and buffers are allocated once; playback only updates vertices.
public sealed class PlayerThrustMesh : MonoBehaviour
{
    sealed class Layer
    {
        public PlayerThrustRig.Geometry geometry;
        public Vector3[] positions;
        public Mesh mesh;
    }

    Layer[] layers;
    public float LastTime { get; private set; }

    public void Initialize(Sprite bodySprite, Sprite armSource, SpriteRenderer appearance)
    {
        layers = new[] {
            CreateLayer("BodyMesh", bodySprite, appearance, 0, false),
            CreateLayer("ArmMesh", armSource, appearance, 1, true)
        };
        Apply(0f, 1f);
    }

    Layer CreateLayer(string layerName, Sprite sprite, SpriteRenderer appearance, int order, bool arm)
    {
        var obj = new GameObject(layerName);
        obj.transform.SetParent(transform, false);
        var filter = obj.AddComponent<MeshFilter>();
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = appearance.sharedMaterial;
        renderer.sortingLayerID = appearance.sortingLayerID;
        renderer.sortingOrder = appearance.sortingOrder + order;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", sprite.texture);
        properties.SetColor("_Color", appearance.color);
        properties.SetColor("_RendererColor", Color.white);
        properties.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
        renderer.SetPropertyBlock(properties);

        var geometry = PlayerThrustRig.CreateGeometry(arm);
        int count = geometry.bindings.Length;
        var positions = new Vector3[count];
        var uvs = new Vector2[count];
        var colors = new Color32[count];
        Rect source = sprite.textureRect;
        for (int i = 0; i < count; i++)
        {
            PlayerThrustRig.Point rest = geometry.bindings[i].rest;
            uvs[i] = new Vector2(
                (source.x + rest.x / PlayerThrustRig.Canvas * source.width) / sprite.texture.width,
                (source.y + (1f - rest.y / PlayerThrustRig.Canvas) * source.height) / sprite.texture.height);
            colors[i] = new Color32(255, 255, 255, 255);
        }
        var mesh = new Mesh { name = layerName };
        mesh.MarkDynamic();
        mesh.vertices = positions;
        mesh.uv = uvs;
        mesh.colors32 = colors;
        mesh.triangles = geometry.triangles;
        mesh.bounds = new Bounds(new Vector3(0f, 7f, 0f), new Vector3(32f, 32f, 1f));
        filter.sharedMesh = mesh;
        return new Layer { geometry = geometry, positions = positions, mesh = mesh };
    }

    public PlayerThrustRig.Pose Apply(float seconds, float duration, float hitSeconds = -1f,
        float idleSeconds = 0f, bool blendIdle = false)
    {
        LastTime = seconds;
        return ApplyPose(blendIdle ? PlayerThrustRig.SampleWithIdle(seconds, duration, hitSeconds, idleSeconds)
            : PlayerThrustRig.Sample(seconds, duration, hitSeconds));
    }

    public PlayerThrustRig.Pose ApplyIdle(float seconds)
    {
        LastTime = 0f;
        return ApplyPose(PlayerThrustRig.SampleIdle(seconds));
    }

    public PlayerThrustRig.Pose ApplySwing(float seconds, float duration, float hitSeconds, float idleSeconds)
    {
        LastTime = seconds;
        return ApplyPose(PlayerSwingRig.Sample(seconds, duration, hitSeconds, idleSeconds));
    }

    PlayerThrustRig.Pose ApplyPose(PlayerThrustRig.Pose pose)
    {
        foreach (Layer layer in layers)
        {
            for (int i = 0; i < layer.positions.Length; i++)
            {
                PlayerThrustRig.Point p = PlayerThrustRig.Deform(layer.geometry.bindings[i], pose);
                layer.positions[i] = new Vector3((p.x - PlayerThrustRig.RootX) / 100f,
                    (PlayerThrustRig.RootY - p.y) / 100f, 0f);
            }
            layer.mesh.SetVertices(layer.positions);
        }
        return pose;
    }

    void OnDestroy()
    {
        if (layers == null) return;
        foreach (Layer layer in layers)
            if (layer.mesh != null) Destroy(layer.mesh);
    }
}
