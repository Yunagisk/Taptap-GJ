using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class MapSceneRoad : MaskableGraphic
{
    public string fromNodeId;
    public string toNodeId;
    public RectTransform from;
    public RectTransform to;
    public RectTransform village;

    [Header("Hand-drawn road")]
    [Min(1)] public float lineWidth = 3.8f;
    [Range(0, 2)] public float bendAmount = 1;
    [Range(0, 1)] public float inkWear = 0.65f;
    [Min(0)] public float villagePadding = 12;
    [Tooltip("Ellipse size relative to the village click target. Enlarge for separate village artwork.")]
    public Vector2 villageBoundsScale = Vector2.one;
    public Vector2 villageBoundsOffset;
    [Tooltip("Extra sideways offsets for the first and second curve control points, in map pixels.")]
    public Vector2 controlOffsets;

    readonly List<Vector2> points = new List<Vector2>();
    Rect lastFrom, lastTo, lastVillage;
    int lastSeed;
    bool geometryChanged = true;
    bool hasPlannedPath;
    MapRoadLayoutGroup layoutGroup;
    float revealProgress = 1;
    bool revealFromStart = true;
    float pathLength;
    float wetPhase = 1;
    readonly List<UIVertex> wetVertices = new List<UIVertex>();
    readonly List<int> wetTriangles = new List<int>();

    public bool IsGeometryReady => layoutGroup == null || !layoutGroup.isActiveAndEnabled || hasPlannedPath;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        geometryChanged = true;
        // All vertices use the same map space as LineRoot, including after Canvas scaling.
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = Vector2.one * 0.5f;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
        layoutGroup = GetComponentInParent<MapRoadLayoutGroup>();
        if (layoutGroup != null) layoutGroup.Register(this);
    }

    void LateUpdate()
    {
        if (from == null || to == null)
        {
            if (points.Count > 0) { points.Clear(); SetVerticesDirty(); }
            geometryChanged = true;
            return;
        }
        if (village == null)
        {
            var view = GetComponentInParent<WorldMapSceneView>();
            if (view != null && view.mapContent != null)
                village = view.mapContent.Find("Node_village") as RectTransform;
        }
        if (layoutGroup == null)
        {
            layoutGroup = GetComponentInParent<MapRoadLayoutGroup>();
            if (layoutGroup != null) layoutGroup.Register(this);
        }
        if (layoutGroup != null && layoutGroup.isActiveAndEnabled) return;
        Rect a = Bounds(from), b = Bounds(to), avoid = village == null ? default : Bounds(village);
        int seed = MapRoadGeometry.Seed(fromNodeId, toNodeId);
        if (!geometryChanged && a == lastFrom && b == lastTo && avoid == lastVillage && seed == lastSeed) return;
        lastFrom = a;
        lastTo = b;
        lastVillage = avoid;
        lastSeed = seed;
        geometryChanged = false;
        SetVerticesDirty();
    }

    Rect Bounds(RectTransform target)
    {
        return MapUITransform.Bounds(target, rectTransform);
    }

    public bool TryGetPathBounds(RectTransform space, out Rect bounds)
    {
        bounds = default;
        if (points.Count < 2) return false;
        var matrix = MapUITransform.RelativeMatrix(rectTransform, space);
        Vector2 min = matrix.MultiplyPoint3x4(points[0]), max = min;
        foreach (var point in points)
        {
            Vector2 local = matrix.MultiplyPoint3x4(point);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    Rect EndpointBounds(RectTransform target, string id)
    {
        Rect bounds = Bounds(target);
        // Badge PNGs include transparent margins around their circular ink silhouette.
        if (target != village && id != null && !id.EndsWith("_boss"))
            bounds = new Rect(bounds.center - bounds.size * 0.39f, bounds.size * 0.78f);
        return bounds;
    }

    public MapRoadLayout.Request RoutingRequest()
    {
        if (from == null || to == null) return default;
        // Include maximum stroke radius, feathering and hand jitter in clearance.
        Rect? avoid = null;
        if (village != null)
        {
            Rect bounds = Bounds(village);
            Vector2 size = Vector2.Scale(bounds.size, villageBoundsScale);
            bounds = new Rect(bounds.center + villageBoundsOffset - size * 0.5f, size);
            avoid = MapRoadGeometry.Expand(bounds, villagePadding + lineWidth + 1);
        }
        return new MapRoadLayout.Request
        {
            fromId = fromNodeId, toId = toNodeId,
            from = EndpointBounds(from, fromNodeId), to = EndpointBounds(to, toNodeId), village = avoid,
            fromVillage = from == village, toVillage = to == village,
            width = lineWidth, bend = bendAmount, offsets = controlOffsets
        };
    }

    public void SetPlannedPath(List<Vector2> path)
    {
        points.Clear();
        points.AddRange(path);
        MeasurePath();
        hasPlannedPath = true;
        SetVerticesDirty();
    }

    void MeasurePath()
    {
        pathLength = 0;
        for (int i = 1; i < points.Count; i++) pathLength += Vector2.Distance(points[i - 1], points[i]);
    }

    public void SetRevealProgress(float progress, string originNodeId, float phase = -1)
    {
        float value = Mathf.Clamp01(progress);
        bool fromStart = string.IsNullOrEmpty(originNodeId) || originNodeId == fromNodeId;
        float wet = phase < 0 ? value : Mathf.Clamp01(phase);
        if (value == revealProgress && fromStart == revealFromStart && wet == wetPhase) return;
        revealProgress = value;
        revealFromStart = fromStart;
        wetPhase = wet;
        SetVerticesDirty();
    }

    public bool TryGetArrivalDirection(string targetNodeId, out Vector3 worldDirection)
    {
        worldDirection = Vector3.zero;
        if (targetNodeId != fromNodeId && targetNodeId != toNodeId) return false;
        bool targetAtEnd = targetNodeId == toNodeId;
        if (points.Count >= 2)
        {
            Vector2 tangent = targetAtEnd ? points[points.Count - 1] - points[points.Count - 2] : points[0] - points[1];
            worldDirection = transform.TransformVector(tangent);
        }
        else if (from != null && to != null)
            worldDirection = targetAtEnd ? to.position - from.position : from.position - to.position;
        return worldDirection.sqrMagnitude > 0.00001f;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (from == null || to == null || revealProgress <= 0) return;
        int seed = MapRoadGeometry.Seed(fromNodeId, toNodeId);
        if (layoutGroup != null && layoutGroup.isActiveAndEnabled)
        {
            if (!hasPlannedPath) return;
        }
        else
        {
            var r = RoutingRequest();
            if (!MapRoadGeometry.Build(r.from, r.to, r.village, r.fromVillage, r.toVillage,
                seed, bendAmount, controlOffsets, points)) return;
            MeasurePath();
        }

        // Temporary wet pigment sits underneath the permanent stroke, confined
        // to the planned path. Its outer radius stays within road clearance.
        if (revealProgress < 1)
        {
            MapInkRevealMesh.BuildRoad(points, lineWidth, revealProgress, wetPhase, revealFromStart,
                seed, color, wetVertices, wetTriangles);
            foreach (var vertex in wetVertices) mesh.AddVert(vertex);
            for (int i = 0; i < wetTriangles.Count; i += 3)
                mesh.AddTriangle(wetTriangles[i], wetTriangles[i + 1], wetTriangles[i + 2]);
        }
        int firstStrokeVertex = mesh.currentVertCount;
        const int columns = 7;
        float distance = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0) distance += Vector2.Distance(points[i - 1], points[i]);
            float revealOpacity = MapRevealMath.RoadOpacity(revealFromStart ? distance : pathLength - distance, pathLength, revealProgress);
            Vector2 tangent = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)];
            Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized;
            Vector2 center = points[i] + normal * ((MapRoadGeometry.Noise(distance / 7, seed) - 0.5f) * 0.55f);
            float half = lineWidth * 0.5f * Mathf.Lerp(0.78f, 1.12f, MapRoadGeometry.Noise(distance / 16, seed + 11));
            float wetness = MapInkRevealMesh.RoadWetness(revealFromStart ? distance : pathLength - distance,
                pathLength, revealProgress, wetPhase);
            half = MapInkRevealMesh.WetStrokeHalfWidth(half, wetness);
            for (int column = 0; column < columns; column++)
            {
                float across = column == 0 ? -half - 0.6f : column == 1 ? -half
                    : column == 2 ? -half * 0.36f : column == 3 ? 0
                    : column == 4 ? half * 0.38f : column == 5 ? half : half + 0.6f;
                float grain = MapRoadGeometry.Noise(distance / 2.8f, seed + column * 173 + 29);
                float dry = Mathf.Pow(Mathf.Clamp01((0.52f - grain) / 0.52f), 2);
                float alpha = column == 0 || column == columns - 1 ? 0
                    : (1 - inkWear * dry) * Mathf.Lerp(0.83f, 1, MapRoadGeometry.Noise(distance / 19, seed + 71));
                Color ink = color;
                ink.a *= alpha * revealOpacity;
                mesh.AddVert(center + normal * across, ink, Vector2.zero);
                if (i == 0 || column == 0) continue;
                int v = firstStrokeVertex + i * columns + column;
                mesh.AddTriangle(v - columns - 1, v - columns, v);
                mesh.AddTriangle(v - columns - 1, v, v - 1);
            }
        }
    }

    public void Refresh(MapProgression map)
    {
        gameObject.SetActive(map.IsRoadVisible(fromNodeId, toNodeId));
    }

    protected override void OnDestroy()
    {
        if (layoutGroup != null) layoutGroup.InvalidateHierarchy();
        base.OnDestroy();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        lineWidth = Mathf.Max(1, lineWidth);
        villagePadding = Mathf.Max(0, villagePadding);
        villageBoundsScale = Vector2.Max(Vector2.one * 0.1f, villageBoundsScale);
        raycastTarget = false;
        geometryChanged = true;
        SetVerticesDirty();
    }
#endif
}
