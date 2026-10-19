using System;
using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(200)]
public class MapRoadLayoutGroup : MonoBehaviour
{
    [Tooltip("Empty space between complete ink strokes, in map pixels.")]
    [Min(0)] public float roadSpacing = 4;

    MapSceneRoad[] roads = Array.Empty<MapSceneRoad>();
    MapRoadLayout.Request[] previous = Array.Empty<MapRoadLayout.Request>();
    MapRoadLayout.Request[] pending = Array.Empty<MapRoadLayout.Request>();
    bool hierarchyDirty = true;
    bool forceLayout = true;
    float previousSpacing;

    void OnEnable() { hierarchyDirty = true; forceLayout = true; }
    void OnTransformChildrenChanged() => InvalidateHierarchy();
    public void InvalidateHierarchy() => hierarchyDirty = true;

    public void Register(MapSceneRoad road)
    {
        foreach (var existing in roads) if (existing == road) return;
        hierarchyDirty = true;
    }

    void LateUpdate()
    {
        if (hierarchyDirty)
        {
            // Inactive/hidden edges reserve their space too, so discovering them
            // never changes existing road shapes or introduces new intersections.
            roads = GetComponentsInChildren<MapSceneRoad>(true);
            Array.Sort(roads, CompareRoads);
            previous = new MapRoadLayout.Request[roads.Length];
            pending = new MapRoadLayout.Request[roads.Length];
            hierarchyDirty = false;
            forceLayout = true;
        }
        bool changed = forceLayout || roadSpacing != previousSpacing;
        for (int i = 0; i < roads.Length; i++)
        {
            pending[i] = roads[i].RoutingRequest();
            changed |= !pending[i].SameAs(previous[i]);
        }
        if (!changed) return;
        MapRoadLayout.Route[] routes;
        int conflicts;
        UnityEngine.Profiling.Profiler.BeginSample("Map.RoadRouting");
        try { routes = MapRoadLayout.PlanCached(pending, roadSpacing, out conflicts); }
        finally { UnityEngine.Profiling.Profiler.EndSample(); }
        for (int i = 0; i < roads.Length; i++) roads[i].SetPlannedPath(routes[i].points);
        Array.Copy(pending, previous, pending.Length);
        previousSpacing = roadSpacing;
        forceLayout = false;
        if (conflicts > 0)
            Debug.LogWarning("Some map roads need more routing space. Adjust their control offsets or node spacing.", this);
    }

    static int CompareRoads(MapSceneRoad a, MapSceneRoad b)
    {
        bool aForward = string.CompareOrdinal(a.fromNodeId, a.toNodeId) <= 0;
        bool bForward = string.CompareOrdinal(b.fromNodeId, b.toNodeId) <= 0;
        int first = string.CompareOrdinal(aForward ? a.fromNodeId : a.toNodeId, bForward ? b.fromNodeId : b.toNodeId);
        return first != 0 ? first : string.CompareOrdinal(aForward ? a.toNodeId : a.fromNodeId, bForward ? b.toNodeId : b.fromNodeId);
    }

#if UNITY_EDITOR
    void OnValidate() { roadSpacing = Mathf.Max(0, roadSpacing); forceLayout = true; }
#endif
}
