using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class Program
{
    static void AddRoad(WildernessEventCatalog catalog, string from, string to)
    {
        var a = catalog.graph.nodes.Single(value => value.id == from);
        var b = catalog.graph.nodes.Single(value => value.id == to);
        if (!a.neighbors.Contains(to)) a.neighbors.Add(to);
        if (!b.neighbors.Contains(from)) b.neighbors.Add(from);
    }
    static void CheckBridges()
    {
        var catalog = Catalog(); var fixture = Case(catalog, "断桥上的绳索");
        var level = fixture.Level; var node = fixture.Node; string original = Save(catalog);
        string from = node.bridgeFrom, to = node.bridgeTo;
        int degreeA = WildernessEventRules.GetNeighbors(level, catalog, from).Count;
        int degreeB = WildernessEventRules.GetNeighbors(level, catalog, to).Count;
        Check(WildernessEventRules.CanRepairBridge(level, catalog, node.id)
            && !WildernessEventRules.GetNeighbors(level, catalog, from).Contains(to), "bridge is a legal pre-existing missing road");
        var resources = Resources(); Enter(level, node);
        Check(WildernessEventRules.TryChoose(level, catalog, resources, Layout(), 0).success, "repair completes real standalone road");
        Check(level.repairedRoads.Count == 1 && resources.world.time == 2 && resources.world.sin == 46, "repair costs two extra outdoor units");
        Check(WildernessEventRules.GetNeighbors(level, catalog, from).Contains(to)
            && WildernessEventRules.GetNeighbors(level, catalog, to).Contains(from), "repair enables both directions");
        Check(WildernessEventRules.GetNeighbors(level, catalog, from).Count == degreeA + 1
            && WildernessEventRules.GetNeighbors(level, catalog, to).Count == degreeB + 1
            && degreeA + 1 <= 4 && degreeB + 1 <= 4, "repair adds one edge respecting degree four");
        Check(!WildernessEventRules.CanRepairBridge(level, catalog, node.id), "same road cannot be repaired twice");
        Check(Save(catalog) == original, "repair is saved override, not a source graph edit");
        var loaded = Copy(level);
        Check(WildernessEventRules.GetNeighbors(loaded, catalog, from).Contains(to), "repaired adjacency survives JSON reload");
        var noBridge = Copy(catalog); noBridge.graph.repairableRoads.Clear();
        string bridgeId = Definition(catalog, "断桥上的绳索").id;
        for (int seed = 0; seed < 80; seed++)
            Check(WildernessEventRules.Generate(noBridge, seed, 1).nodes.All(value => value.templateId != bridgeId),
                "no repairable edge removes entire bridge template " + seed);
        var alreadyThere = Copy(catalog);
        var existing = alreadyThere.graph.nodes.First(value => value.id != alreadyThere.graph.villageNodeId);
        alreadyThere.graph.repairableRoads = new List<WildernessRoad> { new() { from = existing.id, to = existing.neighbors[0] } };
        Check(WildernessEventRules.Generate(alreadyThere, 2, 1).nodes.All(value => value.templateId != bridgeId), "existing edge is not a missing bridge");
        var missingEndpoint = Copy(catalog); missingEndpoint.graph.repairableRoads[0].to = "not-generated";
        Reject(() => WildernessEventRules.Generate(missingEndpoint, 3, 1), "bridge endpoints must already exist");
        var saturated = Copy(catalog);
        var endpoint = saturated.graph.nodes.Single(value => value.id == from);
        foreach (var other in saturated.graph.nodes.Where(value => value.id != from && value.id != to).ToArray())
            if (endpoint.neighbors.Count < 4 && other.neighbors.Count < 4 && !endpoint.neighbors.Contains(other.id)) AddRoad(saturated, from, other.id);
        Check(endpoint.neighbors.Count == 4, "fixture saturates one endpoint at degree four");
        Check(WildernessEventRules.Generate(saturated, 4, 1).nodes.All(value => value.templateId != bridgeId), "adding a fifth neighbor removes bridge from pool");
        var forged = Case(catalog, "断桥上的绳索");
        var pair = catalog.graph.nodes.SelectMany(a => catalog.graph.nodes.Select(b => (a, b)))
            .First(pair => pair.a.id != pair.b.id && !pair.a.neighbors.Contains(pair.b.id)
                && !catalog.graph.repairableRoads.Any(road => road.from == pair.a.id && road.to == pair.b.id
                    || road.from == pair.b.id && road.to == pair.a.id));
        forged.Node.bridgeFrom = pair.a.id; forged.Node.bridgeTo = pair.b.id;
        Check(!WildernessEventRules.CanRepairBridge(forged.Level, catalog, forged.Node.id), "non-catalog edges cannot be fabricated");
        var island = Copy(catalog);
        string partner = island.graph.nodes.First(value => value.id != from && value.id != to && value.id != island.graph.villageNodeId).id;
        foreach (var graphNode in island.graph.nodes)
        {
            if (graphNode.id == to) graphNode.neighbors = new List<string> { partner };
            else if (graphNode.id == partner) graphNode.neighbors = new List<string> { to };
            else graphNode.neighbors.RemoveAll(value => value == to || value == partner);
        }
        var islandFixture = Case(catalog, "断桥上的绳索");
        Check(!WildernessEventRules.CanRepairBridge(islandFixture.Level, island, islandFixture.Node.id), "bridge cannot be an island's only route home");
        Reject(() => WildernessEventRules.Generate(island, 5, 1), "generation rejects pre-repair islands");
    }
}
