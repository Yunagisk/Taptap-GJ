using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackAction
{
    public IEnumerator Execute(EnemyCombatant enemy,PlayerCombatant player,EnemyAttackData attackData)
    {
        if (enemy == null || player == null || attackData == null)
            yield break;

        if (enemy.IsDead || player.IsDead)
            yield break;

        int attackCount = attackData.AttackCount;
        float hitInterval = attackData.HitInterval;

        Debug.Log($"{enemy.name} 使用 {attackData.AttackName}");

        for (int i = 0; i < attackCount; i++)
        {
            // 协程等待期间，攻击者和目标可能已经死亡或销毁。
            if (enemy == null || player == null)
                yield break;

            if (enemy.IsDead || player.IsDead)
                yield break;

            EnemyAttackInfo info = attackData.CreateAttackInfo(enemy,player,i);

            int actualDamage = DamageSystem.DealDamage(info);

            // 当前只对仍然存活的双方执行命中后效果。
            if (enemy == null || player == null)
                yield break;

            if (enemy.IsDead || player.IsDead)
                yield break;

            attackData.OnHit(enemy,player,i,actualDamage);  //目前 OnHit() 按每次命中触发。以后若某种效果需要“整次招式只触发一次”，应放在循环结束后的独立处理位置。

            if (enemy == null || player == null)
                yield break;

            if (enemy.IsDead || player.IsDead)
                yield break;

            bool hasNextHit = i < attackCount - 1;

            if (hasNextHit && hitInterval > 0f)
            {
                yield return new WaitForSeconds(hitInterval);
            }
        }
    }
}
