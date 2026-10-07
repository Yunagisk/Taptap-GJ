using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Combatant : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] protected int maxHp = 100;
    [SerializeField] protected int attack = 10;
    [SerializeField] protected int attackCount = 1;
    [SerializeField] protected int priority = 10;

    [Header("防御属性")]
    [SerializeField,Range(0f,1f)] protected float defenseRate = 0.5f;

    protected int currentHp;

    public int MaxHP => maxHp;
    public int Attack => attack;
    public int AttackCount => attackCount;
    public int CurrentHP => currentHp;
    public int Priority => priority;
    
    public bool IsDead => currentHp <= 0;
    public bool IsDefending { get;private set; }
    

    public event Action<Combatant> OnHpChanged;
    public event Action<Combatant> OnDied;
    public event Action<Combatant,bool> OnDefenseChanged;

    public virtual void Initialize()
    {
        currentHp = MaxHP;

        IsDefending = false;

        OnHpChanged?.Invoke(this);  //如果有人订阅 HP 改变事件，就通知他们，并把当前角色传过去。
    }
    public virtual void TakeDamage(int damage)
    {
        if (IsDead)
        {
            return;
        }
        int finalDamage = damage;

        if (IsDefending)
        {
            finalDamage = Mathf.RoundToInt(damage * defenseRate);
        }

        currentHp -= finalDamage;
        currentHp = Mathf.Max(currentHp, 0);
        Debug.Log($"{gameObject.name}受到{finalDamage}伤害,剩余HP:{currentHp}");

        OnHpChanged?.Invoke(this);

        if (IsDead)
        {
            Die();
        }
    }

    public void StartDefense()
    {
        if (IsDead)
            return;

        IsDefending = true;

        Debug.Log($"{gameObject.name}进入防御状态");

        OnDefenseChanged?.Invoke(this, IsDefending);
    }
    public void EndDefense()
    {
        if(!IsDefending)
            return;

        IsDefending = false;

        Debug.Log($"{gameObject.name}结束防御状态");

        OnDefenseChanged?.Invoke(this, IsDefending);
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name}死亡");
        OnDied?.Invoke(this);
    }
}
