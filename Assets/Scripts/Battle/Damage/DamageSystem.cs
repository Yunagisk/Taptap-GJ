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
                if (baseDamage > 0) resistance.ObserveDamageType(component.damageType);
                reaction = resistance.GetDamageReaction(component.damageType);

                multiplier = resistance.GetDamageMultiplier(component.damageType);
                // 剑技能只忽略抗性减伤，仍保留免疫和原本的弱点关系。
                if (info.ignoreResistance && reaction == DamageReaction.Resistant)
                    multiplier = 1f;
            }

            // 强化与抗性一起计算，每个分量只向下取整一次。
            int typeDamage = Mathf.Max(0,
                Mathf.FloorToInt(baseDamage * multiplier * Mathf.Max(0f, info.damageMultiplier)));

            results[i] = new SingleDamageResult( component.damageType,reaction,baseDamage,typeDamage);

            totalBaseDamage += baseDamage;
            totalTypeDamage += typeDamage;
        }

        // 防御由 PlayerCombatant.TakeDamage 统一处理。
        // 衰弱和破甲在防御之前乘上。全部分量伤害为 0 时，不触发扣血。
        int modifiedDamage = ScaleDirect(info.attacker, info.target, totalTypeDamage);
        int actualDamage = modifiedDamage > 0 ? info.target.TakeDamage(modifiedDamage): 0;

        if (info.target is EnemyCombatant enemy && enemy.Break != null && !info.target.IsDead)
        {
            int gain = StatusRules.BreakFromSegment(PartsFrom(results));
            if (gain > 0 && enemy.Break.TryAdd(gain, enemy.BattleRound))
            {
                Debug.Log($"{enemy.name} 破防。保护到第{enemy.Break.ProtectedThroughRound}轮，下一次行动跳过。");
            }
        }

        string attackerName = info.attacker != null? info.attacker.name: "未知攻击者";

        Debug.Log(
         $"{attackerName} -> {info.target.name}，" +
         $"基础总伤害：{totalBaseDamage}，" +
         $"抗性修正后：{totalTypeDamage}，" +
         $"状态修正后：{modifiedDamage}，" +
         $"实际扣血：{actualDamage}");

        return new AllDamageResult(results,totalBaseDamage,totalTypeDamage,actualDamage);
    }

    static int ScaleDirect(Combatant attacker, Combatant target, int amount)
    {
        bool weak = attacker != null && attacker.Statuses.Has(StatusId.Weak);
        bool sunder = target != null && target.Statuses.Has(StatusId.Sunder);
        return StatusRules.ScaleDirectDamage(amount, weak, sunder);
    }

    static SegmentPart[] PartsFrom(SingleDamageResult[] results)
    {
        SegmentPart[] parts = new SegmentPart[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            parts[i] = new SegmentPart(
                CombatantStatusHost.Map(results[i].damageReaction),
                results[i].baseDamage);
        }
        return parts;
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

        // 衰弱、破甲先计算；酒馆首次受击减伤随后生效，防御仍由 TakeDamage 处理。
        int modifiedDamage = ScaleDirect(info.attacker, info.target, damage);
        var runPlayer = info.target is PlayerCombatant && GameRoot.I != null
            && GameRoot.I.state != null ? GameRoot.I.state.player : null;
        int reductionPercent = runPlayer != null
            ? Mathf.Clamp(runPlayer.nextDirectHitReductionPercent, 0, 100) : 0;
        // 正伤害减免后至少保留 1 点；状态修正后的零伤害不会被抬高。
        int reducedDamage = reductionPercent > 0 && modifiedDamage > 0
            ? Mathf.Max(1, Mathf.FloorToInt(modifiedDamage * (1f - reductionPercent / 100f)))
            : modifiedDamage;
        int actualDamage = reducedDamage > 0 ? info.target.TakeDamage(reducedDamage) : 0;
        // 零伤害、未扣血的命中不消耗；旧战斗回调不能修改重开的状态。
        if (actualDamage > 0 && reductionPercent > 0 && GameRoot.I != null
            && GameRoot.I.state != null && GameRoot.I.state.player == runPlayer)
            runPlayer.nextDirectHitReductionPercent = 0;

        Debug.Log(
            $"{attackerName} -> {targetName}，" +
            $"基础伤害：{damage}，状态修正后：{modifiedDamage}，实际扣血：{actualDamage}");

        return actualDamage;
    }
}
