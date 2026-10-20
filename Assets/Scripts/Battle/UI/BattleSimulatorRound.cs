using UnityEngine;

// Inactive template: references are remapped together before the new round is activated.
public sealed class BattleSimulatorRound : MonoBehaviour
{
    public PlayerCombatant player;
    public BattleFlowController flow;
    public PlayerWeaponLoadout loadout;
    public BattleWeaponController switching;
}
