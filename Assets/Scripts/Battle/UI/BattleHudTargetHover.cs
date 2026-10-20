using UnityEngine;

// The HUD arrow owns targeting clicks; ordinary hover still previews enemies.
public sealed class BattleHudTargetHover : MonoBehaviour
{
    BattleHudView hud;
    EnemyCombatant enemy;
    public void Bind(BattleHudView view, EnemyCombatant unit) { hud = view; enemy = unit; }
    bool IsTargeting => hud != null && hud.TargetingArrow != null && hud.TargetingArrow.IsTargeting;
    void OnMouseEnter() { if (hud != null && !IsTargeting) hud.SelectEnemy(enemy); }
    void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (hud != null && !IsTargeting) hud.SelectEnemy(enemy);
    }
}
