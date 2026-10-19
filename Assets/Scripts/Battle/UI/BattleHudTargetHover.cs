using UnityEngine;

// Existing EnemyTargetSelector remains responsible for confirming world clicks.
public sealed class BattleHudTargetHover : MonoBehaviour
{
    BattleHudView hud;
    EnemyCombatant enemy;
    public void Bind(BattleHudView view, EnemyCombatant unit) { hud = view; enemy = unit; }
    void OnMouseEnter() { if (hud != null) hud.SelectEnemy(enemy); }
    void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (hud != null) hud.SelectEnemy(enemy);
    }
}
