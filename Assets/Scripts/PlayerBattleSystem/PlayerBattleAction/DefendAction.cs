using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefendAction : IBattleAction
{
    public IEnumerator Execute(Combatant user, Combatant target)
    {
        user.StartDefense();
        yield break;
    }
}
