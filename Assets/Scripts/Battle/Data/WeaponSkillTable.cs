using System;

// 十二把武器的技能初测值。和 Assets/Data/Skills 的资源是同一份。
// 0 穿刺，1 劈砍，2 钝击，3 火焰。
public sealed class WeaponSkillDef
{
    public string WeaponId;
    public string SkillName;
    public int Cost;
    public SkillCastSpec Spec;
}

public static class WeaponSkillTable
{
    public static readonly WeaponSkillDef[] All = Build();

    public static WeaponSkillDef Find(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId))
            return null;
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].WeaponId == weaponId)
                return All[i];
        }
        return null;
    }

    static WeaponSkillDef[] Build()
    {
        return new[]
        {
            Row("sword", "穿刺", 2, Aim(SkillAim.OneTarget, new[] { P(30) }, 1, true, 0, 1000)),
            Row("greatsword", "镇压", 3, Aim(SkillAim.OneTarget, new[] { P(36) }, 1, false, 0, 1000, Grant(StatusId.Weak, 1))),
            Row("spear", "穿心", 2, Aim(SkillAim.OneTarget, new[] { P(22) }, 1, false, 1, 1000)),
            Row("dual_swords", "连刺", 3, Aim(SkillAim.OneTarget, new[] { P(5) }, 4, false, 0, 1000)),
            Row("axe", "顺劈", 2, Aim(SkillAim.EachLiving, new[] { S(12) }, 1, false, 0, 1000)),
            Row("sickle", "收割", 3, Aim(SkillAim.OneTarget, new[] { S(9) }, 2, false, 0, 1300)),
            Row("hammer", "破甲", 2, Aim(SkillAim.OneTarget, new[] { B(24) }, 1, false, 0, 1000, Grant(StatusId.Sunder, 2))),
            Row("flail", "缠击", 2, Aim(SkillAim.OneTarget, new[] { B(10) }, 2, false, 0, 1000, Grant(StatusId.Weak, 1))),
            Row("torch", "点燃", 2, Aim(SkillAim.OneTarget, new[] { F(14) }, 1, false, 0, 1000, Grant(StatusId.Ignite, 2))),
            Row("poleaxe", "横扫突刺", 3, Aim(SkillAim.PrimaryThenEach, new[] { P(8) }, 1, false, 0, 1000, default, new[] { S(6) })),
            Row("flame_tongue", "焚心", 3, Aim(SkillAim.OneTarget, new[] { P(12), F(8) }, 1, false, 0, 1000, Grant(StatusId.Steadfast, 1, true))),
            Row("heaven_fire", "烙刑", 3, Aim(SkillAim.OneTarget, new[] { B(10), F(8) }, 1, false, 0, 1000, default, null, new[]
            {
                Grant(StatusId.Sunder, 1),
                Grant(StatusId.Ignite, 1)
            })),
        };
    }

    static WeaponSkillDef Row(string weaponId, string skillName, int cost, SkillCastSpec spec)
    {
        return new WeaponSkillDef
        {
            WeaponId = weaponId,
            SkillName = skillName,
            Cost = cost,
            Spec = spec
        };
    }

    static SkillCastSpec Aim(
        SkillAim aim,
        Share[] segment,
        int count,
        bool ignoreResistance,
        int extraBreak,
        int coefficientMilli,
        SkillStatusGrant oneStatus = default,
        Share[] follow = null,
        SkillStatusGrant[] statuses = null)
    {
        if (statuses == null && oneStatus.stacks > 0)
            statuses = new[] { oneStatus };
        if (statuses == null)
            statuses = Array.Empty<SkillStatusGrant>();
        if (follow == null)
            follow = Array.Empty<Share>();

        return new SkillCastSpec
        {
            Aim = aim,
            Segment = segment,
            SegmentCount = count,
            IgnoreResistance = ignoreResistance,
            ExtraBreakOnWeak = extraBreak,
            CoefficientMilli = coefficientMilli,
            FollowSegment = follow,
            Statuses = statuses
        };
    }

    static SkillStatusGrant Grant(StatusId id, int stacks, bool onSelf = false)
    {
        return new SkillStatusGrant { status = id, stacks = stacks, onSelf = onSelf };
    }

    static Share P(int amount) { return new Share(0, amount); }
    static Share S(int amount) { return new Share(1, amount); }
    static Share B(int amount) { return new Share(2, amount); }
    static Share F(int amount) { return new Share(3, amount); }
}
