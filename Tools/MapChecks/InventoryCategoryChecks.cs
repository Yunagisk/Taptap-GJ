using System;
using System.Collections.Generic;
using System.Text.Json;

static class InventoryCategoryChecks
{
    static int Category(string id)
    {
        switch (id)
        {
            case "axe": case "sickle": return 1;
            case "hammer": case "flail": case "heaven_fire": return 2;
            case "torch": return 3;
            default: return 0;
        }
    }
    public static void Run(Action<bool, string> check)
    {
        var state = new GameState();
        state.inventory.weapons = new List<string> { "sword", "greatsword", "axe", "sickle", "hammer", "torch", "flame_tongue", "heaven_fire" };
        state.inventory.equippedWeaponIds = new List<string> { "axe", "sword", "torch", "hammer" };
        state.player.currentWeaponId = "axe";
        InventoryEquipmentRules.Normalize(state, Category);
        check(string.Join(",",state.inventory.equippedWeaponIds)=="sword,axe,hammer,torch", "legacy loadout is mapped to its damage categories");
        check(state.player.currentWeaponId=="axe" && state.inventory.weapons.Count==8, "migration preserves ownership and current weapon");
        state.inventory.equippedWeaponIds = new List<string> { "sword", "greatsword", "axe", "torch" };
        state.player.currentWeaponId="greatsword";
        InventoryEquipmentRules.Normalize(state, Category);
        check(state.inventory.equippedWeaponIds[0]=="greatsword" && state.inventory.weapons.Contains("sword"), "current weapon wins category conflict without deleting ownership");
        check(state.inventory.equippedWeaponIds[2]=="", "migration does not auto-equip other owned weapons");
        var snapshot=string.Join("|",state.inventory.equippedWeaponIds);
        InventoryEquipmentRules.Normalize(state,Category);
        check(snapshot==string.Join("|",state.inventory.equippedWeaponIds),"category migration is idempotent");
        check(InventoryEquipmentRules.TryEquip(state,0,"flame_tongue",Category) && state.player.currentWeaponId=="flame_tongue", "replacing the active category keeps that category current");
        check(!InventoryEquipmentRules.TryEquip(state,3,"flame_tongue",Category),"mixed-damage weapon belongs to its primary category only");
        check(InventoryEquipmentRules.TryEquip(state,2,"heaven_fire",Category) && state.player.currentWeaponId=="flame_tongue", "equipping another category does not change the current weapon");
        check(!InventoryEquipmentRules.TryEquip(state,0,"spear",Category),"unowned weapon cannot be equipped");
        check(!InventoryEquipmentRules.TryEquip(state,-1,"sword",Category) && !InventoryEquipmentRules.TryEquip(state,4,"torch",Category),"invalid categories are rejected");
        check(InventoryEquipmentRules.HasUnread(state.inventory,0,Category) && InventoryEquipmentRules.HasUnread(state.inventory,1,Category),"new weapons light their category dots");
        InventoryEquipmentRules.Acknowledge(state.inventory,0,Category);
        check(!InventoryEquipmentRules.HasUnread(state.inventory,0,Category) && InventoryEquipmentRules.HasUnread(state.inventory,1,Category),"opening a category only clears its own notification");
        int seen=state.inventory.seenWeaponIds.Count;
        InventoryEquipmentRules.Acknowledge(state.inventory,0,Category);
        check(state.inventory.seenWeaponIds.Count==seen,"repeated acknowledgement creates no duplicates");
        state.inventory.weapons.Add("spear");
        check(InventoryEquipmentRules.HasUnread(state.inventory,0,Category),"another purchase relights a previously acknowledged category");
        var options=new JsonSerializerOptions { IncludeFields=true };
        var restored=JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state,options),options);
        check(InventoryEquipmentRules.HasUnread(restored.inventory,0,Category),"unread acquisition survives serialization");
        InventoryEquipmentRules.Acknowledge(restored.inventory,0,Category);
        var again=JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(restored,options),options);
        check(!InventoryEquipmentRules.HasUnread(again.inventory,0,Category),"cleared notification survives reopening and serialization");
        state.inventory.seenWeaponIds=null;
        check(InventoryEquipmentRules.HasUnread(state.inventory,3,Category),"legacy null notification list is supported");
        InventoryEquipmentRules.Acknowledge(state.inventory,3,Category);
        check(!InventoryEquipmentRules.HasUnread(state.inventory,3,Category),"legacy notification list can be initialized");
        state.inventory.equippedWeaponIds=new List<string>{"","","",""};
        InventoryEquipmentRules.Normalize(state,Category);
        check(state.inventory.equippedWeaponIds.TrueForAll(string.IsNullOrEmpty),"explicit unequip survives category normalization");
        check(InventoryEquipmentRules.TryEquip(state,2,"hammer",Category) && state.player.currentWeaponId=="hammer", "empty loadout can equip a first weapon");
        check(!InventoryEquipmentRules.TryEquip(null,0,"sword",Category),"missing run is rejected");
        InventoryEquipmentRules.Normalize(null,Category);
        InventoryEquipmentRules.Acknowledge(null,0,Category);
        check(!InventoryEquipmentRules.HasUnread(null,0,Category),"missing inventory is safe");
    }
}
