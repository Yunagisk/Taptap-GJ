using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager
{
    public List<EnemyCombatant> GetEnemyTurnOrder(List<EnemyCombatant> enemies)
    {
        return enemies
            .Where(enemy => enemy != null && !enemy.IsDead)
            .OrderByDescending(enemy => enemy.Priority)
            .ToList();
    }
}
