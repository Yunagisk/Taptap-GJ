using UnityEngine;

[CreateAssetMenu(fileName = "WeaponLibrary", menuName = "WeaponLibrary")]
public class WeaponLibrary : ScriptableObject
{
    public WeaponData[] weapons;

    public WeaponData Find(string id)
    {
        if (weapons == null || string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponData weapon = weapons[i];
            if (weapon != null && weapon.WeaponId == id) return weapon;
        }
        return null;
    }

    public int PriceOf(string id)
    {
        WeaponData weapon = Find(id);
        return weapon != null ? weapon.Price : WeaponTable.PriceOf(id);
    }

    public string NameOf(string id)
    {
        WeaponData weapon = Find(id);
        if (weapon != null && !string.IsNullOrEmpty(weapon.WeaponName)) return weapon.WeaponName;
        return WeaponTable.NameOf(id);
    }
}
