using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerWeaponLoadout : MonoBehaviour
{
    [SerializeField] private WeaponData[] equippedWeapons = new WeaponData[4];

    public WeaponData[] EquippedWeapons => equippedWeapons;

    //局外装备武器
    public void EquipWeapon(int slotIndex, WeaponData weapon)
    {
        if (slotIndex < 0 || slotIndex >= equippedWeapons.Length)
            return;

        equippedWeapons[slotIndex] = weapon;
    }
}
