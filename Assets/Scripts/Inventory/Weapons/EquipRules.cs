using System.Collections.Generic;

public readonly struct EquipResult
{
    public readonly bool ok;
    public readonly bool needConfirm;
    public readonly string message;

    public EquipResult(bool ok, bool needConfirm, string message)
    {
        this.ok = ok;
        this.needConfirm = needConfirm;
        this.message = message;
    }

    public static EquipResult Ok(string message) { return new EquipResult(true, false, message); }
    public static EquipResult No(string message) { return new EquipResult(false, false, message); }
    public static EquipResult Confirm(string message) { return new EquipResult(false, true, message); }
}

// 背包是持有列表。四个武器槽是已装配列表。一把武器占一个槽。
public static class EquipRules
{
    public const int SlotCount = 4;

    public static void Ensure(PlayerState player)
    {
        if (player == null) return;
        if (player.equippedWeaponIds == null) player.equippedWeaponIds = new List<string>();
        while (player.equippedWeaponIds.Count < SlotCount) player.equippedWeaponIds.Add("");
        if (player.equippedWeaponIds.Count > SlotCount)
            player.equippedWeaponIds.RemoveRange(SlotCount, player.equippedWeaponIds.Count - SlotCount);
        for (int i = 0; i < SlotCount; i++)
            if (player.equippedWeaponIds[i] == null) player.equippedWeaponIds[i] = "";

        bool any = false;
        for (int i = 0; i < SlotCount; i++)
            if (player.equippedWeaponIds[i].Length > 0) any = true;
        if (!any && !string.IsNullOrEmpty(player.currentWeaponId))
            player.equippedWeaponIds[0] = player.currentWeaponId;
    }

    public static string Slot(PlayerState player, int index)
    {
        Ensure(player);
        if (player == null || index < 0 || index >= SlotCount) return "";
        return player.equippedWeaponIds[index];
    }

    public static void SetStarting(PlayerState player, string weaponId)
    {
        Ensure(player);
        for (int i = 0; i < SlotCount; i++) player.equippedWeaponIds[i] = "";
        player.equippedWeaponIds[0] = weaponId;
        player.currentWeaponId = weaponId;
    }

    public static int IndexOf(PlayerState player, string weaponId)
    {
        Ensure(player);
        if (player == null || string.IsNullOrEmpty(weaponId)) return -1;
        for (int i = 0; i < SlotCount; i++)
            if (player.equippedWeaponIds[i] == weaponId) return i;
        return -1;
    }

    public static EquipResult TryEquip(GameState state, string weaponId, int slot, bool replace)
    {
        if (state == null) return EquipResult.No("没有进行中的局。");
        var offer = WeaponTable.Find(weaponId);
        if (offer == null) return EquipResult.No("没有这把武器。");
        if (slot < 0 || slot >= SlotCount) return EquipResult.No("武器槽无效。");
        if (!state.inventory.weapons.Contains(weaponId))
            return EquipResult.No(offer.displayName + "不在背包里。先购买。");
        Ensure(state.player);

        int existing = IndexOf(state.player, weaponId);
        string occupying = state.player.equippedWeaponIds[slot];
        if (existing == slot)
        {
            state.player.currentWeaponId = weaponId;
            return EquipResult.Ok(offer.displayName + "已在槽" + (slot + 1) + "。");
        }
        if (!string.IsNullOrEmpty(occupying) && occupying != weaponId && !replace)
            return EquipResult.Confirm("槽" + (slot + 1) + "已装备" + WeaponTable.NameOf(occupying)
                + "。确认后换成" + offer.displayName + "。");

        if (existing >= 0) state.player.equippedWeaponIds[existing] = "";
        state.player.equippedWeaponIds[slot] = weaponId;
        state.player.currentWeaponId = weaponId;
        string moved = existing >= 0 ? "，已从槽" + (existing + 1) + "移过来" : "";
        return EquipResult.Ok("已把" + offer.displayName + "装到槽" + (slot + 1) + moved + "。");
    }

    public static EquipResult TryUnequip(GameState state, int slot)
    {
        if (state == null) return EquipResult.No("没有进行中的局。");
        if (slot < 0 || slot >= SlotCount) return EquipResult.No("武器槽无效。");
        Ensure(state.player);
        string id = state.player.equippedWeaponIds[slot];
        if (string.IsNullOrEmpty(id)) return EquipResult.No("这个槽是空的。");
        state.player.equippedWeaponIds[slot] = "";
        if (state.player.currentWeaponId == id)
        {
            state.player.currentWeaponId = "";
            for (int i = 0; i < SlotCount; i++)
            {
                if (string.IsNullOrEmpty(state.player.equippedWeaponIds[i])) continue;
                state.player.currentWeaponId = state.player.equippedWeaponIds[i];
                break;
            }
        }
        return EquipResult.Ok(WeaponTable.NameOf(id) + "已卸下，仍在背包里。");
    }
}
