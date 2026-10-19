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
        public PlayerDamageType damageType;
        public DamageReaction damageReaction;
    }
    [Header("伤害类型抗性")]
    [SerializeField] private List<DamageRule> damageRules = new List<DamageRule>();
    readonly HashSet<PlayerDamageType> observedTypes = new HashSet<PlayerDamageType>();

    public bool IsWeaknessKnown(PlayerDamageType type) =>
        observedTypes.Contains(type) && GetDamageReaction(type) == DamageReaction.Weak;
    public void ObserveDamageType(PlayerDamageType type) => observedTypes.Add(type);
    public void ResetDiscovery() => observedTypes.Clear();

    public int UnknownWeaknessCount
    {
        get
        {
            int count = 0;
            foreach (PlayerDamageType type in Enum.GetValues(typeof(PlayerDamageType)))
                if (GetDamageReaction(type) == DamageReaction.Weak && !IsWeaknessKnown(type)) count++;
            return count;
        }
    }

    public int RevealWeaknesses()
    {
        int count = 0;
        foreach (PlayerDamageType type in Enum.GetValues(typeof(PlayerDamageType)))
            if (GetDamageReaction(type) == DamageReaction.Weak && observedTypes.Add(type)) count++;
        return count;
    }

    // Phase controllers replace the active rules through this entry point.
    // Discovery belongs to the current phase, so a new phase needs a new reveal.
    public void SetStageRules(IEnumerable<DamageRule> rules)
    {
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        var next = new List<DamageRule>();
        foreach (var rule in rules)
            if (rule != null) next.Add(new DamageRule { damageType = rule.damageType, damageReaction = rule.damageReaction });
        damageRules = next;
        ResetDiscovery();
    }

    public DamageReaction GetDamageReaction(PlayerDamageType damageType)
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

    public float GetDamageMultiplier(PlayerDamageType damageType)
    {
        DamageReaction reaction = GetDamageReaction(damageType);
        switch (reaction)
        {
            case DamageReaction.Weak:
                return 1f; // 弱点不加成伤害，只给破防计数。易伤是破甲。
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
