using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WorldMapViewport : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    public RectTransform content;
    [Min(0.05f)] public float focusDuration = 0.35f;
    WorldMapSceneView owner;
    RectTransform window;
    [Header("Navigation UI (authored in the map prefab)")]
    [SerializeField] Transform controls;
    [SerializeField] Button focusButton;
    [SerializeField] Button overviewButton;
    [SerializeField] TMP_Text overviewLabel;
    Vector2 mapSize, center, fromCenter, targetCenter, lastPointer;
    float zoom = 1, fromZoom = 1, targetZoom = 1, startedAt, baseScale = 1;
    bool initialized, moving, dragging;
    bool initialFocusPending, framePending;
    Rect pendingBounds;
    int dragPointer;

    public static WorldMapViewport Configure(WorldMapSceneView view)
    {
        var instance = view.viewport;
        if (instance == null)
        {
            var existing = view.transform.Find("MapViewport");
            if (existing != null) instance = existing.GetComponent<WorldMapViewport>();
            if (instance == null)
            {
                int index = view.mapContent.GetSiblingIndex();
                var obj = new GameObject("MapViewport", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image), typeof(RectMask2D), typeof(WorldMapViewport));
                obj.layer = view.gameObject.layer;
                obj.transform.SetParent(view.transform, false);
                obj.transform.SetSiblingIndex(index);
                instance = obj.GetComponent<WorldMapViewport>();
            }
        }
        view.viewport = instance;
        instance.window = (RectTransform)instance.transform;
        instance.window.anchorMin = Vector2.zero;
        instance.window.anchorMax = Vector2.one;
        instance.window.pivot = Vector2.one * 0.5f;
        instance.window.offsetMin = instance.window.offsetMax = Vector2.zero;
        instance.window.localScale = Vector3.one;
        var surface = instance.GetComponent<Image>();
        surface.color = Color.clear;
        surface.raycastTarget = true;
        instance.content = view.mapContent;
        view.mapContent.SetParent(instance.window, false);
        Vector2 size = view.visualLayout != null ? view.visualLayout.mapSize : new Vector2(1920, 1080);
        Center(view.mapContent, Vector2.zero, size);
        var lineRoot = view.mapContent.Find("LineRoot") as RectTransform;
        if (lineRoot != null) Center(lineRoot, Vector2.zero, size);

        var artwork = view.villageArtwork;
        if (artwork == null)
        {
            Transform child = view.originalBackground != null ? view.originalBackground.transform.Find("VillageArtwork") : null;
            if (child == null) child = view.mapContent.Find("VillageArtwork");
            if (child != null) artwork = child.GetComponent<Image>();
            view.villageArtwork = artwork;
        }
        if (view.originalBackground != null)
        {
            view.originalBackground.transform.SetParent(view.mapContent, false);
            Center(view.originalBackground.rectTransform, Vector2.zero, size);
            view.originalBackground.transform.SetAsFirstSibling();
        }
        if (artwork != null)
        {
            artwork.transform.SetParent(view.mapContent, false);
            Vector2 artSize = view.visualLayout != null ? view.visualLayout.villageArtworkSize : new Vector2(1920, 1080);
            var village = view.mapContent.Find("Node_village") as RectTransform;
            Center(artwork.rectTransform, village != null ? village.anchoredPosition : Vector2.zero, artSize);
            artwork.transform.SetSiblingIndex(view.originalBackground != null ? 1 : 0);
            artwork.raycastTarget = false;
        }
        var paper = view.transform.Find("MapSurface");
        instance.transform.SetSiblingIndex(paper != null ? paper.GetSiblingIndex() + 1 : 0);
        instance.owner = view;
        instance.mapSize = size;
        instance.initialized = true;
#if UNITY_EDITOR
        if (!Application.isPlaying) instance.CreateControls(view.font);
#endif
        instance.BindControls();
        instance.FocusPlayer(true);
        return instance;
    }

    void Update()
    {
        if (!initialized || content == null || window.rect.width < 1 || window.rect.height < 1) return;
        if (initialFocusPending) FocusPlayer(true);
        if (framePending) { framePending = false; EnsureVisible(pendingBounds); }
        baseScale = MapViewportMath.BaseScale(window.rect.size, mapSize);
        float minimum = MapViewportMath.MinimumZoom(window.rect.size, mapSize, baseScale);
        zoom = Mathf.Clamp(zoom, minimum, 1.5f);
        if (moving && !dragging)
        {
            float phase = Mathf.Clamp01((Time.unscaledTime - startedAt) / Mathf.Max(0.05f, focusDuration));
            float eased = MapRevealMath.EaseOutCubic(phase);
            center = Vector2.Lerp(fromCenter, targetCenter, eased);
            zoom = Mathf.Clamp(Mathf.Lerp(fromZoom, Mathf.Clamp(targetZoom, minimum, 1.5f), eased), minimum, 1.5f);
            if (phase >= 1) moving = false;
        }
        ApplyPose();
    }

    void ApplyPose()
    {
        if (window == null || content == null || window.rect.width < 1 || window.rect.height < 1) return;
        baseScale = MapViewportMath.BaseScale(window.rect.size, mapSize);
        float scale = baseScale * zoom;
        center = MapViewportMath.ClampCenter(center, mapSize, window.rect.size / scale);
        Vector3 nextScale = Vector3.one * scale;
        Vector2 nextPosition = -center * scale;
        if (content.localScale != nextScale) content.localScale = nextScale;
        if (content.anchoredPosition != nextPosition) content.anchoredPosition = nextPosition;
        if (overviewLabel != null)
        {
            string label = zoom < 0.99f ? "返回" : "总览";
            if (overviewLabel.text != label) overviewLabel.text = label;
        }
    }

    public void EnsureVisible(Rect bounds)
    {
        if (!initialized || dragging) return;
        if (window.rect.width < 1 || window.rect.height < 1)
        {
            pendingBounds = bounds;
            framePending = true;
            return;
        }
        if (initialFocusPending) FocusPlayer(true);
        float nextZoom = Mathf.Max(1, zoom);
        float scale = baseScale * nextZoom;
        var nextCenter = MapViewportMath.Frame(center, bounds, window.rect.size / scale,
            mapSize, new Vector2(140, 150) / scale);
        MoveTo(nextCenter, nextZoom, false);
    }

    void FocusPlayer() => FocusPlayer(false);

    void FocusPlayer(bool immediate)
    {
        if (window == null || window.rect.width < 1 || window.rect.height < 1)
        {
            initialFocusPending = true;
            return;
        }
        initialFocusPending = false;
        var map = owner != null ? owner.GetComponent<WorldMapController>().Progression : null;
        var node = map != null ? owner.NodeTransform(map.State.currentNodeId) : null;
        MoveTo(node != null ? MapUITransform.Bounds(node, content).center : Vector2.zero, 1, immediate);
    }

    void ToggleOverview()
    {
        if (zoom < 0.99f) { FocusPlayer(); return; }
        MoveTo(Vector2.zero, MapViewportMath.MinimumZoom(window.rect.size, mapSize, baseScale), false);
    }

    void MoveTo(Vector2 point, float nextZoom, bool immediate)
    {
        if (window == null || window.rect.width < 1 || window.rect.height < 1) return;
        baseScale = MapViewportMath.BaseScale(window.rect.size, mapSize);
        nextZoom = Mathf.Clamp(nextZoom, MapViewportMath.MinimumZoom(window.rect.size, mapSize, baseScale), 1.5f);
        targetCenter = MapViewportMath.ClampCenter(point, mapSize, window.rect.size / (baseScale * nextZoom));
        targetZoom = nextZoom;
        fromCenter = center;
        fromZoom = zoom;
        startedAt = Time.unscaledTime;
        moving = !immediate;
        if (immediate) { center = targetCenter; zoom = targetZoom; ApplyPose(); }
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (!initialized || !TryPointer(data, out lastPointer)) return;
        var hit = data.pointerPressRaycast.gameObject;
        if (hit != null && controls != null && hit.transform.IsChildOf(controls)) return;
        moving = false;
        dragging = true;
        dragPointer = data.pointerId;
        data.eligibleForClick = false;
    }

    public void OnDrag(PointerEventData data)
    {
        if (!dragging || data.pointerId != dragPointer || !TryPointer(data, out var point)) return;
        center -= (point - lastPointer) / (baseScale * zoom);
        lastPointer = point;
        ApplyPose();
    }

    public void OnEndDrag(PointerEventData data) { if (data.pointerId == dragPointer) dragging = false; }

    public void OnScroll(PointerEventData data)
    {
        if (!initialized || !TryPointer(data, out var point)) return;
        moving = false;
        float nextZoom = Mathf.Clamp(zoom * Mathf.Pow(1.12f, data.scrollDelta.y),
            MapViewportMath.MinimumZoom(window.rect.size, mapSize, baseScale), 1.5f);
        center = MapViewportMath.ZoomAt(center, point, baseScale * zoom, baseScale * nextZoom);
        zoom = nextZoom;
        ApplyPose();
        data.Use();
    }

    bool TryPointer(PointerEventData data, out Vector2 point) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle(window, data.position, data.enterEventCamera, out point);

    void OnDisable() { moving = false; dragging = false; }

    void BindControls()
    {
        if (controls == null) controls = transform.Find("MapNavigation");
        if (controls == null) return;
        if (focusButton == null) focusButton = controls.Find("Focus")?.GetComponent<Button>();
        if (overviewButton == null) overviewButton = controls.Find("Overview")?.GetComponent<Button>();
        if (overviewLabel == null && overviewButton != null) overviewLabel = overviewButton.GetComponentInChildren<TMP_Text>(true);
        if (focusButton != null)
        {
            focusButton.onClick.RemoveListener(FocusPlayer);
            focusButton.onClick.AddListener(FocusPlayer);
        }
        if (overviewButton != null)
        {
            overviewButton.onClick.RemoveListener(ToggleOverview);
            overviewButton.onClick.AddListener(ToggleOverview);
        }
    }

    // Only the authoring tools create default controls. Runtime binding preserves
    // the positions, fonts and styling saved by the artist in the prefab.
#if UNITY_EDITOR
    void CreateControls(TMP_FontAsset font)
    {
        controls = transform.Find("MapNavigation");
        if (controls != null) return;
        var obj = new GameObject("MapNavigation", typeof(RectTransform));
        obj.layer = gameObject.layer;
        controls = obj.transform;
        controls.SetParent(transform, false);
        var rect = (RectTransform)controls;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.sizeDelta = new Vector2(310, 92);
        rect.anchoredPosition = new Vector2(-24, 24);
        ButtonControl("Focus", "定位", new Vector2(-62, 24), font);
        overviewLabel = ButtonControl("Overview", "总览", new Vector2(-184, 24), font);
        var hint = Label(controls, "Hint", font);
        hint.text = "拖动地图 · 滚轮缩放";
        hint.fontSize = 16;
        hint.alignment = TextAlignmentOptions.MidlineRight;
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(1, 0);
        hint.rectTransform.anchoredPosition = new Vector2(-5, 60);
        hint.rectTransform.sizeDelta = new Vector2(300, 24);
    }

    TMP_Text ButtonControl(string name, string caption, Vector2 position, TMP_FontAsset font)
    {
        var child = controls.Find(name);
        if (child == null)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            obj.layer = gameObject.layer;
            child = obj.transform;
            child.SetParent(controls, false);
        }
        var rect = (RectTransform)child;
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
        rect.pivot = Vector2.one * 0.5f;
        rect.sizeDelta = new Vector2(108, 42);
        rect.anchoredPosition = position;
        child.GetComponent<Image>().color = new Color32(239, 232, 214, 245);
        var outline = child.GetComponent<Outline>();
        outline.effectColor = new Color32(68, 68, 57, 180);
        outline.effectDistance = new Vector2(1, -1);
        var button = child.GetComponent<Button>();
        button.targetGraphic = child.GetComponent<Image>();
        var text = Label(child, "Label", font);
        text.text = caption;
        text.fontSize = 22;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return text;
    }
    static TMP_Text Label(Transform parent, string name, TMP_FontAsset font)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.layer = parent.gameObject.layer;
            child = obj.transform;
            child.SetParent(parent, false);
        }
        var text = child.GetComponent<TMP_Text>();
        text.font = font;
        text.color = new Color32(43, 46, 38, 255);
        text.raycastTarget = false;
        return text;
    }
#endif

    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
