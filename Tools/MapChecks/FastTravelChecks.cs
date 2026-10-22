using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

static class FastTravelChecks
{
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    static string Snapshot(MapProgression map) => JsonSerializer.Serialize(new { map.State, map.World }, Json);
    static MapProgression Known(MapLayout layout)
    {
        var map = new MapProgression(layout, new MapState(), new WorldState(), 712);
        foreach (var node in layout.nodes)
        {
            if (!map.State.visitedNodes.Contains(node.id)) map.State.visitedNodes.Add(node.id);
            if (!map.State.clearedNodes.Contains(node.id)) map.State.clearedNodes.Add(node.id);
            if (!map.State.revealedNodes.Contains(node.id)) map.State.revealedNodes.Add(node.id);
        }
        foreach (var region in layout.regions)
            if (!map.State.spawnedBossNodes.Contains(region.bossNodeId)) map.State.spawnedBossNodes.Add(region.bossNodeId);
        return map;
    }
    public static void Run(Func<MapLayout> layout, Action<bool, string> check)
    {
        var random = new Random(993);
        // Independent distance oracle: only the final node may be unfinished.
        for (int fixture = 0; fixture < 36; fixture++)
        {
            var map = Known(layout());
            foreach (var node in map.Layout.nodes)
            {
                if (node.id == map.Layout.villageNodeId) continue;
                if (random.Next(4) == 0) map.State.visitedNodes.Remove(node.id);
                if (random.Next(6) == 0) map.State.clearedNodes.Remove(node.id);
            }
            string before = Snapshot(map);
            foreach (var target in map.Layout.nodes)
            {
                var distance = new Dictionary<string, int> { [map.State.currentNodeId] = 0 };
                var queue = new Queue<string>(); queue.Enqueue(map.State.currentNodeId);
                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();
                    foreach (string next in map.GetNode(current).neighbors)
                        if (!distance.ContainsKey(next) && map.IsVisible(next)
                            && (next == target.id || map.State.visitedNodes.Contains(next)
                                && map.State.clearedNodes.Contains(next)))
                        { distance[next] = distance[current] + 1; queue.Enqueue(next); }
                }
                bool expected = distance.TryGetValue(target.id, out int steps) && steps > 1;
                bool found = map.TryPlanFastTravel(target.id, out var quote);
                check(found == expected, "fast travel reaches visible nonadjacent destinations through completed intermediates");
                if (!found) continue;
                check(quote.TimeCost == distance[target.id], "fast travel minimizes elapsed time rather than geometric distance");
                check(quote.OriginId == map.State.currentNodeId && quote.DestinationId == target.id
                    && quote.NodeIds.Distinct().Count() == quote.NodeIds.Count, "shortest route has correct endpoints and no loops");
                check(quote.NodeIds.Skip(1).Take(quote.NodeIds.Count - 2)
                    .All(id => map.State.visitedNodes.Contains(id) && map.State.clearedNodes.Contains(id)),
                    "no intermediate node can skip an unfinished encounter");
            }
            check(Snapshot(map) == before, "planning every possible destination never changes any gameplay state");
        }
        var walk = Known(layout());
        var fast = Known(layout());
        walk.World.time = fast.World.time = 2;
        walk.World.sin = fast.World.sin = 98;
        // Allow normal outdoor time to spawn a new boss on the route.
        walk.State.spawnedBossNodes.Remove(walk.Layout.regions[1].bossNodeId);
        fast.State.spawnedBossNodes.Remove(fast.Layout.regions[1].bossNodeId);
        check(fast.TryPlanFastTravel("r3_camp", out var plan) && plan.TimeCost >= 4, "long known route is available");
        foreach (string id in plan.NodeIds.Skip(1))
            check(walk.TryMove(id, out var encounter) && encounter == null, "manual comparison traverses only completed nodes");
        check(fast.TryFastTravel(plan), "validated fast route commits once");
        check(Snapshot(fast) == Snapshot(walk), "fast movement matches every manual step including sin, dawn checkpoints and event clocks");
        check(fast.World.time == plan.ArrivalTime && WorldResourceRules.TimeLabel(plan.ArrivalTime) == WorldResourceRules.TimeLabel(fast.World),
            "arrival quote agrees with the actual shared day and period clock");
        string committed = Snapshot(fast);
        check(!fast.TryFastTravel(plan) && Snapshot(fast) == committed, "confirming the same route twice cannot move or charge twice");

        var adjacent = Known(layout());
        string neighbor = adjacent.GetNeighbors(adjacent.State.currentNodeId)[0];
        check(!adjacent.CanFastTravel(neighbor) && adjacent.TryMove(neighbor, out var adjacentRequest)
            && adjacentRequest == null && adjacent.World.time == 1,
            "adjacent completed nodes use ordinary one-unit movement without fast travel");
        check(!adjacent.CanFastTravel(adjacent.Layout.villageNodeId)
            && adjacent.TryMove(adjacent.Layout.villageNodeId, out _),
            "walking back to an adjacent node never requires fast travel");

        foreach (var kind in new[] { MapNodeKind.Battle, MapNodeKind.Event, MapNodeKind.Camp, MapNodeKind.Boss })
        {
            var arrive = Known(layout());
            var manual = Known(layout());
            string target = arrive.Layout.nodes.First(node => node.kind == kind
                && !arrive.GetNeighbors(arrive.State.currentNodeId).Contains(node.id)).id;
            foreach (var map in new[] { arrive, manual })
            {
                map.State.visitedNodes.Remove(target);
                map.State.clearedNodes.Remove(target);
                map.State.wildernessEvents = new WildernessLevelState { currentNodeId = map.State.currentNodeId };
            }
            string preview = Snapshot(arrive);
            check(arrive.TryPlanFastTravel(target, out var arrivalPlan) && Snapshot(arrive) == preview,
                "an unvisited visible " + kind + " can be previewed without marking it visited or complete");
            // Make the final step arrive exactly at dawn, with its request already pending.
            arrive.World.time = manual.World.time = (3 - arrivalPlan.TimeCost % 3) % 3;
            check(arrive.TryPlanFastTravel(target, out arrivalPlan), "unvisited arrival quote uses the updated departure time");
            MapNodeRequest manualRequest = null;
            foreach (string id in arrivalPlan.NodeIds.Skip(1))
                check(manual.TryMove(id, out manualRequest), "manual comparison reaches the unfinished destination");
            check(arrive.TryFastTravel(arrivalPlan, out var arrivalRequest) && arrivalRequest != null
                && arrivalRequest.nodeId == target && arrivalRequest.kind == kind
                && arrivalRequest.requestId == manualRequest.requestId,
                "fast travel creates exactly the destination's normal " + kind + " entry request");
            check(Snapshot(arrive) == Snapshot(manual) && arrive.World.time % 3 == 0
                && arrive.State.checkpointNodeId == target && !arrive.State.clearedNodes.Contains(target)
                && arrive.State.wildernessEvents.currentNodeId == target,
                "unfinished arrival matches walking, including dawn checkpoint, time, visited state and wilderness position");
            string arrived = Snapshot(arrive);
            check(!arrive.TryFastTravel(arrivalPlan, out _) && Snapshot(arrive) == arrived,
                "repeated confirmation cannot enter the unfinished destination or pay again");
        }

        var blocked = Known(layout());
        blocked.State.clearedNodes.Remove("r1_camp");
        check(blocked.CanFastTravel("r1_camp") && !blocked.CanFastTravel("r1_boss"),
            "an unfinished camp is a valid destination but cannot be crossed to reach its boss");
        blocked.State.revealedNodes.Remove("r1_camp");
        check(!blocked.CanFastTravel("r1_camp"), "unrevealed destinations cannot be reached through fast travel");

        var stale = Known(layout());
        check(stale.TryPlanFastTravel("r2_camp", out var old), "stale-quote fixture has a route");
        stale.World.time++;
        string unchanged = Snapshot(stale);
        check(!stale.TryFastTravel(old) && Snapshot(stale) == unchanged, "time changed since preview rejects the quote without partial movement");
        stale.World.time--;
        string removed = old.NodeIds[1]; stale.State.clearedNodes.Remove(removed);
        unchanged = Snapshot(stale);
        check(!stale.TryFastTravel(old) && Snapshot(stale) == unchanged, "invalidated intermediate node rejects the entire quote atomically");
        stale.State.clearedNodes.Add(removed);
        stale.State.pendingRequestId = "unfinished"; stale.State.pendingNodeId = stale.State.currentNodeId;
        unchanged = Snapshot(stale);
        check(!stale.CanFastTravel("r2_camp") && !stale.TryFastTravel(old) && Snapshot(stale) == unchanged,
            "pending encounters cannot be escaped through fast travel");

        var revived = Known(layout());
        revived.State.currentNodeId = "r1_a"; revived.State.clearedNodes.Remove("r1_a");
        check(!revived.CanFastTravel("r2_camp"), "unfinished origin blocks fast travel");
        revived.State.revivedAtCheckpoint = true; revived.State.checkpointNodeId = "r1_a";
        check(revived.TryPlanFastTravel("r2_camp", out var escape) && revived.TryFastTravel(escape),
            "checkpoint revival can retreat along known completed nodes without replaying its battle");
        check(!revived.State.clearedNodes.Contains("r1_a"), "retreat never silently completes the checkpoint battle");
        var overflow = Known(layout()); overflow.World.time = int.MaxValue - 1;
        check(!overflow.CanFastTravel("r3_camp"), "time overflow is rejected during planning");
        check(WorldResourceRules.TimeLabel(0) == "第 1 天 早晨" && WorldResourceRules.TimeLabel(2) == "第 1 天 夜间"
            && WorldResourceRules.TimeLabel(3) == "第 2 天 早晨", "arrival labels handle dawn and the three daily periods");
    }
}
