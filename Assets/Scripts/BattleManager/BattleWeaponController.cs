using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleWeaponController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerCombatant player;
    [SerializeField] private PlayerWeaponLoadout weaponLoadout;

    private WeaponData[] weapons;
    private int currentIndex;

    public int CurrentIndex => currentIndex;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        weapons = weaponLoadout.EquippedWeapons;
        // 找到第一把有效武器作为初始武器
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
            {
                SwitchWeapon(i);
                break;
            }
        }
    }
    // 局内切换武器
    public void SwitchWeapon(int index)
    {
        if (index < 0 || index >= weapons.Length)
            return;

        if (weapons[index] == null)
            return;

        currentIndex = index;

        player.EquipWeapon(weapons[index]);
    }

}

