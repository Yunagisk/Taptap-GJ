// 自身回合末按中毒 → 点燃 → 再生结算。任何一步致死就停，后面的再生不能把人救回来。
public interface IStatusBody
{
    bool IsDead { get; }
    int MaxHp { get; }
    StatusSet Statuses { get; }
    BreakGauge Break { get; }
    TypeRelation FireRelation { get; }
    int LoseRaw(int amount);
    int HealRaw(int amount);
}

public struct TurnEndResult
{
    public bool IgniteBroke;
}

public static class StatusSettlement
{
    public static TurnEndResult SettleOwnTurn(IStatusBody body, int round)
    {
        TurnEndResult result = new TurnEndResult();
        if (body == null || body.IsDead)
            return result;

        SettlePoison(body);
        if (body.IsDead)
            return result;

        result.IgniteBroke = SettleIgnite(body, round);
        if (body.IsDead)
            return result;

        SettleRegen(body);
        return result;
    }

    static void SettlePoison(IStatusBody body)
    {
        int stacks = body.Statuses.Stacks(StatusId.Poison);
        if (stacks <= 0)
            return;

        body.LoseRaw(stacks);
        body.Statuses.Reduce(StatusId.Poison, 1);
    }

    static bool SettleIgnite(IStatusBody body, int round)
    {
        int stacks = body.Statuses.Stacks(StatusId.Ignite);
        if (stacks <= 0)
            return false;

        int loss = StatusRules.IgniteLoss(body.MaxHp, body.FireRelation);
        if (loss > 0)
            body.LoseRaw(loss);
        if (body.IsDead)
            return false;

        // 点燃层数不增加破防量，一次有效点燃最多 +1。这里不减持续层。
        if (loss <= 0 || body.FireRelation != TypeRelation.Weak || body.Break == null)
            return false;
        return body.Break.TryAdd(1, round);
    }

    static void SettleRegen(IStatusBody body)
    {
        int stacks = body.Statuses.Stacks(StatusId.Regen);
        if (stacks <= 0)
            return;

        body.HealRaw(stacks);
        body.Statuses.Reduce(StatusId.Regen, 1);
    }
}
