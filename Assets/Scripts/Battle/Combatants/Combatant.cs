using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Combatant : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] protected int maxHp = 100;

    protected int currentHp;

    public int MaxHP => maxHp;
    public int CurrentHP => currentHp;
    public bool IsDead => currentHp <= 0;

    // 本场战斗轮。状态施加轮和破防保护都读它。
    public int BattleRound = 1;

    StatusSet statuses;
    bool statusHooked;

    public StatusSet Statuses
    {
        get
        {
            if (statuses == null)
                statuses = new StatusSet();
            if (!statusHooked)
            {
                statusHooked = true;
                statuses.Changed += HandleStatusChanged;
            }
            return statuses;
        }
    }

    public event Action<Combatant> OnHpChanged;
    public event Action<Combatant> OnMarksChanged;
    public event Action<Combatant> OnDied;

    public virtual void Initialize()
    {
        currentHp = MaxHP;
        Statuses.ClearOrdinary();

        OnHpChanged?.Invoke(this);  //如果有人订阅 HP 改变事件，就通知他们，并把当前角色传过去。
        NotifyMarks();
    }

    protected void NotifyMarks()
    {
        OnMarksChanged?.Invoke(this);
    }

    public void ApplyStatus(StatusId id, int stacks)
    {
        Statuses.Apply(id, stacks, BattleRound);
        Debug.Log($"{name} {StatusRules.DisplayName(id)}{Statuses.Stacks(id)}层，第{BattleRound}轮");
    }

    // 中毒和点燃走这里，不吃防御、护盾和破甲。
    public int LoseRaw(int amount)
    {
        if (IsDead)
            return 0;

        amount = Mathf.Max(0, amount);
        int previousHp = currentHp;
        currentHp = Mathf.Max(0, currentHp - amount);
        int lost = previousHp - currentHp;
        if (lost > 0)
            Debug.Log($"{name} 状态扣血 {lost}，剩余HP:{currentHp}");

        OnHpChanged?.Invoke(this);
        if (IsDead)
            Die();
        return lost;
    }

    public int HealRaw(int amount)
    {
        if (IsDead)
            return 0;

        amount = Mathf.Max(0, amount);
        int previousHp = currentHp;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        int healed = currentHp - previousHp;
        if (healed > 0)
        {
            Debug.Log($"{name} 回复 {healed}，剩余HP:{currentHp}");
            OnHpChanged?.Invoke(this);
        }
        return healed;
    }

    void HandleStatusChanged()
    {
        NotifyMarks();
    }
    // 恢复本局生命，并通过现有事件刷新血条。
    protected void RestoreHealth(int savedHp, int savedMaxHp)
    {
        maxHp = Mathf.Max(1, savedMaxHp);
        currentHp = Mathf.Clamp(savedHp, 0, maxHp);
        OnHpChanged?.Invoke(this);
    }

    public virtual int TakeDamage(int damage)
    {
        if (IsDead)
        {
            return 0;
        }

        damage = Mathf.Max(0, damage);

        //注意 这里的actualDamage是实际扣除的血量，和finalDamage数值可能不一样。
        int previousHp = currentHp;
        currentHp = Mathf.Max(0, currentHp - damage);
        int actualDamage = previousHp - currentHp;

        Debug.Log($"{gameObject.name}受到{actualDamage}伤害,剩余HP:{currentHp}"); 

        OnHpChanged?.Invoke(this);

        if (IsDead)
        {
            Die();
        }
        return actualDamage;
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name}死亡");
        Destroy(gameObject);
        OnDied?.Invoke(this);
    }
}


