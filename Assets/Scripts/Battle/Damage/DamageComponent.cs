using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public struct DamageComponent
{
    public PlayerDamageType damageType;

    [Min(0)]
    public int damage;

    public DamageComponent(PlayerDamageType damageType, int damage)
    {
        this.damageType = damageType;
        this.damage = damage;
    }
}
