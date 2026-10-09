using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct EnemyAttackInfo
{
    public int damage;
    public int attackCount;
    public DamageType damageType;

    public EnemyAttackInfo(
        int damage,
        int attackCount,
        DamageType damageType )
    {
        this.damage = damage;
        this.attackCount = attackCount;
        this.damageType = damageType;
    }
}
