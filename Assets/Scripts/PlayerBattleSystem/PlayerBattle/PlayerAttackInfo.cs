using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct PlayerAttackInfo
{
    public Combatant attacker;
    public Combatant target;

    public IReadOnlyList<DamageComponent> damageComponents; 

    public PlayerAttackInfo(Combatant attacker, Combatant target, IReadOnlyList<DamageComponent> damageComponents)
    {
        this.attacker = attacker;
        this.target = target;
        this.damageComponents = damageComponents;
    }
}
