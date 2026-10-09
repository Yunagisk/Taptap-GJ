using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[CreateAssetMenu(
    fileName = "EnemyAttackWay",
    menuName = "EnemyAttackData"
)]
public class EnemyAttackData : ScriptableObject
{
    [Header("攻击信息")]
    [SerializeField] private string attackName;

    [Header("攻击属性")]
    [SerializeField] private int damage = 10;
    [SerializeField] private int attackCount = 1;
    [SerializeField] private DamageType damageType;

    public string AttackName => attackName;
    public int Damage => damage;
    public int AttackCount => attackCount;
    public DamageType DamageType => damageType;

    //将 SO 中的配置转换成定义的 EnemyAttackInfo
    public EnemyAttackInfo CreateAttackInfo()
    {
        return new EnemyAttackInfo(
            damage,
            attackCount,
            damageType
        );
    }
}
