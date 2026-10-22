using System;
using System.Collections;
using UnityEngine;

// 普攻与技能使用相同的命中时点、动作和恢复时间。
public static class PlayerAttackPlayback
{
    public static IEnumerator Play(PlayerCombatant player, WeaponData weapon,
        Combatant visualTarget, Func<bool> hasTarget, Action onHit)
    {
        bool AttackerAlive() => player != null && !player.IsDead;
        if (!AttackerAlive() || !hasTarget()) yield break;
        var animation = player.GetComponent<PlayerAttackAnimation>();
        void Hit()
        {
            if (!AttackerAlive() || !hasTarget()) return;
            if (animation != null && animation.Supports(weapon)) animation.ShowImpact(visualTarget);
            onHit();
        }
        if (animation != null && animation.Supports(weapon))
            yield return animation.PlayAttack(weapon, AttackerAlive, hasTarget, Hit);
        else
        {
            Hit();
            yield return new WaitForSeconds(.3f);
        }
    }
}
