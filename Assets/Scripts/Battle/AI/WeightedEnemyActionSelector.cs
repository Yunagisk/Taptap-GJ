using System;
using System.Collections.Generic;

// Prepare once before the player acts. Reading an intent never consumes RNG.
public sealed class WeightedEnemyActionSelector : IEnemyActionSelector
{
    readonly IReadOnlyList<EnemyAttackData> actions;
    readonly Func<int, int> draw;
    EnemyAttackData locked, followUp;
    public EnemyAttackData LastAction { get; private set; }
    public int ActionsTaken { get; private set; }
    public int StatusReadyAt { get; private set; }
    public bool IsCharged => followUp != null;

    public WeightedEnemyActionSelector(IReadOnlyList<EnemyAttackData> actions, Func<int, int> draw)
    { this.actions = actions; this.draw = draw ?? throw new ArgumentNullException(nameof(draw)); }

    public void Reset()
    { locked = followUp = LastAction = null; ActionsTaken = StatusReadyAt = 0; }

    public EnemyAttackData Prepare(Func<EnemyAttackData, int> weight)
    {
        if (locked != null) return locked;
        if (followUp != null) return locked = followUp;
        if (actions == null) return null;
        var weights = new int[actions.Count];
        int total = 0;
        for (int i = 0; i < actions.Count; i++)
        { weights[i] = actions[i] != null ? Math.Max(0, weight(actions[i])) : 0; total += weights[i]; }
        if (total <= 0) return null;
        int roll = draw(total);
        if (roll < 0 || roll >= total) throw new InvalidOperationException("Random draw is outside its weight range.");
        for (int i = 0; i < actions.Count; i++)
        { roll -= weights[i]; if (roll < 0) return locked = actions[i]; }
        return null;
    }

    public EnemyAttackData PeekAction() => locked;
    public EnemyAttackData SelectAction() => locked;

    public void OnActionCompleted()
    {
        if (locked == null) return;
        ActionsTaken++;
        LastAction = locked;
        followUp = locked.Kind == EnemyActionKind.Charge ? locked.FollowUp : null;
        if (locked.HasStatus) StatusReadyAt = ActionsTaken + 2;
        locked = null;
    }

    public void CancelCharge() { followUp = null; }
    public void SkipAction()
    { ActionsTaken++; locked = followUp = LastAction = null; }
}

// Shared only while preparing a round, so ordering cannot allow duplicate casts.
public sealed class EnemyIntentBudget
{
    readonly HashSet<StatusId> reservedStatuses = new HashSet<StatusId>();
    bool chargeReserved;
    public bool Allows(EnemyAttackData action) =>
        (action.Kind != EnemyActionKind.Charge || !chargeReserved)
        && (!action.HasStatus || !reservedStatuses.Contains(action.StatusOnHit));
    public void Reserve(EnemyAttackData action)
    {
        if (action == null) return;
        if (action.Kind == EnemyActionKind.Charge) chargeReserved = true;
        if (action.HasStatus) reservedStatuses.Add(action.StatusOnHit);
    }
}
