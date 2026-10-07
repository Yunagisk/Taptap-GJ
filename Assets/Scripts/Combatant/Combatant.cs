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

    protected int currentHp;

    public int MaxHP => maxHp;
    public int Attack => attack;
    public int AttackCount => attackCount;
    public int CurrentHP => currentHp;
    public int Priority => priority;
    public bool IsDead => currentHp <= 0;

    private bool isDefending = false;

    public event Action<Combatant> OnHpChanged;
    public event Action<Combatant> OnDied;

    public virtual void Initialize()
    {
        currentHp = MaxHP;

        OnHpChanged?.Invoke(this);  //如果有人订阅 HP 改变事件，就通知他们，并把当前角色传过去。
    }
    public virtual void TakeDamage(int damage)
    {

        int finalDamage = isDefending ? Mathf.FloorToInt(damage * 0.5f) : damage;

        currentHp -= finalDamage;
        currentHp = Math.Clamp(currentHp, 0, maxHp);
        Debug.Log($"{gameObject.name}受到{damage}伤害,剩余HP:{currentHp}");

        OnHpChanged?.Invoke(this);

        if (IsDead)
        {
            Die();
        }
    }
    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name}死亡");
        OnDied?.Invoke(this);
    }

    public void StartDefense()
    {
        isDefending = true;
    }
    public void EndDefense()
    {
        isDefending = false;
    }
}
