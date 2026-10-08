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

        for (int i = 0; i < weapon.AttackCount; i++)
        {
        
            PlayerAttackInfo info = new PlayerAttackInfo(
              user,
              target,
              weapon.DamageType,
              weapon.Attack
            );

            DamageSystem.DealDamage(info);

            if (target.IsDead)
                break;

            Debug.Log($"{user.gameObject.name}攻击{target.gameObject.name}");

            yield return new WaitForSeconds(0.3f);
        }

            player.AddDetermination(1);      
    }

}
