using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//配置敌人的弱点/抗性
public class DamageResistance : MonoBehaviour
{
    [Serializable]
    public class  DamageRule
    {
        public DamageType damageType;
        public DamageReaction damageReaction;
    }
    [Header("伤害类型抗性")]
    [SerializeField] private List<DamageRule> damageRules = new List<DamageRule>();

    public DamageReaction GetDamageReaction(DamageType damageType)
    {
        foreach (DamageRule rule in damageRules)
        {
            if (rule.damageType == damageType)
            {
                return rule.damageReaction;
            }
        }
        return DamageReaction.Normal; 
    }

    public float GetDamageMultiplier(DamageType damageType)
    {
        DamageReaction reaction = GetDamageReaction(damageType);
        switch (reaction)
        {
            case DamageReaction.Weak:
                return 1.5f; 
            case DamageReaction.Normal:
                return 1.0f; 
            case DamageReaction.Resistant:
                return 0.5f; 
            case DamageReaction.Immune:
                return 0.0f; 
            default:
                return 1.0f; 
        }
    }
}
