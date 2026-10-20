using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Preserve readable type rather than shrinking long descriptions into a fixed box.
public static class ScrollableTextRegion
{
    public static ScrollRect Ensure(TMP_Text text)
    {
        if (text == null) return null;
        var existing = text.GetComponentInParent<ScrollRect>();
        if (existing != null && existing.content == text.rectTransform) return existing;
        var content = text.rectTransform;
        var host = new GameObject(text.name + "Scroll", typeof(RectTransform), typeof(Image),
            typeof(RectMask2D), typeof(ScrollRect));
        host.layer = text.gameObject.layer;
        var rect = (RectTransform)host.transform;
        rect.SetParent(content.parent, false);
        rect.SetSiblingIndex(content.GetSiblingIndex());
        rect.anchorMin = content.anchorMin; rect.anchorMax = content.anchorMax;
        rect.pivot = content.pivot; rect.sizeDelta = content.sizeDelta;
        rect.anchoredPosition = content.anchoredPosition;
        host.GetComponent<Image>().color = new Color(1, 1, 1, .001f);
        content.SetParent(rect, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, rect.rect.height);
        text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        var fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = host.GetComponent<ScrollRect>();
        scroll.viewport = rect; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
        return scroll;
    }

    public static void Reset(TMP_Text text)
    {
        var scroll = text != null ? text.GetComponentInParent<ScrollRect>() : null;
        if (scroll == null || scroll.content != text.rectTransform) return;
        scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
    }
}
