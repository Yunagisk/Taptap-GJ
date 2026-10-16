using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A temporary sibling below the badge, so the wash can spread beyond the sprite
// without painting over its ink, intercepting clicks, or changing its UI anchors.
public class MapInkRevealGraphic : MaskableGraphic
{
    readonly List<UIVertex> vertices = new List<UIVertex>();
    readonly List<int> triangles = new List<int>();
    Vector2 direction;
    float phase = 1;
    int seed;
    RectTransform badge;
    float startedAt;
    internal const float Lifetime = 0.65f;

    public void Play(RectTransform target, Vector2 incomingDirection, int noiseSeed)
    {
        badge = target;
        raycastTarget = false;
        if (transform.parent != badge.parent) transform.SetParent(badge.parent, false);
        int index = badge.GetSiblingIndex();
        if (transform.GetSiblingIndex() < index) index--;
        transform.SetSiblingIndex(index);
        rectTransform.anchorMin = badge.anchorMin;
        rectTransform.anchorMax = badge.anchorMax;
        rectTransform.pivot = badge.pivot;
        rectTransform.sizeDelta = badge.sizeDelta;
        rectTransform.anchoredPosition3D = badge.anchoredPosition3D;
        rectTransform.localRotation = badge.localRotation;
        rectTransform.localScale = badge.localScale;
        direction = incomingDirection;
        phase = 0;
        seed = noiseSeed;
        startedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        SetVerticesDirty();
    }

    void Update()
    {
        if (badge == null || !badge.gameObject.activeInHierarchy) { Stop(); return; }
        phase = Mathf.Clamp01((Time.unscaledTime - startedAt) / Lifetime);
        if (phase >= 1) { Stop(); return; }
        SetVerticesDirty();
    }

    public void Stop()
    {
        phase = 1;
        badge = null;
        gameObject.SetActive(false);
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        MapInkRevealMesh.BuildNode(rectTransform.rect, direction, phase, seed,
            new Color32(38, 42, 35, 255), vertices, triangles);
        foreach (var vertex in vertices) mesh.AddVert(vertex);
        for (int i = 0; i < triangles.Count; i += 3)
            mesh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }
}

// Pure mesh generation, shared by the road's wet leading edge and node ripples.
// No textures, shaders, or random frame noise are required.
public static class MapInkRevealMesh
{
    const int Segments = 64;
    static readonly float[] WashRadii = { 0.28f, 0.62f, 0.84f, 1 };
    static readonly float[] WashAlpha = { 0.9f, 0.85f, 0.48f, 0 };
    static readonly float[] RingProfile = { 0, 0.65f, 1, 0.60f, 0 };
    static readonly float[] RoadProfile = { 0, 0.90f, 1, 1, 1, 0.90f, 0 };

    public static void BuildNode(Rect bounds, Vector2 direction, float phase, int seed, Color ink,
        List<UIVertex> vertices, List<int> triangles)
    {
        vertices.Clear();
        triangles.Clear();
        float envelope = MapRevealMath.WetEnvelope(phase);
        if (envelope <= 0 || bounds.width <= 0 || bounds.height <= 0) return;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();
        float size = Mathf.Min(bounds.width, bounds.height);
        float spread = MapRevealMath.EaseOutCubic(phase);
        Vector2 center = bounds.center - direction * (size * 0.13f * (1 - spread));
        AddWash(center, direction, size * Mathf.Lerp(0.20f, 0.64f, spread),
            seed, ink, envelope * 0.36f, vertices, triangles);
        AddRipple(center, direction, size, phase, seed + 137, ink, 0.52f, vertices, triangles);
        float secondPhase = Mathf.Clamp01((phase - 0.16f) / 0.84f);
        AddRipple(center, direction, size * 0.86f, secondPhase, seed + 719, ink, 0.38f, vertices, triangles);
        float thirdPhase = Mathf.Clamp01((phase - 0.30f) / 0.70f);
        AddRipple(center, direction, size * 0.74f, thirdPhase, seed + 1201, ink, 0.24f, vertices, triangles);
    }

    public static void BuildRoad(IList<Vector2> points, float lineWidth, float progress, float phase,
        bool fromStart, int seed, Color ink, List<UIVertex> vertices, List<int> triangles)
    {
        vertices.Clear();
        triangles.Clear();
        float envelope = MapRevealMath.WetEnvelope(phase);
        if (progress <= 0 || progress >= 1 || envelope <= 0 || points.Count < 2) return;
        float length = 0;
        for (int i = 1; i < points.Count; i++) length += Vector2.Distance(points[i - 1], points[i]);
        if (length <= 0.001f) return;
        float distance = 0;
        const int columns = 7;
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0) distance += Vector2.Distance(points[i - 1], points[i]);
            float along = fromStart ? distance : length - distance;
            float wet = RoadWetness(along, length, progress, phase);
            Vector2 tangent = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)];
            Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized;
            // Radius is below the existing 10px minimum road-to-road clearance.
            float radius = Mathf.Min(4.5f, lineWidth * 0.5f + 2.8f)
                * Mathf.Lerp(0.92f, 1, MapRoadGeometry.Noise(distance / 8, seed + 83));
            for (int column = 0; column < columns; column++)
            {
                float across = (column - 3) / 3f;
                float grain = Mathf.Lerp(0.6f, 1, MapRoadGeometry.Noise(distance / 3.5f, seed + column * 71));
                AddVertex(points[i] + normal * (across * radius), ink,
                    wet * grain * RoadProfile[column] * 0.68f, vertices);
                if (i == 0 || column == 0) continue;
                int v = i * columns + column;
                triangles.Add(v - columns - 1); triangles.Add(v - columns); triangles.Add(v);
                triangles.Add(v - columns - 1); triangles.Add(v); triangles.Add(v - 1);
            }
        }
    }

    public static float RoadWetness(float distance, float length, float progress, float phase)
    {
        if (progress <= 0 || progress >= 1) return 0;
        float feather = Mathf.Max(0.1f, Mathf.Min(12, length * 0.2f));
        float front = progress * (length + feather) - feather * 0.5f;
        float wake = Mathf.Clamp(length * 0.24f, 18, 42);
        float delta = distance - front;
        // A broad wet trail follows the nib; only a short soft lead reaches
        // ahead of it, keeping the direction of discovery easy to read.
        return MapRevealMath.WetEnvelope(phase)
            * (1 - MapRevealMath.Smooth(Mathf.Abs(delta) / (delta > 0 ? 8 : wake)));
    }

    public static float WetStrokeHalfWidth(float dryHalf, float wetness) =>
        Mathf.Lerp(dryHalf, Mathf.Max(dryHalf, Mathf.Min(3.4f, dryHalf * 1.9f)), wetness);

    static void AddWash(Vector2 center, Vector2 direction, float radius, int seed, Color ink,
        float opacity, List<UIVertex> vertices, List<int> triangles)
    {
        int first = vertices.Count;
        AddVertex(center, ink, opacity * 0.6f, vertices);
        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * (Mathf.PI * 2 / Segments);
            Vector2 radial = Radial(direction, angle);
            float noise = AngularNoise(angle, seed);
            float edge = radius * Mathf.Lerp(0.91f, 1.08f, noise);
            for (int column = 0; column < WashRadii.Length; column++)
            {
                AddVertex(center + radial * (edge * WashRadii[column]), ink,
                    opacity * WashAlpha[column] * Mathf.Lerp(0.75f, 1, noise), vertices);
                if (i == 0) continue;
                int v = first + 1 + i * WashRadii.Length + column;
                if (column == 0)
                {
                    triangles.Add(first); triangles.Add(v - WashRadii.Length); triangles.Add(v);
                }
                else
                {
                    triangles.Add(v - WashRadii.Length - 1); triangles.Add(v - WashRadii.Length); triangles.Add(v);
                    triangles.Add(v - WashRadii.Length - 1); triangles.Add(v); triangles.Add(v - 1);
                }
            }
        }
    }

    static void AddRipple(Vector2 center, Vector2 direction, float size, float phase, int seed, Color ink,
        float strength, List<UIVertex> vertices, List<int> triangles)
    {
        float opacity = MapRevealMath.WetEnvelope(phase) * strength;
        if (opacity <= 0) return;
        float radius = size * Mathf.Lerp(0.20f, 0.74f, MapRevealMath.EaseOutCubic(phase));
        float width = Mathf.Clamp(size * 0.036f, 1.8f, 4.5f);
        int first = vertices.Count;
        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * (Mathf.PI * 2 / Segments);
            Vector2 radial = Radial(direction, angle);
            float noise = AngularNoise(angle, seed);
            float edge = radius * Mathf.Lerp(0.95f, 1.05f, noise);
            float dry = Mathf.Lerp(0.45f, 1, MapRevealMath.Smooth((noise - 0.16f) / 0.65f));
            for (int column = 0; column < RingProfile.Length; column++)
            {
                AddVertex(center + radial * (edge + (column - 2) * width * 0.5f), ink,
                    opacity * RingProfile[column] * dry, vertices);
                if (i == 0 || column == 0) continue;
                int v = first + i * RingProfile.Length + column;
                triangles.Add(v - RingProfile.Length - 1); triangles.Add(v - RingProfile.Length); triangles.Add(v);
                triangles.Add(v - RingProfile.Length - 1); triangles.Add(v); triangles.Add(v - 1);
            }
        }
    }

    static Vector2 Radial(Vector2 direction, float angle) =>
        direction * Mathf.Cos(angle) + new Vector2(-direction.y, direction.x) * (Mathf.Sin(angle) * 0.90f);

    static float AngularNoise(float angle, int seed) =>
        MapRoadGeometry.Noise(Mathf.Cos(angle) * 3 + 11, seed) * 0.6f
        + MapRoadGeometry.Noise(Mathf.Sin(angle) * 4 + 17, seed + 313) * 0.4f;

    static void AddVertex(Vector2 position, Color ink, float opacity, List<UIVertex> vertices)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        ink.a *= Mathf.Clamp01(opacity);
        vertex.color = ink;
        vertices.Add(vertex);
    }
}
