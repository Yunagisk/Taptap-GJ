using UnityEngine;

// A fixed neck seam and a travelling brim deform the cloth, rather than moving
// the entire portrait. Coordinates are normalized, with Y pointing upwards.
public static class MapHudHoodMotion
{
    public const int CageSize = 6;
    public const int HeadPlaneCount = 6;
    // Outer crown/rear silhouette of the worn cloth, rather than its opening.
    // Clipping the opening would cut the shaded face as well as the hair.
    static readonly Vector2[] headEnvelope =
    {
        new Vector2(.16f,.52f), new Vector2(.265f,.765f),
        new Vector2(.48f,.90f), new Vector2(.56f,.90f),
        new Vector2(.69f,.80f), new Vector2(.78f,.665f),
        new Vector2(.87f,.49f)
    };
    static readonly Vector2[][] poses =
    {
        new[] { new Vector2(.36f,.16f), new Vector2(.87f,.43f), new Vector2(.74f,.79f), new Vector2(.44f,.92f), new Vector2(.12f,.70f), new Vector2(.06f,.27f) },
        new[] { new Vector2(.36f,.16f), new Vector2(.80f,.68f), new Vector2(.61f,.86f), new Vector2(.37f,.91f), new Vector2(.13f,.70f), new Vector2(.105f,.32f) },
        // Clear the crown before folding downward. Keep the rear panel close
        // to the nape instead of pulling the whole hood to the far shoulder.
        new[] { new Vector2(.36f,.16f), new Vector2(.58f,.90f), new Vector2(.43f,.92f), new Vector2(.27f,.84f), new Vector2(.17f,.61f), new Vector2(.17f,.33f) },
        new[] { new Vector2(.36f,.16f), new Vector2(.40f,.52f), new Vector2(.32f,.56f), new Vector2(.23f,.50f), new Vector2(.18f,.36f), new Vector2(.23f,.22f) },
        new[] { new Vector2(.36f,.16f), new Vector2(.43f,.28f), new Vector2(.36f,.35f), new Vector2(.24f,.35f), new Vector2(.17f,.28f), new Vector2(.23f,.20f) }
    };

    public static Vector2 CagePoint(float progress, int index)
    {
        float position = Mathf.Clamp01(progress) * (poses.Length - 1);
        int frame = Mathf.Min((int)position, poses.Length - 2);
        float t = position - frame;
        Vector2 a = poses[frame][index], b = poses[frame + 1][index];
        Vector2 before = frame == 0 ? a * 2 - b : poses[frame - 1][index];
        Vector2 after = frame + 2 == poses.Length ? b * 2 - a : poses[frame + 2][index];
        // Catmull-Rom keeps speed continuous at the crown and the falling fold.
        return .5f * (2 * a + (-before + b) * t
            + (2 * before - 5 * a + 4 * b - after) * t * t
            + (-before + 3 * a - 3 * b + after) * t * t * t);
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // Mean-value cage coordinates are cached once per mesh. They reproduce the
    // original image exactly at progress 0 and keep the cloth continuous.
    public static void WriteWeights(Vector2 point, float[] weights, int offset)
    {
        for (int i = 0; i < CageSize; i++) weights[offset + i] = 0;
        for (int i = 0; i < CageSize; i++)
        {
            Vector2 a = poses[0][i] - point;
            Vector2 b = poses[0][(i + 1) % CageSize] - point;
            float distance = a.magnitude;
            if (distance < .000001f) { weights[offset + i] = 1; return; }
            if (Mathf.Abs(Cross(a, b)) < .0000001f && Vector2.Dot(a, b) <= 0)
            {
                float other = b.magnitude;
                weights[offset + i] = other / (distance + other);
                weights[offset + (i + 1) % CageSize] = distance / (distance + other);
                return;
            }
        }
        float sum = 0;
        for (int i = 0; i < CageSize; i++)
        {
            Vector2 a = poses[0][(i + CageSize - 1) % CageSize] - point;
            Vector2 b = poses[0][i] - point;
            Vector2 c = poses[0][(i + 1) % CageSize] - point;
            float first = Cross(a, b) / (a.magnitude * b.magnitude + Vector2.Dot(a, b));
            float second = Cross(b, c) / (b.magnitude * c.magnitude + Vector2.Dot(b, c));
            float weight = (first + second) / b.magnitude;
            weights[offset + i] = weight;
            sum += weight;
        }
        for (int i = 0; i < CageSize; i++) weights[offset + i] /= sum;
    }

    public static Vector2 Deform(float[] weights, int offset, Vector2[] cage)
    {
        Vector2 result = Vector2.zero;
        for (int i = 0; i < CageSize; i++) result += cage[i] * weights[offset + i];
        return result;
    }

    // The lowered rear flap wraps over the short locks at the jaw. Move cloth
    // over them instead of cutting a hole into the face/neck image.
    public static Vector2 FitCloth(Vector2 source, Vector2 position, float progress)
    {
        float fold = Smooth((source.x - .20f) / .15f) * (1 - Smooth((source.x - .50f) / .35f))
            * Smooth((source.y - .17f) / .13f) * (1 - Smooth((source.y - .40f) / .30f));
        position.x += .055f * fold * (1 - Smooth((progress - .05f) / .25f));
        return position;
    }

    // Hand cloth to the rear from its back edge toward its brim. A narrow
    // moving boundary reads as occlusion rather than a translucent whole hood.
    public static float FrontOpacity(float progress) => progress >= .58f ? 0 : 1;

    public static float FrontOpacity(Vector2 source, float progress)
    {
        float handover = Smooth((progress - .42f) / .16f);
        float edge = Mathf.Lerp(-.03f, 1.03f, handover);
        return Smooth((source.x - edge + .015f) / .03f);
    }

    // Tuck the protruding hair inside the cloth's outer volume. Let the front
    // and crown go first; release the rear tips after the hood clears the head.
    // The six inward half-planes leave the entire face and collar untouched.
    public static void WriteHeadClipPlanes(float progress, Vector3[] planes)
    {
        float p = Mathf.Clamp01(progress);
        for (int i = 0; i < HeadPlaneCount; i++)
        {
            Vector2 start = headEnvelope[i], edge = headEnvelope[i + 1] - start;
            Vector2 normal = new Vector2(edge.y, -edge.x).normalized;
            float release = i < 2 ? Smooth((p - .40f) / .24f)
                : i == 2 ? Smooth((p - .20f) / .30f) : Smooth((p - .20f) / .26f);
            // Rear folds are concave; allow extra room for their edge and for
            // the 64-cell mesh's interpolation instead of using the outer hull.
            float inset = i < 2 ? .040f : .014f;
            planes[i] = new Vector3(normal.x, normal.y,
                -Vector2.Dot(normal, start) - inset + .22f * release);
        }
    }

    public static float HeadOpacity(Vector2 point, Vector3[] planes)
    {
        float distance = float.PositiveInfinity;
        for (int i = 0; i < HeadPlaneCount; i++)
            distance = Mathf.Min(distance, planes[i].x * point.x + planes[i].y * point.y + planes[i].z);
        return Smooth((distance + .004f) / .008f);
    }

    public static float DiskOpacity(Vector2 point, float radius = .445f)
        => 1 - Smooth((Vector2.Distance(point, new Vector2(.5f, .5f)) - radius + .008f) / .008f);

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
}

// Critically damped motion retains velocity if the pointer leaves mid-action.
// duration is approximately the time to reach 99% of a full journey.
public struct MapHudHoodTimeline
{
    public float Progress { get; private set; }
    float velocity;
    public void Reset() { this = default; }
    public float Step(bool expanded, float deltaTime, float duration)
    {
        float dt = Mathf.Max(0, deltaTime);
        if (dt == 0) return Progress;
        float target = expanded ? 1 : 0;
        float omega = 7 / Mathf.Max(.05f, duration);
        float offset = Progress - target;
        float decay = Mathf.Exp(-omega * dt);
        float travel = (velocity + omega * offset) * dt;
        velocity = (velocity - omega * travel) * decay;
        Progress = Mathf.Clamp01(target + (offset + travel) * decay);
        if (Mathf.Abs(Progress - target) < .0001f && Mathf.Abs(velocity) < .001f)
        { Progress = target; velocity = 0; }
        return Progress;
    }
}
