using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyTargetSelector : MonoBehaviour
{
    [Header("基础配置")]
    [SerializeField] private BattleFlowController battleFlow;
    [SerializeField] private EnemyCombatant enemy;

    [Header("选择目标时的高亮效果")]
    [SerializeField] private GameObject targetIndicator;

    private bool isCanSelected = true;


    private void OnEnable()
    {
        if (battleFlow != null)
        {
            battleFlow.OnTargetSelectionChanged += HandleTargetSelectionChanged;
        }
    }

    private void Start()
    {
        if (targetIndicator != null)
        {
            targetIndicator.SetActive(false);
        }
    }
    private void OnDisable()
    {
        if (battleFlow != null)
        {
            battleFlow.OnTargetSelectionChanged -= HandleTargetSelectionChanged;
        }
    }

    private void HandleTargetSelectionChanged(bool isSelected)
    {
        isCanSelected = isSelected && !enemy.IsDead;

        if (targetIndicator != null)
        {
            targetIndicator.SetActive(isCanSelected);
        }
    }

    private void OnMouseDown()
    {
        if (!isCanSelected)
        {
            return;
        }
        battleFlow.AttackTarget(enemy);
    }
}
