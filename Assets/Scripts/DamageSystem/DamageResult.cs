using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//服务于UI、伤害数字、战斗日志服务
//保存伤害结算结果
public struct DamageResult
{
    public DamageType damageType;
    public DamageReaction damageReaction;

    public int baseDamage;
    public int typeDamage;
    public int finalDamage;

    public DamageResult(DamageType damageType, DamageReaction damageReaction, int baseDamage, int typeDamage, int finalDamage)
    {
        this.damageType = damageType;
        this.damageReaction = damageReaction;
        this.baseDamage = baseDamage;
        this.typeDamage = typeDamage;
        this.finalDamage = finalDamage;
    }
}