using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct EnemyAttackInfo
{
    public Combatant attacker;
    public Combatant target;
    public int damage;

    public EnemyAttackInfo(  Combatant attacker,Combatant target,int damage)
    {       
        this.attacker = attacker;
        this.target = target;
        this.damage = damage;
    }
}
