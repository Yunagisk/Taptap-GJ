using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BattleState
{
    None,

    PlayerTurn,
    SelectingTarget,

    EnemyTurn,

    Victory,
    Defeat
}
