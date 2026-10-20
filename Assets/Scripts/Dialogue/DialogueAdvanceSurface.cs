using UnityEngine;
using UnityEngine.EventSystems;

// Legacy scenes keep the controller outside the Canvas, so forward background clicks.
public sealed class DialogueAdvanceSurface : MonoBehaviour, IPointerClickHandler
{
    public DialogueUI owner;
    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null && eventData.button == PointerEventData.InputButton.Left) owner.Advance();
    }
}
