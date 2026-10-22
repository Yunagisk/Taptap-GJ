using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//选择器,用于战斗循环
public interface IEnemyActionSelector
{
    // 战斗开始时重置选择状态。
    void Reset();

    // 获取当前招式，不推进循环。
    EnemyAttackData SelectAction();

    // 招式执行完成后，更新选择状态。
    void OnActionCompleted();
}
