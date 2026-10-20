using System;

public static class StatusRules
{
    public const int DefaultMaxResolve = 3;
    public const int DefaultCap = 3;
    public const int ConfuseCap = 2;

    public static StatusClock ClockOf(StatusId id)
    {
        switch (id)
        {
            case StatusId.Poison:
            case StatusId.Regen:
                return StatusClock.IntensityOnOwnTurnEnd;
            case StatusId.Haste:
            case StatusId.Weak:
            case StatusId.Sunder:
            case StatusId.Exhaust:
            case StatusId.Confuse:
            case StatusId.Ignite:
                return StatusClock.DurationOnRoundEnd;
            default:
                return StatusClock.TriggerCount;
        }
    }

    public static int CapOf(StatusId id)
    {
        return id == StatusId.Confuse ? ConfuseCap : DefaultCap;
    }

    public static bool IsDebuff(StatusId id)
    {
        switch (id)
        {
            case StatusId.Poison:
            case StatusId.Weak:
            case StatusId.Sunder:
            case StatusId.Exhaust:
            case StatusId.Confuse:
            case StatusId.Ignite:
                return true;
            default:
                return false;
        }
    }

    public static string DisplayName(StatusId id)
    {
        switch (id)
        {
            case StatusId.Regen: return "再生";
            case StatusId.Haste: return "加速";
            case StatusId.Steadfast: return "坚定";
            case StatusId.Poison: return "中毒";
            case StatusId.Weak: return "衰弱";
            case StatusId.Sunder: return "破甲";
            case StatusId.Exhaust: return "疲惫";
            case StatusId.Confuse: return "混乱";
            case StatusId.Ignite: return "点燃";
            default: return id.ToString();
        }
    }

    // 衰弱和破甲叠层只延长时间。比例固定，先乘 4/5，再乘 6/5，四舍五入。
    public static int ScaleDirectDamage(int amount, bool attackerWeak, bool targetSunder)
    {
        if (amount <= 0)
            return 0;

        int numerator = 1;
        int denominator = 1;
        if (attackerWeak)
        {
            numerator *= 4;
            denominator *= 5;
        }
        if (targetSunder)
        {
            numerator *= 6;
            denominator *= 5;
        }

        return (amount * numerator + denominator / 2) / denominator;
    }

    public static int ExtraBasicSegments(StatusSet statuses)
    {
        if (statuses == null || !statuses.Has(StatusId.Haste))
            return 0;
        return 1;
    }

    // 先按最大生命 5% 向下取整，再处理火焰抗性或免疫。不受衰弱、破甲、防御影响。
    public static int IgniteLoss(int maxHp, TypeRelation fire)
    {
        if (maxHp <= 0)
            return 0;

        int raw = maxHp * 5 / 100;
        if (fire == TypeRelation.Immune)
            return 0;
        if (fire == TypeRelation.Resistant)
            return raw / 2;
        return raw;
    }

    // 一个混合段最多 +1。整段免疫不加。层数不参加计算。
    public static int BreakFromSegment(SegmentPart[] parts)
    {
        if (parts == null || parts.Length == 0)
            return 0;

        bool anyWeak = false;
        bool anyLive = false;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].BaseDamage <= 0)
                continue;
            if (parts[i].Relation != TypeRelation.Immune)
                anyLive = true;
            if (parts[i].Relation == TypeRelation.Weak)
                anyWeak = true;
        }

        if (!anyWeak || !anyLive)
            return 0;
        return 1;
    }

    // 基础决心先入池。额外的 1 点也能放下才消耗坚定。疲惫把收益直接变成 0。
    public static ResolveGrant GrantResolve(int current, int max, int baseAmount, bool exhausted, int steadfastStacks)
    {
        ResolveGrant result = new ResolveGrant();
        if (baseAmount <= 0 || exhausted || current >= max)
            return result;

        int room = max - current;
        int granted = baseAmount < room ? baseAmount : room;
        if (steadfastStacks > 0 && granted == baseAmount && room >= baseAmount + 1)
        {
            granted += 1;
            result.ConsumeSteadfast = true;
        }

        result.Granted = granted;
        return result;
    }
}

public struct SegmentPart
{
    public TypeRelation Relation;
    public int BaseDamage;

    public SegmentPart(TypeRelation relation, int baseDamage)
    {
        Relation = relation;
        BaseDamage = baseDamage;
    }
}

public struct ResolveGrant
{
    public int Granted;
    public bool ConsumeSteadfast;
}
