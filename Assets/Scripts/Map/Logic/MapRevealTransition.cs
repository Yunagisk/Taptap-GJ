using System;
using System.Collections.Generic;

// One-shot presentation data. It is kept in GameRoot, never in the saved run state.
public sealed class MapRevealTransition
{
    public sealed class Snapshot
    {
        internal readonly MapState owner;
        internal readonly HashSet<string> nodes = new HashSet<string>();
        internal readonly HashSet<string> roads = new HashSet<string>();
        internal readonly HashSet<string> cleared;

        internal Snapshot(MapProgression map)
        {
            owner = map.State;
            cleared = new HashSet<string>(map.State.clearedNodes);
            foreach (var node in map.Layout.nodes)
            {
                if (map.IsVisible(node.id)) nodes.Add(node.id);
                foreach (string neighbor in node.neighbors)
                    if (map.IsRoadVisible(node.id, neighbor)) roads.Add(RoadKey(node.id, neighbor));
            }
        }
    }

    public sealed class Road
    {
        public string fromId, toId, originId;
        public string TargetId => originId == fromId ? toId : fromId;
    }

    readonly MapState owner;
    public readonly string sourceId;
    public readonly List<string> nodeIds = new List<string>();
    public readonly List<Road> roads = new List<Road>();

    MapRevealTransition(MapState state, string source) { owner = state; sourceId = source; }

    public static string RoadKey(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
    public static Snapshot Capture(MapProgression map) => new Snapshot(map);
    public bool Matches(MapProgression map) => ReferenceEquals(owner, map.State) && map.State.clearedNodes.Contains(sourceId);

    public static MapRevealTransition AfterCompletion(MapProgression map, Snapshot before, string sourceId)
    {
        if (before == null || !ReferenceEquals(before.owner, map.State) || before.cleared.Contains(sourceId)
            || !map.State.clearedNodes.Contains(sourceId)) return null;
        var result = new MapRevealTransition(map.State, sourceId);
        foreach (var node in map.Layout.nodes)
        {
            if (map.IsVisible(node.id) && !before.nodes.Contains(node.id)) result.nodeIds.Add(node.id);
            foreach (string neighbor in node.neighbors)
            {
                if (string.CompareOrdinal(node.id, neighbor) >= 0 || !map.IsRoadVisible(node.id, neighbor)
                    || before.roads.Contains(RoadKey(node.id, neighbor))) continue;
                string origin = node.id == sourceId || neighbor == sourceId ? sourceId
                    : before.nodes.Contains(node.id) ? node.id : neighbor;
                result.roads.Add(new Road { fromId = node.id, toId = neighbor, originId = origin });
            }
        }
        // Even a dead end needs the completion crack, with no new roads to reveal.
        return result;
    }
}
