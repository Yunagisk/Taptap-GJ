using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class BattleFlowController : MonoBehaviour {
    [Header("基础单位")]
    [SerializeField] private PlayerCombatant player;
    [SerializeField] private EnemyCombatant enemy;

    [Header("战斗设置")]
    [SerializeField] private float enemyActionDelay = 1.0f;

    private BattleState currentState;
    public BattleState CurrentState => currentState;

    private int round = 0;

    public event Action<BattleState> OnBattleStateChanged;

    public void Start()
    {
        StartBattle();
    }

    private void StartBattle()
    {
        Debug.Log("战斗开始");

        player.Initialize();
        enemy.Initialize();

        StartNewRound();
    }

    private void StartNewRound()
    {
        round++;

        Debug.Log($"====第{round}轮战斗====");

        StartPlayerTurn();

    }

    private void SetState(BattleState newState)
    {
        currentState = newState;

        OnBattleStateChanged?.Invoke(newState);
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
        if(currentState != BattleState.PlayerTurn)
        {
            return;
        }

        Debug.Log("玩家攻击");

        enemy.TakeDamage(player.Attack);

        if (enemy.IsDead)
        {
            Victory();
            return;
        }

        StartCoroutine(EnemyTurn());
    }
    private IEnumerator EnemyTurn()
    {
        SetState(BattleState.EnemyTurn);

        Debug.Log("敌人回合");

        yield return new WaitForSeconds(enemyActionDelay);

        //等待执行动画

        Debug.Log("敌人攻击");

        player.TakeDamage(enemy.Attack);

        if (player.IsDead)
        {
            Defeat();
            yield break;
        }

        StartNewRound();
    }
    private void Victory()
    {
        Debug.Log("战斗胜利");
        SetState(BattleState.Victory);
    }
    private void Defeat()
    {
        Debug.Log("战斗失败");
        SetState(BattleState.Defeat);
    }
}

