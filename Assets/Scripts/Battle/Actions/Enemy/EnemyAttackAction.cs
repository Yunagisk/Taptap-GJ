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
        var animation = enemy.GetComponent<EnemySpriteAnimation>();

        Debug.Log($"{enemy.name} 使用 {attackData.AttackName}");

        for (int i = 0; i < attackCount; i++)
        {
            // 协程等待期间，攻击者和目标可能已经死亡或销毁。
            if (enemy == null || player == null)
                yield break;

            if (enemy.IsDead || player.IsDead)
                yield break;

            int hitIndex = i;
            bool connected = false;
            void ApplyHit()
            {
                if (enemy == null || player == null || enemy.IsDead || player.IsDead) return;
                connected = true;
                EnemyAttackInfo info = attackData.CreateAttackInfo(enemy,player,hitIndex);
                int actualDamage = DamageSystem.DealDamage(info);
                if (enemy != null && player != null && !enemy.IsDead && !player.IsDead)
                    attackData.OnHit(enemy,player,hitIndex,actualDamage);
            }
            if (animation != null && animation.CanPlayAttack)
                yield return animation.PlayAttack(player, () => enemy != null && !enemy.IsDead,
                    () => player != null && !player.IsDead, ApplyHit);
            else
                ApplyHit();

            // A canceled wind-up must not apply the attack's status rider.
            if (!connected) yield break;

            // 命中后等待动画恢复，再决定是否继续下一段。
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

        if (enemy != null && player != null && !player.IsDead)
            attackData.ApplyRider(player);
    }
}
