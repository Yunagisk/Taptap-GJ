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
    
    public event Action<Combatant> OnHpChanged;
    public event Action<Combatant> OnDied;

    public virtual void Initialize()
    {
        currentHp = MaxHP;

        OnHpChanged?.Invoke(this);  //如果有人订阅 HP 改变事件，就通知他们，并把当前角色传过去。
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
        OnDied?.Invoke(this);
    }
}


