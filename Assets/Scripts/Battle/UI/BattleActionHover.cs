using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattleActionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    BattleActionTooltip tooltip;
    Button button;
    BattleActionTooltip.ActionKind action;

    public void Bind(BattleActionTooltip owner, Button target, BattleActionTooltip.ActionKind kind)
    { tooltip = owner; button = target; action = kind; }
    public void OnPointerEnter(PointerEventData data) { if (tooltip != null) tooltip.Show(button, action); }
    public void OnPointerExit(PointerEventData data) { if (tooltip != null) tooltip.Hide(button); }
    void OnDisable() { if (tooltip != null) tooltip.Hide(button); }
}
