using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefendAction : IBattleAction
{
    public IEnumerator Execute(Combatant user, Combatant target)
    {
        PlayerCombatant player = user as PlayerCombatant;
        player.StartDefense();
        player.AddDetermination(1);
       
        yield break;
    }
}
