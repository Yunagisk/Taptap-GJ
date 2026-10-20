using UnityEngine;
using UnityEngine.EventSystems;

// Pointer selection should not leave a red keyboard-focus state after the pointer leaves.
[DisallowMultipleComponent]
public sealed class MainMenuButtonFocus : MonoBehaviour, IPointerExitHandler
{
    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }
}
