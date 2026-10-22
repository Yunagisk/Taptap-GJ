using UnityEngine;
using UnityEngine.UI;

// Window light, drifting leaves and distant birds at the illustration's scale.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class VillageActivityGraphic : MaskableGraphic
{
    // Window interiors, clockwise from the top left. Leave the painted frames visible.
    static readonly Vector4[] Windows = {
        new Vector4(418, 434, 12, 23), new Vector4(389, 471, 10, 18),
        new Vector4(447, 483, 11, 17), new Vector4(546, 484, 11, 18),
        new Vector4(626, 450, 10, 19), new Vector4(677, 434, 11, 18),
        new Vector4(678, 490, 10, 15), new Vector4(546, 541, 11, 17),
        new Vector4(1170, 462, 13, 20), new Vector4(1139, 530, 11, 17),
        new Vector4(1210, 548, 11, 16), new Vector4(1280, 538, 10, 16),
        new Vector4(1326, 526, 11, 17), new Vector4(1524, 520, 11, 16),
        new Vector4(662, 698, 10, 12), new Vector4(625, 687, 8, 12),
        new Vector4(1055, 699, 10, 13), new Vector4(991, 772, 13, 14),
        new Vector4(1040, 774, 9, 13), new Vector4(1280, 728, 10, 12)
    };
    static readonly Vector2[] LeafSources = {
        new Vector2(125, 550), new Vector2(575, 674), new Vector2(1504, 355), new Vector2(1137, 710)
    };
    VillageAmbienceView view;
    float night;

    public void Animate(VillageAmbienceView owner, float nightAmount)
    {
        view = owner;
        night = nightAmount;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (view == null || view.effectStrength <= 0f) return;
        float t = view.AnimationTime;
        if (view.windowLights) DrawWindows(vh, t);
        if (view.fallingLeaves) DrawLeaves(vh, t);
        if (view.flyingBirds) DrawBirds(vh, t);
    }

    void DrawWindows(VertexHelper vh, float t)
    {
        float strength = night * view.effectStrength;
        if (strength <= 0.001f) return;
        for (int i = 0; i < Windows.Length; i++)
        {
            Vector4 w = Windows[i];
            float phase = i * 2.17f;
            float flicker = 0.65f + 0.2f * Mathf.Sin(t * 0.91f + phase)
                + 0.15f * Mathf.Sin(t * 0.37f + phase * 2.3f);
            // Glaze over the existing painted light; never pretend the baked light can switch off.
            Color light = new Color(1f, 0.68f, 0.32f, strength * (0.06f + flicker * 0.10f));
            Vector2 top = new Vector2(w.x, w.y);
            float slope = i < 8 ? (i < 3 ? 2f : -2f) : -1.5f;
            Polygon(vh, top, top + new Vector2(w.z, slope),
                top + new Vector2(w.z, w.w + slope), top + new Vector2(0, w.w), light);
        }
    }

    void DrawLeaves(VertexHelper vh, float t)
    {
        int count = night > 0.8f ? 2 : LeafSources.Length;
        for (int i = 0; i < count; i++)
        {
            float cycle = Mathf.Repeat(t + i * 6.7f, 31f);
            if (cycle > 7.5f) continue;
            float age = cycle / 7.5f;
            Vector2 p = LeafSources[i] + new Vector2(age * 158f + Mathf.Sin(cycle * 3f + i) * 5f,
                age * 73f + Mathf.Sin(cycle * 2f) * 9f);
            float alpha = Mathf.Sin(age * Mathf.PI) * Mathf.Lerp(0.58f, 0.33f, night) * view.effectStrength;
            Color ink = new Color(0.37f, 0.32f, 0.22f, alpha);
            float angle = cycle * 3f + i;
            Vector2 a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.7f;
            Vector2 b = new Vector2(-a.y, a.x) * (0.32f + 0.17f * Mathf.Sin(cycle * 5f));
            Polygon(vh, p - a, p + b, p + a, p - b, ink);
        }
    }

    void DrawBirds(VertexHelper vh, float t)
    {
        float visibility = (1f - night) * view.effectStrength;
        if (visibility <= 0.01f) return;
        float cycle = Mathf.Repeat(t + 4f, 43f);
        if (cycle > 11f) return;
        float age = cycle / 11f;
        for (int i = 0; i < 2; i++)
        {
            Vector2 p = new Vector2(Mathf.Lerp(244, 1550, age) - i * 34f,
                Mathf.Lerp(161, 92, age) + i * 17f + Mathf.Sin(cycle * 1.1f + i) * 5f);
            float flap = Mathf.Sin(t * 10f + i) * 3f;
            float alpha = Mathf.Clamp01(Mathf.Min(age, 1f - age) * 10f) * visibility * 0.7f;
            Color ink = new Color(0.16f, 0.19f, 0.16f, alpha);
            Line(vh, p + new Vector2(-5, -flap), p + new Vector2(-1, -1), 0.95f, ink);
            Line(vh, p + new Vector2(-1, -1), p + new Vector2(2, 0), 1.1f, ink);
            Line(vh, p + new Vector2(2, 0), p + new Vector2(5, -flap * 0.8f), 0.95f, ink);
        }
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * (width * 0.5f);
        Polygon(vh, a + n, b + n, b - n, a - n, color);
    }

    static void Polygon(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        Vertex(vh, a, color); Vertex(vh, b, color); Vertex(vh, c, color); Vertex(vh, d, color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    static void Vertex(VertexHelper vh, Vector2 p, Color color)
        => vh.AddVert(new Vector3(p.x - 960f, 540f - p.y, 0f), color, Vector2.zero);
}
