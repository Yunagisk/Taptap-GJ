using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackAction
{
    public void Execute(
        EnemyCombatant enemy,
        PlayerCombatant player,
        EnemyAttackInfo enemyAttackInfo)
    {
        if (enemy == null || player == null)
            return;

        if (enemy.IsDead || player.IsDead)
            return;

        for (int i = 0; i < enemyAttackInfo.attackCount; i++)
        {
            PlayerAttackInfo info = new PlayerAttackInfo(
                enemy,
                player,
                enemyAttackInfo.damageType,
                enemyAttackInfo.damage
            );

            DamageSystem.DealDamage(info);

            if (player.IsDead)
                break;
        }

        // 后续在这里接入负面效果系统
    }
}
