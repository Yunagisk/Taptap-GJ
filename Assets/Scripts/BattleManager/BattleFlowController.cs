using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using TMPro;

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

    private int round = 0;

    public event Action<BattleState> OnBattleStateChanged;

    public event Action<bool> OnTargetSelectionChanged;

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

        player.Initialize();


        foreach (EnemyCombatant enemy in enemies)
        {
            enemy.Initialize();
        }

        StartNewRound();
    }

    private void StartNewRound()
    {
        round++;

        roundText.text = $"回合：{round}";

        Debug.Log($"====第{round}轮战斗====");

        StartPlayerTurn();

    }

    private void SetState(BattleState newState)
    {
        currentState = newState;

        OnBattleStateChanged?.Invoke(newState);//如果有人订阅了 OnBattleStateChanged 这个事件，就把 newState 传给所有订阅者
    }

    private void StartPlayerTurn()
    {
        SetState(BattleState.PlayerTurn);

        Debug.Log("玩家开始战斗");

        //玩家开始操作
        //......
    }
    public void PlayerAttack()
    {
        if (currentState != BattleState.PlayerTurn)
        {
            return;
        }

        Debug.Log("请选择目标攻击");

        currentAction = new PlayerAttackAction();

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

        StartCoroutine(AttackTargetCoroutine(target));
    }
    private IEnumerator AttackTargetCoroutine(EnemyCombatant target)
    {

        Debug.Log($"玩家选择目标{target.gameObject.name}");
        OnTargetSelectionChanged?.Invoke(false);
        //Debug.Log("执行攻击动画");
        Debug.Log($"玩家攻击{target.gameObject.name}");

        yield return currentAction.Execute(player, target); //等待多段攻击全部完成
        currentAction = null;

        if (CheckAllEnemiesIsDead())
        {
            Victory();
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
        player.EndDefense();
        StartNewRound();
    }
    private void Victory()
    {
        OnTargetSelectionChanged?.Invoke(false);
        Debug.Log("战斗胜利");
        SetState(BattleState.Victory);
    }
    private void Defeat()
    {
        OnTargetSelectionChanged?.Invoke(false);
        Debug.Log("战斗失败");
        SetState(BattleState.Defeat);
    }

    public void PlayerDefend()
    {
        if(currentState != BattleState.PlayerTurn)
        {
            return;
        }

        Debug.Log("玩家选择防御");
        currentAction = new DefendAction();

        StartCoroutine(ExecutePlayerAction(player));
    }

    private IEnumerator ExecutePlayerAction(Combatant user)  //起到判定作用 是否需要选择目标
    {
        SetState(BattleState.ExecutingAction);
        yield return currentAction.Execute(user, user);
        currentAction = null;
        yield return EnemyTurn();
    }
}

