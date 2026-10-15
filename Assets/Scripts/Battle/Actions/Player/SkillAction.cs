using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillAction : IBattleAction
{
    public IEnumerator Execute(Combatant user, Combatant target)
    {
        PlayerCombatant player = user as PlayerCombatant;
        if (player.Statuses.Has(StatusId.Exhaust))
        {
            Debug.Log("疲惫：武器技能不可用");
            yield break;
        }
        SkillData skill = player.CurrentWeapon.Skill;
        if (player.CurrentDetermination < skill.DeterminationCost)
        {
            Debug.Log("决心不足");
            yield break;
        }
        player.SpendDetermination(skill.DeterminationCost);
        // 技能效果先不从战斗行动里执行。点击按钮走 BattleFlow.SpendWeaponResolveOnly。
    }
}
