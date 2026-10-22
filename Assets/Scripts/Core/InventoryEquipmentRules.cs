using System;
using System.Collections.Generic;

// The backpack presents one equipped weapon for each primary damage category.
// Old saves retain active owned weapons; competing equipment is resolved by category.
public static class InventoryEquipmentRules
{
    public static void Normalize(GameState state, Func<string, int> categoryOf)
    {
        if (state == null || categoryOf == null) return;
        EquipmentRules.EnsureInitialized(state);
        var slots = state.inventory.equippedWeaponIds;
        var next = new string[EquipmentRules.SlotCount];
        // Keep the current weapon when an old loadout contains several of one type.
        string current = state.player.currentWeaponId;
        if (!string.IsNullOrEmpty(current) && slots.Contains(current)) Put(current);
        foreach (string id in slots) Put(id);
        for (int i = 0; i < next.Length; i++) slots[i] = next[i] ?? string.Empty;
        EquipmentRules.EnsureInitialized(state);

        void Put(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            int column = categoryOf(id);
            if ((uint)column < next.Length && string.IsNullOrEmpty(next[column])) next[column] = id;
        }
    }

    public static bool TryEquip(GameState state, int column, string id, Func<string, int> categoryOf)
    {
        if (state == null || categoryOf == null || (uint)column >= EquipmentRules.SlotCount
            || string.IsNullOrEmpty(id) || categoryOf(id) != column) return false;
        Normalize(state, categoryOf);
        bool replacesCurrent = state.inventory.equippedWeaponIds[column] == state.player.currentWeaponId;
        bool equipped = EquipmentRules.TryEquip(state, column, id);
        if (equipped && replacesCurrent) EquipmentRules.TrySelectWeapon(state, id);
        return equipped;
    }

    public static bool HasUnread(InventoryState inventory, int column, Func<string, int> categoryOf)
    {
        if (inventory?.weapons == null || categoryOf == null) return false;
        foreach (string id in inventory.weapons)
            if (WeaponTable.Find(id) != null && categoryOf(id) == column
                && (inventory.seenWeaponIds == null || !inventory.seenWeaponIds.Contains(id))) return true;
        return false;
    }

    public static void Acknowledge(InventoryState inventory, int column, Func<string, int> categoryOf)
    {
        if (inventory?.weapons == null || categoryOf == null) return;
        if (inventory.seenWeaponIds == null) inventory.seenWeaponIds = new List<string>();
        foreach (string id in inventory.weapons)
            if (WeaponTable.Find(id) != null && categoryOf(id) == column && !inventory.seenWeaponIds.Contains(id))
                inventory.seenWeaponIds.Add(id);
    }
}
