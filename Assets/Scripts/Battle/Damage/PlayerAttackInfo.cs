using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct PlayerAttackInfo
{
    public Combatant attacker;
    public Combatant target;

    public IReadOnlyList<DamageComponent> damageComponents;
    public float damageMultiplier;

    public PlayerAttackInfo(Combatant attacker, Combatant target, IReadOnlyList<DamageComponent> damageComponents,
        float damageMultiplier = 1f)
    {
        this.attacker = attacker;
        this.target = target;
        this.damageComponents = damageComponents;
        this.damageMultiplier = damageMultiplier;
    }
}
