using System;
using UnityEngine;

// The perspective change is in newly drawn front/side/back sprite frames.
// Only the small idle breathing deformation is evaluated at runtime.
public sealed class PlayerTurnSpriteBody : MonoBehaviour
{
    const int Grid = 32;
    Sprite[] sprites;
    Sprite[] hands;
    PlayerTurnSpriteMotion data;
    Mesh mesh;
    MeshRenderer meshRenderer;
    MeshRenderer handRenderer;
    MaterialPropertyBlock properties;
    MaterialPropertyBlock handProperties;
    Color tint;
    Vector3[] vertices;
    Vector2[] uvs;
    PlayerTurnSpriteMotion.Point[] rest;
    int currentSprite = -1, lastBreathTick = -1;
    float lastWeight = -1f;
    PlayerTurnSpriteMotion.Frame lastFrame;
    PlayerThrustRig.Pose cachedPose;
    bool lastWasIdle;
    public bool IsReady { get; private set; }
    public float LastTime { get; private set; }
    public float LastFrameTime { get; private set; }
    public int LastFrameIndex { get; private set; }
    public string LastView { get; private set; }
    public int SampleRate => data == null ? 0 : data.fps;
    public float Duration(bool swing) => (swing ? data.swing : data.thrust).duration;
    public float HitTime(bool swing) => (swing ? data.swing : data.thrust).hit;
    public float ReleaseTime(bool swing) => (swing ? data.swing : data.thrust).release;

    public bool Initialize(Sprite[] frames, Sprite[] handFrames, TextAsset timing, SpriteRenderer appearance)
    {
        if (frames == null || handFrames == null || frames.Length != handFrames.Length || timing == null || appearance == null) return false;
        try { data = JsonUtility.FromJson<PlayerTurnSpriteMotion>(timing.text); }
        catch (ArgumentException) { return false; }
        if (data == null || !data.IsUsable(frames.Length)) return false;
        foreach (Sprite sprite in frames) if (sprite == null) return false;
        foreach (Sprite sprite in handFrames) if (sprite == null) return false;
        sprites = frames;
        hands = handFrames;
        int count = (Grid + 1) * (Grid + 1);
        vertices = new Vector3[count]; uvs = new Vector2[count];
        rest = new PlayerTurnSpriteMotion.Point[count];
        var colors = new Color32[count]; var triangles = new int[Grid * Grid * 6];
        for (int y = 0; y <= Grid; y++) for (int x = 0; x <= Grid; x++)
        {
            int i = y * (Grid + 1) + x;
            rest[i] = new PlayerTurnSpriteMotion.Point(x * 2048f / Grid, y * 2048f / Grid);
            colors[i] = new Color32(255, 255, 255, 255);
        }
        int next = 0;
        for (int y = 0; y < Grid; y++) for (int x = 0; x < Grid; x++)
        {
            int a = y * (Grid + 1) + x, b = a + 1, c = a + Grid + 1;
            triangles[next++] = a; triangles[next++] = b; triangles[next++] = c;
            triangles[next++] = b; triangles[next++] = c + 1; triangles[next++] = c;
        }
        mesh = new Mesh { name = "RightHandTurn30FPS" }; mesh.MarkDynamic();
        mesh.vertices = vertices; mesh.uv = uvs; mesh.colors32 = colors; mesh.triangles = triangles;
        mesh.bounds = new Bounds(new Vector3(0f, 7f, 0f), new Vector3(32f, 32f, 1f));
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = appearance.sharedMaterial;
        meshRenderer.sortingLayerID = appearance.sortingLayerID;
        meshRenderer.sortingOrder = appearance.sortingOrder;
        var handRoot = new GameObject("GripFingers");
        handRoot.transform.SetParent(transform, false);
        handRoot.AddComponent<MeshFilter>().sharedMesh = mesh;
        handRenderer = handRoot.AddComponent<MeshRenderer>();
        handRenderer.sharedMaterial = appearance.sharedMaterial;
        handRenderer.sortingLayerID = appearance.sortingLayerID;
        handRenderer.sortingOrder = appearance.sortingOrder + 2;
        tint = appearance.color;
        EnsureProperties();
        IsReady = true;
        ApplyIdle(0f);
        return true;
    }

    public PlayerThrustRig.Pose Apply(float seconds, bool swing, float duration, float hit, float idleSeconds)
    {
        var clip = swing ? data.swing : data.thrust;
        float time = clip.Retimed(seconds, duration, hit);
        LastFrameIndex = clip.FrameIndex(time);
        LastTime = seconds;
        return ApplyFrame(clip.frames[LastFrameIndex], idleSeconds, false);
    }

    public PlayerThrustRig.Pose ApplyIdle(float seconds)
    {
        LastTime = LastFrameTime = 0f; LastFrameIndex = 0;
        return ApplyFrame(data.thrust.frames[0], seconds, true);
    }

    PlayerThrustRig.Pose ApplyFrame(PlayerTurnSpriteMotion.Frame frame, float idleSeconds, bool idle)
    {
        if (!idle && !lastWasIdle && ReferenceEquals(frame, lastFrame)) return cachedPose;
        lastWasIdle = idle; lastFrame = frame;
        int tick = Mathf.FloorToInt(idleSeconds * 30f + .00001f);
        if (currentSprite != frame.sprite)
        {
            EnsureProperties();
            Sprite sprite = sprites[frame.sprite];
            Rect rect = sprite.textureRect;
            Vector2 trim = sprite.textureRectOffset;
            for (int i = 0; i < rest.Length; i++)
                uvs[i] = new Vector2((rect.x - trim.x + rest[i].x / 2048f * sprite.rect.width) / sprite.texture.width,
                    (rect.y - trim.y + (1f - rest[i].y / 2048f) * sprite.rect.height) / sprite.texture.height);
            mesh.uv = uvs;
            properties.SetTexture("_MainTex", sprite.texture);
            meshRenderer.SetPropertyBlock(properties);
            handProperties.SetTexture("_MainTex", hands[frame.sprite].texture);
            handRenderer.SetPropertyBlock(handProperties);
            currentSprite = frame.sprite;
        }
        if (tick != lastBreathTick || frame.idleWeight != lastWeight)
        {
            for (int i = 0; i < rest.Length; i++)
            {
                var p = PlayerTurnSpriteMotion.Breathe(rest[i], idleSeconds, frame.idleWeight);
                vertices[i] = new Vector3((p.x - 1040f) / 100f, (1760f - p.y) / 100f, 0f);
            }
            mesh.SetVertices(vertices);
            lastBreathTick = tick; lastWeight = frame.idleWeight;
        }
        LastFrameTime = frame.time; LastView = frame.view;
        var grip = PlayerTurnSpriteMotion.Breathe(frame.grip, idleSeconds, frame.idleWeight);
        cachedPose = new PlayerThrustRig.Pose { grip = new PlayerThrustRig.Point(grip.x, grip.y), weaponAngle = frame.angle };
        return cachedPose;
    }

    void EnsureProperties()
    {
        // MaterialPropertyBlock is not restored by editor script hot reload.
        if (properties == null) properties = CreateProperties();
        if (handProperties == null) handProperties = CreateProperties();
    }

    MaterialPropertyBlock CreateProperties()
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", tint);
        block.SetColor("_RendererColor", Color.white);
        block.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
        return block;
    }

    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
