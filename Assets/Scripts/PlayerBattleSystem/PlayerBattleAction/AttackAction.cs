using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackAction : IBattleAction
{
     public void Execute(Combatant user, Combatant target)
     {
            target.TakeDamage(user.Attack);
     }
}
