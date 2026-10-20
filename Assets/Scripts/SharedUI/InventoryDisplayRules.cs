using System;
using System.Collections.Generic;

// Both category equipment and its picker use the same primary-damage classification.
public static class InventoryDisplayRules
{
    public readonly struct WeaponEntry
    {
        public readonly string id;
        public readonly int column, damageMask;
        public WeaponEntry(string id, int column, int damageMask)
        { this.id = id; this.column = column; this.damageMask = damageMask; }
    }
    public readonly struct ItemEntry
    {
        public readonly string id;
        public readonly int count;
        public ItemEntry(string id, int count) { this.id = id; this.count = count; }
    }
    public static readonly string[] DamageNames = { "穿刺", "挥砍", "钝击", "火焰" };

    public static void Weapons(IReadOnlyList<string> owned,
        Func<string, IReadOnlyList<DamageComponent>> damageOf, List<WeaponEntry> result)
    {
        result.Clear();
        if (owned == null) return;
        foreach (string id in owned)
        {
            if (string.IsNullOrEmpty(id) || result.Exists(entry => entry.id == id)) continue;
            var damage = damageOf(id);
            int column = (int)MapHudWeaponDisplay.PrimaryDamage(damage), mask = 0;
            if (damage != null)
                foreach (var part in damage)
                    if ((uint)part.damageType < 4 && part.damage > 0) mask |= 1 << (int)part.damageType;
            if (mask == 0) mask = 1 << column;
            int at = 0;
            while (at < result.Count && result[at].column <= column) at++;
            result.Insert(at, new WeaponEntry(id, column, mask));
        }
    }

    public static void Items(IReadOnlyList<ItemStack> inventory, List<ItemEntry> result)
    {
        result.Clear();
        if (inventory == null) return;
        foreach (var stack in inventory)
        {
            if (stack == null || stack.count <= 0 || string.IsNullOrEmpty(stack.itemId)) continue;
            string id = ItemTable.CanonicalId(stack.itemId);
            int at = result.FindIndex(entry => entry.id == id);
            if (at < 0) result.Add(new ItemEntry(id, stack.count));
            else result[at] = new ItemEntry(id, (int)Math.Min(int.MaxValue, (long)result[at].count + stack.count));
        }
    }

    public static string ItemName(string id)
    {
        return ItemTable.Find(id)?.displayName ?? id ?? "";
    }
}
