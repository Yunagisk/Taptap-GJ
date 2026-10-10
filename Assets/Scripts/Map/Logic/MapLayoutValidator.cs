using System;
using System.Collections.Generic;

public static class MapLayoutValidator
{
    public static void Validate(MapLayout layout)
    {
        Require(layout != null && !string.IsNullOrEmpty(layout.id), "A template needs an ID.");
        Require(layout.regions.Count == 3 && layout.nodes.Count == 28, "Whitebox must contain 3 regions and 28 nodes.");
        Require(layout.sinThreshold > 0 && layout.initialSin >= 0 && layout.initialSin < layout.sinThreshold
            && layout.sinPerMoveMin > 0 && layout.sinPerMoveMax >= layout.sinPerMoveMin, "Invalid sin configuration.");
        Require(layout.bossNodeClearance >= 0 && layout.bossApproachDistance >= 0, "Invalid boss clearance.");
        var nodes = new Dictionary<string, MapNodeDefinition>();
        foreach (var node in layout.nodes)
        {
            Require(node != null && !string.IsNullOrEmpty(node.id) && !nodes.ContainsKey(node.id), "Node IDs must be unique.");
            nodes.Add(node.id, node);
        }
        Require(nodes.ContainsKey(layout.villageNodeId) && nodes[layout.villageNodeId].kind == MapNodeKind.Village,
            "The village must exist.");
        foreach (var node in nodes.Values)
        {
            Require(node.neighbors.Count > 0 && (node.kind == MapNodeKind.Village || node.neighbors.Count <= 5), "Invalid degree: " + node.id);
            var seen = new HashSet<string>();
            foreach (var neighbor in node.neighbors)
            {
                Require(neighbor != node.id && seen.Add(neighbor) && nodes.ContainsKey(neighbor), "Invalid road: " + node.id);
                Require(nodes[neighbor].neighbors.Contains(node.id), "Roads must be bidirectional: " + node.id);
            }
        }
        var distances = Distances(nodes, layout.villageNodeId, false);
        Require(distances.Count == nodes.Count, "Every node must be reachable from the village.");
        var battleCosts = Distances(nodes, layout.villageNodeId, true);
        var regionIds = new HashSet<int>();
        int initialBosses = 0;
        foreach (var region in layout.regions)
        {
            Require(region.id > 0 && regionIds.Add(region.id), "Region IDs must be unique and positive.");
            Require(nodes.ContainsKey(region.bossNodeId), "Missing boss node.");
            int count = 0, battles = 0, camps = 0, events = 0, bosses = 0;
            foreach (var node in nodes.Values)
            {
                if (node.region != region.id) continue;
                count++;
                if (node.kind == MapNodeKind.Battle) battles++;
                if (node.kind == MapNodeKind.Camp) camps++;
                if (node.kind == MapNodeKind.Event) events++;
                if (node.kind == MapNodeKind.Boss) bosses++;
            }
            Require(count == 9 && battles == 5 && camps == 1 && events == 2 && bosses == 1, "Invalid region composition.");
            Require(nodes[region.bossNodeId].kind == MapNodeKind.Boss && nodes[region.bossNodeId].region == region.id,
                "Boss belongs to the wrong region.");
            var boss = nodes[region.bossNodeId];
            Require(boss.neighbors.Count == 1, "A boss must have exactly one entrance: " + boss.id);
            var approach = nodes[boss.neighbors[0]];
            Require(approach.neighbors.Count == 2, "The final approach to a boss must not branch: " + approach.id);
            Require(approach.kind == MapNodeKind.Camp && approach.region == region.id,
                "The final approach must be the boss region's camp.");
            Require(distances[region.bossNodeId] == 5, "Shortest boss route must contain 5 nodes.");
            Require(battleCosts[region.bossNodeId] >= 3, "A boss route bypasses required ordinary battles.");
            // Count only this region's fights so cross-region roads cannot reuse another boss's quota.
            var regionalBattles = Distances(nodes, layout.villageNodeId, true, region.id);
            Require(regionalBattles[boss.id] >= 3, "A cross-region route bypasses required regional battles: " + boss.id);
            foreach (var other in nodes.Values)
            {
                if (other.id == boss.id) continue;
                float clearance = other.id == approach.id ? layout.bossApproachDistance : layout.bossNodeClearance;
                float dx = other.x - boss.x, dy = other.y - boss.y;
                Require(dx * dx + dy * dy >= clearance * clearance,
                    "A node is too close to a boss: " + boss.id + "/" + other.id);
            }
            if (region.startsSpawned)
            {
                initialBosses++;
                Require(region.bossKey == "sloth", "The tutorial boss must be Sloth.");
            }
        }
        Require(initialBosses == 1, "Only Sloth starts spawned.");
        foreach (var node in nodes.Values)
            Require(node.id == layout.villageNodeId ? node.region == 0 : regionIds.Contains(node.region), "Unassigned node region.");
    }

    // Dijkstra also validates the minimum battle count over every possible route.
    static Dictionary<string, int> Distances(Dictionary<string, MapNodeDefinition> nodes, string start, bool countBattles, int battleRegion = 0)
    {
        var result = new Dictionary<string, int> { [start] = 0 };
        var visited = new HashSet<string>();
        while (true)
        {
            string current = null;
            int best = int.MaxValue;
            foreach (var pair in result)
                if (!visited.Contains(pair.Key) && pair.Value < best) { current = pair.Key; best = pair.Value; }
            if (current == null) break;
            visited.Add(current);
            foreach (string next in nodes[current].neighbors)
            {
                int cost = countBattles ? (nodes[next].kind == MapNodeKind.Battle
                    && (battleRegion == 0 || nodes[next].region == battleRegion) ? 1 : 0) : 1;
                int candidate = best + cost;
                if (!result.ContainsKey(next) || candidate < result[next]) result[next] = candidate;
            }
        }
        return result;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
