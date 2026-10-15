using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSkill", menuName = "SkillData")]
public class SkillData : ScriptableObject
{
    [Header("技能信息")]
    [SerializeField] private string skillName;
    [TextArea]
    [SerializeField] private string skillDescription;
    [Header("决心消耗")]
    [Range(0, 4)]
    [SerializeField] private int determinationCost;

    [Header("技能效果")]
    [SerializeField] private SkillAim aim = SkillAim.OneTarget;
    [SerializeField] private List<DamageComponent> hits = new List<DamageComponent>();
    [Min(1)]
    [SerializeField] private int hitCount = 1;
    [SerializeField] private bool ignoreResistance;
    [SerializeField] private int extraBreakOnWeak;
    [SerializeField] private int coefficientMilli = 1000;
    [SerializeField] private List<DamageComponent> followHits = new List<DamageComponent>();
    [SerializeField] private List<SkillStatusGrant> statuses = new List<SkillStatusGrant>();

    public string SkillName => skillName;
    public string Description => skillDescription;
    public int DeterminationCost => determinationCost;
    public SkillAim Aim => aim;
    public int HitCount => hitCount < 1 ? 1 : hitCount;
    public bool IgnoreResistance => ignoreResistance;
    public int ExtraBreakOnWeak => extraBreakOnWeak;
    public int CoefficientMilli => coefficientMilli <= 0 ? 1000 : coefficientMilli;
    public IReadOnlyList<DamageComponent> Hits => hits;
    public IReadOnlyList<DamageComponent> FollowHits => followHits;
    public IReadOnlyList<SkillStatusGrant> Statuses => statuses;

    public SkillCastSpec ToSpec()
    {
        SkillStatusGrant[] grants = statuses != null && statuses.Count > 0
            ? statuses.ToArray()
            : Array.Empty<SkillStatusGrant>();

        return new SkillCastSpec
        {
            Aim = aim,
            Segment = ToShares(hits),
            SegmentCount = HitCount,
            IgnoreResistance = ignoreResistance,
            ExtraBreakOnWeak = extraBreakOnWeak,
            CoefficientMilli = CoefficientMilli,
            FollowSegment = ToShares(followHits),
            Statuses = grants
        };
    }

    static Share[] ToShares(List<DamageComponent> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<Share>();

        Share[] shares = new Share[source.Count];
        for (int i = 0; i < source.Count; i++)
            shares[i] = new Share((int)source[i].damageType, source[i].damage);
        return shares;
    }
}
