using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

internal static partial class Program
{
    sealed class MapFixture
    {
        public MapLayout Layout;
        public WildernessEventCatalog Catalog;
        public GameState State;
        public MapProgression Map;
        public WildernessLevelState Level => State.map.wildernessEvents;
    }

    static MapFixture MapCase(MapLayout layout, int seed)
    {
        var state = new GameState();
        var map = new MapProgression(layout, state.map, state.world, seed);
        var catalog = MapWildernessRules.BuildCatalog(Catalog(), layout);
        MapWildernessRules.EnsureInitialized(state, layout, catalog);
        return new MapFixture { Layout = layout, State = state, Map = map, Catalog = catalog };
    }

    static MapFixture MapCase(MapLayout layout, string title)
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var fixture = MapCase(layout, seed);
            if (fixture.Level.nodes.Any(node => Definition(fixture.Catalog, title).id == node.templateId)) return fixture;
        }
        throw new Exception("No production-map fixture contains " + title);
    }

    static List<string> MapPath(MapFixture f, string destination)
    {
        var parents = new Dictionary<string, string> { [f.State.map.currentNodeId] = null };
        var queue = new Queue<string>(); queue.Enqueue(f.State.map.currentNodeId);
        while (queue.Count > 0)
        {
            string here = queue.Dequeue();
            if (here == destination) break;
            foreach (string next in f.Map.GetNeighbors(here))
                if (!parents.ContainsKey(next)) { parents.Add(next, here); queue.Enqueue(next); }
        }
        if (!parents.ContainsKey(destination)) throw new Exception("Missing map path to " + destination);
        var path = new List<string>();
        for (string here = destination; here != f.State.map.currentNodeId; here = parents[here]) path.Add(here);
        path.Reverse(); return path;
    }

    // Travel through actual production rules. No direct position/cleared-node setup is used.
    static MapNodeRequest WalkTo(MapFixture f, string destination)
    {
        var path = MapPath(f, destination);
        MapNodeRequest result = null;
        foreach (string id in path)
        {
            int time = f.State.world.time, sin = f.State.world.sin;
            int bosses = f.State.map.spawnedBossNodes.Count;
            Check(f.Map.TryMove(id, out result), "production path moves to " + id);
            Check(f.State.world.time == time + 1 && f.Level.currentNodeId == id,
                "map movement spends one unit and synchronizes wilderness position");
            Check(f.State.world.sin == (f.State.map.spawnedBossNodes.Count > bosses ? 0 : Math.Min(100, sin + 3)),
                "map movement has the existing +3 outdoor sin rule");
            if (id == destination) return result;
            if (result == null) continue;
            if (result.kind == MapNodeKind.Event)
            {
                Check(MapWildernessRules.TryOpen(f.State, f.Layout, f.Catalog, result), "intermediate question opens");
                WildernessEventRules.Leave(f.Level);
                Check(f.Map.FinishEvent(result.requestId), "intermediate unanswered question is abandoned");
            }
            else Check(f.Map.TryCompleteNode(result.requestId), "intermediate ordinary node completes");
        }
        return result;
    }

    static void CheckMapIntegration()
    {
        foreach (string name in new[] { "CenteredThreeSinWhitebox.json", "ThreeSinWhitebox.json" })
        {
            string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));
            var layout = JsonSerializer.Deserialize<MapLayout>(source, Json);
            MapLayoutValidator.Validate(layout);
            CheckSixSlotGeneration(layout);
            CheckIntegratedOptions(layout);
            CheckIntegratedRequestsAndPersistence(layout);
            CheckIntegratedAbandonAndMigration(layout);
            CheckIntegratedBridge(layout);
            CheckUnchangedMapKinds(layout);
            Check(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name)) == source,
                name + " integration never writes source map JSON");
        }
    }

    static void CheckSixSlotGeneration(MapLayout layout)
    {
        var source = Catalog(); string sourceBefore = Save(source), layoutBefore = Save(layout);
        var catalog = MapWildernessRules.BuildCatalog(source, layout);
        Check(layout.nodes.Count == 28 && layout.nodes.Count(node => node.kind == MapNodeKind.Event) == 6,
            "formal map keeps twenty-eight nodes and six Event locations");
        Check(catalog.graph.nodes.Count == 28 && catalog.questionNodeIds.Count == 6
            && catalog.questionNodeIds.ToHashSet().SetEquals(layout.nodes.Where(node => node.kind == MapNodeKind.Event).Select(node => node.id)),
            "six configured question IDs come exactly from real Event nodes");
        Check(catalog.graph.repairableRoads.Count == (layout.id == "three-sins-centered-whitebox-v1" ? 2 : 3),
            "only safe regional shortcuts enter the catalog; centered region three fails its regional battle quota");
        foreach (var region in layout.regions)
        {
            string from = "r" + region.id + "_y", to = "r" + region.id + "_w";
            var withCandidate = Copy(layout);
            withCandidate.nodes.Single(node => node.id == from).neighbors.Add(to);
            withCandidate.nodes.Single(node => node.id == to).neighbors.Add(from);
            bool valid = true;
            try { MapLayoutValidator.Validate(withCandidate); } catch (InvalidOperationException) { valid = false; }
            Check(catalog.graph.repairableRoads.Any(road => road.from == from && road.to == to) == valid,
                "full graph validation decides candidate eligibility " + layout.id + "/" + region.id);
        }
        Check(Save(source) == sourceBefore && Save(layout) == layoutBefore, "map catalog build is read-only");
        var selected = new HashSet<string>();
        for (int seed = 0; seed < 160; seed++)
        {
            var state = new GameState(); new MapProgression(layout, state.map, state.world, seed);
            MapWildernessRules.EnsureInitialized(state, layout, catalog);
            var level = state.map.wildernessEvents;
            Check(level.nodes.Count == 6 && level.nodes.Select(node => node.templateId).Distinct().Count() == 6,
                "production seed " + seed + " samples six unique templates");
            Check(level.nodes.Select(node => node.id).ToHashSet().SetEquals(catalog.questionNodeIds),
                "production seed " + seed + " uses six existing positions");
            Check(level.nodes.All(node => !node.shown && !node.resolved && !node.abandoned && node.chosenIndex == -1),
                "production seed " + seed + " begins unclaimed");
            string before = Save(state);
            MapWildernessRules.EnsureInitialized(state, layout, catalog);
            Check(Save(state) == before, "same run initialization does not reroll seed " + seed);
            var same = WildernessEventRules.Generate(catalog, seed, state.world.stage);
            Check(Save(same) == Save(level), "production seed " + seed + " uses deterministic weighted generation");
            WildernessEventRules.ValidateLevel(level, catalog);
            foreach (var node in level.nodes)
            {
                selected.Add(node.templateId);
                if (!string.IsNullOrEmpty(node.bridgeFrom))
                    Check(node.id == node.bridgeFrom || node.id == node.bridgeTo,
                        "production bridge event is located at the bridge being repaired");
                var definition = WildernessEventRules.FindDefinition(catalog, node.templateId);
                Check(node.rolls.Count == 2 && node.rolls.All(roll => roll != null), "all option outcomes are pre-rolled");
                for (int choice = 0; choice < 2; choice++)
                {
                    var outcome = definition.choices[choice].outcomes[node.rolls[choice].outcomeIndex];
                    Check(!outcome.encounter || catalog.enemyVariants.Any(enemy => enemy.id == node.rolls[choice].enemyId && enemy.stage == 1),
                        "pre-rolled enemy belongs to generation stage");
                }
            }
        }
        Check(selected.Count == 10, "six-location sampling still draws from all ten templates");
        Check(Save(source) == sourceBefore && Save(layout) == layoutBefore, "all sampling leaves both source documents intact");
    }

    static void CheckIntegratedOptions(MapLayout layout)
    {
        foreach (var expected in ExpectedCases.All)
            for (int choice = 0; choice < expected.Choices.Length; choice++)
                for (int branch = 0; branch < expected.Choices[choice].Outcomes.Length; branch++)
                {
                    var f = MapCase(layout, expected.Title);
                    var node = f.Level.nodes.Single(value => value.templateId == Definition(f.Catalog, expected.Title).id);
                    Force(f.Catalog, f.Level, node, choice, branch);
                    string rolls = Save(f.Level.nodes.Select(value => value.rolls).ToList());
                    var request = WalkTo(f, node.id);
                    Check(request != null && request.kind == MapNodeKind.Event && f.State.map.pendingNodeId == node.id,
                        "arriving creates the original Event request");
                    Check(MapWildernessRules.TryOpen(f.State, layout, f.Catalog, request), "current formal-map question opens");
                    string snapshot = Save(f.State);
                    Check(!f.Map.TryCompleteNode(request.requestId) && Save(f.State) == snapshot,
                        "legacy completion cannot bypass a configured question");
                    Check(!f.Map.FinishEvent(request.requestId) && Save(f.State) == snapshot,
                        "unfinished question cannot release the pending request");
                    f.State.world.sin = 40; f.State.player.hp = 50;
                    f.State.player.nextAttackBonusPercent = 9; f.State.player.nextDirectHitReductionPercent = 7;
                    f.State.inventory.items.Add(new ItemStack("existing-item", 3));
                    string inventory = Save(f.State.inventory);
                    int time = f.State.world.time, sinRolls = f.State.map.sinRollCount, coins = f.State.world.sinRemnant;
                    var want = expected.Choices[choice]; var outcome = want.Outcomes[branch];
                    int cost = want.Time + (outcome.Encounter ? 1 : 0);
                    string label = "formal " + expected.Title + " " + choice + "/" + branch;
                    var result = WildernessEventRules.TryChoose(f.Level, f.Catalog, f.State, layout, choice);
                    Check(result.success && node.chosenIndex == choice && node.resolved, label + " succeeds");
                    Check(f.State.world.time == time + cost && f.State.map.sinRollCount == sinRolls + cost,
                        label + " exact extra cost, no fixed placeholder completion charge");
                    Check(f.State.world.sin == Math.Clamp(40 + cost * 3 + outcome.Sin, 0, 100), label + " shared sin");
                    Check(f.State.player.hp == Math.Min(100, 50 + outcome.Heal), label + " shared health");
                    Check(Save(f.State.inventory) == inventory && f.State.player.nextAttackBonusPercent == 9
                        && f.State.player.nextDirectHitReductionPercent == 7, label + " item, attack and hit remain text");
                    Check(f.State.player.nextCampRecoveryBonusPercent == outcome.Camp, label + " camp bonus uses shared state");
                    Check(Save(f.Level.nodes.Select(value => value.rolls).ToList()) == rolls, label + " results never reroll");
                    snapshot = Save(f.State);
                    Check(!WildernessEventRules.TryChoose(f.Level, f.Catalog, f.State, layout, choice).success
                        && Save(f.State) == snapshot, label + " duplicate choice is inert");
                    if (outcome.Encounter)
                    {
                        Check(result.encounterPending && node.encounterPending && !string.IsNullOrEmpty(node.enemyId)
                            && result.message.Contains(f.Catalog.enemyVariants.Find(enemy => enemy.id == node.enemyId).name),
                            label + " encounter retains its fixed enemy");
                        Check(!f.Map.FinishEvent(request.requestId) && Save(f.State) == snapshot,
                            label + " hidden encounter must settle before map navigation");
                        WildernessEventRules.Leave(f.Level);
                        Check(Save(f.State) == snapshot, label + " closing cannot skip encounter");
                        var reloaded = Copy(f.State);
                        var loadedMap = new MapProgression(layout, reloaded.map, reloaded.world, -1);
                        MapWildernessRules.EnsureInitialized(reloaded, layout, f.Catalog);
                        Check(WildernessEventRules.ResolveEncounter(reloaded.map.wildernessEvents, reloaded, true).success,
                            label + " pending encounter resumes through whole GameState JSON");
                        Check(reloaded.world.sinRemnant == coins + 1 && reloaded.world.time == time + cost,
                            label + " exactly one currency and no second time charge");
                        string paid = Save(reloaded);
                        Check(!WildernessEventRules.ResolveEncounter(reloaded.map.wildernessEvents, reloaded, true).success
                            && Save(reloaded) == paid, label + " saved victory cannot pay twice");
                        Check(loadedMap.FinishEvent(request.requestId), label + " restored encounter releases map request");
                        Check(WildernessEventRules.ResolveEncounter(f.Level, f.State, false).success
                            && f.State.world.sinRemnant == coins, label + " text defeat grants no currency");
                    }
                    Check(f.Map.FinishEvent(request.requestId) && f.State.world.time == time + cost
                        && f.State.map.sinRollCount == sinRolls + cost && f.State.map.clearedNodes.Contains(node.id)
                        && string.IsNullOrEmpty(f.State.map.pendingRequestId), label + " finish reveals neighbors for free");
                    snapshot = Save(f.State);
                    Check(!f.Map.FinishEvent(request.requestId) && !MapWildernessRules.TryOpen(f.State, layout, f.Catalog, request)
                        && Save(f.State) == snapshot, label + " stale callbacks after finish are inert");
                    Check(f.Map.GetNeighbors(node.id).All(f.Map.IsVisible), label + " actual map neighbors are revealed");
                }
    }

    static void CheckIntegratedRequestsAndPersistence(MapLayout layout)
    {
        var f = MapCase(layout, "棺下敲击");
        var target = f.Level.nodes.Single(node => node.templateId == Definition(f.Catalog, "棺下敲击").id);
        var other = f.Level.nodes.First(node => node.id != target.id);
        string initial = Save(f.State);
        Check(!MapWildernessRules.TryOpen(f.State, layout, f.Catalog,
            new MapNodeRequest { requestId = "forged", nodeId = target.id, kind = MapNodeKind.Event })
            && Save(f.State) == initial, "opening before actual movement cannot mutate the run");
        var request = WalkTo(f, target.id);
        foreach (var wrong in new[] {
            new MapNodeRequest { requestId = "stale", nodeId = target.id, kind = MapNodeKind.Event },
            new MapNodeRequest { requestId = request.requestId, nodeId = other.id, kind = MapNodeKind.Event },
            new MapNodeRequest { requestId = request.requestId, nodeId = target.id, kind = MapNodeKind.Battle }, null })
        {
            string before = Save(f.State);
            Check(!MapWildernessRules.TryOpen(f.State, layout, f.Catalog, wrong) && Save(f.State) == before,
                "wrong or stale Event request is transactionally inert");
        }
        Check(MapWildernessRules.TryOpen(f.State, layout, f.Catalog, request), "valid request opens after stale callbacks");
        string opened = Save(f.State);
        Check(!MapWildernessRules.TryOpen(f.State, layout, f.Catalog, request) && Save(f.State) == opened,
            "same active request cannot regenerate prompt");
        var restored = Copy(f.State);
        Check(Save(restored) == opened, "whole GameState preserves all map and event fields");
        var originalObject = f.Level;
        f.Map = new MapProgression(layout, f.State.map, f.State.world, 99999);
        MapWildernessRules.EnsureInitialized(f.State, layout, f.Catalog);
        Check(ReferenceEquals(f.Level, originalObject) && Save(f.State) == opened,
            "scene reconstruction in same run keeps the existing level object and rolls");
        var restoredMap = new MapProgression(layout, restored.map, restored.world, -9999);
        MapWildernessRules.EnsureInitialized(restored, layout, f.Catalog);
        Check(Save(restored) == opened && restoredMap.GetPendingRequest().requestId == request.requestId,
            "deserializing run retains pending request and pre-generated results");
        f.State.world.stage = 3;
        string beforeStage = Save(f.Level);
        MapWildernessRules.EnsureInitialized(f.State, layout, f.Catalog);
        Check(Save(f.Level) == beforeStage && f.Level.stage == 1, "world stage changes do not redraw saved enemy stages");
        var fresh = MapCase(layout, 5678);
        Check(fresh.Level.seed == 5678 && fresh.Level.nodes.All(node => !node.shown && !node.resolved && !node.abandoned)
            && fresh.Level.history.Count == 0 && fresh.Level.repairedRoads.Count == 0,
            "new run gets fresh questions and no previous history or bridges");
        var invalid = Copy(f.State); invalid.map.randomSeed++;
        string bad = Save(invalid);
        Reject(() => MapWildernessRules.EnsureInitialized(invalid, layout, f.Catalog), "seed mismatch refuses regeneration");
        Check(Save(invalid) == bad, "seed mismatch leaves the run unchanged");
        invalid = Copy(f.State); invalid.map.currentNodeId = layout.villageNodeId;
        bad = Save(invalid);
        Reject(() => MapWildernessRules.EnsureInitialized(invalid, layout, f.Catalog), "active question cannot move independently of map");
        Check(Save(invalid) == bad, "invalid active location leaves the run unchanged");
    }

    static void CheckIntegratedAbandonAndMigration(MapLayout layout)
    {
        var f = MapCase(layout, 83); var node = f.Level.nodes[0];
        var request = WalkTo(f, node.id);
        Check(MapWildernessRules.TryOpen(f.State, layout, f.Catalog, request), "question opens before abandonment");
        string rolls = Save(node.rolls); int time = f.State.world.time, sin = f.State.world.sin;
        WildernessEventRules.Leave(f.Level);
        Check(node.shown && node.abandoned && !node.resolved && f.Map.FinishEvent(request.requestId),
            "unselected close permanently abandons and releases question node");
        Check(f.State.world.time == time && f.State.world.sin == sin, "unselected close adds no option time or sin");
        string away = f.Map.GetNeighbors(node.id).First(id => f.State.map.clearedNodes.Contains(id));
        Check(f.Map.TryMove(away, out var transit) && transit == null, "leave to cleared neighbor");
        Check(f.Map.TryMove(node.id, out transit) && transit == null && f.State.world.time == time + 2,
            "return to unanswered node costs ordinary roads and creates no request");
        Check(!MapWildernessRules.TryOpen(f.State, layout, f.Catalog, request) && Save(node.rolls) == rolls,
            "abandoned question remains consumed after revisiting");
        var loaded = Copy(f.State); new MapProgression(layout, loaded.map, loaded.world, -1);
        MapWildernessRules.EnsureInitialized(loaded, layout, f.Catalog);
        Check(loaded.map.wildernessEvents.nodes.Single(value => value.id == node.id).abandoned,
            "abandoned prompt persists through whole-run JSON");
        var legacy = new GameState(); var map = new MapProgression(layout, legacy.map, legacy.world, 88);
        var locations = layout.nodes.Where(value => value.kind == MapNodeKind.Event).Select(value => value.id).ToArray();
        legacy.map.visitedNodes.Add(locations[0]); legacy.map.clearedNodes.Add(locations[1]);
        int legacyTime = legacy.world.time, legacySin = legacy.world.sin;
        MapWildernessRules.EnsureInitialized(legacy, layout, f.Catalog);
        Check(legacy.map.wildernessEvents.nodes.Where(value => value.id == locations[0] || value.id == locations[1])
            .All(value => value.shown && value.abandoned && !value.resolved),
            "legacy visited or cleared questions migrate as consumed without rewards");
        Check(legacy.map.wildernessEvents.nodes.Count(value => !value.shown) == 4
            && legacy.world.time == legacyTime && legacy.world.sin == legacySin,
            "legacy migration preserves resources and four unseen questions");
        var legacyPending = new GameState(); var legacyMap = new MapProgression(layout, legacyPending.map, legacyPending.world, 92);
        var traversal = new MapFixture { State = legacyPending, Layout = layout, Catalog = f.Catalog, Map = legacyMap };
        // Set up an old pending Event using the real legacy travel and completion behavior.
        MapNodeRequest oldRequest = null;
        foreach (string id in MapPath(traversal, locations[0]))
        {
            Check(legacyMap.TryMove(id, out oldRequest), "legacy path moves before content initialization");
            if (id != locations[0] && oldRequest != null) Check(legacyMap.TryCompleteNode(oldRequest.requestId), "legacy intermediary settles");
        }
        MapWildernessRules.EnsureInitialized(legacyPending, layout, f.Catalog);
        Check(!MapWildernessRules.TryOpen(legacyPending, layout, f.Catalog, oldRequest)
            && legacyMap.FinishEvent(oldRequest.requestId), "legacy pending event can exit without issuing new rewards");
    }

    static void CheckIntegratedBridge(MapLayout layout)
    {
        string layoutBefore = Save(layout);
        var f = MapCase(layout, "断桥上的绳索");
        var node = f.Level.nodes.Single(value => value.templateId == Definition(f.Catalog, "断桥上的绳索").id);
        string from = node.bridgeFrom, to = node.bridgeTo;
        var request = WalkTo(f, node.id);
        Check(MapWildernessRules.TryOpen(f.State, layout, f.Catalog, request), "formal bridge event opens");
        Check(!f.Map.GetNeighbors(from).Contains(to) && !f.Map.GetNeighbors(to).Contains(from), "unrepaired road is absent in both directions");
        int time = f.State.world.time;
        Check(WildernessEventRules.TryChoose(f.Level, f.Catalog, f.State, layout, 0).success
            && f.State.world.time == time + 2, "formal bridge repair pays exactly two extra outdoor units");
        Check(f.Map.FinishEvent(request.requestId), "bridge Event finishes without placeholder charge");
        Check(f.Map.GetNeighbors(from).Contains(to) && f.Map.GetNeighbors(to).Contains(from),
            "production adjacency includes real bidirectional repaired road");
        Check(Save(layout) == layoutBefore, "repair never changes shared MapLayout");
        node.chosenIndex = 1;
        Check(!f.Map.GetNeighbors(from).Contains(to) && !f.Map.GetNeighbors(to).Contains(from),
            "forged search-under-bridge choice cannot authorize a repaired road");
        node.chosenIndex = 0;
        var combined = Copy(layout);
        foreach (var road in f.Level.repairedRoads)
        {
            combined.nodes.Single(value => value.id == road.from).neighbors.Add(road.to);
            combined.nodes.Single(value => value.id == road.to).neighbors.Add(road.from);
        }
        MapLayoutValidator.Validate(combined);
        Check(true, "repaired full graph retains all regional battle and boss constraints");
        var approach = WalkTo(f, from);
        if (approach != null)
        {
            if (approach.kind == MapNodeKind.Event)
            {
                Check(MapWildernessRules.TryOpen(f.State, layout, f.Catalog, approach), "bridge endpoint question opens once");
                WildernessEventRules.Leave(f.Level); Check(f.Map.FinishEvent(approach.requestId), "bridge endpoint released");
            }
            else Check(f.Map.TryCompleteNode(approach.requestId), "bridge endpoint battle completed");
        }
        Check(f.Map.IsVisible(to) && f.Map.IsRoadVisible(from, to) && f.Map.IsRoadVisible(to, from),
            "bridge reveals its real opposite endpoint and visible road");
        time = f.State.world.time;
        MapNodeRequest cross = null;
        Check(f.Map.CanMove(to) && f.Map.TryMove(to, out cross) && f.State.world.time == time + 1,
            "repaired shortcut is actually traversable at ordinary movement cost");
        if (cross != null)
        {
            if (cross.kind == MapNodeKind.Event)
            {
                Check(MapWildernessRules.TryOpen(f.State, layout, f.Catalog, cross), "far bridge endpoint question opens");
                WildernessEventRules.Leave(f.Level); Check(f.Map.FinishEvent(cross.requestId), "far endpoint question settles");
            }
            else Check(f.Map.TryCompleteNode(cross.requestId), "far bridge endpoint battle settles");
        }
        Check(f.Map.CanMove(from), "repaired road supports return travel");
        var loaded = Copy(f.State); var loadedMap = new MapProgression(layout, loaded.map, loaded.world, -1);
        MapWildernessRules.EnsureInitialized(loaded, layout, f.Catalog);
        Check(loadedMap.GetNeighbors(from).Contains(to) && loadedMap.IsRoadVisible(from, to) && loadedMap.CanMove(from),
            "bridge adjacency, visibility and navigation survive whole-run JSON reload");
        var injected = MapCase(layout, 55);
        injected.Level.repairedRoads.Add(new WildernessRoad { from = "r1_y", to = "r2_c" });
        Check(!injected.Map.GetNeighbors("r1_y").Contains("r2_c"), "serialized cross-region bypass cannot inject map road");
        injected.Level.repairedRoads.Clear();
        var candidate = injected.Catalog.graph.repairableRoads[0];
        injected.Level.repairedRoads.Add(Copy(candidate));
        Check(!injected.Map.GetNeighbors(candidate.from).Contains(candidate.to), "serialized candidate without settled repair cannot inject map road");
        injected.Level.repairedRoads.Clear();
        injected.Level.repairedRoads.Add(new WildernessRoad { from = layout.villageNodeId, to = "r1_boss" });
        Check(!injected.Map.GetNeighbors(layout.villageNodeId).Contains("r1_boss"), "serialized direct boss entrance is ignored");
        var all = Copy(layout);
        foreach (var road in MapWildernessRules.GetRepairableRoads(layout))
        {
            all.nodes.Single(value => value.id == road.from).neighbors.Add(road.to);
            all.nodes.Single(value => value.id == road.to).neighbors.Add(road.from);
        }
        MapLayoutValidator.Validate(all);
        Check(true, "all allowed shortcuts together preserve full map invariants");
    }

    static void CheckUnchangedMapKinds(MapLayout layout)
    {
        var f = MapCase(layout, 111);
        Check(f.Map.CanReenterVillage(layout.villageNodeId) && f.State.world.time == 0, "same-node village entry remains free");
        Check(f.Map.TryMove("r1_a", out var first) && first.kind == MapNodeKind.Battle,
            "ordinary battle still issues original request");
        Check(!f.Map.FinishEvent(first.requestId), "event-only completion cannot clear ordinary battle");
        int time = f.State.world.time;
        Check(f.Map.TryCompleteNode(first.requestId) && f.State.world.time == time + 1,
            "ordinary battle keeps existing settlement time");
        var camp = WalkTo(f, "r1_camp"); time = f.State.world.time;
        Check(camp.kind == MapNodeKind.Camp && !f.Map.FinishEvent(camp.requestId)
            && f.Map.TryCompleteNode(camp.requestId) && f.State.world.time == time + 1,
            "camp retains original completion behavior");
        Check(f.Map.TryMove("r1_boss", out var boss) && boss.kind == MapNodeKind.Boss, "original Sloth entrance remains reachable");
        time = f.State.world.time;
        Check(!f.Map.FinishEvent(boss.requestId) && f.Map.TryCompleteNode(boss.requestId)
            && f.State.world.time == time + 1 && f.State.map.defeatedBossNodes.Contains("r1_boss"),
            "boss completion and defeated-boss history stay intact");
        var revive = MapCase(layout, 333);
        Check(revive.Map.TryMove("r1_a", out var pending), "revival fixture starts ordinary battle");
        string levelBefore = Save(revive.Level.nodes);
        time = revive.State.world.time;
        Check(revive.Map.TryReviveAtCheckpoint(pending.requestId) && revive.State.map.currentNodeId == layout.villageNodeId
            && revive.Level.currentNodeId == layout.villageNodeId && revive.State.world.time == time
            && Save(revive.Level.nodes) == levelBefore, "checkpoint revival synchronizes event position without reroll or extra time");
    }
}
