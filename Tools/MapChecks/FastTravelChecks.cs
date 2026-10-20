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
        // Independent distance oracle over random known-node subgraphs.
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
                        if (!distance.ContainsKey(next) && map.State.visitedNodes.Contains(next)
                            && map.State.clearedNodes.Contains(next) && map.IsVisible(next))
                        { distance[next] = distance[current] + 1; queue.Enqueue(next); }
                }
                bool expected = target.id != map.State.currentNodeId && distance.ContainsKey(target.id);
                bool found = map.TryPlanFastTravel(target.id, out var quote);
                check(found == expected, "fast travel finds exactly the reachable, completed known destinations");
                if (!found) continue;
                check(quote.TimeCost == distance[target.id], "fast travel minimizes elapsed time rather than geometric distance");
                check(quote.OriginId == map.State.currentNodeId && quote.DestinationId == target.id
                    && quote.NodeIds.Distinct().Count() == quote.NodeIds.Count, "shortest route has correct endpoints and no loops");
                check(quote.NodeIds.Skip(1).All(id => map.State.visitedNodes.Contains(id) && map.State.clearedNodes.Contains(id)),
                    "no intermediate or destination node can trigger an unfinished encounter");
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
        check(!revived.CanFastTravel(revived.Layout.villageNodeId), "unfinished origin blocks fast travel");
        revived.State.revivedAtCheckpoint = true; revived.State.checkpointNodeId = "r1_a";
        check(revived.TryPlanFastTravel(revived.Layout.villageNodeId, out var escape) && revived.TryFastTravel(escape),
            "checkpoint revival can retreat along known completed nodes without replaying its battle");
        check(!revived.State.clearedNodes.Contains("r1_a"), "retreat never silently completes the checkpoint battle");
        var overflow = Known(layout()); overflow.World.time = int.MaxValue - 1;
        check(!overflow.CanFastTravel("r3_camp"), "time overflow is rejected during planning");
        check(WorldResourceRules.TimeLabel(0) == "第 1 天 早晨" && WorldResourceRules.TimeLabel(2) == "第 1 天 夜间"
            && WorldResourceRules.TimeLabel(3) == "第 2 天 早晨", "arrival labels handle dawn and the three daily periods");
    }
}
