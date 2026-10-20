public static class VillageIntroRules
{
    public static bool IsPending(GameState state) => state != null && state.story != null
        && state.story.useVillageIntroduction && state.story.stage == MainStoryStage.ArrivedVillage;

    public static bool IsAwaitingMayor(GameState state) => state != null && state.story != null
        && state.story.useVillageIntroduction && state.story.stage == MainStoryStage.VillageIntroductionComplete;

    public static bool IsGuideRun(GameState state) => state?.story != null && state.story.useVillageIntroduction;
    public static bool IsRestricted(GameState state) => IsGuideRun(state) && !state.story.guideCompleted;
    public static bool NeedsBlacksmith(GameState state) => IsRestricted(state)
        && state.story.stage == MainStoryStage.CommissionAccepted && !state.story.guideEquipmentReady;
    public static bool NeedsReport(GameState state) => IsRestricted(state)
        && state.story.stage == MainStoryStage.TutorialCompleted;
    public static bool CanLeaveVillage(GameState state) => !IsRestricted(state)
        || state.story.stage == MainStoryStage.CommissionAccepted && state.story.guideEquipmentReady;
    public static bool IsTutorialBattle(GameState state, string nodeId) => IsRestricted(state)
        && state.story.stage == MainStoryStage.CommissionAccepted && state.story.guideEquipmentReady
        && !string.IsNullOrEmpty(nodeId) && state.story.tutorialNodeId == nodeId;

    public static bool CanMoveTo(GameState state, MapLayout layout, string nodeId)
    {
        if (!IsRestricted(state)) return true;
        if (layout == null || state.map == null) return false;
        if (NeedsReport(state)) return nodeId == layout.villageNodeId;
        return state.story.guideEquipmentReady && state.story.guideOutsideReady
            && state.story.stage == MainStoryStage.CommissionAccepted
            && (nodeId == state.story.tutorialNodeId || nodeId == layout.villageNodeId);
    }

    public static bool ApplyReward(GameState state, string key, GuideReward reward)
    {
        if (!IsRestricted(state) || reward <= GuideReward.None || reward > GuideReward.Torch || string.IsNullOrEmpty(key)) return false;
        if (state.story.guideRewards == null) state.story.guideRewards = new System.Collections.Generic.List<string>();
        if (state.story.guideRewards.Contains(key)) return false;
        // Rewards are valid only at their authored stage, including when resuming an interrupted conversation.
        if (reward == GuideReward.Supplies && state.story.stage != MainStoryStage.VillageIntroductionComplete) return false;
        if (reward != GuideReward.Supplies && (state.story.stage != MainStoryStage.CommissionAccepted
            || state.story.guideEquipmentReady)) return false;
        if (reward != GuideReward.Supplies && reward != GuideReward.SwordAndLantern
            && !string.IsNullOrEmpty(state.story.guideSecondWeaponId)) return false;
        EquipmentRules.EnsureInitialized(state);
        if (reward == GuideReward.Supplies)
        {
            if (state.inventory.items == null) state.inventory.items = new System.Collections.Generic.List<ItemStack>();
            foreach (string id in new[] { "bandage", "holy_water" })
                if (!state.inventory.items.Exists(item => item != null && item.itemId == id && item.count > 0))
                    state.inventory.items.Add(new ItemStack(id, 1));
        }
        else
        {
            string id = reward == GuideReward.SwordAndLantern ? "sword" : reward == GuideReward.Axe ? "axe"
                : reward == GuideReward.Hammer ? "hammer" : "torch";
            if (!state.inventory.weapons.Contains(id)) state.inventory.weapons.Add(id);
            // The backpack sorts equipped weapons into Pierce, Slash, Blunt and Fire columns.
            int slot = reward == GuideReward.SwordAndLantern ? 0 : reward == GuideReward.Axe ? 1
                : reward == GuideReward.Hammer ? 2 : 3;
            EquipmentRules.TryEquip(state, slot, id);
            if (reward != GuideReward.SwordAndLantern) state.story.guideSecondWeaponId = id;
            if (reward == GuideReward.SwordAndLantern)
            {
                EquipmentRules.TrySelectWeapon(state, id);
                state.player.lanternCharges = state.player.maxLanternCharges;
            }
        }
        state.story.guideRewards.Add(key);
        return true;
    }
}
