using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DamageSystem
{
    public static DamageResult DealDamage(PlayerAttackInfo info)
    {
        if (info.target == null)
        {
            Debug.LogWarning("目标为空");
            return default;
        }

        int baseDamage = Mathf.Max(0, info.baseDamage);
        DamageReaction damageReaction = DamageReaction.Normal;
        float multiplier = 1.0f;

        //获取目标伤害抗性组件
        DamageResistance resistance = info.target.GetComponent<DamageResistance>();

        if (resistance != null)
        {
            damageReaction = resistance.GetDamageReaction(info.damageType);
            multiplier = resistance.GetDamageMultiplier(info.damageType);
        }
        //类型修正后的伤害
        int typeDamage = Mathf.FloorToInt(baseDamage * multiplier);

        //免疫
        if (typeDamage == 0)
        {
            Debug.Log($"目标 {info.target.name} 对 {info.damageType} 免疫，伤害为0");
            return new DamageResult
            {
                damageType = info.damageType,
                damageReaction = damageReaction,
                baseDamage = baseDamage,
                typeDamage = typeDamage,
                finalDamage = 0
            };
        }

        //最终交给Combantant
        int finalDamage = info.target.TakeDamage(typeDamage);

        Debug.Log(
            $"{info.attacker.name}->{info.target.name} " +
            $"{info.damageType} 的伤害反应为 {damageReaction}，" +
            $"基础伤害为 {baseDamage}，" +
            $"类型修正后伤害为 {typeDamage}，" +
            $"最终造成伤害为 {finalDamage}");

        return new DamageResult
        (
            info.damageType,
                        damageReaction,
                        baseDamage,
                        typeDamage,
                        finalDamage
        );
    }
}
