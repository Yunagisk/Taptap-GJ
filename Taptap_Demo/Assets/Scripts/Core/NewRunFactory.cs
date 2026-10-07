using System;

public static class NewRunFactory
{
    public static readonly string[] StartingWeapons = { "sword", "axe", "hammer", "dual_swords" };

    public static GameState Create(MapLayout layout, string weaponId, int seed)
    {
        if (Array.IndexOf(StartingWeapons, weaponId) < 0)
            throw new ArgumentException("Choose a valid starting weapon.", nameof(weaponId));
        var state = new GameState();
        state.player.currentWeaponId = weaponId;
        state.inventory.weapons.Clear();
        state.inventory.weapons.Add(weaponId);
        state.shop.availableWeaponIds.Remove(weaponId);
        // Build and validate a fresh map before the caller replaces the current run.
        new MapProgression(layout, state.map, state.world, seed);
        return state;
    }

    public static string WeaponName(string id)
    {
        switch (id)
        {
            case "sword": return "剑";
            case "axe": return "斧";
            case "hammer": return "锤";
            case "dual_swords": return "双剑";
            case "torch": return "火把";
            default: return id;
        }
    }
}
