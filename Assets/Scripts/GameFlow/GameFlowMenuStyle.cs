using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared by the saved map menus and their runtime binding.
public static class GameFlowMenuStyle
{
    static readonly Color Ink = new Color32(48, 41, 32, 255);
    static readonly Color FaintInk = new Color32(87, 73, 53, 150);

    public static void Apply(GameFlowController flow)
    {
        if (flow.menuPaper == null) return;
        var pause = Window(flow.pauseOverlay, flow.menuPaper, new Vector2(600, 650));
        var settings = Window(flow.settingsOverlay, flow.menuPaper, new Vector2(740, 430));
        var confirm = Window(flow.confirmOverlay, flow.menuPaper, new Vector2(740, 330));
        foreach (var overlay in new[] { flow.pauseOverlay, flow.settingsOverlay, flow.confirmOverlay })
        {
            if (overlay == null) continue;
            foreach (var label in overlay.GetComponentsInChildren<TMP_Text>(true))
            {
                if (flow.font != null) label.font = SharedUIFont.Get(flow.font);
                label.color = Ink;
            }
            foreach (var button in overlay.GetComponentsInChildren<Button>(true)) StyleButton(button);
        }
        if (pause != null)
        {
            Title(pause, "PauseTitle", 48);
            Place(pause.Find("Btn_Resume"), new Vector2(92, -164), new Vector2(416, 66));
            Place(pause.Find("Btn_PauseSettings"), new Vector2(92, -258), new Vector2(416, 66));
            Place(pause.Find("Btn_ReturnTitle"), new Vector2(92, -352), new Vector2(416, 66));
            Place(pause.Find("Btn_PauseQuit"), new Vector2(92, -446), new Vector2(416, 66));
            Decoration(pause, "TitleRule", InventoryInkGraphic.Shape.Rule,
                new Vector2(80, -130), new Vector2(440, 2), FaintInk);
            Decoration(pause, "BottomSeal", InventoryInkGraphic.Shape.Diamond,
                new Vector2(292, -572), new Vector2(16, 20), Ink);
        }
        if (settings != null)
        {
            Title(settings, "SettingsTitle", 34);
            Place(settings.Find("VolumeLabel"), new Vector2(64, -142), new Vector2(120, 44));
            Place(settings.Find("MasterVolume"), new Vector2(212, -146), new Vector2(298, 36));
            Place(settings.Find("VolumeValue"), new Vector2(552, -142), new Vector2(110, 44));
            Place(settings.Find("Fullscreen"), new Vector2(64, -230), new Vector2(400, 44));
            Place(settings.Find("Btn_SettingsDone"), new Vector2(486, -312), new Vector2(190, 58));
            Place(settings.Find("Btn_CloseSettings"), new Vector2(642, -46), new Vector2(44, 44));
            var slider = flow.volumeSlider;
            if (slider != null)
            {
                Tint(slider.transform.Find("Track"), new Color32(158, 141, 111, 160));
                Tint(slider.transform.Find("FillArea/Fill"), Ink);
                Tint(slider.transform.Find("HandleArea/Handle"), Ink);
            }
            var toggle = flow.fullscreenToggle;
            if (toggle != null)
            {
                Tint(toggle.transform.Find("Box"), new Color32(219, 203, 173, 255));
                Tint(toggle.transform.Find("Box/Checkmark"), Ink);
                Frame(toggle.transform.Find("Box"), InventoryInkGraphic.Shape.Frame, 1);
            }
        }
        if (confirm != null)
        {
            Place(confirm.Find("ConfirmationText"), new Vector2(64, -62), new Vector2(612, 100));
            var text = confirm.Find("ConfirmationText").GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 26;
            text.enableWordWrapping = true;
            Place(confirm.Find("Btn_ConfirmCancel"), new Vector2(64, -212), new Vector2(232, 62));
            Place(confirm.Find("Btn_ConfirmAccept"), new Vector2(444, -212), new Vector2(232, 62));
        }
    }

    static RectTransform Window(GameObject overlay, Sprite paper, Vector2 size)
    {
        if (overlay == null) return null;
        var backdrop = overlay.GetComponent<Image>();
        if (backdrop != null) backdrop.color = new Color32(28, 24, 19, 150);
        var window = overlay.transform.Find("Window") as RectTransform;
        if (window == null) return null;
        window.sizeDelta = size;
        var image = window.GetComponent<Image>();
        image.sprite = paper;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;
        Frame(window, InventoryInkGraphic.Shape.OrnateFrame, 30);
        return window;
    }

    public static void StyleButton(Button button)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image == null) return;
        image.sprite = null;
        image.color = new Color32(202, 181, 143, 55);
        image.raycastTarget = true;
        var outline = button.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(166, 145, 110, 255);
        colors.selectedColor = new Color32(180, 158, 119, 255);
        colors.pressedColor = new Color32(125, 106, 76, 255);
        colors.disabledColor = new Color32(220, 213, 199, 110);
        colors.fadeDuration = .12f;
        button.colors = colors;
        Frame(button.transform, InventoryInkGraphic.Shape.OrnateFrame, 0);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = Ink;
            label.fontSize = 28;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.transform.SetAsLastSibling();
        }
    }

    static void Title(RectTransform window, string name, float top)
    {
        var title = window.Find(name);
        if (title == null) return;
        Place(title, new Vector2(80, -top), new Vector2(window.sizeDelta.x - 160, 64));
        var label = title.GetComponent<TMP_Text>();
        label.fontSize = 40;
        label.alignment = TextAlignmentOptions.Center;
    }

    static void Place(Transform target, Vector2 position, Vector2 size)
    {
        if (target is not RectTransform rect) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Tint(Transform target, Color color)
    {
        if (target != null && target.TryGetComponent<Image>(out var image)) image.color = color;
    }

    static void Frame(Transform parent, InventoryInkGraphic.Shape shape, float inset)
    {
        if (parent == null) return;
        var graphic = InkGraphic(parent, "InkFrame", shape, Ink);
        var rect = graphic.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
        graphic.stroke = 1.2f;
    }

    static void Decoration(Transform parent, string name, InventoryInkGraphic.Shape shape,
        Vector2 position, Vector2 size, Color color)
    {
        var graphic = InkGraphic(parent, name, shape, color);
        Place(graphic.transform, position, size);
    }

    static InventoryInkGraphic InkGraphic(Transform parent, string name, InventoryInkGraphic.Shape shape, Color color)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(InventoryInkGraphic)).transform;
            child.gameObject.layer = parent.gameObject.layer;
            child.SetParent(parent, false);
        }
        var graphic = child.GetComponent<InventoryInkGraphic>();
        graphic.shape = shape; graphic.color = color; graphic.raycastTarget = false;
        return graphic;
    }
}
