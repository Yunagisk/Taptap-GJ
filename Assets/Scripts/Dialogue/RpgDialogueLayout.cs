using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reuses serialized UI references, including the older copies inside IntroCanvas.
// The editor builder also calls Ensure to leave a fully editable prefab.
[DisallowMultipleComponent]
public sealed class RpgDialogueLayout : MonoBehaviour
{
    public static readonly Color Ink = new Color32(29, 27, 25, 250);
    public static readonly Color Paper = new Color32(241, 229, 204, 255);
    public static readonly Color Gold = new Color32(174, 148, 102, 255);

    public RectTransform stage;
    public RectTransform window;
    public RectTransform nameplate;
    public ScrollRect choiceScroll;
    public TMP_Text pageText;
    public TMP_Text inputHint;
    public TMP_Text continueLabel;
    public DialogueUI ui;

    static readonly Vector2 Reference = new Vector2(1920, 1080);
    Vector2 lastSize;

    public static RpgDialogueLayout Ensure(DialogueUI owner)
    {
        var view = owner.GetComponent<RpgDialogueLayout>();
        if (view == null) view = owner.gameObject.AddComponent<RpgDialogueLayout>();
        if (view.stage == null) view.Build(owner);
        view.RefreshLayout();
        return view;
    }

    void LateUpdate() { RefreshLayout(); }

    public void RefreshLayout()
    {
        if (stage == null || ui == null || ui.visibleRoot == null) return;
        var bounds = ((RectTransform)ui.visibleRoot.transform).rect.size;
        if (bounds == lastSize) return;
        lastSize = bounds;
        float fit = Mathf.Min(bounds.x / Reference.x, bounds.y / Reference.y);
        stage.localScale = Vector3.one * Mathf.Max(.01f, fit);
    }

    void Build(DialogueUI owner)
    {
        ui = owner;
        // Legacy Intro keeps its manager outside the Canvas and has no visibleRoot.
        if (ui.visibleRoot == null && ui.contentText != null)
            ui.visibleRoot = ui.contentText.transform.parent.gameObject;
        if (ui.visibleRoot == null) ui.visibleRoot = Rect("VisibleRoot", transform).gameObject;
        var overlay = (RectTransform)ui.visibleRoot.transform;
        Stretch(overlay);
        var shade = overlay.GetComponent<Image>();
        if (shade == null) shade = overlay.gameObject.AddComponent<Image>();
        shade.color = new Color(.025f, .02f, .015f, .24f);
        shade.raycastTarget = true;
        var surface = overlay.GetComponent<DialogueAdvanceSurface>();
        if (surface == null) surface = overlay.gameObject.AddComponent<DialogueAdvanceSurface>();
        surface.owner = ui;
        stage = Rect("RpgStage", overlay);
        stage.anchorMin = stage.anchorMax = new Vector2(.5f, 0);
        stage.pivot = new Vector2(.5f, 0);
        stage.sizeDelta = Reference;

        if (ui.avatarImage == null) ui.avatarImage = Rect("Avatar", stage).gameObject.AddComponent<Image>();
        CharacterPortraitStyle.Apply(ui.avatarImage, ui.avatarImage.sprite);
        ui.avatarImage.transform.SetParent(stage, false);
        Place(ui.avatarImage.rectTransform, 65, 55, 470, 665);
        ui.avatarImage.preserveAspect = true;
        ui.avatarImage.raycastTarget = false;

        window = ui.contentText != null ? ui.contentText.transform.parent as RectTransform : null;
        if (window == null || window == overlay) window = Rect("Window", stage);
        window.SetParent(stage, false);
        Place(window, 80, 55, 1760, 252);
        DisableImage(window);
        Panel(window, Ink, 18);
        var accent = Rect("Accent", window);
        Place(accent, 25, 231, 300, 3);
        var line = accent.gameObject.AddComponent<Image>();
        line.color = Gold; line.raycastTarget = false;

        if (ui.contentText == null) ui.contentText = Label("Content", window);
        ui.contentText.transform.SetParent(window, false);
        TextStyle(ui.contentText, 32, Paper);
        ui.contentText.alignment = TextAlignmentOptions.TopLeft;
        ui.contentText.lineSpacing = 12;
        ui.contentText.overflowMode = TextOverflowModes.Page;
        ui.contentText.enableAutoSizing = false;
        ui.contentText.margin = Vector4.zero;

        nameplate = Rect("Nameplate", stage);
        Place(nameplate, 550, 282, 340, 62);
        Panel(nameplate, Paper, 10);
        if (ui.speakerText == null) ui.speakerText = Label("Speaker", nameplate);
        ui.speakerText.transform.SetParent(nameplate, false);
        TextStyle(ui.speakerText, 31, Ink);
        Stretch(ui.speakerText.rectTransform);
        ui.speakerText.margin = new Vector4(27, 3, 27, 3);
        ui.speakerText.enableAutoSizing = false;
        ui.speakerText.overflowMode = TextOverflowModes.Ellipsis;

        inputHint = Label("InputHint", window);
        Place(inputHint.rectTransform, 475, 15, 800, 30);
        TextStyle(inputHint, 20, Gold);
        pageText = Label("Page", window);
        Place(pageText.rectTransform, 1390, 15, 100, 34);
        TextStyle(pageText, 21, Gold);
        pageText.alignment = TextAlignmentOptions.Right;

        if (ui.continueButton == null)
            ui.continueButton = Rect("Continue", window).gameObject.AddComponent<Button>();
        ui.continueButton.transform.SetParent(window, false);
        Place((RectTransform)ui.continueButton.transform, 1510, 10, 215, 42);
        var continueImage = ui.continueButton.GetComponent<Image>();
        if (continueImage == null) continueImage = ui.continueButton.gameObject.AddComponent<Image>();
        continueImage.color = new Color(1, 1, 1, .001f);
        ui.continueButton.targetGraphic = continueImage;
        ui.continueButton.navigation = new Navigation { mode = Navigation.Mode.None };
        continueLabel = ui.continueButton.GetComponentInChildren<TMP_Text>(true);
        if (continueLabel == null) continueLabel = Label("Label", ui.continueButton.transform);
        TextStyle(continueLabel, 23, Paper);
        Stretch(continueLabel.rectTransform);
        continueLabel.alignment = TextAlignmentOptions.Center;

        var scrollRoot = Rect("ChoiceViewport", stage);
        Place(scrollRoot, 990, 365, 850, 390);
        choiceScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        choiceScroll.horizontal = false;
        choiceScroll.movementType = ScrollRect.MovementType.Clamped;
        choiceScroll.scrollSensitivity = 45;
        scrollRoot.gameObject.AddComponent<RectMask2D>();
        var scrollHit = scrollRoot.gameObject.AddComponent<Image>();
        scrollHit.color = new Color(1, 1, 1, .001f);
        if (ui.choiceParent != null)
        {
            var oldLayout = ui.choiceParent.GetComponent<LayoutGroup>();
            if (oldLayout != null && !(oldLayout is VerticalLayoutGroup))
            {
                ui.choiceParent.gameObject.SetActive(false);
                ui.choiceParent = null;
            }
        }
        if (ui.choiceParent == null) ui.choiceParent = Rect("Choices", scrollRoot);
        ui.choiceParent.SetParent(scrollRoot, false);
        var choices = (RectTransform)ui.choiceParent;
        choices.anchorMin = new Vector2(0, 1); choices.anchorMax = Vector2.one;
        choices.pivot = new Vector2(.5f, 1);
        choices.anchoredPosition = Vector2.zero;
        choices.sizeDelta = Vector2.zero;
        var list = choices.GetComponent<VerticalLayoutGroup>();
        if (list == null) list = choices.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 12;
        list.padding = new RectOffset(4, 4, 4, 4);
        list.childControlHeight = list.childControlWidth = true;
        list.childForceExpandWidth = true; list.childForceExpandHeight = false;
        var fitter = choices.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = choices.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        choiceScroll.viewport = scrollRoot; choiceScroll.content = choices;
        choiceScroll.verticalNormalizedPosition = 1;
        // The portrait is in front of the band so its face can never be covered.
        window.SetAsFirstSibling();
        ui.avatarImage.transform.SetSiblingIndex(1);
    }

    public void Present(DialogueNode node)
    {
        RefreshLayout();
        bool narration = node.lineKind == DialogueLineKind.Narration
            || (node.lineKind == DialogueLineKind.Dialogue && (string.IsNullOrWhiteSpace(node.speaker) || node.speaker == "旁白"));
        bool system = node.lineKind == DialogueLineKind.System || node.speaker == "系统提示";
        bool portrait = !narration && !system && node.avatar != null;
        ui.avatarImage.enabled = portrait;
        float start = portrait ? 475 : 70;
        Place(ui.contentText.rectTransform, start, 72, 1685 - start, 133);
        inputHint.rectTransform.anchoredPosition = new Vector2(start, 15);
        nameplate.gameObject.SetActive(!narration || system);
        if (system) ui.speakerText.text = string.IsNullOrWhiteSpace(node.speaker) ? "系统提示" : node.speaker;
        float width = Mathf.Clamp(ui.speakerText.GetPreferredValues(ui.speakerText.text).x + 56, 250, 760);
        Place(nameplate, portrait ? 550 : 150, 282, width, 62);
    }

    public void SetPage(int page, int count, bool choices)
    {
        pageText.text = count > 1 ? page + " / " + count : "";
        continueLabel.text = page < count ? "翻页  ›" : "继续  ›";
        inputHint.text = choices ? "↑ ↓ 选择    确认 / 数字键 回应" : "空格 / 确认 继续";
    }

    public void SetChoicesVisible(bool visible)
    {
        choiceScroll.gameObject.SetActive(visible);
        if (visible) choiceScroll.verticalNormalizedPosition = 1;
    }

    public void StyleChoice(Button button, string text, int index)
    {
        DisableImage((RectTransform)button.transform);
        var panel = Panel((RectTransform)button.transform, Ink, 12);
        panel.raycastTarget = true;
        button.targetGraphic = panel;
        button.transition = Selectable.Transition.None;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) label = Label("Label", button.transform);
        TextStyle(label, 29, Paper);
        label.text = text;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.margin = new Vector4(81, 12, 28, 12);
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Overflow;
        Stretch(label.rectTransform);
        var number = Label("ChoiceNumber", button.transform);
        TextStyle(number, 24, Gold);
        number.text = index.ToString("00");
        number.font = label.font;
        number.alignment = TextAlignmentOptions.Center;
        number.rectTransform.anchorMin = Vector2.zero;
        number.rectTransform.anchorMax = new Vector2(0, 1);
        number.rectTransform.pivot = new Vector2(0, .5f);
        number.rectTransform.anchoredPosition = new Vector2(12, 0);
        number.rectTransform.sizeDelta = new Vector2(60, 0);
        var height = button.GetComponent<LayoutElement>();
        if (height == null) height = button.gameObject.AddComponent<LayoutElement>();
        height.preferredHeight = Mathf.Max(76, label.GetPreferredValues(text, 850 - 8 - 109, float.PositiveInfinity).y + 26);
        var view = button.GetComponent<DialogueChoiceView>();
        if (view == null) view = button.gameObject.AddComponent<DialogueChoiceView>();
        view.panel = panel; view.caption = label; view.number = number; view.layout = this;
        view.SetSelected(false);
    }

    public void KeepChoiceVisible(RectTransform row)
    {
        if (choiceScroll == null || row == null) return;
        Canvas.ForceUpdateCanvases();
        var content = choiceScroll.content;
        float hidden = content.rect.height - choiceScroll.viewport.rect.height;
        if (hidden <= 0) return;
        float top = -row.anchoredPosition.y - row.rect.height * (1 - row.pivot.y);
        float offset = content.anchoredPosition.y;
        float bottom = top + row.rect.height;
        if (top < offset) offset = top;
        else if (bottom > offset + choiceScroll.viewport.rect.height) offset = bottom - choiceScroll.viewport.rect.height;
        choiceScroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(offset / hidden);
    }

    public void FitChoices()
    {
        Canvas.ForceUpdateCanvases();
        choiceScroll.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Clamp(choiceScroll.content.rect.height, 84, 390));
        Canvas.ForceUpdateCanvases();
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(parent, false);
        return rect;
    }
    TMP_Text Label(string name, Transform parent)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (ui.contentText != null) label.font = ui.contentText.font;
        label.raycastTarget = false;
        return label;
    }
    static void TextStyle(TMP_Text label, float size, Color color)
    {
        label.fontSize = size; label.color = color;
        label.richText = false; label.raycastTarget = false;
        label.enableWordWrapping = true;
        label.alignment = TextAlignmentOptions.MidlineLeft;
    }
    static DialoguePanelGraphic Panel(RectTransform rect, Color color, float cut)
    {
        // Unity only permits one Graphic on each object; preserve legacy Images and IDs.
        var frame = rect.Find("RpgFrame") as RectTransform;
        if (frame == null)
        {
            frame = Rect("RpgFrame", rect);
            Stretch(frame);
            frame.SetAsFirstSibling();
        }
        var panel = frame.GetComponent<DialoguePanelGraphic>();
        if (panel == null) panel = frame.gameObject.AddComponent<DialoguePanelGraphic>();
        panel.color = color; panel.cornerCut = cut;
        panel.raycastTarget = false;
        return panel;
    }
    static void DisableImage(RectTransform rect)
    {
        var image = rect.GetComponent<Image>();
        if (image != null) { image.enabled = false; image.raycastTarget = false; }
    }
    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
