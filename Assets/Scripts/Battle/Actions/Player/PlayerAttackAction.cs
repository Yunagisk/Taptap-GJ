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

        var sourceComponents = weapon.DamageComponents;

        if (sourceComponents == null || sourceComponents.Count == 0)
            yield break;

        // 固定本次行动的攻击次数和伤害配置。
        int attackCount = weapon.AttackCount;

        DamageComponent[] components = new DamageComponent[sourceComponents.Count];

        for (int i = 0; i < sourceComponents.Count; i++)
        {
            components[i] = sourceComponents[i];
        }

        for (int i = 0; i < attackCount; i++)
        {
            if (player == null || player.IsDead)
                yield break;

            if (target == null || target.IsDead)
                break;

            PlayerAttackInfo info = new PlayerAttackInfo(
                player,
                target,
                components);

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
                                                