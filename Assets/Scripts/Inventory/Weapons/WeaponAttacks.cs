using System.Collections.Generic;
using UnityEngine;

public static class WeaponAttacks
{
    public static bool TryBuild(WeaponData weapon, out DamageComponent[] components, out int attackCount)
    {
        components = System.Array.Empty<DamageComponent>();
        attackCount = 1;
        if (weapon == null) return false;

        WeaponProfile profile = WeaponStatTable.Find(weapon.WeaponId);
        if (profile != null)
        {
            attackCount = profile.hits < 1 ? 1 : profile.hits;
            var list = new List<DamageComponent>(4);
            if (profile.pierce > 0) list.Add(new DamageComponent(PlayerDamageType.Pierce, profile.pierce));
            if (profile.slash > 0) list.Add(new DamageComponent(PlayerDamageType.Slash, profile.slash));
            if (profile.blunt > 0) list.Add(new DamageComponent(PlayerDamageType.Blunt, profile.blunt));
            if (profile.fire > 0) list.Add(new DamageComponent(PlayerDamageType.Fire, profile.fire));
            components = list.ToArray();
            return components.Length > 0;
        }

        var source = weapon.DamageComponents;
        if (source == null || source.Count == 0) return false;
        components = new DamageComponent[source.Count];
        for (int i = 0; i < source.Count; i++) components[i] = source[i];
        attackCount = weapon.AttackCount < 1 ? 1 : weapon.AttackCount;
        return true;
    }
}
