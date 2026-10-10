using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//一个敌人的行动协调者：保存招式配置，判断能否行动，选出招式并交给执行器，最后推进攻击循环。
public class EnemyCombatant : Combatant
{
    [Header("行动优先级")]
    [SerializeField] private int priority = 10;

    [Header("攻击循环：按列表顺序执行，允许重复招式")]
    [SerializeField]
    private List<EnemyAttackData> attackDatas = new();

    private IEnemyActionSelector actionSelector; //负责选哪一招

    private readonly EnemyAttackAction attackAction = new EnemyAttackAction();//负责执行选中的招式

    private bool isActing;  //敌人是否正在行动中，避免重复调用。
    public int Priority => priority;

    // 未来可以在这里增加眩晕等状态检查。
    public virtual bool CanAct => !IsDead; //跳过行动不推进循环

    public override void Initialize()
    {
        base.Initialize();

        isActing = false;

        actionSelector = CreateActionSelector();
        actionSelector?.Reset();    //“如果 actionSelector 不为空，就调用 Reset()
    }

    // Boss 以后可以继承 EnemyCombatant，
    // 重写此方法，返回自己的行动选择器。
    protected virtual IEnemyActionSelector CreateActionSelector()
    {
        return new CycleActionSelector(attackDatas);
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

        //第二步，选招并锁定行动状态：
        EnemyAttackData selectedAction =actionSelector.SelectAction();

        if (selectedAction == null)
        {
            Debug.LogWarning($"{name} 没有可用招式，跳过本次行动");
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
    }

}
