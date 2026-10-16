using System;
using System.Collections.Generic;
using UnityEngine;

// Plans all edges together, including undiscovered ones. No map state is read or changed.
public static class MapRoadLayout
{
    public struct Request
    {
        public string fromId, toId;
        public Rect from, to;
        public Rect? village;
        public bool fromVillage, toVillage;
        public float width, bend;
        public Vector2 offsets;

        public bool SameAs(Request other) => fromId == other.fromId && toId == other.toId
            && from == other.from && to == other.to && village.HasValue == other.village.HasValue
            && (!village.HasValue || village.Value == other.village.Value)
            && fromVillage == other.fromVillage && toVillage == other.toVillage
            && width == other.width && bend == other.bend && offsets == other.offsets;
    }

    public sealed class Route
    {
        public readonly List<Vector2> points;
        public readonly Rect bounds;
        public readonly float radius;

        public Route(List<Vector2> path, float width)
        {
            points = path;
            // Enclose width variation, feathered edges and the small ink-center jitter.
            radius = width * 0.56f + 0.9f;
            if (path.Count == 0) { bounds = default; return; }
            Vector2 min = path[0], max = min;
            foreach (var p in path) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }

    static readonly float[] Bends = { -2, -1.5f, -1, -0.6f, -0.25f, 0, 0.25f, 0.6f, 1, 1.5f, 2 };

    public static Route[] Plan(IList<Request> requests, float spacing, out int remainingConflicts)
    {
        int count = requests.Count;
        var routes = new Route[count];
        var candidates = new List<Route>[count];
        var nodeBounds = new Dictionary<string, Rect>();
        var villages = new HashSet<string>();
        foreach (var r in requests)
        {
            if (!string.IsNullOrEmpty(r.fromId)) nodeBounds[r.fromId] = r.from;
            if (!string.IsNullOrEmpty(r.toId)) nodeBounds[r.toId] = r.to;
            if (r.fromVillage) villages.Add(r.fromId);
            if (r.toVillage) villages.Add(r.toId);
        }
        for (int i = 0; i < count; i++)
        {
            var r = requests[i];
            var points = new List<Vector2>();
            MapRoadGeometry.Build(r.from, r.to, r.village, r.fromVillage, r.toVillage,
                MapRoadGeometry.Seed(r.fromId, r.toId), r.bend, r.offsets, points);
            routes[i] = new Route(points, r.width);
            if (points.Count < 2 || !FitsNodes(routes[i], r, nodeBounds, villages))
            {
                candidates[i] = Candidates(r, nodeBounds, villages);
                if (candidates[i].Count > 0) routes[i] = candidates[i][0];
            }
        }

        // Coordinate descent: move the most congested edge first, and accept only
        // improvements. Candidate order favors the original hand-drawn shape.
        var congestion = new int[count];
        for (int pass = 0; pass < 12; pass++)
        {
            Array.Clear(congestion, 0, count);
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    if (Conflict(routes[i], routes[j], spacing)) { congestion[i]++; congestion[j]++; }
            var order = new List<int>();
            for (int i = 0; i < count; i++) if (congestion[i] > 0) order.Add(i);
            if (order.Count == 0) break;
            order.Sort((a, b) => congestion[b] != congestion[a] ? congestion[b].CompareTo(congestion[a]) : a.CompareTo(b));
            bool improved = false;
            foreach (int index in order)
            {
                int bestCost = ConflictsWith(routes[index], routes, index, spacing);
                if (bestCost == 0) continue;
                if (candidates[index] == null) candidates[index] = Candidates(requests[index], nodeBounds, villages);
                Route best = routes[index];
                foreach (var candidate in candidates[index])
                {
                    int cost = ConflictsWith(candidate, routes, index, spacing);
                    if (cost >= bestCost) continue;
                    best = candidate;
                    bestCost = cost;
                    if (cost == 0) break;
                }
                if (best == routes[index]) continue;
                routes[index] = best;
                improved = true;
            }
            if (!improved)
            {
                // Two branches leaving the same node can need to move together:
                // changing either one alone would still collide with the other.
                for (int i = 0; i < count && !improved; i++)
                    for (int j = i + 1; j < count && !improved; j++)
                    {
                        if (!Conflict(routes[i], routes[j], spacing)) continue;
                        if (candidates[i] == null) candidates[i] = Candidates(requests[i], nodeBounds, villages);
                        if (candidates[j] == null) candidates[j] = Candidates(requests[j], nodeBounds, villages);
                        var firstOptions = candidates[i].FindAll(route => ConflictsWith(route, routes, i, spacing, j) == 0);
                        var secondOptions = candidates[j].FindAll(route => ConflictsWith(route, routes, j, spacing, i) == 0);
                        foreach (var a in firstOptions)
                        {
                            foreach (var b in secondOptions)
                            {
                                if (Conflict(a, b, spacing)) continue;
                                routes[i] = a;
                                routes[j] = b;
                                improved = true;
                                break;
                            }
                            if (improved) break;
                        }
                    }
                if (!improved) break;
            }
        }
        remainingConflicts = 0;
        for (int i = 0; i < count; i++)
        {
            if (routes[i].points.Count < 2 || !FitsNodes(routes[i], requests[i], nodeBounds, villages)) remainingConflicts++;
            for (int j = i + 1; j < count; j++)
                if (Conflict(routes[i], routes[j], spacing)) remainingConflicts++;
        }
        return routes;
    }

    static List<Route> Candidates(Request r, Dictionary<string, Rect> nodeBounds, HashSet<string> villages)
    {
        int seed = MapRoadGeometry.Seed(r.fromId, r.toId);
        float bend = Mathf.Clamp(Vector2.Distance(r.from.center, r.to.center) * 0.19f, 12, 46) * Mathf.Max(0.35f, r.bend);
        float sign = (seed & 1) == 0 ? 1 : -1;
        Vector2 original = new Vector2(sign * bend, sign * bend * ((seed & 2) != 0 ? -0.8f : 0.75f));
        var controls = new List<Vector2>();
        foreach (float first in Bends)
            foreach (float second in Bends) controls.Add(new Vector2(first * bend, second * bend));
        controls.Sort((a, b) =>
        {
            int distance = (a - original).sqrMagnitude.CompareTo((b - original).sqrMagnitude);
            return distance != 0 ? distance : a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
        });
        var result = new List<Route>();
        foreach (var control in controls)
        {
            var points = new List<Vector2>();
            if (MapRoadGeometry.TryCurve(r.from, r.to, r.village, r.fromVillage, r.toVillage,
                control.x + r.offsets.x, control.y + r.offsets.y, points))
            {
                var route = new Route(points, r.width);
                if (FitsNodes(route, r, nodeBounds, villages)) result.Add(route);
            }
        }
        return result;
    }

    static bool FitsNodes(Route route, Request request, Dictionary<string, Rect> nodeBounds, HashSet<string> villages)
    {
        foreach (var node in nodeBounds)
        {
            if (node.Key == request.fromId || node.Key == request.toId || villages.Contains(node.Key)) continue;
            Rect avoid = MapRoadGeometry.Expand(node.Value, route.radius + 2);
            if (!avoid.Overlaps(route.bounds)) continue;
            if (!MapRoadGeometry.IsClearOfRectangle(route.points, avoid)) return false;
        }
        return true;
    }

    static int ConflictsWith(Route candidate, Route[] routes, int ownIndex, float spacing, int ignoredIndex = -1)
    {
        int count = 0;
        for (int i = 0; i < routes.Length; i++)
            if (i != ownIndex && i != ignoredIndex && Conflict(candidate, routes[i], spacing)) count++;
        return count;
    }

    public static bool Conflict(Route a, Route b, float spacing)
    {
        float gap = a.radius + b.radius + Mathf.Max(0, spacing);
        if (!MapRoadGeometry.Expand(a.bounds, gap).Overlaps(b.bounds)) return false;
        float squaredGap = gap * gap;
        for (int i = 1; i < a.points.Count; i++)
        {
            Vector2 p = a.points[i - 1], q = a.points[i];
            float xMin = Mathf.Min(p.x, q.x) - gap, xMax = Mathf.Max(p.x, q.x) + gap;
            float yMin = Mathf.Min(p.y, q.y) - gap, yMax = Mathf.Max(p.y, q.y) + gap;
            for (int j = 1; j < b.points.Count; j++)
            {
                Vector2 u = b.points[j - 1], v = b.points[j];
                if (Mathf.Max(u.x, v.x) < xMin || Mathf.Min(u.x, v.x) > xMax
                    || Mathf.Max(u.y, v.y) < yMin || Mathf.Min(u.y, v.y) > yMax) continue;
                if (MapRoadGeometry.SegmentDistanceSquared(p, q, u, v) < squaredGap) return true;
            }
        }
        return false;
    }
}
