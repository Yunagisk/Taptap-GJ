using System;

public static class NewRunFactory
{
    public static readonly string[] StartingWeapons = { "sword", "axe", "hammer" };

    public static GameState CreateForIntroduction(MapLayout layout, int seed)
    {
        var state = Create(layout, "sword", seed);
        state.story.useVillageIntroduction = true;
        state.story.tutorialNodeId = null;
        MainStoryRules.EnsureInitialized(state, layout);
        state.player.resolve = 0;
        state.player.currentWeaponId = string.Empty;
        state.player.lanternCharges = 0;
        state.inventory.weapons.Clear();
        state.inventory.seenWeaponIds.Clear();
        state.inventory.equippedWeaponIds.Clear();
        EquipmentRules.EnsureInitialized(state);
        ShopRules.StockNewRun(state.shop, null);
        return state;
    }

    public static GameState Create(MapLayout layout, string weaponId, int seed)
    {
        if (Array.IndexOf(StartingWeapons, weaponId) < 0)
            throw new ArgumentException("Choose a valid starting weapon.", nameof(weaponId));
        var state = new GameState();
        state.map.slothChapterOnly = true;
        state.player.currentWeaponId = weaponId;
        state.inventory.weapons.Clear();
        state.inventory.weapons.Add(weaponId);
        state.inventory.seenWeaponIds.Clear();
        state.inventory.seenWeaponIds.Add(weaponId);
        state.world.sinRemnant = WeaponTable.StartingRemnant;
        ShopRules.StockNewRun(state.shop, weaponId);
        // Build and validate a fresh map before the caller replaces the current run.
        new MapProgression(layout, state.map, state.world, seed);
        EquipmentRules.EnsureInitialized(state);
        MainStoryRules.EnsureInitialized(state, layout);
        VillageEventRules.InitializeForRun(state, layout);
        return state;
    }

    public static string WeaponName(string id)
    {
        return WeaponTable.NameOf(id);
    }
}
