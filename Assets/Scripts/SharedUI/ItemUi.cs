using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small controls added to the existing parchment and battle panels.
public static class ItemUi
{
    public static Button Button(Transform parent, TMP_FontAsset font, string name, Vector2 position,
        Vector2 size, Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color32(231, 224, 205, 255);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = new Color32(255, 247, 222, 255);
        colors.pressedColor = new Color32(189, 176, 144, 255);
        colors.disabledColor = new Color32(163, 158, 145, 200);
        button.colors = colors;
        button.onClick.AddListener(() => onClick());
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 3); labelRect.offsetMax = new Vector2(-10, -3);
        var text = label.GetComponent<TextMeshProUGUI>();
        text.font = SharedUIFont.Get(font);
        text.fontSize = text.fontSizeMax = 18; text.fontSizeMin = 12; text.enableAutoSizing = true;
        text.color = new Color32(35, 32, 27, 255);
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        return button;
    }
}
