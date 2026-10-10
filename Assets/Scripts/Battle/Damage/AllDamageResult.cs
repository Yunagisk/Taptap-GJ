using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//服务于UI、伤害数字、战斗日志服务
//保存伤害结算结果

// 单个伤害分量的结算结果。保存各分量明细
// typeDamage 尚未经过防御，也未受目标剩余血量限制。
public struct SingleDamageResult
{
    public PlayerDamageType damageType;
    public DamageReaction damageReaction;

    public int baseDamage;
    public int typeDamage;

    public SingleDamageResult(PlayerDamageType damageType, DamageReaction damageReaction, int baseDamage, int typeDamage)
    {
        this.damageType = damageType;
        this.damageReaction = damageReaction;
        this.baseDamage = baseDamage;
        this.typeDamage = typeDamage;
    }

    // 一次命中的完整结算结果。
    public struct AllDamageResult
    {
        public SingleDamageResult[] components;

        // 所有分量的基础伤害总和。
        public int baseDamage;

        // 所有分量经过抗性修正后的总和。
        public int typeDamage;

        // 经过防御、剩余血量限制后的实际扣血。
        public int finalDamage;

        public AllDamageResult(
            SingleDamageResult[] components,
            int baseDamage,
            int typeDamage,
            int finalDamage)
        {
            this.components = components;
            this.baseDamage = baseDamage;
            this.typeDamage = typeDamage;
            this.finalDamage = finalDamage;
        }
    }
}