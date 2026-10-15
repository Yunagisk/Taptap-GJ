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
        if (skillButton != null)
        {
            skillButton.onClick.RemoveListener(SpendSkillResolve);
            skillButton.onClick.AddListener(SpendSkillResolve);
        }

        if(battleFlow != null)
        {
            battleFlow.OnBattleStateChanged +=HandleBattleStateChanged;
            if (battleFlow.Player != null)
            {
                battleFlow.Player.OnDeterminationChanged += HandleDeterminationChanged;
                battleFlow.Player.OnWeaponChanged += HandleWeaponChanged;
            }
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
        if (skillButton != null)
            skillButton.onClick.RemoveListener(SpendSkillResolve);

        if(battleFlow != null)
        {
            battleFlow.OnBattleStateChanged -=HandleBattleStateChanged;
            if (battleFlow.Player != null)
            {
                battleFlow.Player.OnDeterminationChanged -= HandleDeterminationChanged;
                battleFlow.Player.OnWeaponChanged -= HandleWeaponChanged;
            }
        }
    }

    // 点击只扣决心。不提交攻击，不结算技能伤害和状态。
    public void SpendSkillResolve()
    {
        if (battleFlow == null)
            return;
        battleFlow.SpendWeaponResolveOnly();
        RefreshSkillButton(battleFlow.CurrentState);
    }

    void HandleDeterminationChanged(int determination)
    {
        if (battleFlow != null)
            RefreshSkillButton(battleFlow.CurrentState);
    }

    void HandleWeaponChanged(WeaponData weapon)
    {
        if (battleFlow != null)
            RefreshSkillButton(battleFlow.CurrentState);
    }

    private void HandleBattleStateChanged(BattleState state)
    {
        bool canUseAction = state == BattleState.PlayerTurn;

        attackButton.interactable = canUseAction;
        defendButton.interactable = canUseAction;
        RefreshSkillButton(state);
        itemButton0.interactable = canUseAction;
        itemButton1.interactable = canUseAction;
        itemButton2.interactable = canUseAction;
    }

    void RefreshSkillButton(BattleState state)
    {
        if (skillButton == null)
            return;

        bool canUseAction = state == BattleState.PlayerTurn;
        PlayerCombatant player = battleFlow != null ? battleFlow.Player : null;
        SkillData skill = player != null && player.CurrentWeapon != null ? player.CurrentWeapon.Skill : null;
        int cost = skill != null ? skill.DeterminationCost : 0;
        int current = player != null ? player.CurrentDetermination : 0;
        bool exhausted = battleFlow != null && battleFlow.WeaponSkillBlocked;
        skillButton.interactable = canUseAction && SkillRules.CanPay(current, cost, exhausted);
    }
}
