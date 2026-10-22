using UnityEngine;
using UnityEngine.UI;

// Mesh-only UI: no textures, material instances or raycast surface.
public sealed class BattleTargetingGraphic : MaskableGraphic
{
    const int Samples = 96;
    readonly Vector2[] points = new Vector2[Samples + 1];
    readonly float[] lengths = new float[Samples + 1];
    Vector2 start, tip;
    bool validTarget;
    float phase;
    VertexHelper mesh;
    public Vector2 Origin => start;
    public Vector2 Tip => tip;
    public bool HasTarget => validTarget;

    public void SetCurve(Vector2 from, Vector2 to, bool valid, float animationTime)
    {
        start = from; tip = to; validTarget = valid;
        phase = Mathf.Repeat(animationTime * 28, 23);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear(); mesh = helper;
        if ((tip - start).sqrMagnitude < 16) return;
        for (int i = 0; i <= Samples; i++)
        {
            BattleTargetingCurve.Evaluate(start.x, start.y, tip.x, tip.y,
                i / (float)Samples, out float x, out float y);
            points[i] = new Vector2(x, y);
            lengths[i] = i == 0 ? 0 : lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }
        Color border = validTarget ? new Color32(250, 197, 70, 245) : new Color32(185, 163, 123, 220);
        Color ink = new Color32(37, 34, 29, 255);
        float bodyEnd = Mathf.Max(0, lengths[Samples] - 21);
        // Dash lengths are measured along the curve, so stretching the bow does
        // not stretch individual dashes or change their thickness.
        for (float distance = -phase; distance < bodyEnd; distance += 23)
        {
            float from = Mathf.Max(0, distance), to = Mathf.Min(bodyEnd, distance + 14);
            if (to <= from) continue;
            if (validTarget) Stroke(from, to, 15, new Color32(245, 179, 45, 45));
            Stroke(from, to, 8, border);
            Stroke(from, to, 4, ink);
        }
        Vector2 tangent = (points[Samples] - points[Samples - 1]).normalized;
        Vector2 normal = new Vector2(-tangent.y, tangent.x);
        Triangle(tip + tangent * 2, tip - tangent * 29 + normal * 16,
            tip - tangent * 29 - normal * 16, border);
        Triangle(tip - tangent * 2, tip - tangent * 24 + normal * 10,
            tip - tangent * 24 - normal * 10, ink);
        float radius = 6;
        Triangle(start + Vector2.up * radius, start + Vector2.right * radius, start - Vector2.up * radius, border);
        Triangle(start + Vector2.up * radius, start - Vector2.up * radius, start - Vector2.right * radius, border);
    }

    void Stroke(float from, float to, float width, Color tint)
    {
        for (int i = 1; i <= Samples; i++)
        {
            float a = Mathf.Max(from, lengths[i - 1]), b = Mathf.Min(to, lengths[i]);
            if (b <= a || lengths[i] <= lengths[i - 1]) continue;
            Vector2 p = Vector2.Lerp(points[i - 1], points[i], (a - lengths[i - 1]) / (lengths[i] - lengths[i - 1]));
            Vector2 q = Vector2.Lerp(points[i - 1], points[i], (b - lengths[i - 1]) / (lengths[i] - lengths[i - 1]));
            Vector2 d = (q - p).normalized;
            Vector2 n = new Vector2(-d.y, d.x) * (width * .5f);
            int v = mesh.currentVertCount;
            mesh.AddVert(p - n, tint, Vector2.zero); mesh.AddVert(p + n, tint, Vector2.zero);
            mesh.AddVert(q + n, tint, Vector2.zero); mesh.AddVert(q - n, tint, Vector2.zero);
            mesh.AddTriangle(v, v + 1, v + 2); mesh.AddTriangle(v, v + 2, v + 3);
        }
    }
    void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int v = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero); mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddTriangle(v, v + 1, v + 2);
    }
}
