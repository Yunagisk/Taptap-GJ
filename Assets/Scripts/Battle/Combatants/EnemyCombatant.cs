using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//一个敌人的行动协调者：保存招式配置，判断能否行动，选出招式并交给执行器，最后推进攻击循环。
public class EnemyCombatant : Combatant
{
    [Header("行动优先级")]
    [SerializeField] private int priority = 10;
    [SerializeField] private string displayName;

    [Header("攻击循环：按列表顺序执行，允许重复招式")]
    [SerializeField]
    private List<EnemyAttackData> attackDatas = new();

    [Header("小怪数值与随机行动；留空时使用旧循环")]
    [SerializeField] private SmallMonsterData definition;
    WeightedEnemyActionSelector weighted;

    private IEnemyActionSelector actionSelector; //负责选哪一招

    private readonly EnemyAttackAction attackAction = new EnemyAttackAction();//负责执行选中的招式

    private bool isActing;  //敌人是否正在行动中，避免重复调用。
    private bool remnantPaid;
    public int Priority => priority;
    public virtual EnemyAttackData UpcomingAction => weighted != null ? weighted.PeekAction()
        : (actionSelector as CycleActionSelector)?.PeekAction();
    public SmallMonsterData Definition => definition;
    public void ConfigureGuide(SmallMonsterData data) { if (data != null) definition = data; }
    public string DisplayName => definition != null ? definition.displayName
        : !string.IsNullOrWhiteSpace(displayName) ? displayName : name.Replace("(Clone)", "");
    public bool IsGuarding { get; private set; }
    public bool IsCharging => !(Break?.SkipPending ?? false)
        && (weighted != null ? weighted.IsCharged : (actionSelector as CycleActionSelector)?.IsCharged == true);

    [Header("罪骸")]
    [SerializeField] private bool isBoss;
    public int SinRemnantReward => isBoss ? WeaponTable.BossRemnant : WeaponTable.SmallMonsterRemnant;

    [Header("破防阈值")]
    [SerializeField] private int breakThreshold = 2;

    BreakGauge breakGauge;
    public BreakGauge Break => breakGauge;

    // 死亡才不能行动。破防跳过仍要推进招式循环。
    public virtual bool CanAct => !IsDead;

    public override void Initialize()
    {
        if (definition != null)
        {
            maxHp = definition.maxHp;
            priority = 12;
            isBoss = false;
            breakThreshold = definition.breakThreshold;
            attackDatas = new List<EnemyAttackData>(definition.actions);
            var resistance = GetComponent<DamageResistance>();
            if (resistance == null) resistance = gameObject.AddComponent<DamageResistance>();
            resistance.SetStageRules(definition.resistances);
        }
        IsGuarding = false;
        remnantPaid = false;
        base.Initialize();

        isActing = false;
        ResetBreak(breakThreshold);
        actionSelector = CreateActionSelector();
        actionSelector?.Reset();    //“如果 actionSelector 不为空，就调用 Reset()
        GetComponent<DamageResistance>()?.ResetDiscovery();
        BindSpeciesDiscovery();
        NotifyMarks();
    }

    void BindSpeciesDiscovery()
    {
        var resistance = GetComponent<DamageResistance>();
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (definition == null || resistance == null || state == null) return;
        if (state.monsterWeaknesses == null) state.monsterWeaknesses = new List<MonsterWeaknessDiscovery>();
        string id = definition.kind.ToString();
        var record = state.monsterWeaknesses.Find(entry => entry != null && entry.monsterId == id);
        if (record == null)
        {
            record = new MonsterWeaknessDiscovery { monsterId = id };
            state.monsterWeaknesses.Add(record);
        }
        if (record.observedTypes == null) record.observedTypes = new List<PlayerDamageType>();
        resistance.ShareDiscovery(record.observedTypes);
    }

    void ResetBreak(int threshold)
    {
        if (breakGauge != null)
            breakGauge.Changed -= HandleBreakChanged;
        breakGauge = new BreakGauge(threshold);
        breakGauge.Changed += HandleBreakChanged;
    }

    void HandleBreakChanged()
    {
        if (Break.SkipPending) weighted?.CancelCharge();
        NotifyMarks();
    }

    // Boss 以后可以继承 EnemyCombatant，
    // 重写此方法，返回自己的行动选择器。
    protected virtual IEnemyActionSelector CreateActionSelector()
    {
        weighted = definition != null ? new WeightedEnemyActionSelector(attackDatas,
            total => Random.Range(0, total)) : null;
        if (weighted != null) return weighted;
        return new CycleActionSelector(attackDatas);
    }

    public void PrepareIntent(PlayerCombatant player, EnemyIntentBudget budget)
    {
        if (weighted == null || IsDead || Break.SkipPending) return;
        var action = weighted.Prepare(candidate =>
        {
            if (!budget.Allows(candidate)) return 0;
            if (candidate.Kind == EnemyActionKind.Guard && weighted.LastAction != null
                && weighted.LastAction.Kind == EnemyActionKind.Guard) return 0;
            if (candidate.HasStatus)
            {
                if (weighted.ActionsTaken < weighted.StatusReadyAt || player == null) return 0;
                int stacks = player.Statuses.Stacks(candidate.StatusOnHit);
                if (candidate.StatusOnHit == StatusId.Poison ? stacks >= 2 : stacks > 0) return 0;
            }
            return candidate.Kind == EnemyActionKind.Guard && CurrentHP * 10 < MaxHP * 3
                && definition.lowHealthGuardWeight > 0 ? definition.lowHealthGuardWeight : candidate.Weight;
        });
        budget.Reserve(action);
        NotifyMarks();
    }

    public void StartGuard() { IsGuarding = true; NotifyMarks(); }

    public override int TakeDamage(int damage)
    {
        int actualDamage = base.TakeDamage(IsGuarding ? Mathf.Max(0, damage) / 2 : damage);
        if (actualDamage > 0 && !IsDead) GetComponent<EnemySpriteAnimation>()?.PlayHurt();
        return actualDamage;
    }

    protected override void Die()
    {
        GetComponent<EnemySpriteAnimation>()?.PlayDefeat();
        if (!remnantPaid)
        {
            remnantPaid = true;
            if (GameRoot.I != null) ShopRules.GrantRemnant(GameRoot.I.state.world, SinRemnantReward);
        }
        base.Die();
    }

    public IEnumerator TakeTurn(PlayerCombatant player)
    {
        if (isActing || !CanAct)
            yield break;

        if (player == null || player.IsDead)
            yield break;

        if (actionSelector == null)
        {
            Debug.LogWarning($"{name} 没有初始化行动选择器");
            yield break;
        }

        IsGuarding = false;
        NotifyMarks();

        if (Break != null && Break.ConsumeSkip())
        {
            EnemyAttackData skipped = actionSelector.SelectAction();
            string skippedName = skipped != null ? skipped.AttackName : "行动";
            Debug.Log($"{name} 破防，跳过 {skippedName}");
            if (weighted != null) weighted.SkipAction();
            else if (actionSelector is CycleActionSelector cycle) cycle.SkipAction();
            else actionSelector.OnActionCompleted();
            SettleOwnTurn(player);
            yield break;
        }

        //第二步，选招并锁定行动状态：
        EnemyAttackData selectedAction =actionSelector.SelectAction();

        if (selectedAction == null)
        {
            Debug.LogWarning($"{name} 没有可用招式，跳过本次行动");
            SettleOwnTurn(player);
            yield break;
        }

        isActing = true;

        try
        {

            //第三步，等待整招执行完：
            yield return attackAction.Execute(this,player,selectedAction);

            // 一整次招式结束后才推进，连击不会重复推进。
            if (this != null && !IsDead)
            {
                //第四步，推进循环并解除行动状态：
                actionSelector.OnActionCompleted();
            }
        }
        //敌人仍然存在且存活，就推进到下一招。
        finally
        {
            isActing = false;
        }

        SettleOwnTurn(player);
    }

    void SettleOwnTurn(PlayerCombatant player)
    {
        if (this == null || IsDead || player == null || player.IsDead)
            return;

        TurnEndResult end = StatusSettlement.SettleOwnTurn(new CombatantStatusHost(this), BattleRound);
        if (end.IgniteBroke)
            Debug.Log($"{name} 点燃造成破防，下一次行动跳过");
    }

}
