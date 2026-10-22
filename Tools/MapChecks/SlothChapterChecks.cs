using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

static class SlothChapterChecks
{
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    static Action<bool, string> check;

    public static void Run(Func<MapLayout> load, Action<bool, string> assert)
    {
        check = assert;
        CheckRequestIdentity();
        CheckRejectedDecisions(load());
        foreach (var fate in new[] { SlothFate.Spared, SlothFate.Killed })
            CheckCompleteBranch(load(), fate);
        CheckChapterVisibilityAndWorldRules(load());
        CheckBossCheckpoint(load());
    }

    static string Snapshot(object value) => JsonSerializer.Serialize(value, Json);
    static GameState Clone(GameState state) => JsonSerializer.Deserialize<GameState>(Snapshot(state), Json);
    static string Gameplay(GameState state) => Snapshot(new
    {
        state.player, state.inventory, state.world, state.map, state.shop, state.monsterWeaknesses
    });

    static void RejectWithoutMutation(GameState state, Func<bool> action, string name)
    {
        string before = Snapshot(state);
        check(!action() && Snapshot(state) == before, name);
    }

    static GameState Ready(MapLayout layout, int seed = 851)
    {
        var state = NewRunFactory.Create(layout, "sword", seed);
        state.story.stage = MainStoryStage.TutorialCompleted;
        return state;
    }

    // Follow actual graph edges and settle each encounter; fixtures do not teleport to the boss.
    static List<string> Route(MapLayout layout, string from, string to)
    {
        var previous = new Dictionary<string, string> { [from] = null };
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.Count > 0 && !previous.ContainsKey(to))
        {
            string currentId = queue.Dequeue();
            var current = layout.nodes.Find(node => node.id == currentId);
            foreach (string id in current.neighbors)
            {
                var next = layout.nodes.Find(node => node.id == id);
                if (previous.ContainsKey(id) || next.kind == MapNodeKind.Boss && id != to) continue;
                previous.Add(id, current.id);
                queue.Enqueue(id);
            }
        }
        check(previous.ContainsKey(to), "sloth chapter has a real route between its village and boss");
        var result = new List<string>();
        for (string id = to; id != from; id = previous[id]) result.Add(id);
        result.Reverse();
        return result;
    }

    static MapNodeRequest EnterBoss(MapLayout layout, GameState state, bool atDawn = false)
    {
        var map = new MapProgression(layout, state.map, state.world, state.map.randomSeed);
        var route = Route(layout, state.map.currentNodeId, state.story.slothNodeId);
        foreach (string id in route)
        {
            bool boss = id == state.story.slothNodeId;
            if (boss && atDawn)
            {
                int untilNight = (2 - state.world.time % 3 + 3) % 3;
                WorldResourceRules.SpendTime(layout, state.map, state.world, untilNight, false);
            }
            int before = state.world.time;
            check(map.TryMove(id, out var request) && state.world.time == before + 1,
                "sloth chapter approaches each node through a charged real road");
            if (boss)
            {
                check(SlothChapterRules.IsSlothRequest(request), "boss approach produces a real sloth request");
                return request;
            }
            if (request != null)
                check(map.TryCompleteNode(request.requestId), "sloth approach settles its actual encounter");
        }
        throw new Exception("Missing Sloth boss request.");
    }

    static void CheckRequestIdentity()
    {
        check(!SlothChapterRules.IsSlothRequest(null), "null request cannot select the sloth chapter");
        check(!SlothChapterRules.IsSlothRequest(new MapNodeRequest { kind = MapNodeKind.Battle, bossKey = "sloth" }),
            "ordinary battle cannot masquerade as sloth by key");
        check(!SlothChapterRules.IsSlothRequest(new MapNodeRequest { kind = MapNodeKind.Boss, bossKey = "placeholder_2" }),
            "unfinished boss key cannot select the sloth chapter");
        check(SlothChapterRules.IsSlothRequest(new MapNodeRequest { kind = MapNodeKind.Boss, bossKey = "sloth" }),
            "the authored boss identity selects the sloth chapter");
    }

    static void CheckRejectedDecisions(MapLayout layout)
    {
        check(!SlothChapterRules.TryChooseFate(null, SlothFate.Spared), "null run rejects a fate");
        var missing = new GameState { story = null };
        RejectWithoutMutation(missing, () => SlothChapterRules.TryChooseFate(missing, SlothFate.Spared),
            "missing story rejects a fate without inventing progress");
        missing = new GameState { map = null };
        RejectWithoutMutation(missing, () => SlothChapterRules.TryChooseFate(missing, SlothFate.Spared),
            "missing map rejects a fate without changing state");
        var state = Ready(layout);
        RejectWithoutMutation(state, () => SlothChapterRules.TryChooseFate(state, SlothFate.Spared),
            "unfought sloth cannot receive an outcome in the village");
        EnterBoss(layout, state);
        RejectWithoutMutation(state, () => SlothChapterRules.TryChooseFate(state, SlothFate.Killed),
            "an unconfronted boss request cannot receive an outcome");
        state.story.slothConfronted = true;
        foreach (var fate in new[] { SlothFate.Undecided, (SlothFate)(-1), (SlothFate)99 })
            RejectWithoutMutation(state, () => SlothChapterRules.TryChooseFate(state, fate),
                "unset and unknown outcomes are rejected atomically");
        var wrongPosition = Clone(state);
        wrongPosition.map.currentNodeId = layout.villageNodeId;
        RejectWithoutMutation(wrongPosition, () => SlothChapterRules.TryChooseFate(wrongPosition, SlothFate.Spared),
            "an obsolete pending boss away from the current node cannot receive a fate");
        var wrongPending = Clone(state);
        wrongPending.map.pendingNodeId = layout.villageNodeId;
        RejectWithoutMutation(wrongPending, () => SlothChapterRules.TryChooseFate(wrongPending, SlothFate.Spared),
            "a different active node cannot receive sloth's fate");
        var noRequest = Clone(state);
        noRequest.map.pendingRequestId = null;
        RejectWithoutMutation(noRequest, () => SlothChapterRules.TryChooseFate(noRequest, SlothFate.Spared),
            "a missing request cannot authorize an unsettled fate");
        var noIdentity = Clone(state);
        noIdentity.story.slothNodeId = null;
        RejectWithoutMutation(noIdentity, () => SlothChapterRules.TryChooseFate(noIdentity, SlothFate.Spared),
            "missing sloth identity cannot authorize a fate");
        RejectWithoutMutation(state, () => SlothChapterRules.TryFinishChapter(state),
            "an ongoing boss encounter cannot finish the chapter");
    }

    static void CheckCompleteBranch(MapLayout layout, SlothFate fate)
    {
        var state = Ready(layout, 860 + (int)fate);
        var request = EnterBoss(layout, state);
        state.story.slothConfronted = true;
        state.player.hp = 37;
        state.player.resolve = 2;
        state.player.nextAttackBonusPercent = 15;
        state.player.nextDirectHitReductionPercent = 10;
        state.player.nextCampRecoveryBonusPercent = 20;
        state.inventory.items.Add(new ItemStack(ItemTable.HolyWater, 1));
        string before = Gameplay(state);
        // Victory is checked by BattleResultUI; these pure rules validate the exact pending location.
        check(SlothChapterRules.TryChooseFate(state, fate) && state.story.slothFate == fate,
            "victory presentation records the selected outcome for its real pending sloth request");
        check(Gameplay(state) == before && state.story.stage == MainStoryStage.TutorialCompleted,
            "choosing a fate changes no resource, buff, clock, inventory, map or completion stage");
        RejectWithoutMutation(state, () => SlothChapterRules.TryChooseFate(state, fate),
            "double clicking the same fate never grants or settles anything twice");
        RejectWithoutMutation(state, () => SlothChapterRules.TryChooseFate(state,
            fate == SlothFate.Spared ? SlothFate.Killed : SlothFate.Spared),
            "a recorded fate cannot be replaced with the other branch");
        RejectWithoutMutation(state, () => MainStoryRules.TryCompleteDialogue(state,
            "obsolete_turn_in", StoryDialogueEffect.TurnInCommission),
            "the old turn-in dialogue cannot bypass the submission ending");
        state = Clone(state);
        check(state.story.slothFate == fate && state.story.slothConfronted,
            "the selected fate survives serialization before map settlement");
        var map = new MapProgression(layout, state.map, state.world, state.map.randomSeed);
        int time = state.world.time;
        check(map.TryCompleteNode(request.requestId) && state.world.time == time + 1,
            "sloth battle settlement charges the existing single unit");
        MainStoryRules.OnNodeCompleted(state, request);
        check(state.story.stage == MainStoryStage.SlothDefeated
            && state.map.defeatedBossNodes.Contains(request.nodeId)
            && state.map.clearedNodes.Contains(request.nodeId)
            && state.map.currentNodeId == request.nodeId,
            "real boss completion advances the story while keeping the player at the boss");
        RejectWithoutMutation(state, () => map.TryCompleteNode(request.requestId),
            "repeated boss settlement cannot charge another unit");
        check(!SlothChapterRules.CanEndAtVillage(state) && !SlothChapterRules.IsFinished(state),
            "victory alone leaves the chapter unfinished outside the village");
        RejectWithoutMutation(state, () => SlothChapterRules.TryFinishChapter(state),
            "the chapter cannot end before walking back to the village");
        foreach (string id in Route(layout, request.nodeId, layout.villageNodeId))
        {
            time = state.world.time;
            check(map.TryMove(id, out var returnRequest) && returnRequest == null
                && state.world.time == time + 1,
                "homecoming retraces cleared roads and pays only their ordinary travel time");
            if (id != layout.villageNodeId)
                check(!SlothChapterRules.CanEndAtVillage(state),
                    "intermediate return nodes cannot trigger the ending");
        }
        check(SlothChapterRules.CanEndAtVillage(state), "both completed fates become finishable upon returning home");
        var pendingHome = Clone(state);
        pendingHome.map.pendingRequestId = "unsettled";
        RejectWithoutMutation(pendingHome, () => SlothChapterRules.TryFinishChapter(pendingHome),
            "homecoming cannot finish while a request remains unresolved");
        var noFate = Clone(state);
        noFate.story.slothFate = SlothFate.Undecided;
        RejectWithoutMutation(noFate, () => SlothChapterRules.TryFinishChapter(noFate),
            "returning after victory still requires the player's final decision");
        var missingWin = Clone(state);
        missingWin.map.defeatedBossNodes.Clear();
        RejectWithoutMutation(missingWin, () => SlothChapterRules.TryFinishChapter(missingWin),
            "a narrative milestone without the actual boss victory cannot finish");
        before = Gameplay(state);
        check(SlothChapterRules.TryFinishChapter(state) && SlothChapterRules.IsFinished(state)
            && state.story.stage == MainStoryStage.CommissionTurnedIn && state.story.slothEndingSeen,
            "homecoming records a completed chapter for each fate");
        check(Gameplay(state) == before, "ending grants no invented power, resource, recovery or time cost");
        RejectWithoutMutation(state, () => SlothChapterRules.TryFinishChapter(state),
            "reopening the ending cannot settle the chapter twice");
        state = Clone(state);
        MainStoryRules.EnsureInitialized(state, layout);
        check(SlothChapterRules.IsFinished(state) && state.story.slothFate == fate
            && !SlothChapterRules.CanEndAtVillage(state),
            "completed chapter and its unique branch survive restoration");
        string finished = Snapshot(state);
        MainStoryRules.OnNodeCompleted(state, request);
        check(Snapshot(state) == finished && SlothChapterRules.IsFinished(state),
            "a late duplicated victory callback cannot reopen the finished chapter");
    }

    static void CheckChapterVisibilityAndWorldRules(MapLayout layout)
    {
        var chapter = Ready(layout, 880);
        var generic = Clone(chapter);
        generic.map.slothChapterOnly = false;
        var chapterMap = new MapProgression(layout, chapter.map, chapter.world, 880);
        var genericMap = new MapProgression(layout, generic.map, generic.world, 880);
        check(chapter.map.slothChapterOnly, "new playable runs use the sloth submission scope");
        for (int elapsed = 1; elapsed <= 88; elapsed++)
        {
            string a = WorldResourceRules.SpendTime(layout, chapter.map, chapter.world, 1, false);
            string b = WorldResourceRules.SpendTime(layout, generic.map, generic.world, 1, false);
            check(chapter.world.time == elapsed && chapter.world.sin == Math.Min(100, 40 + elapsed * 3)
                && chapter.map.sinRollCount == elapsed,
                "chapter sin increases and caps without resetting for unavailable bosses");
            check(a == null
                && chapter.map.checkpointNodeId == generic.map.checkpointNodeId
                && chapter.map.checkpointTime == generic.map.checkpointTime,
                "chapter emits no phantom boss announcement and retains dawn checkpoints");
        }
        check(chapter.map.spawnedBossNodes.Count == 1 && chapter.world.sin == 100
            && generic.map.spawnedBossNodes.Count == 3 && generic.world.sin == 100,
            "chapter generates only its playable boss while generic mode still generates all three");
        foreach (var node in layout.nodes)
        {
            bool placeholder = node.kind == MapNodeKind.Boss && node.id != chapter.story.slothNodeId;
            check(chapterMap.IsVisible(node.id) == (!placeholder && genericMap.IsVisible(node.id)),
                "chapter scope changes only unfinished boss visibility");
            if (!placeholder) continue;
            check(!chapterMap.IsVisible(node.id) && genericMap.IsVisible(node.id),
                "generic three-boss whitebox retains its spawned bosses");
            foreach (string neighbor in node.neighbors)
                check(!chapterMap.IsRoadVisible(node.id, neighbor),
                    "roads to unavailable bosses are hidden");
            chapter.map.currentNodeId = node.neighbors[0];
            if (!chapter.map.clearedNodes.Contains(node.neighbors[0]))
                chapter.map.clearedNodes.Add(node.neighbors[0]);
            RejectWithoutMutation(chapter, () => chapterMap.TryMove(node.id, out _),
                "hidden placeholder bosses cannot be entered and rejected movement is free");
        }
        int oldSin = chapter.world.sin, oldRolls = chapter.map.sinRollCount;
        WorldResourceRules.SpendTime(layout, chapter.map, chapter.world, 5, true);
        check(chapter.world.time == 93 && chapter.world.sin == oldSin && chapter.map.sinRollCount == oldRolls,
            "village time still advances without outdoor sin growth in the submission");
    }

    static void CheckBossCheckpoint(MapLayout layout)
    {
        var state = Ready(layout, 890);
        var request = EnterBoss(layout, state, true);
        state.story.slothConfronted = true;
        var map = new MapProgression(layout, state.map, state.world, state.map.randomSeed);
        check(state.world.time % 3 == 0 && state.map.checkpointNodeId == request.nodeId,
            "arriving at the boss at dawn records that real node as the checkpoint");
        int time = state.world.time, sin = state.world.sin, remnant = state.world.sinRemnant;
        RejectWithoutMutation(state, () => map.TryReviveAtCheckpoint("stale-request"),
            "stale boss failures cannot trigger revival");
        check(map.TryReviveAtCheckpoint(request.requestId) && state.map.revivedAtCheckpoint
            && state.map.currentNodeId == request.nodeId && map.GetPendingRequest() == null,
            "a failed boss battle can revive at its own dawn checkpoint");
        check(state.world.time == time && state.world.sin == sin && state.world.sinRemnant == remnant
            && !state.map.clearedNodes.Contains(request.nodeId)
            && !state.map.defeatedBossNodes.Contains(request.nodeId)
            && state.story.stage == MainStoryStage.TutorialCompleted
            && state.story.slothFate == SlothFate.Undecided && !SlothChapterRules.IsFinished(state),
            "boss revival is free and cannot create a victory, outcome, resource or ending");
        RejectWithoutMutation(state, () => SlothChapterRules.TryChooseFate(state, SlothFate.Spared),
            "reviving clears authorization to choose the defeated boss's fate");
        MapNodeRequest retry = null;
        check(map.CanMove(request.nodeId) && map.TryMove(request.nodeId, out retry)
            && retry != null && retry.requestId != request.requestId
            && SlothChapterRules.IsSlothRequest(retry),
            "an uncleared boss checkpoint supports a fresh battle request in place");
        check(state.world.time == time && state.world.sin == sin && !state.map.revivedAtCheckpoint,
            "retrying without traversing a road does not charge travel time or sin");
        RejectWithoutMutation(state, () => map.TryCompleteNode(request.requestId),
            "the previous boss attempt cannot settle a fresh retry");
        check(map.TryReviveAtCheckpoint(retry.requestId)
            && map.TryMove(map.GetNode(request.nodeId).neighbors[0], out var escape)
            && escape == null && state.world.time == time + 1,
            "a second failure can also retreat from the dawn boss checkpoint through a normal charged road");
        check(!state.map.defeatedBossNodes.Contains(request.nodeId) && !SlothChapterRules.IsFinished(state),
            "retreating after failure never completes the chapter");
    }
}
