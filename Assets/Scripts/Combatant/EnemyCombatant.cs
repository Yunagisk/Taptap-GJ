using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCombatant : Combatant
{
    [Header("行动优先级")]
    [SerializeField] private int priority = 10;

    public int Priority => priority;

    [Header("攻击配置")]
    [SerializeField]
    private List<EnemyAttackData> attackDatas = new();

    public EnemyAttackInfo GetAttackInfo()
    {
        // 过滤掉没有配置的攻击
        List<EnemyAttackData> availableAttacks =
            attackDatas.FindAll(data => data != null);

        if (availableAttacks.Count == 0)
        {
            Debug.LogWarning($"{name}没有配置攻击数据");
            return default;
        }

        // 随机选择一个攻击配置
        int index = Random.Range(0, availableAttacks.Count);

        EnemyAttackData selectedAttack = availableAttacks[index];

        Debug.Log($"{name}选择攻击：{selectedAttack.AttackName}");

        return selectedAttack.CreateAttackInfo();
    }

}
