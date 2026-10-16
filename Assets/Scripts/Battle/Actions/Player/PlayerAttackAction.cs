using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackAction : IBattleAction
{
    public IEnumerator Execute(Combatant user, Combatant target)
    {
        if (user is not PlayerCombatant player)
            yield break;
        if (target == null || player.IsDead || target.IsDead)
            yield break;

        WeaponData weapon = player.CurrentWeapon;
        if (weapon == null || weapon.AttackCount <= 0)
            yield break;

        var sourceComponents = weapon.DamageComponents;

        if (sourceComponents == null || sourceComponents.Count == 0)
            yield break;

        // 固定本次行动的攻击次数和伤害配置，不修改武器资产。加速只给普攻多 1 段。
        int attackCount = player.AttackCount;
        DamageComponent[] components = new DamageComponent[sourceComponents.Count];
        bool hasDamage = false;
        for (int i = 0; i < sourceComponents.Count; i++)
        {
            components[i] = sourceComponents[i];
            if (components[i].damage > 0) hasDamage = true;
        }
        if (!hasDamage)
            yield break;

        // 真正开始有效攻击时才消费；整次多段攻击共用同一个倍率。
        var runPlayer = GameRoot.I != null && GameRoot.I.state != null
            ? GameRoot.I.state.player : null;
        int bonusPercent = runPlayer != null ? Mathf.Max(0, runPlayer.nextAttackBonusPercent) : 0;
        float damageMultiplier = 1f + bonusPercent / 100f;
        if (runPlayer != null && bonusPercent > 0) runPlayer.nextAttackBonusPercent = 0;

        for (int i = 0; i < attackCount; i++)
        {
            if (player == null || player.IsDead)
                yield break;

            if (target == null || target.IsDead)
                break;

            PlayerAttackInfo info = new PlayerAttackInfo(
                player,
                target,
                components,
                damageMultiplier);

            DamageSystem.DealDamage(info);

            if (target == null || target.IsDead)
                break;

            yield return new WaitForSeconds(0.3f);
        }

        if (player != null && !player.IsDead)
        {
            player.AddDetermination(1);
        }
    }

}
                                                