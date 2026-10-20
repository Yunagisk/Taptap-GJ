using System;
using System.Linq;
using System.Text.Json;

static class GuideQuestChecks
{
    public static void Run(Func<MapLayout> load, Action<bool, string> check)
    {
        foreach (var reward in new[] { GuideReward.Axe, GuideReward.Hammer, GuideReward.Torch })
        {
            var layout = load();
            var state = NewRunFactory.CreateForIntroduction(layout, 901 + (int)reward);
            check(VillageIntroRules.IsPending(state) && state.inventory.weapons.Count == 0 && state.player.resolve == 0,
                "guide starts before equipment and combat lessons");
            var village = layout.nodes.Find(node => node.id == layout.villageNodeId);
            var target = layout.nodes.Find(node => node.id == state.story.tutorialNodeId);
            check(village.neighbors.Contains(target.id) && village.neighbors.All(id =>
                layout.nodes.Find(node => node.id == id).kind != MapNodeKind.Battle
                || layout.nodes.Find(node => node.id == id).x <= target.x), "guide uses the eastern adjacent encounter");
            check(!VillageIntroRules.ApplyReward(state, "early", GuideReward.Supplies)
                && !MainStoryRules.TryCompleteDialogue(state, "early_report", StoryDialogueEffect.GuideComplete),
                "guide refuses rewards and completion before the authored stages");
            MainStoryRules.TryCompleteDialogue(state, "intro", StoryDialogueEffect.VillageIntroduction);
            check(VillageIntroRules.ApplyReward(state, "supplies", GuideReward.Supplies)
                && !VillageIntroRules.ApplyReward(state, "supplies", GuideReward.Supplies)
                && state.inventory.items.Count == 2 && state.inventory.items.All(item => item.count == 1),
                "supply grant is capped and paid only once");
            check(!VillageIntroRules.CanLeaveVillage(state), "guide cannot leave without the blacksmith visit");
            MainStoryRules.TryCompleteDialogue(state, "mayor", StoryDialogueEffect.AcceptCommission);
            check(!MainStoryRules.TryCompleteDialogue(state, "empty_forge", StoryDialogueEffect.GuideEquipmentReady),
                "empty blacksmith completion cannot unlock the city gate");
            check(VillageIntroRules.ApplyReward(state, "starter", GuideReward.SwordAndLantern)
                && state.player.lanternCharges == 2 && state.player.currentWeaponId == "sword", "forge grants and equips the starter set");
            check(VillageIntroRules.ApplyReward(state, "second", reward)
                && !VillageIntroRules.ApplyReward(state, "other_second", GuideReward.Axe)
                && state.inventory.weapons.Count == 2, "reopening the forge cannot claim a different free second weapon");
            check(MainStoryRules.TryCompleteDialogue(state, "forge", StoryDialogueEffect.GuideEquipmentReady)
                && VillageIntroRules.CanLeaveVillage(state), "two received weapons unlock departure");
            check(!VillageIntroRules.CanMoveTo(state, layout, target.id), "outside narration must finish before the first movement");
            MainStoryRules.TryCompleteDialogue(state, "outside", StoryDialogueEffect.GuideOutsideReady);
            check(VillageIntroRules.CanMoveTo(state, layout, target.id)
                && village.neighbors.Where(id => id != target.id).All(id => !VillageIntroRules.CanMoveTo(state, layout, id)),
                "only the tutorial route is available before the first victory");
            var map = new MapProgression(layout, state.map, state.world, 901);
            check(map.TryMove(target.id, out var request), "guide enters the real tutorial node");
            check(map.TryCompleteNode(request.requestId), "guide settles the real tutorial battle");
            MainStoryRules.OnNodeCompleted(state, request);
            check(VillageIntroRules.NeedsReport(state) && state.world.time == 2
                && VillageIntroRules.CanMoveTo(state, layout, layout.villageNodeId)
                && !VillageIntroRules.CanMoveTo(state, layout, target.id), "victory requires the return route and retains the current time rules");
            var options = new JsonSerializerOptions { IncludeFields = true };
            state = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state, options), options);
            check(VillageIntroRules.NeedsReport(state) && state.story.guideSecondWeaponId != null
                && !VillageIntroRules.ApplyReward(state, "starter", GuideReward.SwordAndLantern),
                "saved guide progression retains rewards and the report objective");
            check(MainStoryRules.TryCompleteDialogue(state, "report", StoryDialogueEffect.GuideComplete)
                && !MainStoryRules.TryCompleteDialogue(state, "report", StoryDialogueEffect.GuideComplete)
                && !VillageIntroRules.IsRestricted(state) && MainStoryRules.GoalText(state).Contains("懒惰"),
                "the report unlocks free exploration and hands off to the sloth story exactly once");
        }
        var legacy = NewRunFactory.Create(load(), "axe", 911);
        check(!VillageIntroRules.IsRestricted(legacy) && VillageIntroRules.CanLeaveVillage(legacy),
            "legacy and debug runs keep their free exploration entry");
    }
}
