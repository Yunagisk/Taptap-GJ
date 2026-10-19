using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillAction : IBattleAction
{
    readonly IReadOnlyList<EnemyCombatant> enemies;

    public SkillAction(IReadOnlyList<EnemyCombatant> enemies) { this.enemies = enemies; }

    public IEnumerator Execute(Combatant user, Combatant target)
    {
        if (user is not PlayerCombatant player || player.IsDead || player.Statuses.Has(StatusId.Exhaust))
            yield break;
        var weapon = player.CurrentWeapon;
        var skill = weapon != null ? weapon.Skill : null;
        if (skill == null || !skill.IsConfigured || enemies == null) yield break;

        // 锁定本次的敌人集合；死亡目标跳过，多段不转移到其他敌人。
        var targets = new List<EnemyCombatant>();
        foreach (var enemy in enemies)
            if (enemy != null && !enemy.IsDead && !targets.Contains(enemy)) targets.Add(enemy);
        if (targets.Count == 0 || skill.RequiresPrimaryTarget
            && (target is not EnemyCombatant primary || !targets.Contains(primary))) yield break;
        if (!player.SpendDetermination(skill.DeterminationCost)) yield break;

        // 破防保护覆盖当轮及下一轮。跳过敌方行动后，下一次玩家行动仍能用收割增伤。
        bool brokenAtStart = target is EnemyCombatant victim && victim.Break != null
            && victim.Break.IsProtected(victim.BattleRound);
        float conditionalMultiplier = brokenAtStart ? skill.BrokenTargetMultiplier : 1f;
        var runPlayer = GameRoot.I != null && GameRoot.I.state != null ? GameRoot.I.state.player : null;
        int bonus = runPlayer != null ? Mathf.Max(0, runPlayer.nextAttackBonusPercent) : 0;
        float multiplier = conditionalMultiplier * (1f + bonus / 100f);
        if (runPlayer != null && bonus > 0) runPlayer.nextAttackBonusPercent = 0;

        var hitTypes = new Dictionary<Combatant, HashSet<PlayerDamageType>>();
        bool hitAny = false;
        foreach (var stage in skill.Stages)
        {
            var stageTargets = stage.target == SkillTarget.AllEnemies ? targets
                : new List<EnemyCombatant> { (EnemyCombatant)target };
            bool HasTarget() => stageTargets.Exists(enemy => enemy != null && !enemy.IsDead);
            for (int segment = 0; segment < stage.hitCount; segment++)
            {
                if (player == null || player.IsDead) yield break;
                if (!HasTarget()) break;
                var visualTarget = stageTargets.Find(enemy => enemy != null && !enemy.IsDead);
                void Hit()
                {
                    foreach (var enemy in stageTargets)
                    {
                        if (player == null || player.IsDead || enemy == null || enemy.IsDead) continue;
                        var result = DamageSystem.DealDamage(new PlayerAttackInfo(player, enemy,
                            stage.damageComponents, multiplier, skill.IgnoreResistance));
                        hitAny = true;
                        if (!hitTypes.TryGetValue(enemy, out var types))
                        { types = new HashSet<PlayerDamageType>(); hitTypes.Add(enemy, types); }
                        bool extraBreak = false;
                        foreach (var part in result.components)
                        {
                            if (part.baseDamage <= 0 || part.damageReaction == DamageReaction.Immune) continue;
                            types.Add(part.damageType);
                            if (part.damageType == skill.ExtraBreakDamageType
                                && part.damageReaction == DamageReaction.Weak) extraBreak = true;
                        }
                        if (extraBreak && enemy != null && !enemy.IsDead && enemy.Break != null)
                            enemy.Break.TryAdd(skill.ExtraBreakOnWeakness, enemy.BattleRound);
                    }
                }
                yield return PlayerAttackPlayback.Play(player, weapon, visualTarget, HasTarget, Hit);
            }
        }

        if (player == null || player.IsDead || !hitAny || skill.StatusEffects == null) yield break;
        foreach (var effect in skill.StatusEffects)
        {
            if (effect == null || effect.stacks <= 0) continue;
            if (effect.applyToSelf) player.ApplyStatus(effect.status, effect.stacks);
            else foreach (var pair in hitTypes)
                if (pair.Key != null && !pair.Key.IsDead && pair.Value.Contains(effect.requiredDamageType))
                    pair.Key.ApplyStatus(effect.status, effect.stacks);
        }
        // 技能不产生普攻决心，也不受加速的额外普攻段影响。
    }
}
