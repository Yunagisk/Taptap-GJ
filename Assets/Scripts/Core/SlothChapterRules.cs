using System;

// Narrative flags never grant combat effects or spend world time.
public static class SlothChapterRules
{
    public static bool IsSlothRequest(MapNodeRequest request)
    {
        return request != null && request.kind == MapNodeKind.Boss
            && string.Equals(request.bossKey, "sloth", StringComparison.Ordinal);
    }

    public static bool IsFinished(GameState state) => state?.story != null
        && state.story.slothEndingSeen && state.story.stage == MainStoryStage.CommissionTurnedIn;

    public static bool TryChooseFate(GameState state, SlothFate fate)
    {
        if (state?.story == null || state.map == null
            || fate != SlothFate.Spared && fate != SlothFate.Killed
            || state.story.slothFate != SlothFate.Undecided || IsFinished(state)) return false;
        bool won = state.story.stage == MainStoryStage.SlothDefeated
            && state.map.defeatedBossNodes.Contains(state.story.slothNodeId);
        // The victory presentation precedes map settlement; its caller verifies battle victory.
        bool pending = state.story.slothConfronted && !string.IsNullOrEmpty(state.story.slothNodeId)
            && state.map.currentNodeId == state.story.slothNodeId
            && state.map.pendingNodeId == state.story.slothNodeId
            && !string.IsNullOrEmpty(state.map.pendingRequestId);
        if (!won && !pending) return false;
        state.story.slothFate = fate;
        return true;
    }

    public static bool CanEndAtVillage(GameState state)
    {
        return state?.story != null && state.map != null
            && state.story.stage == MainStoryStage.SlothDefeated
            && state.story.slothFate != SlothFate.Undecided
            && !state.story.slothEndingSeen
            && !string.IsNullOrEmpty(state.story.villageNodeId)
            && state.map.currentNodeId == state.story.villageNodeId
            && string.IsNullOrEmpty(state.map.pendingRequestId)
            && state.map.defeatedBossNodes.Contains(state.story.slothNodeId);
    }

    public static bool TryFinishChapter(GameState state)
    {
        if (!CanEndAtVillage(state)) return false;
        state.story.stage = MainStoryStage.CommissionTurnedIn;
        state.story.slothEndingSeen = true;
        return true;
    }
}
