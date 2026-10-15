using System;
using System.Collections.Generic;

// Adapts the shared wilderness content to the existing map without changing its template roads.
public static class MapWildernessRules
{
    public static WildernessEventCatalog BuildCatalog(WildernessEventCatalog source, MapLayout layout)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        MapLayoutValidator.Validate(layout);
        var catalog = new WildernessEventCatalog {
            templates = source.templates,
            enemyVariants = source.enemyVariants,
            graph = new WildernessGraph { villageNodeId = layout.villageNodeId }
        };
        foreach (var node in layout.nodes)
        {
            catalog.graph.nodes.Add(new WildernessGraphNode {
                id = node.id, x = node.x, y = node.y, neighbors = new List<string>(node.neighbors)
            });
            if (node.kind == MapNodeKind.Event) catalog.questionNodeIds.Add(node.id);
        }
        if (catalog.questionNodeIds.Count != 6)
            throw new InvalidOperationException("当前大地图必须保留 6 个问号节点。");
        catalog.graph.repairableRoads = GetRepairableRoads(layout);
        WildernessEventRules.ValidateCatalog(catalog);
        return catalog;
    }

    // Candidates are shortcuts inside one region. Validate each complete graph to protect boss routes.
    public static List<WildernessRoad> GetRepairableRoads(MapLayout layout)
    {
        var result = new List<WildernessRoad>();
        if (layout == null || layout.nodes == null || layout.regions == null) return result;
        foreach (var region in layout.regions)
        {
            string prefix = "r" + region.id + "_";
            string from = prefix + "y", to = prefix + "w";
            var first = layout.nodes.Find(node => node != null && node.id == from);
            var second = layout.nodes.Find(node => node != null && node.id == to);
            if (first == null || second == null || first.region != region.id || second.region != region.id
                || !MayRepairAt(first) || !MayRepairAt(second)
                || first.neighbors.Contains(to) || second.neighbors.Contains(from)
                || first.neighbors.Count >= 4 || second.neighbors.Count >= 4) continue;
            var candidate = CopyLayout(layout);
            candidate.nodes.Find(node => node.id == from).neighbors.Add(to);
            candidate.nodes.Find(node => node.id == to).neighbors.Add(from);
            try { MapLayoutValidator.Validate(candidate); }
            catch (InvalidOperationException) { continue; }
            result.Add(new WildernessRoad { from = from, to = to });
        }
        return result;
    }

    public static void EnsureInitialized(GameState state, MapLayout layout, WildernessEventCatalog catalog)
    {
        RequireState(state, layout);
        WildernessEventRules.ValidateCatalog(catalog);
        if (state.map.wildernessEvents == null)
        {
            // Generate into a local value so a validation failure never installs a partial level.
            var generated = WildernessEventRules.Generate(catalog, state.map.randomSeed, state.world.stage);
            generated.currentNodeId = state.map.currentNodeId;
            foreach (var node in generated.nodes)
                if (state.map.visitedNodes.Contains(node.id) || state.map.clearedNodes.Contains(node.id))
                {
                    node.shown = true;
                    node.abandoned = true;
                }
            WildernessEventRules.ValidateLevel(generated, catalog);
            state.map.wildernessEvents = generated;
            return;
        }
        var level = state.map.wildernessEvents;
        if (level.seed != state.map.randomSeed)
            throw new InvalidOperationException("野外事件存档的种子与当前地图不一致，不能重新抽取。");
        if (!string.IsNullOrEmpty(level.activeNodeId) && level.activeNodeId != state.map.currentNodeId)
            throw new InvalidOperationException("未结束的野外事件与当前位置不一致。");
        string previousNodeId = level.currentNodeId;
        level.currentNodeId = state.map.currentNodeId;
        try { WildernessEventRules.ValidateLevel(level, catalog); }
        catch
        {
            level.currentNodeId = previousNodeId;
            throw;
        }
    }

    public static bool TryOpen(GameState state, MapLayout layout, WildernessEventCatalog catalog, MapNodeRequest request)
    {
        if (state == null || state.map == null || request == null || request.kind != MapNodeKind.Event
            || string.IsNullOrEmpty(request.requestId) || request.requestId != state.map.pendingRequestId
            || request.nodeId != state.map.pendingNodeId || request.nodeId != state.map.currentNodeId)
            return false;
        var mapNode = layout == null || layout.nodes == null ? null : layout.nodes.Find(node => node.id == request.nodeId);
        if (mapNode == null || mapNode.kind != MapNodeKind.Event) return false;
        EnsureInitialized(state, layout, catalog);
        return WildernessEventRules.TryEnter(state.map.wildernessEvents, request.nodeId);
    }

    static bool MayRepairAt(MapNodeDefinition node)
    {
        return node.kind == MapNodeKind.Event || node.kind == MapNodeKind.Battle;
    }

    static void RequireState(GameState state, MapLayout layout)
    {
        if (state == null || state.world == null || state.map == null
            || state.map.visitedNodes == null || state.map.clearedNodes == null)
            throw new InvalidOperationException("当前地图状态不可用。");
        MapLayoutValidator.Validate(layout);
        if (state.map.templateId != layout.id
            || !layout.nodes.Exists(node => node.id == state.map.currentNodeId))
            throw new InvalidOperationException("野外事件与当前地图配置不匹配。");
    }

    static MapLayout CopyLayout(MapLayout source)
    {
        var copy = new MapLayout {
            id = source.id, villageNodeId = source.villageNodeId,
            initialSin = source.initialSin, sinThreshold = source.sinThreshold,
            sinPerMoveMin = source.sinPerMoveMin, sinPerMoveMax = source.sinPerMoveMax,
            bossNodeClearance = source.bossNodeClearance, bossApproachDistance = source.bossApproachDistance,
            regions = source.regions
        };
        foreach (var node in source.nodes)
            copy.nodes.Add(new MapNodeDefinition {
                id = node.id, displayName = node.displayName, kind = node.kind, region = node.region,
                x = node.x, y = node.y, neighbors = new List<string>(node.neighbors)
            });
        return copy;
    }
}
