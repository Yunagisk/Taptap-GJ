using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Asks before a map movement enters the village; the controller owns the pending move.</summary>
public class VillageEntryConfirmationUI : MonoBehaviour
{
    public WorldMapController controller;
    public GameFlowController flow;
    public Button confirmButton;
    public Button cancelButton;
    public RectTransform window;
    public TMP_Text messageText;

    public bool IsOpen => gameObject.activeInHierarchy && controller != null
        && controller.IsVillageEntryConfirmationOpen;

    GameObject previousFocus;
    bool hiding;
    bool listenersBound;

    public bool Show(string destinationName)
    {
        var parentCanvas = GetComponentInParent<Canvas>(true);
        if (!enabled || controller == null || !controller.isActiveAndEnabled
            || (flow != null && flow.IsLoading) || parentCanvas == null
            || !parentCanvas.isActiveAndEnabled || window == null || messageText == null
            || !CanUse(confirmButton, false) || !CanUse(cancelButton, false))
            return false;
        if (!gameObject.activeInHierarchy && EventSystem.current != null)
            previousFocus = EventSystem.current.currentSelectedGameObject;
        string name = string.IsNullOrWhiteSpace(destinationName) ? "村庄" : destinationName;
        messageText.text = "是否进入" + name + "？";
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy) return false;
        FitWindow();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
        return true;
    }

    public void Hide(bool restoreFocus)
    {
        if (hiding) return;
        hiding = true;
        try
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            if (restoreFocus && previousFocus != null && EventSystem.current != null)
            {
                var button = previousFocus.GetComponent<Button>();
                if (CanUse(button, true))
                    EventSystem.current.SetSelectedGameObject(previousFocus);
            }
            previousFocus = null;
        }
        finally { hiding = false; }
    }

    void OnEnable()
    {
        if (listenersBound) return;
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        listenersBound = true;
    }

    void OnDisable()
    {
        if (listenersBound)
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            listenersBound = false;
        }
        // Disabling a parent Canvas or this component must not leave a pending move locked.
        if (!hiding && controller != null && controller.IsVillageEntryConfirmationOpen)
            controller.CancelVillageEntry();
    }

    void Confirm()
    {
        if (enabled && IsOpen && CanUse(confirmButton, true)
            && controller.isActiveAndEnabled && (flow == null || !flow.IsLoading))
            controller.ConfirmVillageEntry();
    }

    void Cancel()
    {
        if (enabled && IsOpen && CanUse(cancelButton, true))
            controller.CancelVillageEntry();
    }

    static bool CanUse(Button button, bool requireActive)
    {
        return button != null && button.enabled && button.IsInteractable()
            && (!requireActive || button.gameObject.activeInHierarchy);
    }

    void LateUpdate() { FitWindow(); }

    void FitWindow()
    {
        if (window == null) return;
        var parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;
        var canvasRect = parentCanvas.transform as RectTransform;
        if (canvasRect == null || window.sizeDelta.x <= 0 || window.sizeDelta.y <= 0) return;
        Vector2 size = canvasRect.rect.size;
        float scale = Mathf.Max(0.1f, Mathf.Min(1f,
            (size.x - 48) / window.sizeDelta.x, (size.y - 48) / window.sizeDelta.y));
        window.localScale = Vector3.one * scale;
    }
}
