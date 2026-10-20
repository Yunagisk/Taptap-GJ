using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BattleIntentHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    BattleHudEnemyView row;
    public EnemyCombatant Enemy => row != null ? row.Enemy : null;
    public void Bind(BattleHudEnemyView view) => row = view;
    public void OnPointerEnter(PointerEventData data) { if (row != null) row.SetIntentHovered(true); }
    public void OnPointerExit(PointerEventData data) { if (row != null) row.SetIntentHovered(false); }
    void OnDisable() { if (row != null) row.SetIntentHovered(false, false); }
}
