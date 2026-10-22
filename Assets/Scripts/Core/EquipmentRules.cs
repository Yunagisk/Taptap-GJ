using System.Collections.Generic;

// 库存与装备分开：买到武器只解锁，四槽装备必须由玩家选择。
public static class EquipmentRules
{
    public const int SlotCount = 4;

    public static void EnsureInitialized(GameState state)
    {
        if (state == null) return;
        if (state.inventory == null) state.inventory = new InventoryState();
        if (state.inventory.weapons == null) state.inventory.weapons = new List<string>();
        if (state.player == null) state.player = new PlayerState();
        state.player.NormalizeResolve();
        var slots = state.inventory.equippedWeaponIds;
        // Retire the old weapon in saved runs as well as in the catalogue.
        bool retiredEquipped = state.player.currentWeaponId == "dual_swords"
            || slots != null && slots.Contains("dual_swords");
        bool retiredOwned = state.inventory.weapons.RemoveAll(id => id == "dual_swords") > 0;
        state.inventory.seenWeaponIds?.RemoveAll(id => id == "dual_swords");
        // A run that started with only the retired weapon still needs a usable weapon.
        if ((retiredOwned || retiredEquipped) && string.IsNullOrEmpty(FirstOwnedWeapon(state)))
            state.inventory.weapons.Add("sword");
        bool migrate = slots == null || slots.Count == 0;
        if (slots == null) state.inventory.equippedWeaponIds = slots = new List<string>();
        if (slots.Count > SlotCount) slots.RemoveRange(SlotCount, slots.Count - SlotCount);
        while (slots.Count < SlotCount) slots.Add(string.Empty);
        if (migrate)
        {
            string initialWeapon = IsOwnedWeapon(state, state.player.currentWeaponId)
                ? state.player.currentWeaponId : FirstOwnedWeapon(state);
            slots[0] = initialWeapon ?? string.Empty;
        }
        var equipped = new HashSet<string>();
        for (int i = 0; i < SlotCount; i++)
        {
            string id = slots[i];
            if (string.IsNullOrEmpty(id)) continue;
            if (!IsOwnedWeapon(state, id) || !equipped.Add(id)) slots[i] = string.Empty;
        }
        if (retiredEquipped && string.IsNullOrEmpty(FirstEquippedWeapon(slots)))
            slots[0] = FirstOwnedWeapon(state);
        if (!IsOwnedWeapon(state, state.player.currentWeaponId)
            || !slots.Contains(state.player.currentWeaponId))
            state.player.currentWeaponId = FirstEquippedWeapon(slots);
    }

    public static bool TryEquip(GameState state, int slot, string weaponId)
    {
        if (state == null || slot < 0 || slot >= SlotCount) return false;
        EnsureInitialized(state);
        if (!string.IsNullOrEmpty(weaponId) && !IsOwnedWeapon(state, weaponId)) return false;
        var slots = state.inventory.equippedWeaponIds;
        if (!string.IsNullOrEmpty(weaponId))
            for (int i = 0; i < SlotCount; i++)
                if (i != slot && slots[i] == weaponId) slots[i] = string.Empty;
        slots[slot] = weaponId ?? string.Empty;
        EnsureInitialized(state);
        return true;
    }

    public static bool TrySelectWeapon(GameState state, string id)
    {
        if (state == null || string.IsNullOrEmpty(id)) return false;
        EnsureInitialized(state);
        if (!IsOwnedWeapon(state, id) || !state.inventory.equippedWeaponIds.Contains(id)) return false;
        state.player.currentWeaponId = id;
        return true;
    }

    static bool IsOwnedWeapon(GameState state, string id)
    {
        return WeaponTable.Find(id) != null && state.inventory.weapons.Contains(id);
    }

    static string FirstOwnedWeapon(GameState state)
    {
        foreach (string id in state.inventory.weapons)
            if (IsOwnedWeapon(state, id)) return id;
        return string.Empty;
    }

    static string FirstEquippedWeapon(List<string> slots)
    {
        foreach (string id in slots)
            if (!string.IsNullOrEmpty(id)) return id;
        return string.Empty;
    }
}
