using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCombatant : Combatant
{
    public IBattleAction GetBattleAction()  //告诉 BattleManager，这个敌人这回合准备执行哪个战斗行动
    {
        return new AttackAction();
    }
}
