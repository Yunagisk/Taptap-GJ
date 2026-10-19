using System;
using UnityEngine;

public enum SkillTarget { PrimaryEnemy, AllEnemies }

[Serializable]
public sealed class SkillStage
{
    public SkillTarget target;
    [Min(1)] public int hitCount = 1;
    public DamageComponent[] damageComponents = Array.Empty<DamageComponent>();
}

[Serializable]
public sealed class SkillStatusEffect
{
    public bool applyToSelf;
    public StatusId status;
    [Min(1)] public int stacks = 1;
    public PlayerDamageType requiredDamageType;
}

[CreateAssetMenu(fileName = "NewSkill", menuName = "SkillData")]
public class SkillData : ScriptableObject
{
    [Header("技能信息")]
    [SerializeField] private string skillName;
    [TextArea]
    [SerializeField] private string skillDescription;
    [Header("决心消耗")]
    [Range(0,4)]
    [SerializeField] private int determinationCost;

    [Header("按顺序执行；同一段内的分量合并结算")]
    [SerializeField] private SkillStage[] stages = Array.Empty<SkillStage>();
    [Header("整次技能结束后，每个目标只施加一次")]
    [SerializeField] private SkillStatusEffect[] statusEffects = Array.Empty<SkillStatusEffect>();
    [SerializeField] private bool ignoreResistance;
    [SerializeField, Min(0)] private int extraBreakOnWeakness;
    [SerializeField] private PlayerDamageType extraBreakDamageType;
    [SerializeField, Min(1f)] private float brokenTargetMultiplier = 1f;

    public string SkillName => skillName;
    public string Description => skillDescription;
    public int DeterminationCost => determinationCost;
    public SkillStage[] Stages => stages;
    public SkillStatusEffect[] StatusEffects => statusEffects;
    public bool IgnoreResistance => ignoreResistance;
    public int ExtraBreakOnWeakness => extraBreakOnWeakness;
    public PlayerDamageType ExtraBreakDamageType => extraBreakDamageType;
    public float BrokenTargetMultiplier => Mathf.Max(1f, brokenTargetMultiplier);

    public bool RequiresPrimaryTarget
    {
        get
        {
            if (stages != null)
                foreach (var stage in stages)
                    if (stage != null && stage.target == SkillTarget.PrimaryEnemy) return true;
            return false;
        }
    }

    public bool IsConfigured
    {
        get
        {
            if (determinationCost < 0 || stages == null || stages.Length == 0) return false;
            foreach (var stage in stages)
            {
                if (stage == null || stage.hitCount <= 0 || stage.damageComponents == null) return false;
                bool hasDamage = false;
                foreach (var part in stage.damageComponents) if (part.damage > 0) hasDamage = true;
                if (!hasDamage) return false;
            }
            return true;
        }
    }

}
