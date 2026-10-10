using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleActionUI : MonoBehaviour
{
    [Header("基础按钮")]
    [SerializeField] private Button attackButton;
    [SerializeField] private Button defendButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Button itemButton0;
    [SerializeField] private Button itemButton1;
    [SerializeField] private Button itemButton2;

    [Header("战斗流程")]
    [SerializeField] private BattleFlowController battleFlow;

 
    private void OnEnable()
    {
        if(battleFlow != null)
        {
            battleFlow.OnBattleStateChanged +=HandleBattleStateChanged;
        }   
    }
    private void Start()
    {
        if(battleFlow != null)
        {
            HandleBattleStateChanged(battleFlow.CurrentState);
        }
    }
    private void OnDisable()
    {
        if(battleFlow != null)
        {
            battleFlow.OnBattleStateChanged -=HandleBattleStateChanged;
        }
    }
    private void HandleBattleStateChanged(BattleState state)
    {
        bool canUseAction = state == BattleState.PlayerTurn;

        attackButton.interactable = canUseAction;
        defendButton.interactable = canUseAction;
        skillButton.interactable = canUseAction;
        itemButton0.interactable = canUseAction;
        itemButton1.interactable = canUseAction;
        itemButton2.interactable = canUseAction;
    }
}
