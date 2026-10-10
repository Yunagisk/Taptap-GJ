using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CycleActionSelector : IEnemyActionSelector
{
    private readonly IReadOnlyList<EnemyAttackData> actions;

    private int currentIndex;   //下一次从哪个位置开始找招式。
    private int selectedIndex = -1; //这次实际选中了哪个位置，-1 表示还没选中。

    public CycleActionSelector(IReadOnlyList<EnemyAttackData> actions)
    {
        this.actions = actions;
        Reset();
    }

    public void Reset()
    {
        currentIndex = 0;
        selectedIndex = -1;
    }

    public EnemyAttackData SelectAction() // 找出当前招式，但不推进循环
    {
        selectedIndex = -1;

        if (actions == null || actions.Count == 0)
            return null;

        // 从当前位置开始寻找有效招式，跳过空引用。
        for (int offset = 0; offset < actions.Count; offset++)
        {
            int index = (currentIndex + offset) % actions.Count;

            if (actions[index] == null)
                continue;

            selectedIndex = index;
            return actions[index];
        }

        return null;
    }

    //本招式执行完成后，指向下一个招式。
    public void OnActionCompleted()
    {
        if (selectedIndex < 0 || actions == null || actions.Count == 0)
        {
            return;
        }

        currentIndex = (selectedIndex + 1) % actions.Count;
        selectedIndex = -1;
    }
}
