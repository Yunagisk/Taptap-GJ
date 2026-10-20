using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The map owns settlement; this modal presents one explicit rest action.
public sealed class MapCampUI : MonoBehaviour
{
    public RectTransform window;
    public TMP_Text description, recoveryText;
    public Button restButton;
    WorldMapController controller;
    GameFlowController flow;
    GameState shownRun;
    string shownRequestId;
    GameObject previousFocus;
    bool listening;

    public bool IsOpen => enabled && gameObject.activeInHierarchy;
    public bool CanRest => IsOpen && controller != null && controller.isActiveAndEnabled
        && !controller.IsEnteringNode && !MapSceneTransition.IsBusy && shownRun != null && shownRun.player.hp > 0
        && (flow == null || !flow.IsLoading && !Visible(flow.pauseOverlay) && !Visible(flow.settingsOverlay)
            && !Visible(flow.confirmOverlay) && !Visible(flow.loadingOverlay));

    public void Bind(WorldMapController owner)
    {
        controller = owner;
        flow = FindObjectOfType<GameFlowController>();
    }

    public bool CanShow()
    {
        var canvas = GetComponentInParent<Canvas>(true);
        return enabled && controller != null && controller.isActiveAndEnabled && canvas != null
            && canvas.isActiveAndEnabled && window != null && description != null && recoveryText != null
            && restButton != null && restButton.enabled;
    }

    public bool Show()
    {
        if (!CanShow()) return false;
        var pending = controller.Progression?.GetPendingRequest();
        if (pending == null || pending.kind != MapNodeKind.Camp) return false;
        if (!IsOpen && EventSystem.current != null) previousFocus = EventSystem.current.currentSelectedGameObject;
        shownRun = GameRoot.I.state;
        shownRequestId = pending.requestId;
        // Keep the camp above map UI while existing pause/settings overlays remain usable.
        transform.SetAsLastSibling();
        if (flow != null)
            foreach (var overlay in new[] { flow.pauseOverlay, flow.settingsOverlay, flow.confirmOverlay, flow.loadingOverlay })
                if (overlay != null && overlay.transform.parent == transform.parent
                    && overlay.transform.GetSiblingIndex() < transform.GetSiblingIndex())
                    transform.SetSiblingIndex(overlay.transform.GetSiblingIndex());
        gameObject.SetActive(true);
        Refresh();
        FitWindow();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(restButton.gameObject);
        return IsOpen;
    }

    public void Hide()
    {
        shownRun = null;
        shownRequestId = null;
        if (gameObject.activeSelf) gameObject.SetActive(false);
        if (previousFocus != null && EventSystem.current != null)
        {
            var selectable = previousFocus.GetComponent<Selectable>();
            if (selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable())
                EventSystem.current.SetSelectedGameObject(previousFocus);
        }
        previousFocus = null;
    }

    void OnEnable()
    {
        if (!listening && restButton != null) { restButton.onClick.AddListener(Rest); listening = true; }
    }

    void OnDisable()
    {
        if (listening && restButton != null) restButton.onClick.RemoveListener(Rest);
        listening = false;
    }

    void Rest()
    {
        if (IsOpen && restButton.isActiveAndEnabled && restButton.IsInteractable()) controller.RestAtCamp();
    }

    void LateUpdate()
    {
        if (!IsOpen) return;
        var run = GameRoot.I != null ? GameRoot.I.state : null;
        if (controller == null || !controller.isActiveAndEnabled || run == null || run != shownRun
            || controller.Progression?.GetPendingRequest()?.requestId != shownRequestId)
        { Hide(); return; }
        Refresh();
        FitWindow();
    }

    void Refresh()
    {
        if (shownRun == null) return;
        int amount = WorldResourceRules.CampRecoveryPreview(shownRun.player);
        recoveryText.text = amount > 0 ? "恢复 " + amount + " 点生命  ·  休息耗时 1" : "生命已满  ·  休息耗时 1";
        restButton.interactable = CanRest;
    }

    static bool Visible(GameObject root) => root != null && root.activeInHierarchy;

    void FitWindow()
    {
        if (window == null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var size = ((RectTransform)canvas.transform).rect.size;
        window.localScale = Vector3.one * Mathf.Max(.1f, Mathf.Min(1f,
            (size.x - 48) / window.sizeDelta.x, (size.y - 48) / window.sizeDelta.y));
    }
}
