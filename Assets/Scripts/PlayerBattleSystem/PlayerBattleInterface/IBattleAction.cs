using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBattleAction
{
    void Execute(Combatant user, Combatant target);
}
