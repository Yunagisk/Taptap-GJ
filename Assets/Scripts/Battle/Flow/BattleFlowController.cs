using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class BattleFlowController : MonoBehaviour
{
    [Header("基础单位")]
    [SerializeField] private PlayerCombatant player;
    [SerializeField] private List<EnemyCombatant> enemies;

    [Header("战斗设置")]
    [SerializeField] private float enemyActionDelay = 1.0f;
    [SerializeField] private TMP_Text roundText;

    

    private TurnManager turnManager;

    private IBattleAction currentAction;

    private BattleState currentState;

    public BattleState CurrentState => currentState;
    public PlayerCombatant Player => player;
    public IReadOnlyList<EnemyCombatant> Enemies => enemies;
    public Combatant ActiveCombatant { get; private set; }
    public int RoundNumber => round;
    public GameState ItemState => GameRoot.I != null ? GameRoot.I.state : null;
    public string ActionMessage { get; private set; }
    public bool IsSelectingLantern => currentAction is ItemAction item && item.ItemId == ItemTable.Lantern;
    public GuideBattleController Guide { get; private set; }
    public bool GuideAllowsAction(string action) => Guide == null || Guide.Allows(action);

    private int round = 0;

    public event Action<BattleState> OnBattleStateChanged;

    public event Action<bool> OnTargetSelectionChanged;

    public bool WeaponSkillBlocked =>
        player != null && player.Statuses.Has(StatusId.Exhaust);

    public bool IsSelectingSkill => currentState == BattleState.SelectingTarget && currentAction is SkillAction;
    // Spawned encounters supply their participants before this component's first Start.
    public void ConfigureParticipants(PlayerCombatant participant, IEnumerable<EnemyCombatant> roster)
    {
        if (round != 0) throw new InvalidOperationException("Configure participants before the battle starts.");
        player = participant;
        enemies = new List<EnemyCombatant>(roster);
    }
    public bool CanUseWeaponSkill => SkillUnavailableReason == null;
    public string SkillUnavailableReason
    {
        get
        {
            if (!GuideAllowsAction("skill")) return Guide.Instruction;
            if (currentState != BattleState.PlayerTurn || player == null || player.IsDead) return "当前不能行动";
            var skill = player.CurrentWeapon != null ? player.CurrentWeapon.Skill : null;
            if (skill == null || !skill.IsConfigured) return "未配置技能";
            if (WeaponSkillBlocked) return "疲惫：技能不可用";
            if (player.CurrentDetermination < skill.DeterminationCost) return "决心不足";
            if (enemies == null || !enemies.Exists(enemy => enemy != null && !enemy.IsDead)) return "没有存活目标";
            return null;
        }
    }

    public void PlayerSkill()
    {
        if (!CanUseWeaponSkill) return;
        ActionMessage = null;
        currentAction = new SkillAction(enemies);
        if (player.CurrentWeapon.Skill.RequiresPrimaryTarget)
        {
            SetState(BattleState.SelectingTarget);
            OnTargetSelectionChanged?.Invoke(true);
        }
        else StartCoroutine(AttackTargetCoroutine(null));
    }

    private void Awake()
    {
        turnManager = new TurnManager();
    }

    public void Start()
    {
        StartBattle();
    }

    private void StartBattle()
    {
        Debug.Log("战斗开始");

        if (GameRoot.I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        GameRoot.I.activeBattle = this;

        player.Initialize();


        foreach (EnemyCombatant enemy in enemies)
        {
            enemy.Initialize();
        }

        Guide = GuideBattleController.Attach(this);

        StartNewRound();
    }

    private void StartNewRound()
    {
        round++;
        StampRound();

        // Lock actual random intents before the player makes a decision.
        var planningOrder = new List<EnemyCombatant>(enemies);
        for (int i = planningOrder.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var swap = planningOrder[i]; planningOrder[i] = planningOrder[j]; planningOrder[j] = swap;
        }
        var budget = new EnemyIntentBudget();
        foreach (var enemy in planningOrder) if (enemy != null) enemy.PrepareIntent(player, budget);

        roundText.text = $"回合：{round}";

        Debug.Log($"====第{round}轮战斗====");

        StartPlayerTurn();

    }

    void StampRound()
    {
        if (player != null)
            player.BattleRound = round;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
                enemies[i].BattleRound = round;
        }
    }

    private void SetState(BattleState newState)
    {
        currentState = newState;

        OnBattleStateChanged?.Invoke(newState);//如果有人订阅了 OnBattleStateChanged 这个事件，就把 newState 传给所有订阅者
    }

    private void StartPlayerTurn()
    {
        ActiveCombatant = player;
        SetState(BattleState.PlayerTurn);
        Guide?.ReadyForPlayer();

        Debug.Log("玩家开始战斗");

        //玩家开始操作
        //......
    }
    public void PlayerAttack()
    {
        if (!GuideAllowsAction("attack") || currentState != BattleState.PlayerTurn || player == null
            || player.IsDead || player.CurrentWeapon == null)
        {
            return;
        }

        Debug.Log("请选择目标攻击");

        currentAction = new PlayerAttackAction();
        ActionMessage = null;

        if (player.Statuses.Has(StatusId.Confuse))
        {
            EnemyCombatant rolled = PickLivingEnemy();
            if (rolled == null)
            {
                currentAction = null;
                return;
            }
            Debug.Log($"混乱：普攻改为 {rolled.name}");
            SetState(BattleState.ExecutingAction);
            OnTargetSelectionChanged?.Invoke(false);
            StartCoroutine(AttackTargetCoroutine(rolled));
            return;
        }

        SetState(BattleState.SelectingTarget);

        OnTargetSelectionChanged?.Invoke(true);  //true 通知敌人现在可以被选择为攻击目标

    }

    public void AttackTarget(EnemyCombatant target)
    {
        if (currentState != BattleState.SelectingTarget)
        {
            return;
        }

        if (target == null || target.IsDead)
        {
            return;
        }

        if (!enemies.Contains(target)) return;
        if (currentAction is ItemAction item)
        {
            var check = ItemAction.Validate(ItemState, item.ItemId, player, target);
            if (!check.ok) { ActionMessage = check.message; return; }
        }

        StartCoroutine(AttackTargetCoroutine(target));
    }
    public void CancelTargetSelection()
    {
        if (currentState != BattleState.SelectingTarget) return;
        currentAction = null;
        OnTargetSelectionChanged?.Invoke(false);
        SetState(BattleState.PlayerTurn);
        ActionMessage = null;
    }
    private IEnumerator AttackTargetCoroutine(EnemyCombatant target)
    {
        if (currentAction == null || target != null && target.IsDead
            || target == null && (currentAction is not SkillAction
                || player.CurrentWeapon == null || player.CurrentWeapon.Skill == null
                || player.CurrentWeapon.Skill.RequiresPrimaryTarget))
            yield break;
        // Lock target selection and weapon switching for the complete animation.
        if (currentState != BattleState.ExecutingAction)
            SetState(BattleState.ExecutingAction);
        Debug.Log(target != null ? $"玩家选择目标{target.gameObject.name}" : "玩家施放全体技能");
        OnTargetSelectionChanged?.Invoke(false);
        //Debug.Log("执行攻击动画");
        if (target != null) Debug.Log($"玩家攻击{target.gameObject.name}");

        yield return currentAction.Execute(player, target); //等待多段攻击全部完成
        if (!CompleteItemAction()) yield break;
        yield return FinishPlayerAction();
    }

    public ItemResult CheckItem(string id, bool selectingTarget = false)
    {
        id = ItemTable.CanonicalId(id);
        if (!GuideAllowsAction(id)) return new ItemResult(false, Guide.Instruction);
        if (currentState != BattleState.PlayerTurn || player == null || player.IsDead)
            return new ItemResult(false, "当前无法使用道具。");
        if (id == ItemTable.Lantern && selectingTarget)
        {
            bool possible = false;
            foreach (var enemy in enemies)
                if (enemy != null && !enemy.IsDead
                    && ItemAction.Validate(ItemState, id, player, enemy).ok) { possible = true; break; }
            if (!possible) return new ItemResult(false, "没有可揭露弱点的存活敌人，或提灯次数不足。");
        }
        return ItemAction.Validate(ItemState, id, player, selectingTarget: selectingTarget);
    }

    public ItemResult PlayerUseItem(string id)
    {
        id = ItemTable.CanonicalId(id);
        var check = CheckItem(id, id == ItemTable.Lantern);
        ActionMessage = check.message;
        if (!check.ok) return check;
        currentAction = new ItemAction(ItemState, id);
        if (id == ItemTable.Lantern)
        {
            ActionMessage = "选择敌人使用提灯 · Esc 取消";
            SetState(BattleState.SelectingTarget);
            OnTargetSelectionChanged?.Invoke(true);
        }
        else StartCoroutine(ExecutePlayerAction(player));
        return check;
    }

    bool CompleteItemAction()
    {
        if (!(currentAction is ItemAction item)) return true;
        ActionMessage = item.Result.message;
        if (item.Result.ok) return true;
        currentAction = null;
        SetState(BattleState.PlayerTurn);
        return false;
    }

    void OnDestroy()
    {
        if (GameRoot.I != null && GameRoot.I.activeBattle == this) GameRoot.I.activeBattle = null;
    }

    EnemyCombatant PickLivingEnemy()
    {
        List<EnemyCombatant> living = new List<EnemyCombatant>();
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && !enemies[i].IsDead)
                living.Add(enemies[i]);
        }
        if (living.Count == 0)
            return null;
        return living[UnityEngine.Random.Range(0, living.Count)];
    }

    IEnumerator FinishPlayerAction()
    {
        string action = currentAction is ItemAction item ? item.ItemId : currentAction is DefendAction ? "defend"
            : currentAction is SkillAction ? "skill" : "attack";
        Guide?.ActionCompleted(action);
        currentAction = null;
        if (player == null || player.IsDead)
        {
            Defeat();
            yield break;
        }

        if (CheckAllEnemiesIsDead())
        {
            Victory();
            yield break;
        }

        TurnEndResult end = StatusSettlement.SettleOwnTurn(new CombatantStatusHost(player), round);
        if (end.IgniteBroke)
            Debug.Log($"{player.name} 点燃造成破防");

        if (player == null || player.IsDead)
        {
            Defeat();
            yield break;
        }

        yield return EnemyTurn();
    }

    private bool CheckAllEnemiesIsDead()
    {
        foreach (EnemyCombatant enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                return false;
            }
        }
        return true;
    }
    private IEnumerator EnemyTurn()
    {
        SetState(BattleState.EnemyTurn);

        Debug.Log("敌人回合");

        List<EnemyCombatant> turnOrder = turnManager.GetEnemyTurnOrder(enemies);

        foreach (EnemyCombatant enemy in turnOrder)
        {
            if (player == null || player.IsDead)
            {
                Defeat();
                yield break;
            }

            if (enemy == null || enemy.IsDead)
                continue;

            ActiveCombatant = enemy;
            yield return new WaitForSeconds(enemyActionDelay);

            // 等待期间单位状态可能发生变化，再检查一次。
            if (player == null || player.IsDead)
            {
                Defeat();
                yield break;
            }

            if (enemy == null || enemy.IsDead)
                continue;

            // 等待整个招式执行完毕，再轮到下一名敌人。
            yield return enemy.TakeTurn(player);

            if (player == null || player.IsDead)
            {
                Defeat();
                yield break;
            }
        }

        if (CheckAllEnemiesIsDead())
        {
            Victory();
            yield break;
        }
        TickDurationStatuses();
        player.EndDefense();
        StartNewRound();
    }

    void TickDurationStatuses()
    {
        if (player != null && !player.IsDead)
            player.Statuses.TickDuration(round);
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && !enemies[i].IsDead)
                enemies[i].Statuses.TickDuration(round);
        }
    }

    void ClearBattleStatuses()
    {
        if (player != null)
        {
            player.Statuses.ClearOrdinary();
            player.EndDefense();
        }
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
                enemies[i].Statuses.ClearOrdinary();
        }
    }
    private void Victory()
    {
        if (currentState == BattleState.Victory || currentState == BattleState.Defeat)
        {
            return;
        }
        OnTargetSelectionChanged?.Invoke(false);
        currentAction = null;

        Debug.Log("战斗胜利");
        ActiveCombatant = null;
        ClearBattleStatuses();
        SetState(BattleState.Victory);
    }
    private void Defeat()
    {
        if (currentState == BattleState.Victory || currentState == BattleState.Defeat)
        {
            return;
        }
        OnTargetSelectionChanged?.Invoke(false);
        currentAction = null;

        Debug.Log("战斗失败");
        ActiveCombatant = null;
        ClearBattleStatuses();
        SetState(BattleState.Defeat);
    }

    public void PlayerDefend()
    {
        if(!GuideAllowsAction("defend") || currentState != BattleState.PlayerTurn)
        {
            return;
        }

        Debug.Log("玩家选择防御");
        ActionMessage = null;
        currentAction = new DefendAction();

        StartCoroutine(ExecutePlayerAction(player));
    }

    private IEnumerator ExecutePlayerAction(Combatant user)  //起到判定作用 是否需要选择目标
    {
        SetState(BattleState.ExecutingAction);
        yield return currentAction.Execute(user, user);
        if (!CompleteItemAction()) yield break;
        yield return FinishPlayerAction();
    }
}

