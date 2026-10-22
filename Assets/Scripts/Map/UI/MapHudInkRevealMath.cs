using UnityEngine;

// Images and text sample the same front in their wing's coordinates. A short
// health fill therefore reveals at the same point as its full-width frame.
public struct MapHudInkRevealState
{
    public Rect bounds;
    public Vector2 origin;
    public float innerRadius;
    public bool radial;
    public float progress;
    public int seed;

    public float Opacity(Vector2 position)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;
        // Keep the node's irregular front, but trim its hidden lead-in/tail so
        // the longer HUD animation stays visibly active through its duration.
        float waterProgress = Mathf.Lerp(0.10f, 0.90f, progress);
        if (!radial) return MapRevealMath.WaterOpacity(position, bounds, Vector2.right, waterProgress, seed);
        var closest = new Vector2(Mathf.Clamp(origin.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(origin.y, bounds.yMin, bounds.yMax));
        float nearest = Mathf.Max(innerRadius, Vector2.Distance(origin, closest));
        Vector2 farthest = Vector2.Max(new Vector2(Mathf.Abs(origin.x - bounds.xMin), Mathf.Abs(origin.y - bounds.yMin)),
            new Vector2(Mathf.Abs(bounds.xMax - origin.x), Mathf.Abs(bounds.yMax - origin.y)));
        var radialBounds = new Rect(nearest, bounds.yMin, Mathf.Max(0.001f, farthest.magnitude - nearest), bounds.height);
        return MapRevealMath.WaterOpacity(new Vector2(Vector2.Distance(position, origin), position.y),
            radialBounds, Vector2.right, waterProgress, seed);
    }
}

// A reversal starts at the current front. Full open/close journeys both take
// duration seconds; interrupted journeys take only their remaining distance.
public struct MapHudInkTimeline
{
    public float Progress { get; private set; }
    float from, destination, elapsed, travelDuration;

    public void Reset() { this = default; }

    public float Step(bool expanded, float deltaTime, float duration)
    {
        float next = expanded ? 1 : 0;
        if (destination != next)
        {
            from = Progress;
            destination = next;
            elapsed = 0;
            travelDuration = Mathf.Max(0.001f, Mathf.Abs(destination - from) * Mathf.Max(0.01f, duration));
        }
        if (Progress == destination) return Progress;
        elapsed += Mathf.Max(0, deltaTime);
        float remaining = 1 - Mathf.Clamp01(elapsed / travelDuration);
        Progress = Mathf.Lerp(from, destination, 1 - remaining * remaining);
        return Progress;
    }
}
