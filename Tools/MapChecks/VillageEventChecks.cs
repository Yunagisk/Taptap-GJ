using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// Expected data below is transcribed independently from the village-event design tables.
public static class VillageEventChecks
{
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

    // id | earliest day | lifetime days | weight | choices:
    // time, sin, healing, first-hit reduction, complete-attack bonus, camp-recovery bonus, text-only item.
    static readonly string[] Expected =
    {
        "v01|1|3|3|1,-15,0,0,0,0,;1,20,0,0,0,0,绷带",
        "v02|1|2|3|1,-12,10,0,0,0,;1,15,0,15,0,0,",
        "v03|3|3|2|2,-20,0,0,0,0,净化剂;1,20,0,0,0,0,绷带",
        "v04|4|3|2|1,-15,0,0,0,0,绷带;1,15,25,0,0,0,",
        "v05|6|2|2|1,-15,0,0,0,0,;1,20,0,0,15,0,",
        "v06|7|3|2|2,-20,0,0,0,0,坚定药剂;1,15,0,0,0,0,",
        "v07|9|2|2|1,-15,10,0,0,0,;1,20,25,0,0,0,",
        "v08|10|2|2|2,-20,0,0,0,0,浓缩药剂;1,25,0,0,0,0,",
        "v09|12|3|1|1,-18,0,0,0,0,绷带;1,20,0,0,0,0,浓缩药剂",
        "v10|14|4|1|2,-20,0,0,0,20,;1,12,0,0,0,0,",
        "v11|16|3|1|1,-15,0,0,0,0,净化剂;1,15,0,0,0,0,绷带",
        "v12|20|4|1|2,-25,0,0,0,0,浓缩药剂;1,20,0,0,15,0,",
        "v13|1|2|2|1,-15,0,0,0,0,;1,0,0,0,0,0,;1,20,0,0,0,0,"
    };

    public static void Run(Func<MapLayout> loadLayout, Action<bool, string> check)
    {
        if (loadLayout == null || check == null) throw new ArgumentNullException();
        var suite = new Suite(loadLayout, check);
        suite.AllChoices();
        suite.OneTimeBonuses();
        suite.ClockAndSin();
        suite.GenerationAndPersistence();
        suite.LifetimesAndConditions();
        suite.InvalidCallbacks();
        suite.InvalidConfigurationIsAtomic();
        suite.RevivalAndNewRun();
    }

    sealed class Suite
    {
        readonly Func<MapLayout> loadLayout;
        readonly Action<bool, string> check;

        public Suite(Func<MapLayout> loadLayout, Action<bool, string> check)
        {
            this.loadLayout = loadLayout;
            this.check = check;
        }

        void Check(bool condition, string name) { check(condition, "village events: " + name); }
        static string Snapshot<T>(T value) { return JsonSerializer.Serialize(value, Json); }
        static GameState Clone(GameState value) { return JsonSerializer.Deserialize<GameState>(Snapshot(value), Json); }

        GameState Fresh(MapLayout layout, int seed = 24680)
        {
            return NewRunFactory.Create(layout, "sword", seed);
        }

        // Fixtures supply an actual appeared occurrence, then exercise production settlement.
        GameState WithEvent(MapLayout layout, string definitionId, int appearedTime = 0, int lifetime = 99)
        {
            var state = Fresh(layout);
            state.world.time = appearedTime;
            state.world.sin = 40;
            state.player.hp = 35;
            state.world.villageEvents = new VillageEventState
            {
                initialized = true,
                randomSeed = state.map.randomSeed,
                lastProcessedTime = appearedTime
            };
            state.world.villageEvents.occurrences.Add(new VillageEventOccurrence
            {
                id = "test:" + definitionId,
                definitionId = definitionId,
                appearedTime = appearedTime,
                expiresTime = appearedTime + lifetime
            });
            return state;
        }

        public void AllChoices()
        {
            int choiceCount = 0;
            foreach (string spec in Expected)
            {
                var columns = spec.Split('|');
                string id = columns[0];
                int day = int.Parse(columns[1]), duration = int.Parse(columns[2]), weight = int.Parse(columns[3]);
                var choices = columns[4].Split(';');
                var definition = VillageEventRules.FindDefinition(id);
                Check(definition != null, id + " exists");
                Check(definition.earliestDay == day && definition.durationDays == duration && definition.weight == weight,
                    id + " earliest day / duration / weight match the design table");
                Check(!string.IsNullOrWhiteSpace(definition.title) && !string.IsNullOrWhiteSpace(definition.description),
                    id + " provides player-facing title and description");
                Check(definition.requiresLivingSloth == (id == "v13"), id + " has the correct Sloth condition");
                Check(definition.choices.Length == choices.Length, id + " number of choices");
                for (int index = 0; index < choices.Length; index++)
                {
                    choiceCount++;
                    var expected = choices[index].Split(',');
                    int cost = int.Parse(expected[0]), sin = int.Parse(expected[1]), heal = int.Parse(expected[2]);
                    int hit = int.Parse(expected[3]), attack = int.Parse(expected[4]), camp = int.Parse(expected[5]);
                    string item = expected[6];
                    var choice = definition.choices[index];
                    string name = id + "/" + index;
                    Check(choice.timeCost == cost && choice.sinDelta == sin && choice.healAmount == heal,
                        name + " exact time, sin, and healing");
                    Check(choice.nextDirectHitReductionPercent == hit && choice.nextAttackBonusPercent == attack
                        && choice.nextCampRecoveryBonusPercent == camp, name + " exact one-use bonus");
                    Check(string.IsNullOrEmpty(item) ? string.IsNullOrEmpty(choice.rewardText)
                        : choice.rewardText != null && choice.rewardText.Contains(item) && choice.rewardText.Contains("1"),
                        name + " exact text-only item reward");
                    Check(!string.IsNullOrWhiteSpace(choice.label), name + " has a selectable label");

                    var layout = loadLayout();
                    var state = WithEvent(layout, id, (day - 1) * 3, duration * 3);
                    string inventoryBefore = Snapshot(state.inventory);
                    int remnant = state.world.sinRemnant;
                    int start = state.world.time;
                    var occurrence = state.world.villageEvents.occurrences[0];
                    var result = VillageEventRules.TryChoose(state, layout, occurrence.id, index);
                    Check(result.success && !string.IsNullOrEmpty(result.message), name + " settles successfully with feedback");
                    Check(occurrence.resolved && occurrence.chosenIndex == index,
                        name + " commits one-time resolution");
                    Check(state.world.time == start + cost, name + " advances the shared clock exactly once");
                    Check(state.world.sin == 40 + sin, name + " applies only the selected sin change inside village");
                    Check(state.player.hp == 35 + heal, name + " heals the shared player exactly");
                    Check(state.player.nextDirectHitReductionPercent == hit
                        && state.player.nextAttackBonusPercent == attack
                        && state.player.nextCampRecoveryBonusPercent == camp, name + " stores only the specified bonus");
                    Check(state.world.sinRemnant == remnant && Snapshot(state.inventory) == inventoryBefore,
                        name + " does not exchange sin for currency or grant actual items");
                    Check(state.world.villageEvents.rewardTexts.Count == (item.Length == 0 ? 0 : 1),
                        name + " records each item reward only as text");
                    if (item.Length > 0)
                        Check(state.world.villageEvents.rewardTexts[0].Contains(item)
                            && result.message.Contains(item) && result.message.Contains("文字"),
                            name + " reports its temporary text reward");
                    string after = Snapshot(state);
                    Check(!VillageEventRules.TryChoose(state, layout, occurrence.id, index).success
                        && Snapshot(state) == after, name + " duplicate click changes no state");
                    var restored = Clone(state);
                    new MapProgression(layout, restored.map, restored.world, -999);
                    VillageEventRules.InitializeForRun(restored, layout);
                    VillageEventRules.GetActiveEvents(restored, layout);
                    Check(Snapshot(restored) == after, name + " full-state JSON reload preserves settlement and random sequence");
                    Check(!VillageEventRules.TryChoose(restored, layout, occurrence.id, index).success
                        && Snapshot(restored) == after, name + " reload cannot duplicate rewards");
                }
            }
            Check(choiceCount == 27, "all 27 choices across 12 ordinary events and the 3-choice conditional event were covered");
            Check(VillageEventRules.FindDefinition(null) == null && VillageEventRules.FindDefinition("not-an-event") == null,
                "unknown definitions are not invented");
        }

        public void OneTimeBonuses()
        {
            var layout = loadLayout();
            foreach (var pair in new[] { new[] { "v02", "1" }, new[] { "v05", "1" }, new[] { "v10", "0" } })
            {
                var state = WithEvent(layout, pair[0]);
                state.player.nextDirectHitReductionPercent = 30;
                state.player.nextAttackBonusPercent = 30;
                state.player.nextCampRecoveryBonusPercent = 30;
                Check(VillageEventRules.TryChoose(state, layout, state.world.villageEvents.occurrences[0].id,
                    int.Parse(pair[1])).success, pair[0] + " bonus choice remains available with an existing bonus");
                Check(state.player.nextDirectHitReductionPercent == 30 && state.player.nextAttackBonusPercent == 30
                    && state.player.nextCampRecoveryBonusPercent == 30, pair[0] + " never stacks or downgrades existing bonuses");
            }
            var healing = WithEvent(layout, "v04");
            healing.player.hp = 95;
            VillageEventRules.TryChoose(healing, layout, "test:v04", 1);
            Check(healing.player.hp == 100, "event healing respects global max HP");
            healing.player.hp = 50;
            healing.player.nextCampRecoveryBonusPercent = 20;
            Check(WorldResourceRules.RecoverAtCamp(healing.player) == 30 && healing.player.hp == 80
                && healing.player.nextCampRecoveryBonusPercent == 0, "camp consumes its 20 percent bonus on actual recovery");
            Check(WorldResourceRules.RecoverAtCamp(healing.player) == 20 && healing.player.hp == 100,
                "later camp recovery does not reuse the consumed bonus");
            healing.player.nextCampRecoveryBonusPercent = 20;
            Check(WorldResourceRules.RecoverAtCamp(healing.player) == 0 && healing.player.nextCampRecoveryBonusPercent == 20,
                "full-health camp does not waste a recovery bonus");
        }

        public void ClockAndSin()
        {
            var layout = loadLayout();
            var state = Fresh(layout);
            Check(state.world.sin == 40 && state.world.time == 0 && WorldResourceRules.Day(state.world) == 1,
                "new run starts on day 1 at 40 sin");
            Check(layout.sinPerMoveMin == 3 && layout.sinPerMoveMax == 3,
                "outside natural growth is configured at 3 per world unit");
            WorldResourceRules.SpendTime(layout, state.map, state.world, 1, true);
            Check(state.world.sin == 40 && state.world.time == 1, "one village unit has no natural sin growth");
            WorldResourceRules.SpendTime(layout, state.map, state.world, 2, true);
            Check(state.world.sin == 40 && state.world.time == 3 && WorldResourceRules.Day(state.world) == 2,
                "three periods advance one day without village natural growth");
            Check(state.map.checkpointNodeId == layout.villageNodeId && state.map.checkpointTime == 3,
                "morning in the village stores the current checkpoint");
            WorldResourceRules.SpendTime(layout, state.map, state.world, 2, false);
            Check(state.world.time == 5 && state.world.sin == 46, "two wilderness time units add exactly 6 sin");
            string wilderness = layout.nodes.First(node => node.kind == MapNodeKind.Battle).id;
            state.map.currentNodeId = wilderness;
            WorldResourceRules.SpendTime(layout, state.map, state.world, 1, false);
            Check(state.map.checkpointNodeId == wilderness && state.map.checkpointTime == 6 && state.world.sin == 49,
                "wilderness morning updates checkpoint and still charges natural sin");

            int remnant = state.world.sinRemnant;
            state.world.sin = 3;
            WorldResourceRules.ChangeSin(layout, state.map, state.world, -15);
            Check(state.world.sin == 0 && state.world.sinRemnant == remnant, "sin clamps at zero and never changes sin remnants");
            while (state.map.spawnedBossNodes.Count < layout.regions.Count)
            {
                int before = state.map.spawnedBossNodes.Count;
                state.world.sin = 99;
                string spawned = WorldResourceRules.ChangeSin(layout, state.map, state.world, 1);
                Check(spawned != null && state.world.sin == 0 && state.map.spawnedBossNodes.Count == before + 1
                    && state.map.revealedNodes.Contains(spawned), "reaching 100 reveals one next boss and resets sin");
            }
            state.world.sin = 99;
            WorldResourceRules.ChangeSin(layout, state.map, state.world, int.MaxValue);
            Check(state.world.sin == 100, "sin remains capped at 100 when all bosses are already spawned");
            WorldResourceRules.ChangeSin(layout, state.map, state.world, int.MinValue);
            Check(state.world.sin == 0, "large negative changes still clamp without overflow");
            state.world.sin = 99;
            WorldResourceRules.SpendTime(layout, state.map, state.world, 1, false);
            Check(state.world.sin == 100, "natural sin still advances after all bosses are spawned");

            var byChoice = WithEvent(layout, "v01");
            byChoice.world.sin = 90;
            int bosses = byChoice.map.spawnedBossNodes.Count;
            var choiceResult = VillageEventRules.TryChoose(byChoice, layout, "test:v01", 1);
            Check(choiceResult.success && choiceResult.spawnedBossId != null && byChoice.world.sin == 0
                && byChoice.map.spawnedBossNodes.Count == bosses + 1,
                "a tavern sin increase uses the same threshold boss-spawn rule");

            var combat = Fresh(layout);
            var progression = new MapProgression(layout, combat.map, combat.world, 1);
            string target = layout.nodes.First(node => node.id == layout.villageNodeId).neighbors
                .First(id => layout.nodes.Any(node => node.id == id && node.kind == MapNodeKind.Battle));
            Check(progression.TryMove(target, out var request) && request != null, "combat regression can enter an ordinary fight");
            int beforeSin = combat.world.sin;
            Check(progression.TryCompleteNode(request.requestId) && combat.world.sin == beforeSin + 3,
                "clearing a monster spends outside time and never subtracts sin");
        }

        public void GenerationAndPersistence()
        {
            var layout = loadLayout();
            var first = Fresh(layout, 1001);
            var second = Fresh(layout, 1001);
            Check(Snapshot(first) == Snapshot(second), "same run seed produces the same initial event history");
            int maxObservedIncrease = 0;
            for (int tick = 0; tick < 120; tick++)
            {
                int count = first.world.villageEvents.occurrences.Count;
                WorldResourceRules.SpendTime(layout, first.map, first.world, 1, true);
                WorldResourceRules.SpendTime(layout, second.map, second.world, 1, true);
                for (int query = 0; query < 3; query++)
                {
                    VillageEventRules.InitializeForRun(first, layout);
                    VillageEventRules.GetActiveEvents(first, layout);
                }
                int added = first.world.villageEvents.occurrences.Count - count;
                maxObservedIncrease = Math.Max(maxObservedIncrease, added);
                Check(added >= 0 && added <= 2, "each world unit can produce at most two new occurrences, tick " + tick);
                Check(Snapshot(first.world.villageEvents) == Snapshot(second.world.villageEvents),
                    "queries and repeated initialization never reroll events, tick " + tick);
            }
            Check(maxObservedIncrease == 2, "a single world unit can produce both successful attempts");
            var records = first.world.villageEvents.occurrences;
            Check(records.Count == 13 && records.Select(item => item.definitionId).Distinct().Count() == 13,
                "the finite pool appears at most once per definition, including expired events");
            foreach (var record in records)
            {
                var definition = VillageEventRules.FindDefinition(record.definitionId);
                Check(record.appearedTime / 3 + 1 >= definition.earliestDay,
                    record.definitionId + " never appears before its earliest day");
                Check(record.expiresTime - record.appearedTime == definition.durationDays * 3,
                    record.definitionId + " lifetime starts at its actual appearance");
            }
            string unchanged = Snapshot(first);
            for (int query = 0; query < 20; query++)
            {
                VillageEventRules.GetActiveEvents(first, layout);
                VillageEventRules.InitializeForRun(first, layout);
                VillageEventRules.EnsureInitialized(first.world);
                new MapProgression(layout, first.map, first.world, 77);
            }
            Check(Snapshot(first) == unchanged, "reopening/reloading an exhausted pool neither rolls nor revives expired events");
            var restored = Clone(first);
            WorldResourceRules.SpendTime(layout, first.map, first.world, 7, true);
            WorldResourceRules.SpendTime(layout, restored.map, restored.world, 7, true);
            Check(Snapshot(first) == Snapshot(restored), "JSON roundtrip retains the deterministic future random sequence");

            // Distribution is checked by observed outcomes, independent of the RNG/hash implementation.
            int[] counts = new int[3];
            var firstIds = new Dictionary<string, int> { { "v01", 0 }, { "v02", 0 }, { "v13", 0 } };
            const int samples = 1800;
            for (int seed = 1; seed <= samples; seed++)
            {
                var sample = Fresh(layout, seed);
                var occurrences = sample.world.villageEvents.occurrences;
                Check(occurrences.Count <= 2, "initial generation never exceeds two attempts, seed " + seed);
                counts[occurrences.Count]++;
                if (occurrences.Count > 0) firstIds[occurrences[0].definitionId]++;
            }
            Check(Math.Abs(counts[0] / (double)samples - 0.2025) < 0.05
                && Math.Abs(counts[1] / (double)samples - 0.4950) < 0.05
                && Math.Abs(counts[2] / (double)samples - 0.3025) < 0.05,
                "observed 0/1/2 outcomes agree with two independent 55 percent attempts");
            int nonempty = samples - counts[0];
            Check(Math.Abs(firstIds["v01"] / (double)nonempty - 0.375) < 0.06
                && Math.Abs(firstIds["v02"] / (double)nonempty - 0.375) < 0.06
                && Math.Abs(firstIds["v13"] / (double)nonempty - 0.250) < 0.06,
                "initial weighted pool honors 3:3:2 weights");

            var migration = Fresh(layout);
            migration.world.time = 100;
            migration.world.sin = 73;
            migration.player.hp = 41;
            migration.world.villageEvents = null;
            VillageEventRules.EnsureInitialized(migration.world);
            Check(migration.world.time == 100 && migration.world.sin == 73 && migration.player.hp == 41,
                "missing event state is repaired without resetting existing world/player resources");
            VillageEventRules.InitializeForRun(migration, layout);
            Check(migration.world.villageEvents.occurrences.Count <= 2
                && migration.world.villageEvents.occurrences.All(item => item.appearedTime == 100),
                "old-run migration rolls only the current time, with no historical backlog");
            Check(migration.world.time == 100 && migration.world.sin == 73 && migration.player.hp == 41,
                "event migration is free of resource changes");
        }

        public void LifetimesAndConditions()
        {
            var layout = loadLayout();
            var timed = WithEvent(layout, "v01", 5, 9);
            timed.world.time = 13;
            Check(VillageEventRules.GetActiveEvents(timed, layout).Any(item => item.id == "test:v01"),
                "an event remains live until the full duration after its actual appearance");
            timed.world.time = 14;
            Check(!VillageEventRules.GetActiveEvents(timed, layout).Any(item => item.id == "test:v01")
                && timed.world.villageEvents.occurrences[0].expired, "event expires exactly at its appearance-based deadline");
            int before = timed.world.time, sin = timed.world.sin, hp = timed.player.hp;
            Check(!VillageEventRules.TryChoose(timed, layout, "test:v01", 0).success
                && timed.world.time == before && timed.world.sin == sin && timed.player.hp == hp,
                "expired choices cannot spend time or grant outcomes");
            WorldResourceRules.SpendTime(layout, timed.map, timed.world, 100, true);
            Check(timed.world.villageEvents.occurrences.Count(item => item.definitionId == "v01") == 1,
                "an expired event never returns to the one-time pool");

            string slothId = layout.regions.First(region => region.bossKey == "sloth").bossNodeId;
            var conditional = WithEvent(layout, "v13", 0, 6);
            Check(VillageEventRules.GetActiveEvents(conditional, layout).Count == 1,
                "conspiracy is offered while Sloth lives");
            string prior = Snapshot(conditional.world.villageEvents);
            VillageEventRules.GetActiveEvents(conditional, null);
            Check(Snapshot(conditional.world.villageEvents) == prior,
                "temporarily missing map layout cannot invalidate a live conditional event");
            conditional.map.defeatedBossNodes.Add(slothId);
            Check(VillageEventRules.GetActiveEvents(conditional, layout).Count == 0
                && conditional.world.villageEvents.occurrences[0].expired,
                "Sloth death immediately invalidates an unresolved conspiracy");
            before = conditional.world.time; sin = conditional.world.sin;
            Check(!VillageEventRules.TryChoose(conditional, layout, "test:v13", 2).success
                && conditional.world.time == before && conditional.world.sin == sin,
                "a stale conspiracy choice after Sloth death cannot settle");
            WorldResourceRules.SpendTime(layout, conditional.map, conditional.world, 10, true);
            Check(conditional.world.villageEvents.occurrences.Count(item => item.definitionId == "v13") == 1,
                "invalidated conspiracy never reappears");

            var deadFirst = Fresh(layout);
            deadFirst.map.defeatedBossNodes.Add(slothId);
            deadFirst.world.villageEvents = new VillageEventState();
            VillageEventRules.InitializeForRun(deadFirst, layout);
            WorldResourceRules.SpendTime(layout, deadFirst.map, deadFirst.world, 70, true);
            Check(deadFirst.world.villageEvents.occurrences.All(item => item.definitionId != "v13"),
                "a dead Sloth excludes conspiracy before any generation attempt");
        }

        public void InvalidCallbacks()
        {
            var layout = loadLayout();
            var state = WithEvent(layout, "v01");
            string before = Snapshot(state);
            Check(!VillageEventRules.TryChoose(state, layout, "missing", 0).success && Snapshot(state) == before,
                "unknown occurrence is a no-op");
            Check(!VillageEventRules.TryChoose(state, layout, "test:v01", -1).success && Snapshot(state) == before,
                "negative choice index is a no-op");
            Check(!VillageEventRules.TryChoose(state, layout, "test:v01", 100).success && Snapshot(state) == before,
                "out-of-range choice index is a no-op");
            Check(!VillageEventRules.TryChoose(state, null, "test:v01", 0).success && Snapshot(state) == before,
                "missing layout is rejected before committing a choice");
            state.map.currentNodeId = layout.nodes.First(node => node.kind == MapNodeKind.Battle).id;
            before = Snapshot(state);
            Check(!VillageEventRules.TryChoose(state, layout, "test:v01", 0).success && Snapshot(state) == before,
                "a callback outside the village is a complete no-op");
            state.map.currentNodeId = layout.villageNodeId;
            state.map.pendingNodeId = "battle";
            state.map.pendingRequestId = "request";
            before = Snapshot(state);
            Check(!VillageEventRules.TryChoose(state, layout, "test:v01", 0).success && Snapshot(state) == before,
                "a pending battle blocks stale tavern callbacks");
            state.map.pendingRequestId = null;
            before = Snapshot(state);
            Check(!VillageEventRules.TryChoose(state, layout, "test:v01", 0).success && Snapshot(state) == before,
                "an orphaned pending node also blocks tavern callbacks");
            // The event is live for one more unit, but its two-unit choice would overflow the clock.
            var overflow = WithEvent(layout, "v03", int.MaxValue - 1, 1);
            before = Snapshot(overflow);
            Check(!VillageEventRules.TryChoose(overflow, layout, "test:v03", 0).success
                && Snapshot(overflow) == before && !overflow.world.villageEvents.occurrences[0].resolved,
                "clock boundary never partially commits a failed settlement");
        }

        public void InvalidConfigurationIsAtomic()
        {
            var changes = new Action<MapLayout>[]
            {
                layout => layout.regions = null,
                layout => layout.regions[0] = null,
                layout => layout.sinPerMoveMax = layout.sinPerMoveMin - 1,
                layout => layout.sinThreshold = 0
            };
            for (int index = 0; index < changes.Length; index++)
            {
                var layout = loadLayout();
                var state = WithEvent(layout, "v01");
                state.world.sin = 90;
                // This unresolved record would be marked expired if validation happened after Expire.
                state.world.villageEvents.occurrences.Add(new VillageEventOccurrence
                {
                    id = "previously-expiring", definitionId = "v02", appearedTime = -9, expiresTime = 0
                });
                state.world.villageEvents.rewardTexts.Add("a previously recorded reward");
                changes[index](layout);
                string before = Snapshot(state);
                var result = VillageEventRules.TryChoose(state, layout, "test:v01", 1);
                Check(!result.success && !string.IsNullOrWhiteSpace(result.message),
                    "malformed configuration is rejected without throwing, case " + index);
                Check(Snapshot(state) == before,
                    "malformed configuration preserves all resources, resolution, expiry and reward history, case " + index);
            }
            var missingLayout = loadLayout();
            var missingState = WithEvent(missingLayout, "v01");
            missingState.world.villageEvents = null;
            missingLayout.regions = null;
            string snapshot = Snapshot(missingState);
            Check(!VillageEventRules.TryChoose(missingState, missingLayout, "test:v01", 1).success
                && Snapshot(missingState) == snapshot,
                "invalid configuration cannot even initialize a missing event-state field");
        }

        public void RevivalAndNewRun()
        {
            var layout = loadLayout();
            var state = WithEvent(layout, "v01");
            Check(VillageEventRules.TryChoose(state, layout, "test:v01", 1).success,
                "pre-death event can settle a text-only reward");
            var progression = new MapProgression(layout, state.map, state.world, 7);
            string next = layout.nodes.First(node => node.id == layout.villageNodeId).neighbors
                .First(id => layout.nodes.Any(node => node.id == id && node.kind == MapNodeKind.Battle));
            Check(progression.TryMove(next, out var request) && request != null, "revival fixture enters a real pending battle");
            state.map.checkpointNodeId = layout.villageNodeId;
            string history = Snapshot(state.world.villageEvents);
            int time = state.world.time, sin = state.world.sin;
            Check(progression.TryReviveAtCheckpoint(request.requestId), "checkpoint revival succeeds through production progression");
            Check(state.world.time == time && state.world.sin == sin && Snapshot(state.world.villageEvents) == history,
                "revival never rewinds clock, sin, event occurrences, or reward history");
            Check(!VillageEventRules.TryChoose(state, layout, "test:v01", 1).success
                && state.world.villageEvents.rewardTexts.Count == 1, "revival cannot claim a previously settled event again");
            var fresh = Fresh(layout, state.map.randomSeed);
            Check(fresh.world.time == 0 && fresh.world.sin == 40 && fresh.player.hp == 100
                && fresh.player.nextDirectHitReductionPercent == 0 && fresh.player.nextAttackBonusPercent == 0
                && fresh.player.nextCampRecoveryBonusPercent == 0, "restart resets global resources and one-time bonuses");
            Check(!ReferenceEquals(fresh.world.villageEvents, state.world.villageEvents)
                && fresh.world.villageEvents.rewardTexts.Count == 0
                && fresh.world.villageEvents.occurrences.All(item => !item.resolved),
                "restart creates a new finite pool without inheriting old rewards or resolutions");
        }
    }
}
