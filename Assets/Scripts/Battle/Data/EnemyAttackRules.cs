// 小怪一招一份数据。喘息和准备占一次行动，不进伤害结算，也不挂状态。
// 直接攻击才走 DamageSystem 的基础结算，状态在整招打完后施加一次。
public enum EnemyMoveKind
{
    Strike = 0,
    Prepare = 1,
    Gasp = 2
}

public static class EnemyAttackRules
{
    public static bool UsesBasicDamage(EnemyMoveKind kind)
    {
        return kind == EnemyMoveKind.Strike;
    }

    public static bool AppliesRider(EnemyMoveKind kind, bool applyStatus, int stacks)
    {
        return kind == EnemyMoveKind.Strike && applyStatus && stacks > 0;
    }
}
