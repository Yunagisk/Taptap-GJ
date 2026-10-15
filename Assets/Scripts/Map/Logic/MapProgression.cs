using System;
using System.Collections.Generic;

// Owns progression data; views and external encounters cannot complete a stale visit.
public sealed class MapProgression
{
    readonly Dictionary<string, MapNodeDefinition> nodes = new Dictionary<string, MapNodeDefinition>();
    readonly List<WildernessRoad> repairableRoads;
    public MapLayout Layout { get; }
    public MapState State { get; }
    public WorldState World { get; }
    public string LastSpawnedBossId { get; private set; }
    public bool IsComplete => State.defeatedBossNodes.Count == Layout.regions.Count;

    public MapProgression(MapLayout layout, MapState state, WorldState world, int newGameSeed)
    {
        MapLayoutValidator.Validate(layout);
        Layout = layout;
        State = state ?? throw new ArgumentNullException(nameof(state));
        World = world ?? throw new ArgumentNullException(nameof(world));
        foreach (var node in layout.nodes) nodes.Add(node.id, node);
        repairableRoads = MapWildernessRules.GetRepairableRoads(layout);
        if (string.IsNullOrEmpty(state.templateId)) Initialize(newGameSeed);
        else if (state.templateId != layout.id)
            throw new InvalidOperationException("Map state belongs to a different template.");
        if (string.IsNullOrEmpty(State.checkpointNodeId)) State.checkpointNodeId = Layout.villageNodeId;
        WorldResourceRules.Normalize(World);
    }

    void Initialize(int seed)
    {
        State.templateId = Layout.id;
        State.randomSeed = seed;
        State.sinRollCount = 0;
        State.requestSequence = 0;
        State.currentNodeId = Layout.villageNodeId;
        State.checkpointNodeId = Layout.villageNodeId;
        State.checkpointTime = 0;
        State.revivedAtCheckpoint = false;
        State.pendingNodeId = null;
        State.pendingRequestId = null;
        State.wildernessEvents = null;
        State.visitedNodes.Clear();
        State.clearedNodes.Clear();
        State.revealedNodes.Clear();
        State.spawnedBossNodes.Clear();
        State.defeatedBossNodes.Clear();
        State.visitedNodes.Add(Layout.villageNodeId);
        State.clearedNodes.Add(Layout.villageNodeId);
        State.revealedNodes.Add(Layout.villageNodeId);
        World.time = 0;
        World.sin = Layout.initialSin;
        World.villageEvents = new VillageEventState();
        RevealNeighbors(Layout.villageNodeId);
        foreach (var region in Layout.regions)
            if (region.startsSpawned) SpawnBoss(region.bossNodeId);
    }

    public MapNodeDefinition GetNode(string id)
    {
        return id != null && nodes.TryGetValue(id, out var node) ? node : null;
    }

    public List<string> GetNeighbors(string id)
    {
        var result = new List<string>();
        var node = GetNode(id);
        if (node == null) return result;
        result.AddRange(node.neighbors);
        var level = State.wildernessEvents;
        if (level == null || level.repairedRoads == null || level.nodes == null) return result;
        foreach (var road in level.repairedRoads)
        {
            if (road == null || !repairableRoads.Exists(value => SameRoad(value, road.from, road.to))) continue;
            // A serialized road is only active after its one-time repair choice was settled.
            if (!level.nodes.Exists(value => value != null && value.shown && value.resolved
                && !value.abandoned && value.templateId == "w08" && value.chosenIndex == 0
                && !value.encounterPending && !value.encounterResolved
                && value.rolls != null && value.rolls.Count == 2 && value.rolls[0] != null
                && value.rolls[0].outcomeIndex == 0 && string.IsNullOrEmpty(value.rolls[0].enemyId)
                && SameRoad(road, value.bridgeFrom, value.bridgeTo))) continue;
            string other = road.from == id ? road.to : road.to == id ? road.from : null;
            if (!string.IsNullOrEmpty(other) && !result.Contains(other)) result.Add(other);
        }
        return result;
    }

    public bool IsVisible(string id)
    {
        var node = GetNode(id);
        return node != null && State.revealedNodes.Contains(id)
            && (node.kind != MapNodeKind.Boss || State.spawnedBossNodes.Contains(id));
    }

    public bool IsRoadVisible(string from, string to)
    {
        if (!IsVisible(from) || !IsVisible(to) || !GetNeighbors(from).Contains(to)) return false;
        var villageNeighbors = GetNeighbors(Layout.villageNodeId);
        // Roads inside the initially revealed village area are visible before any visit.
        return State.clearedNodes.Contains(from) || State.clearedNodes.Contains(to)
            || (villageNeighbors.Contains(from) && villageNeighbors.Contains(to));
    }

    // 原地重新打开村庄界面，不算走过一条道路。
    public bool CanReenterVillage(string nodeId)
    {
        var node = GetNode(nodeId);
        return node != null && node.kind == MapNodeKind.Village
            && nodeId == Layout.villageNodeId && nodeId == State.currentNodeId
            && string.IsNullOrEmpty(State.pendingRequestId) && IsVisible(nodeId);
    }

    public bool CanMove(string destinationId)
    {
        var current = GetNode(State.currentNodeId);
        if (current != null && destinationId == current.id)
            return string.IsNullOrEmpty(State.pendingRequestId) && State.revivedAtCheckpoint
                && current.id == State.checkpointNodeId && current.kind == MapNodeKind.Battle
                && !State.clearedNodes.Contains(current.id) && IsVisible(current.id);
        return current != null && string.IsNullOrEmpty(State.pendingRequestId)
            && (State.clearedNodes.Contains(current.id) || State.revivedAtCheckpoint)
            && destinationId != current.id
            && GetNeighbors(current.id).Contains(destinationId) && IsVisible(destinationId);
    }

    public bool TryMove(string destinationId, out MapNodeRequest request)
    {
        request = null;
        if (!CanMove(destinationId)) return false;
        LastSpawnedBossId = null;
        bool retryCurrentBattle = destinationId == State.currentNodeId;
        State.revivedAtCheckpoint = false;
        State.currentNodeId = destinationId;
        if (State.wildernessEvents != null) State.wildernessEvents.currentNodeId = destinationId;
        AddOnce(State.visitedNodes, destinationId);
        // Retrying at the checkpoint traverses no road.
        if (!retryCurrentBattle)
        {
            // Every road traverses the wilderness, including the road home.
            LastSpawnedBossId = WorldResourceRules.SpendTime(Layout, State, World, 1, false);
        }
        if (!State.clearedNodes.Contains(destinationId))
        {
            State.requestSequence++;
            State.pendingNodeId = destinationId;
            State.pendingRequestId = State.randomSeed.ToString() + ":" + State.requestSequence + ":" + destinationId;
            request = GetPendingRequest();
        }
        return true;
    }

    public MapNodeRequest GetPendingRequest()
    {
        if (string.IsNullOrEmpty(State.pendingRequestId)) return null;
        var node = GetNode(State.pendingNodeId);
        string bossKey = null;
        foreach (var region in Layout.regions)
            if (region.bossNodeId == node.id) bossKey = region.bossKey;
        return new MapNodeRequest
        {
            requestId = State.pendingRequestId,
            nodeId = node.id,
            kind = node.kind,
            region = node.region,
            bossKey = bossKey
        };
    }

    public bool TryCompleteNode(string requestId)
    {
        if (string.IsNullOrEmpty(requestId) || requestId != State.pendingRequestId) return false;
        var node = GetNode(State.pendingNodeId);
        if (node == null || (node.kind == MapNodeKind.Event && State.wildernessEvents != null)) return false;
        LastSpawnedBossId = WorldResourceRules.SpendTime(Layout, State, World, 1, false);
        AddOnce(State.clearedNodes, node.id);
        if (node.kind == MapNodeKind.Boss) AddOnce(State.defeatedBossNodes, node.id);
        State.pendingNodeId = null;
        State.pendingRequestId = null;
        RevealNeighbors(node.id);
        // A just-defeated Sloth invalidates its unfinished conditional event immediately.
        VillageEventRules.OnTimeAdvanced(World, State, Layout);
        return true;
    }

    // Event choices already paid their configured time; finishing must not add the old placeholder cost.
    public bool FinishEvent(string requestId)
    {
        if (string.IsNullOrEmpty(requestId) || requestId != State.pendingRequestId) return false;
        var node = GetNode(State.pendingNodeId);
        var level = State.wildernessEvents;
        var question = WildernessEventRules.FindNode(level, State.pendingNodeId);
        if (node == null || node.kind != MapNodeKind.Event || node.id != State.currentNodeId
            || level == null || level.currentNodeId != node.id || question == null || !question.shown
            || (!question.resolved && !question.abandoned) || question.encounterPending) return false;
        if (level.activeNodeId == node.id) WildernessEventRules.Leave(level);
        LastSpawnedBossId = null;
        AddOnce(State.clearedNodes, node.id);
        State.pendingNodeId = null;
        State.pendingRequestId = null;
        RevealNeighbors(node.id);
        if (level.repairedRoads != null)
            foreach (var road in level.repairedRoads)
                if (road != null && GetNeighbors(road.from).Contains(road.to))
                {
                    AddOnce(State.revealedNodes, road.from);
                    AddOnce(State.revealedNodes, road.to);
                }
        return true;
    }

    public bool TryReviveAtCheckpoint(string requestId)
    {
        var pending = GetPendingRequest();
        if (pending == null || pending.kind != MapNodeKind.Battle
            || pending.requestId != requestId || GetNode(State.checkpointNodeId) == null)
            return false;
        State.pendingNodeId = null;
        State.pendingRequestId = null;
        State.currentNodeId = State.checkpointNodeId;
        if (State.wildernessEvents != null) State.wildernessEvents.currentNodeId = State.currentNodeId;
        // A battle reached at dawn can be uncleared; permit leaving without clearing it.
        State.revivedAtCheckpoint = true;
        return true;
    }

    void RevealNeighbors(string id)
    {
        foreach (string neighbor in GetNeighbors(id)) AddOnce(State.revealedNodes, neighbor);
    }

    void SpawnBoss(string id)
    {
        AddOnce(State.spawnedBossNodes, id);
        AddOnce(State.revealedNodes, id);
    }

    static bool SameRoad(WildernessRoad road, string from, string to)
    {
        return road != null && ((road.from == from && road.to == to) || (road.from == to && road.to == from));
    }

    static void AddOnce(List<string> list, string id)
    {
        if (!list.Contains(id)) list.Add(id);
    }
}
