using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

// Owns progression data; views and external encounters cannot complete a stale visit.
public sealed class MapProgression
{
    readonly Dictionary<string, MapNodeDefinition> nodes = new Dictionary<string, MapNodeDefinition>();
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
        if (string.IsNullOrEmpty(state.templateId)) Initialize(newGameSeed);
        else if (state.templateId != layout.id)
            throw new InvalidOperationException("Map state belongs to a different template.");
        if (string.IsNullOrEmpty(State.checkpointNodeId)) State.checkpointNodeId = Layout.villageNodeId;
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
        RevealNeighbors(Layout.villageNodeId);
        foreach (var region in Layout.regions)
            if (region.startsSpawned) SpawnBoss(region.bossNodeId);
    }

    public MapNodeDefinition GetNode(string id)
    {
        return id != null && nodes.TryGetValue(id, out var node) ? node : null;
    }

    public bool IsVisible(string id)
    {
        var node = GetNode(id);
        return node != null && State.revealedNodes.Contains(id)
            && (node.kind != MapNodeKind.Boss || State.spawnedBossNodes.Contains(id));
    }

    public bool IsRoadVisible(string from, string to)
    {
        if (!IsVisible(from) || !IsVisible(to) || !GetNode(from).neighbors.Contains(to)) return false;
        var villageNeighbors = GetNode(Layout.villageNodeId).neighbors;
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
            && current.neighbors.Contains(destinationId) && IsVisible(destinationId);
    }

    public bool TryMove(string destinationId, out MapNodeRequest request)
    {
        request = null;
        if (!CanMove(destinationId)) return false;
        LastSpawnedBossId = null;
        bool retryCurrentBattle = destinationId == State.currentNodeId;
        State.revivedAtCheckpoint = false;
        State.currentNodeId = destinationId;
        AddOnce(State.visitedNodes, destinationId);
        // Retrying at the checkpoint traverses no road.
        if (!retryCurrentBattle)
        {
            World.time++;
            // Every road in this prototype traverses the wilderness, including the road home.
            AdvanceSin();
        }
        if (!State.clearedNodes.Contains(destinationId))
        {
            State.requestSequence++;
            State.pendingNodeId = destinationId;
            State.pendingRequestId = State.randomSeed.ToString() + ":" + State.requestSequence + ":" + destinationId;
            request = GetPendingRequest();
        }
        UpdateMorningCheckpoint();
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
        World.time++;
        AddOnce(State.clearedNodes, node.id);
        if (node.kind == MapNodeKind.Boss) AddOnce(State.defeatedBossNodes, node.id);
        State.pendingNodeId = null;
        State.pendingRequestId = null;
        RevealNeighbors(node.id);
        UpdateMorningCheckpoint();
        return true;
    }

    void UpdateMorningCheckpoint()
    {
        // Existing clock: morning, afternoon, night.
        if (World.time % 3 != 0) return;
        State.checkpointNodeId = State.currentNodeId;
        State.checkpointTime = World.time;
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
        // A battle reached at dawn can be uncleared; permit leaving without clearing it.
        State.revivedAtCheckpoint = true;
        return true;
    }

    void RevealNeighbors(string id)
    {
        foreach (string neighbor in nodes[id].neighbors) AddOnce(State.revealedNodes, neighbor);
    }

    void AdvanceSin()
    {
        if (State.spawnedBossNodes.Count == Layout.regions.Count) return;
        byte[] bytes;
        using (var hash = SHA256.Create())
            bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(State.randomSeed + ":sin:" + State.sinRollCount));
        State.sinRollCount++;
        uint roll = (uint)bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16) | ((uint)bytes[3] << 24);
        World.sin += Layout.sinPerMoveMin + (int)(roll % (uint)(Layout.sinPerMoveMax - Layout.sinPerMoveMin + 1));
        if (World.sin < Layout.sinThreshold) return;
        foreach (var region in Layout.regions)
        {
            if (State.spawnedBossNodes.Contains(region.bossNodeId)) continue;
            SpawnBoss(region.bossNodeId);
            LastSpawnedBossId = region.bossNodeId;
            World.sin = 0;
            break;
        }
    }

    void SpawnBoss(string id)
    {
        AddOnce(State.spawnedBossNodes, id);
        AddOnce(State.revealedNodes, id);
    }

    static void AddOnce(List<string> list, string id)
    {
        if (!list.Contains(id)) list.Add(id);
    }
}
