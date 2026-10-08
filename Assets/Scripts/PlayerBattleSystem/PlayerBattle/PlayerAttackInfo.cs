using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct PlayerAttackInfo
{
    public Combatant attacker;
    public Combatant target;

    public int baseDamage;

    public DamageType damageType;

    public PlayerAttackInfo(Combatant attacker, Combatant target, DamageType damageType,int baseDamage)
    {
        this.baseDamage = baseDamage;
        this.damageType = damageType;
        this.attacker = attacker;
        this.target = target;
    }
}
