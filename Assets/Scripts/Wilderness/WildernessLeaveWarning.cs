using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Attached to the leave button. No gameplay state changes until confirmation.
public sealed class WildernessLeaveWarning : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    MapWildernessEventUI owner;
    GameObject hint, confirmation;
    GameState pendingRun;
    string pendingNodeId;
    bool hovering;
    public Button ConfirmButton { get; private set; }
    public Button CancelButton { get; private set; }
    public bool IsOpen => confirmation != null && confirmation.activeInHierarchy;
    public bool IsHintVisible => hint != null && hint.activeInHierarchy;

    public void Bind(MapWildernessEventUI view) { owner = view; }
    public void OnPointerEnter(PointerEventData data) { hovering = true; RefreshHint(); }
    public void OnPointerExit(PointerEventData data) { hovering = false; RefreshHint(); }
    void OnDisable() { Hide(); }
    void LateUpdate()
    {
        if (IsOpen && !MatchesCurrentEvent()) Cancel();
        RefreshHint();
    }

    void RefreshHint()
    {
        bool show = hovering && owner != null && owner.IsOpen && owner.IsChoosing && !IsOpen;
        if (show && hint == null)
        {
            var frame = Frame("LeaveWarningHint", owner.window, new Vector2(520, 58));
            frame.anchorMin = frame.anchorMax = new Vector2(1, 1);
            frame.pivot = new Vector2(1, 1); frame.anchoredPosition = new Vector2(-48, -588);
            frame.GetComponent<Graphic>().raycastTarget = false;
            var text = Label("Warning", frame, "离开后将无法继续本次事件。", 23);
            text.alignment = TextAlignmentOptions.Center;
            hint = frame.gameObject;
        }
        if (hint != null) hint.SetActive(show);
    }

    public void Show()
    {
        if (owner == null || !owner.IsOpen || !owner.IsChoosing || IsOpen) return;
        EnsureConfirmation();
        pendingRun = GameRoot.I.state; pendingNodeId = pendingRun.map.wildernessEvents.activeNodeId;
        hovering = false; RefreshHint();
        confirmation.transform.SetAsLastSibling(); confirmation.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(CancelButton.gameObject);
    }

    public bool MatchesCurrentEvent()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        return state != null && state == pendingRun && owner != null && owner.IsOpen && owner.IsChoosing
            && state.map?.wildernessEvents?.activeNodeId == pendingNodeId;
    }

    public void Cancel()
    {
        bool focus = IsOpen && owner != null && owner.IsOpen;
        Hide();
        if (focus && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(owner.closeButton.gameObject);
    }

    public void Hide()
    {
        hovering = false; pendingRun = null; pendingNodeId = null;
        if (hint != null) hint.SetActive(false);
        if (confirmation != null) confirmation.SetActive(false);
    }

    void Confirm()
    {
        if (!IsOpen || !MatchesCurrentEvent()) { Cancel(); return; }
        owner.controller.ConfirmLeaveWildernessEvent();
    }

    void EnsureConfirmation()
    {
        if (confirmation != null) return;
        confirmation = new GameObject("LeaveEventConfirmation", typeof(RectTransform), typeof(Image));
        confirmation.layer = owner.gameObject.layer; confirmation.SetActive(false);
        var root = (RectTransform)confirmation.transform; root.SetParent(owner.transform, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        confirmation.GetComponent<Image>().color = new Color(0, 0, 0, .52f);
        var frame = Frame("ConfirmationCard", root, new Vector2(680, 310));
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
        var title = Label("Title", frame, "离开事件？", 32);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(.5f, 1);
        title.rectTransform.pivot = new Vector2(.5f, 1); title.rectTransform.anchoredPosition = new Vector2(0, -28);
        title.rectTransform.sizeDelta = new Vector2(584, 44);
        var message = Label("Message", frame, "离开后，你将无法继续本次事件。\n确定要离开吗？", 26);
        message.rectTransform.anchorMin = message.rectTransform.anchorMax = new Vector2(.5f, 1);
        message.rectTransform.pivot = new Vector2(.5f, 1); message.rectTransform.anchoredPosition = new Vector2(0, -91);
        message.rectTransform.sizeDelta = new Vector2(584, 86); message.lineSpacing = 8;
        CancelButton = MakeButton("CancelLeave", frame, "继续事件", -145, new Color32(223, 208, 183, 255));
        ConfirmButton = MakeButton("ConfirmLeave", frame, "确认离开", 145, new Color32(193, 165, 133, 255));
        CancelButton.onClick.AddListener(Cancel); ConfirmButton.onClick.AddListener(Confirm);
        CancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = ConfirmButton,
            selectOnRight = ConfirmButton, selectOnUp = ConfirmButton, selectOnDown = ConfirmButton };
        ConfirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = CancelButton,
            selectOnRight = CancelButton, selectOnUp = CancelButton, selectOnDown = CancelButton };
    }

    RectTransform Frame(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic));
        go.layer = owner.gameObject.layer; var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false); rect.sizeDelta = size;
        var graphic = go.GetComponent<BattleHudGraphic>(); graphic.fill = new Color32(239, 225, 198, 255);
        graphic.ink = new Color32(111, 84, 52, 255); graphic.stroke = 1.2f;
        return rect;
    }

    TMP_Text Label(string name, RectTransform parent, string value, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = owner.gameObject.layer; go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>(); text.font = SharedUIFont.Get(owner.eventTitle.font);
        text.text = value; text.fontSize = size; text.color = new Color32(66, 49, 31, 255);
        text.raycastTarget = false; text.enableWordWrapping = true;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(16, 8); text.rectTransform.offsetMax = new Vector2(-16, -8);
        return text;
    }

    Button MakeButton(string name, Transform parent, string caption, float x, Color fill)
    {
        var rect = Frame(name, parent, new Vector2(250, 58));
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(x, 58);
        var graphic = rect.GetComponent<BattleHudGraphic>(); graphic.fill = fill;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
        var label = Label("Label", rect, caption, 26); label.alignment = TextAlignmentOptions.Center;
        return button;
    }
}
