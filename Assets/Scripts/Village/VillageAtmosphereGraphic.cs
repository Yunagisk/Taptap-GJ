using UnityEngine;
using UnityEngine.UI;

// One UI mesh and one small procedural cloud texture; no particle plugin required.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class VillageAtmosphereGraphic : MaskableGraphic
{
    static readonly Vector2[] Chimneys = {
        new Vector2(490, 348), new Vector2(1340, 369), new Vector2(1017, 632)
    };
    static readonly Vector2[] Mist = {
        new Vector2(110, 760), new Vector2(410, 963), new Vector2(995, 994),
        new Vector2(1630, 886), new Vector2(1850, 610), new Vector2(1270, 231)
    };
    Texture2D cloud;
    VillageAmbienceView view;
    float night;
    float fog;

    public override Texture mainTexture => cloud != null ? cloud : Texture2D.whiteTexture;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        cloud = CreateCloud();
        SetMaterialDirty();
    }

    public void Animate(VillageAmbienceView owner, float nightAmount, float fogAmount)
    {
        view = owner;
        night = nightAmount;
        fog = fogAmount;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (view == null || view.effectStrength <= 0f) return;
        float t = view.AnimationTime;
        if (view.driftingFog)
        {
            for (int i = 0; i < Mist.Length; i++)
            {
                float phase = t * 0.021f + i * 1.79f;
                Vector2 p = Mist[i] + new Vector2(Mathf.Sin(phase) * 105f, Mathf.Cos(phase * 0.63f) * 13f);
                float alpha = (0.075f + 0.02f * Mathf.Sin(phase * 1.3f)) * fog * view.effectStrength;
                Color tint = Color.Lerp(new Color(0.72f, 0.75f, 0.64f, alpha),
                    new Color(0.47f, 0.53f, 0.51f, alpha), night);
                Quad(vh, p, new Vector2(i == 5 ? 430 : 700, i == 5 ? 100 : 210), tint, i * 0.47f);
            }
        }
        if (view.chimneySmoke)
        {
            for (int c = 0; c < Chimneys.Length; c++)
            for (int puff = 0; puff < 7; puff++)
            {
                float age = Mathf.Repeat(t * 0.105f + puff / 7f + c * 0.29f, 1f);
                float wind = 16f + 5f * Mathf.Sin(t * 0.12f);
                Vector2 p = Chimneys[c] + new Vector2(age * wind + Mathf.Sin(age * 6f + c) * age * 7f,
                    -age * 90f - 3f);
                float size = Mathf.Lerp(9f, 45f, age);
                float alpha = Mathf.Sin(age * Mathf.PI) * Mathf.Lerp(0.18f, 0.11f, night) * view.effectStrength;
                Color tint = Color.Lerp(new Color(0.61f, 0.62f, 0.55f, alpha),
                    new Color(0.49f, 0.51f, 0.48f, alpha), night);
                Quad(vh, p, new Vector2(size, size * 1.35f), tint, age * 0.8f + c);
            }
        }
    }

    static void Quad(VertexHelper vh, Vector2 p, Vector2 size, Color tint, float rotation)
    {
        int start = vh.currentVertCount;
        float s = Mathf.Sin(rotation), c = Mathf.Cos(rotation);
        for (int i = 0; i < 4; i++)
        {
            float u = i == 1 || i == 2 ? 1f : 0f;
            float v = i >= 2 ? 1f : 0f;
            Vector2 offset = new Vector2((u - 0.5f) * size.x, (v - 0.5f) * size.y);
            Vector2 rotated = new Vector2(offset.x * c - offset.y * s, offset.x * s + offset.y * c);
            Vector2 screen = p + rotated;
            vh.AddVert(new Vector3(screen.x - 960f, 540f - screen.y), tint, new Vector2(u, v));
        }
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    static Texture2D CreateCloud()
    {
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            name = "Village watercolour cloud (runtime)", wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float r = Mathf.Sqrt(Mathf.Pow((u - 0.5f) * 2f, 2f) + Mathf.Pow((v - 0.5f) * 2f, 2f));
            float edge = 1f - Mathf.SmoothStep(0.1f, 1f, Mathf.Clamp01(r));
            float noise = 0.5f + 0.35f * Mathf.PerlinNoise(u * 5.5f + 7f, v * 5.5f + 13f)
                + 0.15f * Mathf.PerlinNoise(u * 15f, v * 15f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * edge * edge * noise));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    protected override void OnDestroy()
    {
        if (cloud != null) Destroy(cloud);
        base.OnDestroy();
    }
}
