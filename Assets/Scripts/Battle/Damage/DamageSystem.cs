using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static SingleDamageResult;

public static class DamageSystem
{
    public static AllDamageResult DealDamage(PlayerAttackInfo info)
    {

        if (info.target == null || info.target.IsDead)
        {
            return EmptyResult();
        }

        if (info.damageComponents == null ||
            info.damageComponents.Count == 0)
        {
            return EmptyResult();
        }

        DamageResistance resistance = info.target.GetComponent<DamageResistance>();

                                                     
        SingleDamageResult[] results = new SingleDamageResult[info.damageComponents.Count];

        int totalBaseDamage = 0;
        int totalTypeDamage = 0;

        for (int i = 0; i < info.damageComponents.Count; i++)
        {
            DamageComponent component = info.damageComponents[i];

            int baseDamage = Mathf.Max(0, component.damage);
            DamageReaction reaction = DamageReaction.Normal;
            float multiplier = 1f;

            if (resistance != null)
            {
                reaction = resistance.GetDamageReaction(component.damageType);

                multiplier = resistance.GetDamageMultiplier(component.damageType);
            }

            // 每个分量分别修正并向下取整。
            int typeDamage = Mathf.Max( 0,Mathf.FloorToInt(baseDamage * multiplier));

            results[i] = new SingleDamageResult( component.damageType,reaction,baseDamage,typeDamage);

            totalBaseDamage += baseDamage;
            totalTypeDamage += typeDamage;
        }

        // 防御由 PlayerCombatant.TakeDamage 统一处理。
        // 全部分量伤害为 0 时，不触发扣血。
        int actualDamage = totalTypeDamage > 0 ? info.target.TakeDamage(totalTypeDamage): 0;

        string attackerName = info.attacker != null? info.attacker.name: "未知攻击者";

        Debug.Log(
         $"{attackerName} -> {info.target.name}，" +
         $"基础总伤害：{totalBaseDamage}，" +
         $"抗性修正后：{totalTypeDamage}，" +
         $"实际扣血：{actualDamage}");

        return new AllDamageResult(results,totalBaseDamage,totalTypeDamage,actualDamage);
    }

    private static AllDamageResult EmptyResult()
    {
        return new AllDamageResult(Array.Empty<SingleDamageResult>(),0,0,0);
    }

    //=================敌人伤害处理系统==================
    public static int DealDamage(EnemyAttackInfo info)
    {
        if (info.attacker == null || info.target == null)
            return 0;

        if (info.attacker.IsDead || info.target.IsDead)
            return 0;

        int damage = Mathf.Max(0, info.damage);

        if (damage == 0)
            return 0;

        // 提前保存名称，避免死亡事件影响日志取值。
        string attackerName = info.attacker.name;
        string targetName = info.target.name;

        // 玩家防御仍由 PlayerCombatant.TakeDamage 处理。
        int actualDamage = info.target.TakeDamage(damage);

        Debug.Log(
            $"{attackerName} -> {targetName}，" +
            $"基础伤害：{damage}，实际扣血：{actualDamage}");

        return actualDamage;
    }
}
