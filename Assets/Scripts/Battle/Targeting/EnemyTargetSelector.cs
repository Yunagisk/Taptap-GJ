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
    bool externalIndicator;
    bool externalClickHandling;

    public void Bind(BattleFlowController flow, EnemyCombatant unit)
    {
        if (battleFlow != null) battleFlow.OnTargetSelectionChanged -= HandleTargetSelectionChanged;
        battleFlow = flow; enemy = unit;
        if (isActiveAndEnabled && battleFlow != null)
            battleFlow.OnTargetSelectionChanged += HandleTargetSelectionChanged;
    }

    public void UseExternalIndicator(bool handlesClicks = false)
    {
        externalIndicator = true;
        externalClickHandling = handlesClicks;
        if (targetIndicator != null) targetIndicator.SetActive(false);
    }


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
            targetIndicator.SetActive(isCanSelected && !externalIndicator);
        }
    }

    private void OnMouseDown()
    {
        if (externalClickHandling) return;
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (!isCanSelected)
        {
            return;
        }
        if (battleFlow != null && enemy != null) battleFlow.AttackTarget(enemy);
    }
}
