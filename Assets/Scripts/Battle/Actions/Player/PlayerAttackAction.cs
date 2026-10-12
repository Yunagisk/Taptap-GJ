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
        if (weapon == null) yield break;
        if (!WeaponAttacks.TryBuild(weapon, out DamageComponent[] components, out int attackCount))
            yield break;

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
                                                