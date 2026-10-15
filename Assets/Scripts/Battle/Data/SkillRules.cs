using System;

// 武器技能的结算规则。战斗流程还没调用这里。
// 伤害只算到防御之前：抗性向下取整后合计，再乘武器系数，再吃衰弱和破甲。
// 免疫不受“无视抗性”影响。状态在全部段打完后另算，不按段叠加。
public enum SkillAim
{
    OneTarget = 0,
    EachLiving = 1,
    PrimaryThenEach = 2
}

public struct Share
{
    public int Type;
    public int Amount;

    public Share(int type, int amount)
    {
        Type = type;
        Amount = amount;
    }
}

[Serializable]
public struct SkillStatusGrant
{
    public StatusId status;
    public int stacks;
    public bool onSelf;
}

public sealed class SkillCastSpec
{
    public SkillAim Aim;
    public Share[] Segment = Array.Empty<Share>();
    public int SegmentCount = 1;
    public bool IgnoreResistance;
    public int ExtraBreakOnWeak;
    public int CoefficientMilli = 1000;
    public Share[] FollowSegment = Array.Empty<Share>();
    public SkillStatusGrant[] Statuses = Array.Empty<SkillStatusGrant>();
}

public static class SkillRules
{
    public static bool CanPay(int current, int cost, bool exhausted)
    {
        if (exhausted || cost <= 0)
            return false;
        return current >= cost;
    }

    public static int SegmentDamage(
        Share[] shares,
        TypeRelation[] relationsByType,
        bool ignoreResistance,
        int coefficientMilli,
        bool attackerWeak,
        bool targetSunder)
    {
        int sum = TypeSum(shares, relationsByType, ignoreResistance);
        if (sum <= 0)
            return 0;
        sum = ApplyCoefficient(sum, coefficientMilli);
        return StatusRules.ScaleDirectDamage(sum, attackerWeak, targetSunder);
    }

    // 普通弱点命中 +1。额外破防只在这次已经打中弱点时加上。保护期两者都是 0。
    public static int SegmentBreak(
        Share[] shares,
        TypeRelation[] relationsByType,
        int extraOnWeak,
        bool protectedNow)
    {
        if (protectedNow || shares == null || shares.Length == 0)
            return 0;

        SegmentPart[] parts = new SegmentPart[shares.Length];
        for (int i = 0; i < shares.Length; i++)
        {
            parts[i] = new SegmentPart(RelationOf(relationsByType, shares[i].Type), shares[i].Amount);
        }

        int gain = StatusRules.BreakFromSegment(parts);
        if (gain > 0 && extraOnWeak > 0)
            gain += extraOnWeak;
        return gain;
    }

    public static int ApplyCoefficient(int amount, int milli)
    {
        if (amount <= 0)
            return 0;
        if (milli == 1000)
            return amount;
        if (milli < 0)
            milli = 0;
        return (amount * milli + 500) / 1000;
    }

    static int TypeSum(Share[] shares, TypeRelation[] relationsByType, bool ignoreResistance)
    {
        if (shares == null)
            return 0;

        int sum = 0;
        for (int i = 0; i < shares.Length; i++)
        {
            int amount = shares[i].Amount;
            if (amount <= 0)
                continue;

            TypeRelation relation = RelationOf(relationsByType, shares[i].Type);
            if (relation == TypeRelation.Immune)
                continue;
            if (relation == TypeRelation.Resistant && !ignoreResistance)
                amount = amount / 2;
            sum += amount;
        }
        return sum;
    }

    static TypeRelation RelationOf(TypeRelation[] relationsByType, int type)
    {
        if (relationsByType == null || type < 0 || type >= relationsByType.Length)
            return TypeRelation.Normal;
        return relationsByType[type];
    }
}
