using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBattleAction
{
    IEnumerator Execute(Combatant user, Combatant target);
}
