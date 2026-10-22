using System;
using System.Collections.Generic;

// Display actual equipped weapons once each. Damage is a sort key, not a slot
// that merges different weapons or duplicates a mixed-damage weapon.
public static class MapHudWeaponDisplay
{
    public readonly struct Entry
    {
        public readonly string id;
        public readonly PlayerDamageType primary;
        public readonly int damageMask;
        public Entry(string id, PlayerDamageType primary, int mask)
        { this.id = id; this.primary = primary; damageMask = mask; }
        public string DamageLabel => Labels[damageMask & 15];
    }

    static readonly string[] Labels =
    {
        "—", "刺", "斩", "刺/斩", "钝", "刺/钝", "斩/钝", "刺/斩\n钝",
        "火", "刺/火", "斩/火", "刺/斩\n火", "钝/火", "刺/钝\n火", "斩/钝\n火", "刺/斩\n钝/火"
    };

    public static void Build(IReadOnlyList<string> equipped,
        Func<string, IReadOnlyList<DamageComponent>> damageOf, List<Entry> output)
    {
        output.Clear();
        if (equipped == null) return;
        for (int i = 0; i < equipped.Count && output.Count < MapHudWeaponArcLayout.Capacity; i++)
        {
            string id = equipped[i];
            if (string.IsNullOrEmpty(id) || output.Exists(entry => entry.id == id)) continue;
            var damage = damageOf(id);
            int mask = 0;
            if (damage != null)
                for (int j = 0; j < damage.Count; j++)
                    if (damage[j].damage > 0 && (uint)damage[j].damageType < 4)
                        mask |= 1 << (int)damage[j].damageType;
            var primary = PrimaryDamage(damage);
            if (mask == 0 && damage != null && damage.Count > 0) mask = 1 << (int)primary;
            var next = new Entry(id, primary, mask);
            int at = 0;
            while (at < output.Count && (int)output[at].primary <= (int)primary) at++;
            output.Insert(at, next); // Stable within each damage category.
        }
    }

    public static PlayerDamageType PrimaryDamage(IReadOnlyList<DamageComponent> components)
    {
        var type = PlayerDamageType.Pierce;
        int largest = int.MinValue;
        if (components != null)
            for (int i = 0; i < components.Count; i++)
                if ((uint)components[i].damageType < 4 && components[i].damage > largest)
                { largest = components[i].damage; type = components[i].damageType; }
        return type;
    }
}
