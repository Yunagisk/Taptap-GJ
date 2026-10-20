using System;
using System.Collections.Generic;

// 主线只记录已完成的对话与遭遇；地图、奖励和商店规则由调用方处理。
public static class MainStoryRules
{
    public static void EnsureInitialized(GameState state, MapLayout layout)
    {
        if (state == null) return;
        if (state.story == null) state.story = new MainStoryState();
        if (state.story.completedDialogueIds == null)
            state.story.completedDialogueIds = new List<string>();
        if (layout != null)
        {
            state.story.villageNodeId = layout.villageNodeId;
            var sloth = layout.regions != null
                ? layout.regions.Find(region => region != null && region.bossKey == "sloth") : null;
            state.story.slothNodeId = sloth != null ? sloth.bossNodeId : null;
            if (layout.nodes != null)
            {
                var village = layout.nodes.Find(node => node != null && node.id == layout.villageNodeId);
                if (village != null && village.neighbors != null)
                {
                    var existing = layout.nodes.Find(node => node != null && node.id == state.story.tutorialNodeId
                        && node.kind == MapNodeKind.Battle && village.neighbors.Contains(node.id));
                    if (existing == null)
                    {
                        state.story.tutorialNodeId = null;
                        MapNodeDefinition first = null;
                        foreach (string neighborId in village.neighbors)
                        {
                            var neighbor = layout.nodes.Find(node => node != null && node.id == neighborId
                                && node.kind == MapNodeKind.Battle);
                            if (neighbor == null) continue;
                            // Legacy runs keep the first neighbor; guide runs leave through the east gate.
                            if (first == null || state.story.useVillageIntroduction && neighbor.x > first.x) first = neighbor;
                        }
                        state.story.tutorialNodeId = first != null ? first.id : null;
                    }
                }
            }
        }
        ReconcileCompletedNodes(state);
    }

    // 当前阶段仍允许自由探索；承认已经完成的节点，避免接委托后无法重打而卡住。
    public static void ReconcileCompletedNodes(GameState state)
    {
        if (state == null || state.story == null || state.map == null) return;
        if (state.story.stage == MainStoryStage.CommissionAccepted
            && !string.IsNullOrEmpty(state.story.tutorialNodeId)
            && state.map.clearedNodes != null
            && state.map.clearedNodes.Contains(state.story.tutorialNodeId))
            state.story.stage = MainStoryStage.TutorialCompleted;
        if (state.story.stage == MainStoryStage.TutorialCompleted
            && !string.IsNullOrEmpty(state.story.slothNodeId)
            && state.map.defeatedBossNodes != null
            && state.map.defeatedBossNodes.Contains(state.story.slothNodeId))
            state.story.stage = MainStoryStage.SlothDefeated;
    }
    public static bool TryCompleteDialogue(GameState state, string dialogueId, StoryDialogueEffect effect)
    {
        if (state == null || string.IsNullOrWhiteSpace(dialogueId)) return false;
        EnsureInitialized(state, null);
        if (state.story.completedDialogueIds.Contains(dialogueId)) return false;
        var stage = state.story.stage;
        switch (effect)
        {
            case StoryDialogueEffect.None:
                break;
            case StoryDialogueEffect.VillageIntroduction:
                if (stage != MainStoryStage.ArrivedVillage) return false;
                stage = MainStoryStage.VillageIntroductionComplete;
                break;
            case StoryDialogueEffect.AcceptCommission:
                if (stage != MainStoryStage.VillageIntroductionComplete) return false;
                stage = MainStoryStage.CommissionAccepted;
                break;
            case StoryDialogueEffect.TurnInCommission:
                // The submission ends through the automatic homecoming, after the final choice.
                if (state.map != null && state.map.slothChapterOnly) return false;
                if (stage != MainStoryStage.SlothDefeated) return false;
                stage = MainStoryStage.CommissionTurnedIn;
                break;
            case StoryDialogueEffect.GuideEquipmentReady:
                if (!VillageIntroRules.NeedsBlacksmith(state) || !state.inventory.weapons.Contains("sword")
                    || string.IsNullOrEmpty(state.story.guideSecondWeaponId)
                    || !state.inventory.weapons.Contains(state.story.guideSecondWeaponId)) return false;
                state.story.guideEquipmentReady = true;
                break;
            case StoryDialogueEffect.GuideOutsideReady:
                if (!VillageIntroRules.IsRestricted(state) || !state.story.guideEquipmentReady
                    || stage != MainStoryStage.CommissionAccepted) return false;
                state.story.guideOutsideReady = true;
                break;
            case StoryDialogueEffect.GuideComplete:
                if (!VillageIntroRules.NeedsReport(state)) return false;
                state.story.guideCompleted = true;
                break;
            default:
                return false;
        }
        state.story.stage = stage;
        state.story.completedDialogueIds.Add(dialogueId);
        ReconcileCompletedNodes(state);
        return true;
    }

    // 仅在地图确认节点完成后调用。重复回调和其他节点不会推进主线。
    public static void OnNodeCompleted(GameState state, MapNodeRequest request)
    {
        if (state == null || request == null || string.IsNullOrEmpty(request.nodeId)) return;
        EnsureInitialized(state, null);
        if (state.story.stage == MainStoryStage.CommissionAccepted
            && request.kind == MapNodeKind.Battle
            && request.nodeId == state.story.tutorialNodeId)
        {
            state.story.stage = MainStoryStage.TutorialCompleted;
        }
        else if (!VillageIntroRules.IsRestricted(state)
            && request.kind == MapNodeKind.Boss
            && string.Equals(request.bossKey, "sloth", StringComparison.Ordinal)
            && state.map != null && state.map.defeatedBossNodes.Contains(request.nodeId)
            && state.map.clearedNodes.Contains(request.nodeId)
            && state.story.stage != MainStoryStage.CommissionTurnedIn)
        {
            state.story.stage = MainStoryStage.SlothDefeated;
        }
        ReconcileCompletedNodes(state);
    }

    public static string GoalText(GameState state)
    {
        if (VillageIntroRules.IsPending(state)) return "与村民交谈，确认自己的身份。";
        if (VillageIntroRules.IsAwaitingMayor(state)) return "前往村长家，与哈罗德交谈。";
        if (VillageIntroRules.NeedsBlacksmith(state)) return "前往西城区铁匠铺，向加斯帕领取武器。";
        if (VillageIntroRules.NeedsReport(state)) return "返回埃尔南多，向哈罗德复命。";
        if (VillageIntroRules.IsRestricted(state) && state.story.guideEquipmentReady)
            return "前往东城门外，清剿旧磨坊附近的怪物。";
        switch (state != null && state.story != null ? state.story.stage : MainStoryStage.ArrivedVillage)
        {
            case MainStoryStage.ArrivedVillage: return "前往酒馆，与村民交谈，了解村庄。";
            case MainStoryStage.VillageIntroductionComplete: return "前往酒馆，与村长交谈，接受清剿委托。";
            case MainStoryStage.CommissionAccepted: return "前往村外的战场，完成首次清剿。";
            case MainStoryStage.TutorialCompleted: return state.story.slothInvestigated
                ? "前往暮林中的祭所，阻止西奥多。"
                : "向村长与神父了解求救之事，或前往暮林中的祭所调查。";
            case MainStoryStage.SlothDefeated: return "返回埃尔南多，向哈罗德复命。";
            case MainStoryStage.CommissionTurnedIn: return "懒惰篇已完成。感谢游玩。";
            default: return "前往酒馆，确认当前目标。";
        }
    }
}
