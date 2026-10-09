using System;
using System.IO;
using System.Text.Json;

static class Program
{
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    static int checks;
    static string layoutPath = Path.Combine(AppContext.BaseDirectory, "ThreeSinWhitebox.json");

    static MapLayout Layout() => JsonSerializer.Deserialize<MapLayout>(File.ReadAllText(layoutPath), Json);
    static MapProgression NewMap(int seed = 12345) => new MapProgression(Layout(), new MapState(), new WorldState(), seed);

    static void Check(bool result, string name)
    {
        if (!result) throw new Exception("FAILED: " + name);
        checks++;
    }

    static MapNodeRequest Move(MapProgression map, string id)
    {
        int oldTime = map.World.time;
        Check(map.TryMove(id, out var request), "move " + id);
        Check(map.World.time == oldTime + 1, "exactly one time unit per edge");
        return request;
    }

    static void Visit(MapProgression map, string id)
    {
        var request = Move(map, id);
        if (request != null) Complete(map, request.requestId);
    }

    static bool Complete(MapProgression map, string requestId)
    {
        int oldTime = map.World.time;
        int oldSin = map.World.sin;
        int oldRolls = map.State.sinRollCount;
        int oldBosses = map.State.spawnedBossNodes.Count;
        Check(map.TryCompleteNode(requestId), "complete " + requestId);
        Check(map.World.time == oldTime + 1, "exactly one time unit per node completion");
        Check(map.World.sin == oldSin && map.State.sinRollCount == oldRolls
            && map.State.spawnedBossNodes.Count == oldBosses, "completion does not change movement-based sin or boss spawning");
        Check(!map.TryCompleteNode(requestId) && map.World.time == oldTime + 1, "repeated completion never charges time twice");
        return true;
    }

    static void Main(string[] args)
    {
        if (args.Length > 0) layoutPath = Path.GetFullPath(args[0]);
        CheckMorningRevival();
        var map = NewMap();
        foreach (string weapon in NewRunFactory.StartingWeapons)
        {
            var fresh = NewRunFactory.Create(Layout(), weapon, 67890);
            Check(fresh.player.currentWeaponId == weapon && fresh.inventory.weapons.Count == 1 && fresh.inventory.weapons[0] == weapon,
                "new game owns only its chosen weapon");
            Check(fresh.shop.availableWeaponIds.Count == 3 && !fresh.shop.availableWeaponIds.Contains(weapon), "chosen weapon removed from new shop stock");
            Check(fresh.player.hp == 100 && fresh.player.resolve == 1 && fresh.player.lanternCharges == 2, "new game initializes player resources");
            Check(fresh.world.time == 0 && fresh.world.sin == 40 && fresh.map.currentNodeId == "village", "new game starts at village at time zero");
            Check(fresh.map.revealedNodes.Count == 5 && fresh.map.clearedNodes.Count == 1 && fresh.map.spawnedBossNodes.Count == 1
                && fresh.map.defeatedBossNodes.Count == 0 && fresh.inventory.items.Count == 0, "new game resets all progression and inventory");
            fresh.player.hp = 1;
            fresh.inventory.items.Add(new ItemStack("old-run-item", 5));
            fresh.shop.availableWeaponIds.Clear();
            var next = NewRunFactory.Create(Layout(), weapon, 67891);
            Check(next.player.hp == 100 && next.inventory.items.Count == 0 && next.shop.availableWeaponIds.Count == 3
                && next.map.randomSeed == 67891, "successive new game does not inherit previous state");
        }
        bool invalidWeaponRejected = false;
        try { NewRunFactory.Create(Layout(), "invalid", 1); }
        catch (ArgumentException) { invalidWeaponRejected = true; }
        Check(invalidWeaponRejected, "invalid starting weapon rejected");
        var invalidNewMap = Layout();
        invalidNewMap.nodes.Clear();
        bool invalidNewMapRejected = false;
        try { NewRunFactory.Create(invalidNewMap, "sword", 1); }
        catch (InvalidOperationException) { invalidNewMapRejected = true; }
        Check(invalidNewMapRejected, "invalid map cannot create a new run");
        if (map.Layout.id == "three-sins-centered-whitebox-v1")
        {
            var village = map.GetNode("village");
            Check(village.x == 0 && village.y == 0, "village centered in map");
            for (int i = 0; i < map.Layout.nodes.Count; i++)
                for (int j = i + 1; j < map.Layout.nodes.Count; j++)
                {
                    var a = map.Layout.nodes[i];
                    var b = map.Layout.nodes[j];
                    Check(Math.Abs(a.x - b.x) >= 112 || Math.Abs(a.y - b.y) >= 64, "node rectangles do not overlap: " + a.id + "/" + b.id);
                }
            int crossRoads = 0, roads = 0;
            foreach (var a in map.Layout.nodes)
                foreach (string id in a.neighbors)
                {
                    if (string.CompareOrdinal(a.id, id) >= 0) continue;
                    roads++;
                    var b = map.GetNode(id);
                    if (a.region != 0 && b.region != 0 && a.region != b.region) crossRoads++;
                    foreach (var other in map.Layout.nodes)
                        if (other.id != a.id && other.id != b.id)
                            Check(!RoadIntersectsNode(a, b, other), "road does not pass through an unrelated node: " + a.id + "/" + b.id + "/" + other.id);
                }
            Check(roads == 42 && crossRoads == 3, "42 roads include three direct inter-region links");
            int initialRoads = 0;
            foreach (var node in map.Layout.nodes)
                foreach (string neighbor in node.neighbors)
                    if (string.CompareOrdinal(node.id, neighbor) < 0 && map.IsRoadVisible(node.id, neighbor)) initialRoads++;
            Check(initialRoads == 5, "village roads and both entrance cross-links visible at game start");
            foreach (var edge in new[] { new[] { "r1_a", "r2_a" }, new[] { "r1_a", "r3_a" } })
            {
                Check(map.IsRoadVisible(edge[0], edge[1]) && map.IsRoadVisible(edge[1], edge[0]),
                    "roads between initial village neighbors visible in both directions before completion");
                var initialVisit = NewMap();
                Move(initialVisit, edge[0]);
                Check(initialVisit.IsRoadVisible(edge[0], edge[1]), "entering an unfinished node keeps initial cross-link visible");
                Check(!initialVisit.TryMove(edge[1], out _) && initialVisit.World.time == 1,
                    "visible initial cross-link does not bypass completion or charge invalid movement");
            }
            Check(!map.IsRoadVisible("r2_a", "r3_a"), "visible nodes without a connection do not gain a road");
            foreach (var region in map.Layout.regions)
            {
                var boss = map.GetNode(region.bossNodeId);
                foreach (var other in map.Layout.nodes)
                {
                    if (other.id == boss.id) continue;
                    float clearance = other.id == boss.neighbors[0] ? 180 : 220;
                    float dx = boss.x - other.x, dy = boss.y - other.y;
                    Check(dx * dx + dy * dy >= clearance * clearance, "boss has clear surroundings: " + boss.id + "/" + other.id);
                }
            }
            var crossMap = NewMap();
            var entry = Move(crossMap, "r2_a");
            Check(!crossMap.IsVisible("r3_x") && !crossMap.IsRoadVisible("r2_a", "r3_x"), "cross-region route hidden until departure node completes");
            Check(!crossMap.TryMove("r3_x", out _), "pending fight blocks cross-region movement");
            Check(Complete(crossMap, entry.requestId), "complete cross-region departure");
            Check(crossMap.IsVisible("r3_x") && crossMap.IsRoadVisible("r2_a", "r3_x"), "completion reveals cross-region neighbor and road");
            Visit(crossMap, "r3_x");
            Check(Move(crossMap, "r2_a") == null, "cross-region cleared transit works in reverse");
            foreach (var edge in new[] { new[] { "r1_a", "r2_a" }, new[] { "r1_a", "r3_a" } })
            {
                var linked = NewMap();
                Visit(linked, edge[0]);
                Visit(linked, edge[1]);
                Check(Move(linked, edge[0]) == null, "inter-region connection is bidirectional");
            }

            var bypass = Layout();
            bypass.nodes.Find(n => n.id == "r1_y").neighbors.Add("r2_c");
            bypass.nodes.Find(n => n.id == "r2_c").neighbors.Add("r1_y");
            bool bypassRejected = false;
            try { MapLayoutValidator.Validate(bypass); }
            catch (InvalidOperationException error) { bypassRejected = error.Message.Contains("regional battles"); }
            Check(bypassRejected, "cross-region paths cannot reuse another region's three fights");

            var crowded = Layout();
            crowded.nodes.Find(n => n.id == "r1_w").x = crowded.nodes.Find(n => n.id == "r1_boss").x + 100;
            crowded.nodes.Find(n => n.id == "r1_w").y = crowded.nodes.Find(n => n.id == "r1_boss").y;
            bool crowdingRejected = false;
            try { MapLayoutValidator.Validate(crowded); }
            catch (InvalidOperationException error) { crowdingRejected = error.Message.Contains("too close"); }
            Check(crowdingRejected, "validator rejects nodes crowded around a boss");
        }
        Check(map.Layout.nodes.Count == 28 && map.Layout.regions.Count == 3, "region and node counts");
        foreach (var region in map.Layout.regions)
        {
            var boss = map.GetNode(region.bossNodeId);
            Check(boss.neighbors.Count == 1, "boss has one entrance: " + boss.id);
            var approach = map.GetNode(boss.neighbors[0]);
            Check(approach.kind == MapNodeKind.Camp && approach.neighbors.Count == 2,
                "final camp has no branch: " + approach.id);

            var branching = Layout();
            branching.nodes.Find(n => n.id == approach.id).neighbors.Add("r" + region.id + "_w");
            branching.nodes.Find(n => n.id == "r" + region.id + "_w").neighbors.Add(approach.id);
            bool branchRejected = false;
            try { MapLayoutValidator.Validate(branching); }
            catch (InvalidOperationException error) { branchRejected = error.Message.Contains("final approach"); }
            Check(branchRejected, "validator rejects a branch at the final camp: " + approach.id);

            var extraEntrance = Layout();
            extraEntrance.nodes.Find(n => n.id == boss.id).neighbors.Add("r" + region.id + "_w");
            extraEntrance.nodes.Find(n => n.id == "r" + region.id + "_w").neighbors.Add(boss.id);
            bool entranceRejected = false;
            try { MapLayoutValidator.Validate(extraEntrance); }
            catch (InvalidOperationException error) { entranceRejected = error.Message.Contains("exactly one entrance"); }
            Check(entranceRejected, "validator rejects a second boss entrance: " + boss.id);
        }
        Check(map.World.time == 0 && map.World.sin == 40, "initial resources");
        Check(map.State.revealedNodes.Count == 5, "village, three entrances, Sloth initially visible");
        foreach (string entrance in map.GetNode("village").neighbors)
            Check(map.IsRoadVisible("village", entrance) && map.IsRoadVisible(entrance, "village"), "initial village entrance road visible");
        Check(!map.IsRoadVisible("missing", "village") && !map.IsRoadVisible("village", "missing")
            && !map.IsRoadVisible("village", "village"), "invalid road endpoints rejected");
        Check(map.IsVisible("r1_boss") && !map.IsVisible("r2_boss"), "tutorial boss only");
        Check(!map.IsRoadVisible("r1_camp", "r1_boss"), "Sloth route initially hidden");
        Check(!map.TryMove("r1_boss", out _) && map.World.time == 0, "no teleport to revealed boss");
        Check(!map.TryMove("missing", out _) && !map.TryMove("village", out _), "invalid and self moves rejected");
        var first = Move(map, "r1_a");
        Check(first != null && first.kind == MapNodeKind.Battle, "battle request context");
        Check(!map.State.clearedNodes.Contains("r1_a") && !map.IsVisible("r1_b"), "arrival does not clear or reveal");
        Check(map.World.sin >= 48 && map.World.sin <= 52, "sin roll within 8 to 12");
        Check(!map.TryMove("village", out _) && map.World.time == 1, "pending node blocks all movement");
        Check(!map.TryCompleteNode("bad-token") && map.World.time == 1, "invalid callback rejected without charging time");
        Check(Complete(map, first.requestId), "complete current visit");
        Check(map.World.time == 2, "moving to and handling a new node costs two units");
        Check(map.IsVisible("r1_b") && map.IsVisible("r1_x") && !map.IsVisible("r1_c"), "reveal immediate neighbors only");
        Check(!map.TryCompleteNode(first.requestId) && map.World.time == 2, "duplicate callback has no effect or time cost");
        var second = Move(map, "r1_b");
        Check(!map.TryCompleteNode(first.requestId) && map.State.pendingRequestId == second.requestId && map.World.time == 3,
            "stale callback cannot complete a later visit or charge time");
        Check(Complete(map, second.requestId), "complete second visit");
        Check(Move(map, "r1_a") == null, "cleared node transit never replays content");
        Check(Move(map, "village") == null, "village transit has no pending content");

        var restoredState = JsonSerializer.Deserialize<MapState>(JsonSerializer.Serialize(map.State, Json), Json);
        var restoredWorld = JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(map.World, Json), Json);
        var restored = new MapProgression(Layout(), restoredState, restoredWorld, 999);
        Check(restored.World.time == map.World.time && restored.State.randomSeed == 12345, "state reconstruction does not reset progress or seed");
        for (int i = 0; i < 32; i++)
        {
            string target = i % 2 == 0 ? "r1_a" : "village";
            int previousSpawnCount = map.State.spawnedBossNodes.Count;
            int oldSin = map.World.sin;
            Move(map, target);
            Move(restored, target);
            Check(map.World.sin == restored.World.sin && map.State.sinRollCount == restored.State.sinRollCount, "restored sin stream stays deterministic");
            Check(map.State.spawnedBossNodes.Count - previousSpawnCount <= 1, "at most one spawn per move");
            if (map.State.spawnedBossNodes.Count > previousSpawnCount)
                Check(map.World.sin == 0, "spawn resets sin and discards overflow");
            else if (previousSpawnCount < 3)
                Check(map.World.sin - oldSin >= 8 && map.World.sin - oldSin <= 12, "continued natural sin bounds");
        }
        Check(map.State.spawnedBossNodes.Count == 3 && map.State.spawnedBossNodes[1] == "r2_boss", "remaining bosses spawn in region order");
        int stoppedRolls = map.State.sinRollCount;
        Visit(map, "r1_a");
        Check(map.State.sinRollCount == stoppedRolls, "sin growth stops after all bosses spawn");
        Check(!map.IsRoadVisible("r2_camp", "r2_boss"), "remote spawn does not reveal its road");
        Visit(map, "r1_b");
        Visit(map, "r1_c");
        Check(!map.IsRoadVisible("r1_camp", "r1_boss"), "boss road stays hidden before camp completion");
        Visit(map, "r1_camp");
        Check(map.IsRoadVisible("r1_camp", "r1_boss"), "camp completion reveals boss road");
        var sloth = Move(map, "r1_boss");
        Check(sloth.bossKey == "sloth" && sloth.kind == MapNodeKind.Boss, "Sloth transition metadata");
        Check(Complete(map, sloth.requestId), "Sloth whitebox completion");
        foreach (string key in new[] { "camp", "c", "b", "a" }) Visit(map, "r1_" + key);
        Visit(map, "village");
        for (int region = 2; region <= 3; region++)
        {
            foreach (string key in new[] { "a", "b", "c", "camp", "boss" }) Visit(map, "r" + region + "_" + key);
            Check(map.State.defeatedBossNodes.Count == region, "boss completion recorded once");
            if (region == 2)
            {
                foreach (string key in new[] { "camp", "c", "b", "a" }) Visit(map, "r2_" + key);
                Visit(map, "village");
            }
        }
        Check(map.IsComplete && map.State.defeatedBossNodes.Count == 3, "three-boss flow completes");

        var pendingMap = NewMap();
        var pending = Move(pendingMap, "r1_a");
        var stateCopy = JsonSerializer.Deserialize<MapState>(JsonSerializer.Serialize(pendingMap.State, Json), Json);
        var worldCopy = JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(pendingMap.World, Json), Json);
        var resumed = new MapProgression(Layout(), stateCopy, worldCopy, 777);
        Check(resumed.GetPendingRequest().requestId == pending.requestId && resumed.World.time == 1,
            "restoring pending visit does not charge movement or completion");
        Complete(resumed, pending.requestId);
        Check(resumed.World.time == 2, "resumed completion charges exactly one additional time unit");

        for (int region = 1; region <= 3; region++)
        {
            var branchMap = NewMap();
            Visit(branchMap, "r1_a");
            while (branchMap.State.spawnedBossNodes.Count < 3)
            {
                Visit(branchMap, "village");
                Visit(branchMap, "r1_a");
            }
            Visit(branchMap, "village");
            string prefix = "r" + region + "_";
            foreach (string key in new[] { "a", "x", "y", "z", "w" }) Visit(branchMap, prefix + key);
            Check(branchMap.IsVisible(prefix + "c") && !branchMap.IsVisible(prefix + "camp"),
                "side route joins before final camp: " + prefix);
            Visit(branchMap, prefix + "c");
            Visit(branchMap, prefix + "camp");
            Check(branchMap.IsRoadVisible(prefix + "camp", prefix + "boss"), "final camp reveals boss road: " + prefix);
            Visit(branchMap, prefix + "boss");
            Check(branchMap.State.defeatedBossNodes.Contains(prefix + "boss"), "boss reachable via side route: " + prefix);
        }

        var broken = Layout();
        broken.nodes[0].neighbors.Add("r1_boss");
        broken.nodes.Find(n => n.id == "r1_boss").neighbors.Add("village");
        bool rejected = false;
        try { MapLayoutValidator.Validate(broken); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "validator rejects shortcuts bypassing required fights");
        Console.WriteLine("PASS: " + checks + " checks; topology, reveal, transit, callbacks, sin spawning, restoration, complete three-boss flow.");
    }

    static void CheckMorningRevival()
    {
        var map = NewMap();
        Check(map.State.checkpointNodeId == "village" && map.State.checkpointTime == 0,
            "new run starts with village checkpoint");
        var first = Move(map, "r1_a");
        Check(map.State.checkpointNodeId == "village", "afternoon does not change checkpoint");
        Complete(map, first.requestId);
        Check(map.State.checkpointNodeId == "village", "night does not change checkpoint");
        var dawn = Move(map, "r1_b");
        Check(map.World.time == 3 && map.State.checkpointNodeId == "r1_b"
            && map.State.checkpointTime == 3, "morning arrival records current unfinished battle");
        int sin = map.World.sin, rolls = map.State.sinRollCount;
        int cleared = map.State.clearedNodes.Count;
        Check(!map.TryReviveAtCheckpoint(first.requestId)
            && map.State.pendingRequestId == dawn.requestId, "stale revive cannot cancel another battle");
        Check(map.TryReviveAtCheckpoint(dawn.requestId), "revive at unfinished morning checkpoint");
        Check(map.State.currentNodeId == "r1_b" && map.GetPendingRequest() == null,
            "revival restores checkpoint position and cancels pending battle");
        Check(map.World.time == 3 && map.World.sin == sin && map.State.sinRollCount == rolls,
            "revival does not advance time or sin");
        Check(map.State.clearedNodes.Count == cleared && !map.State.clearedNodes.Contains("r1_b")
            && !map.IsVisible("r1_c"), "revival does not clear battle or reveal its rewards");
        Check(!map.TryReviveAtCheckpoint(dawn.requestId), "duplicate revival rejected");
        var copied = JsonSerializer.Deserialize<MapState>(JsonSerializer.Serialize(map.State, Json), Json);
        var world = JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(map.World, Json), Json);
        var restored = new MapProgression(Layout(), copied, world, 999);
        Check(restored.State.checkpointNodeId == "r1_b" && restored.State.checkpointTime == 3
            && restored.CanMove("r1_a"), "checkpoint and ability to leave survive reconstruction");
        Check(restored.CanMove("r1_b"), "revived unfinished current battle is clickable");
        int retryTime = restored.World.time, retrySin = restored.World.sin;
        int retryRolls = restored.State.sinRollCount;
        Check(restored.TryMove("r1_b", out var directRetry) && directRetry != null
            && directRetry.kind == MapNodeKind.Battle && directRetry.requestId != dawn.requestId,
            "clicking current checkpoint creates a fresh battle request");
        Check(restored.World.time == retryTime && restored.World.sin == retrySin
            && restored.State.sinRollCount == retryRolls, "in-place retry charges no travel time or sin");
        Check(!restored.CanMove("r1_b") && !restored.TryMove("r1_b", out _),
            "pending retry blocks duplicate clicks");
        Check(!restored.TryCompleteNode(dawn.requestId)
            && !restored.TryReviveAtCheckpoint(dawn.requestId), "old battle callbacks cannot resolve direct retry");
        Check(restored.TryReviveAtCheckpoint(directRetry.requestId) && restored.CanMove("r1_b"),
            "dying during direct retry allows another direct retry");
        Check(restored.TryMove("r1_b", out var winningRetry)
            && restored.TryCompleteNode(winningRetry.requestId), "direct retry can complete normally");
        Check(!restored.CanMove("r1_b") && restored.State.clearedNodes.Contains("r1_b")
            && restored.World.time == retryTime + 1, "cleared current node cannot replay and victory costs one period");
        Check(Move(map, "r1_a") == null && !map.State.revivedAtCheckpoint,
            "player can leave unfinished checkpoint without replaying cleared neighbor");
        var retry = Move(map, "r1_b");
        Check(retry.requestId != dawn.requestId && !map.TryCompleteNode(dawn.requestId),
            "retry creates fresh request and rejects old victory");
        Check(map.TryReviveAtCheckpoint(retry.requestId) && map.State.currentNodeId == "r1_b",
            "later death still uses latest dawn checkpoint");

        var completion = NewMap();
        Visit(completion, "r1_a");
        Visit(completion, "r1_b");
        Visit(completion, "r1_c");
        Check(completion.World.time == 6 && completion.State.checkpointNodeId == "r1_c"
            && completion.State.checkpointTime == 6, "completion entering morning updates checkpoint");
        Move(completion, "r1_b");
        Check(completion.State.checkpointNodeId == "r1_c", "daytime movement preserves checkpoint");

        var initialDeath = NewMap();
        var encounter = Move(initialDeath, "r1_a");
        Check(initialDeath.TryReviveAtCheckpoint(encounter.requestId)
            && initialDeath.State.currentNodeId == "village" && initialDeath.CanMove("r1_a"),
            "death before second morning revives at village and permits retry");

        var fresh = NewRunFactory.Create(Layout(), "axe", 54321);
        Check(fresh.map.checkpointNodeId == "village" && fresh.map.checkpointTime == 0
            && !fresh.map.revivedAtCheckpoint && fresh.player.hp == fresh.player.maxHp,
            "restart creates full health and a fresh initial checkpoint");
    }

    static bool RoadIntersectsNode(MapNodeDefinition a, MapNodeDefinition b, MapNodeDefinition node)
    {
        float enter = 0, leave = 1;
        return IntersectsAxis(a.x, b.x, node.x - 64, node.x + 64, ref enter, ref leave)
            && IntersectsAxis(a.y, b.y, node.y - 40, node.y + 40, ref enter, ref leave);
    }

    static bool IntersectsAxis(float start, float end, float min, float max, ref float enter, ref float leave)
    {
        float delta = end - start;
        if (Math.Abs(delta) < 0.0001f) return start >= min && start <= max;
        float a = (min - start) / delta, b = (max - start) / delta;
        enter = Math.Max(enter, Math.Min(a, b));
        leave = Math.Min(leave, Math.Max(a, b));
        return enter <= leave;
    }
}
