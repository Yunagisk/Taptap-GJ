using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombatant : Combatant
{

    // ================= 初始化 =================

    public override void Initialize()
    {
        base.Initialize();

        currentDetermination = 0;
        IsDefending = false;

        OnDeterminationChanged?.Invoke(currentDetermination);
        OnDefenseChanged?.Invoke(this, IsDefending);

    }

    // ================= 武器 =================
    [Header("当前武器")]
    [SerializeField] private WeaponData currentWeapon;

    public WeaponData CurrentWeapon => currentWeapon;

    public int Attack => currentWeapon.Attack;
    public int AttackCount => currentWeapon.AttackCount;
    public  DamageType CurrentDamageType => currentWeapon.DamageType;

    public event Action<WeaponData> OnWeaponChanged;

    public void EquipWeapon(WeaponData weapon)
    {
        if(weapon == null)
            return;

        if (weapon == currentWeapon)
            return;

        currentWeapon = weapon;

        Debug.Log($"玩家装备武器：{currentWeapon.WeaponName}");

        OnWeaponChanged?.Invoke(currentWeapon);
    }

    // ================= 防御系统 =================

    [Header("防御属性")]
    [SerializeField, Range(0f, 1f)]
    private float defenseRate = 0.5f;

    public bool IsDefending { get; private set; }

    public event Action<Combatant, bool> OnDefenseChanged;


    public void StartDefense()
    {
        if (IsDead || IsDefending)
            return;

        IsDefending = true;

        Debug.Log($"{gameObject.name}进入防御状态");

        OnDefenseChanged?.Invoke(this, IsDefending);
    }


    public void EndDefense()
    {
        if (!IsDefending)
            return;

        IsDefending = false;

        Debug.Log($"{gameObject.name}结束防御状态");

        OnDefenseChanged?.Invoke(this, IsDefending);
    }

    // ================= 决心系统 =================
    [Header("决心值")]
    private const int maxDetermination = 4;
    [SerializeField] private int currentDetermination = 0;

    public int CurrentDetermination => currentDetermination;

    public event Action<int> OnDeterminationChanged;

    //增加决心
    public void AddDetermination(int amount = 1)
    {
        if (amount <= 0)
            return;

        int newValue = Mathf.Clamp(
            currentDetermination + amount,
            0,
            maxDetermination
        );

        if (newValue == currentDetermination)
            return;

        currentDetermination = newValue;

        OnDeterminationChanged?.Invoke(currentDetermination);
    }

    //消耗决心
    public bool SpendDetermination(int amount)
    {
        if (amount < 0 || currentDetermination < amount)
            return false;

        currentDetermination -= amount;

        if (amount > 0)
        {
            OnDeterminationChanged?.Invoke(currentDetermination);
        }

        return true;
    }

    // ================= 受伤逻辑 =================

    public override int TakeDamage(int damage)
    {
        int finalDamage = Mathf.Max(0, damage);

        if (IsDefending)
        {
            finalDamage = Mathf.FloorToInt(
                finalDamage * defenseRate
            );
        }

        return base.TakeDamage(finalDamage);
    }


}