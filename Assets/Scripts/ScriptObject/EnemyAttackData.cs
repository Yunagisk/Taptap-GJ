using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//CalculateDamage() 是给未来 Boss 伤害变种预留的扩展位置；OnHit() 是未来附加效果的入口，目前不执行任何效果。
[CreateAssetMenu(fileName = "KBM",menuName = "EnemyAttackData")]
public class EnemyAttackData : ScriptableObject
{
    [Header("招式名称")]
    [SerializeField] private string attackName;

    [Header("每次命中的基础伤害")]
    [Min(0)]
    [SerializeField] private int damage = 10;

    [Header("命中次数")]
    [Min(1)]
    [SerializeField] private int attackCount = 1;

    [Header("两次命中之间的间隔")]
    [Min(0f)]
    [SerializeField] private float hitInterval = 0.3f;

    public string AttackName =>string.IsNullOrWhiteSpace(attackName) ? name : attackName;
    public int Damage => Mathf.Max(0, damage);
    public int AttackCount => Mathf.Max(1, attackCount);
    public float HitInterval => Mathf.Max(0f, hitInterval);

    //将 SO 中的配置转换成定义的 EnemyAttackInfo

    public EnemyAttackInfo CreateAttackInfo(Combatant attacker,Combatant target,int hitIndex)
    {
        int calculatedDamage = CalculateDamage(attacker,target,hitIndex);

        return new EnemyAttackInfo(attacker,target,Mathf.Max(0, calculatedDamage));
    }


    // hitIndex 从 0 开始。
    // 未来可派生新的招式 SO，重写这里实现特殊伤害数值。
    public virtual int CalculateDamage(Combatant attacker,Combatant target,int hitIndex)
    {
        return Damage;
    }

    // 每次命中完成后调用，包括实际伤害为 0 的情况。
    // 未来在这里向目标的状态组件施加效果。
    public virtual void OnHit(Combatant attacker,Combatant target,int hitIndex,int actualDamage)
    {

    }
}
