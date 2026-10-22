using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DialogueChoiceView : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    public DialoguePanelGraphic panel;
    public TMP_Text caption;
    public TMP_Text number;
    public RpgDialogueLayout layout;

    public void OnSelect(BaseEventData eventData)
    {
        SetSelected(true);
        if (layout != null) layout.KeepChoiceVisible((RectTransform)transform);
    }
    public void OnDeselect(BaseEventData eventData) { SetSelected(false); }
    public void OnPointerEnter(PointerEventData eventData)
    {
        var button = GetComponent<Button>();
        if (button != null && button.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
    public void SetSelected(bool selected)
    {
        if (panel != null)
        {
            panel.color = selected ? new Color32(100, 42, 36, 255) : RpgDialogueLayout.Ink;
            panel.borderColor = selected ? RpgDialogueLayout.Paper : RpgDialogueLayout.Gold;
            panel.SetVerticesDirty();
        }
        if (caption != null) caption.color = RpgDialogueLayout.Paper;
        if (number != null) number.color = selected ? RpgDialogueLayout.Paper : RpgDialogueLayout.Gold;
    }
}
