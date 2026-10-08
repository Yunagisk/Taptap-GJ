using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillAction : IBattleAction
{
    public IEnumerator Execute(Combatant user, Combatant target)
    {
        PlayerCombatant player = user as PlayerCombatant;
        SkillData skill = player.CurrentWeapon.Skill;
        if (player.CurrentDetermination < skill.DeterminationCost)
        {
            Debug.Log("决心不足");
            yield break;
        }
        player.SpendDetermination(skill.DeterminationCost);
    }
}
