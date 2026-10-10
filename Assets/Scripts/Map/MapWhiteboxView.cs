using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(WorldMapController))]
public class MapWhiteboxView : MonoBehaviour
{
    public TMP_FontAsset font;
    public bool useWhiteboxCompletion = true;

    readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    readonly Dictionary<string, TMP_Text> labels = new Dictionary<string, TMP_Text>();
    readonly Dictionary<string, Outline> outlines = new Dictionary<string, Outline>();
    readonly List<Road> roads = new List<Road>();
    readonly Color ink = new Color32(39, 46, 43, 255);
    readonly Color green = new Color32(40, 117, 91, 255);
    readonly Color red = new Color32(165, 60, 63, 255);
    WorldMapController controller;
    RectTransform mapArea;
    RectTransform mapContent;
    Canvas canvas;
    TMP_FontAsset runtimeFont;
    TMP_Text resources;
    TMP_Text bossSummary;
    TMP_Text detail;
    TMP_Text notice;
    TMP_Text completionLabel;
    Button completeButton;
    string selectedNodeId;
    string displayedRequestId;
    Vector2 lastAreaSize;

    class Road
    {
        public string from;
        public string to;
        public Image image;
    }

    void Start()
    {
        controller = GetComponent<WorldMapController>();
        if (controller.Progression == null) return;
        if (font != null && font.sourceFontFile != null)
            runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
        Build();
        controller.StateChanged += Refresh;
        selectedNodeId = controller.Progression.State.currentNodeId;
        Refresh();
    }

    void OnDestroy()
    {
        if (controller != null) controller.StateChanged -= Refresh;
        if (canvas != null) Destroy(canvas.gameObject);
        if (runtimeFont != null)
        {
            foreach (var texture in runtimeFont.atlasTextures)
                if (texture != null) Destroy(texture);
            Destroy(runtimeFont.material);
            Destroy(runtimeFont);
        }
    }

    void LateUpdate()
    {
        if (mapArea == null || mapArea.rect.size == lastAreaSize) return;
        lastAreaSize = mapArea.rect.size;
        float scale = Mathf.Min(lastAreaSize.x / 1240f, lastAreaSize.y / 800f);
        mapContent.localScale = Vector3.one * scale;
    }

    void Build()
    {
        var canvasObject = new GameObject("MapWhiteboxCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        if (EventSystem.current == null)
        {
            var eventObject = new GameObject("MapEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventObject.transform.SetParent(canvas.transform, false);
        }

        var background = Panel("Background", canvas.transform, new Color32(239, 242, 240, 255));
        Stretch(background.rectTransform, Vector2.zero, Vector2.one);
        Text("Title", "原罪地图 · 白盒", canvas.transform, 28, ink, new Vector2(0.025f, 0.91f), new Vector2(0.74f, 0.97f));
        notice = Text("Notice", "", canvas.transform, 18, green, new Vector2(0.025f, 0.86f), new Vector2(0.74f, 0.91f));
        var divider = Panel("Divider", canvas.transform, new Color32(200, 209, 203, 255));
        Stretch(divider.rectTransform, new Vector2(0.765f, 0.055f), new Vector2(0.766f, 0.95f));
        mapArea = Rect("MapArea", canvas.transform);
        Stretch(mapArea, new Vector2(0.015f, 0.075f), new Vector2(0.75f, 0.855f));
        mapContent = Rect("MapContent", mapArea);
        mapContent.sizeDelta = new Vector2(1240, 800);

        foreach (var region in controller.Progression.Layout.regions)
        {
            var boss = controller.Progression.GetNode(region.bossNodeId);
            var heading = TextAt("Region_" + region.id, region.displayName, mapContent, 20, ink,
                new Vector2(boss.x + 60, 350), new Vector2(270, 40));
            heading.alignment = TextAlignmentOptions.Center;
        }

        foreach (var node in controller.Progression.Layout.nodes)
        {
            foreach (string neighborId in node.neighbors)
            {
                if (string.CompareOrdinal(node.id, neighborId) >= 0) continue;
                var neighbor = controller.Progression.GetNode(neighborId);
                var line = Panel("Road_" + node.id + "_" + neighborId, mapContent, new Color32(159, 174, 164, 255));
                Vector2 from = new Vector2(node.x, node.y), to = new Vector2(neighbor.x, neighbor.y);
                line.rectTransform.anchoredPosition = (from + to) * 0.5f;
                line.rectTransform.sizeDelta = new Vector2(Vector2.Distance(from, to), 3);
                line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                roads.Add(new Road { from = node.id, to = neighborId, image = line });
            }
        }
        foreach (var node in controller.Progression.Layout.nodes) BuildNode(node);

        Text("SidebarTitle", "探索记录", canvas.transform, 24, ink, new Vector2(0.795f, 0.88f), new Vector2(0.98f, 0.95f));
        resources = Text("Resources", "", canvas.transform, 20, ink, new Vector2(0.795f, 0.68f), new Vector2(0.98f, 0.865f));
        bossSummary = Text("BossSummary", "", canvas.transform, 18, ink, new Vector2(0.795f, 0.48f), new Vector2(0.98f, 0.66f));
        detail = Text("NodeDetail", "", canvas.transform, 19, ink, new Vector2(0.795f, 0.23f), new Vector2(0.98f, 0.45f));
        completeButton = Command("CompleteNode", "完成节点", canvas.transform, CompleteDisplayedNode,
            new Vector2(0.795f, 0.14f), new Vector2(0.98f, 0.20f));
        completionLabel = completeButton.GetComponentInChildren<TMP_Text>();
        Text("Legend", "绿色：可前往     灰色：已完成     黄色边框：当前位置     红色：原罪", canvas.transform,
            17, ink, new Vector2(0.025f, 0.015f), new Vector2(0.75f, 0.06f));
    }

    void BuildNode(MapNodeDefinition node)
    {
        var image = Panel("Node_" + node.id, mapContent, Color.white);
        image.raycastTarget = true;
        image.rectTransform.anchoredPosition = new Vector2(node.x, node.y);
        image.rectTransform.sizeDelta = new Vector2(104, 60);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.88f, 0.95f, 0.90f);
        colors.pressedColor = new Color(0.75f, 0.85f, 0.79f);
        colors.disabledColor = Color.white;
        button.colors = colors;
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(2, -2);
        outline.useGraphicAlpha = false;
        var label = Text("Label", "", image.transform, 17, ink, Vector2.zero, Vector2.one);
        label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => controller.MoveToNode(node.id));
        var trigger = image.gameObject.AddComponent<EventTrigger>();
        var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        hover.callback.AddListener(_ =>
        {
            if (!string.IsNullOrEmpty(controller.Progression.State.pendingRequestId)) return;
            selectedNodeId = node.id;
            RefreshDetail();
        });
        trigger.triggers.Add(hover);
        buttons.Add(node.id, button);
        labels.Add(node.id, label);
        outlines.Add(node.id, outline);
    }

    void Refresh()
    {
        var map = controller.Progression;
        foreach (var node in map.Layout.nodes)
        {
            var button = buttons[node.id];
            button.gameObject.SetActive(map.IsVisible(node.id));
            bool current = map.State.currentNodeId == node.id;
            bool cleared = map.State.clearedNodes.Contains(node.id);
            bool available = map.CanMove(node.id) || map.CanReenterVillage(node.id);
            button.interactable = available;
            button.image.color = cleared ? new Color32(218, 225, 220, 255) : Color.white;
            outlines[node.id].effectColor = current ? new Color32(202, 157, 39, 255)
                : available ? green : node.kind == MapNodeKind.Boss ? red : new Color32(178, 190, 181, 255);
            labels[node.id].color = node.kind == MapNodeKind.Boss ? red : available ? green : ink;
            string suffix = current ? "当前位置" : cleared ? "已完成" : available ? "可前往" : "";
            labels[node.id].text = node.displayName + (suffix.Length > 0 ? "\n<size=13>" + suffix + "</size>" : "");
        }
        foreach (var road in roads)
            road.image.gameObject.SetActive(map.IsRoadVisible(road.from, road.to));
        string[] periods = { "早晨", "下午", "夜间" };
        resources.text = "第 " + (map.World.time / 3 + 1) + " 天 · " + periods[map.World.time % 3]
            + "\n世界时间  " + map.World.time + "\n罪恶  " + map.World.sin + " / " + map.Layout.sinThreshold
            + "\n已击败原罪  " + map.State.defeatedBossNodes.Count + " / 3";
        string summary = "";
        foreach (var region in map.Layout.regions)
        {
            string status = map.State.defeatedBossNodes.Contains(region.bossNodeId) ? "已击败"
                : map.State.spawnedBossNodes.Contains(region.bossNodeId) ? "已出现" : "未出现";
            summary += map.GetNode(region.bossNodeId).displayName + "  ·  " + status + "\n";
        }
        bossSummary.text = summary.TrimEnd();
        notice.text = controller.Notice;
        var pending = map.GetPendingRequest();
        displayedRequestId = pending?.requestId;
        if (pending != null) selectedNodeId = pending.nodeId;
        completeButton.gameObject.SetActive(pending != null);
        completeButton.interactable = useWhiteboxCompletion;
        completionLabel.text = useWhiteboxCompletion ? "完成节点" : "等待节点结果";
        RefreshDetail();
    }

    void RefreshDetail()
    {
        var map = controller.Progression;
        var node = map.GetNode(selectedNodeId);
        if (node == null) return;
        bool pending = map.State.pendingNodeId == node.id && !string.IsNullOrEmpty(map.State.pendingRequestId);
        string status = pending ? "处理中" : map.CanReenterVillage(node.id) ? "点击进入村庄 · 不耗时"
            : map.State.clearedNodes.Contains(node.id) ? "已完成"
            : map.CanMove(node.id) ? "可前往 · 耗时 1" : "尚不可到达";
        string[] kinds = { "村庄", "普通战", "问号事件", "火堆", "原罪首领" };
        detail.text = node.displayName + "\n" + kinds[(int)node.kind] + "\n" + status
            + (map.IsComplete ? "\n\n白盒流程完成" : "");
    }

    void CompleteDisplayedNode()
    {
        if (!useWhiteboxCompletion || string.IsNullOrEmpty(displayedRequestId)) return;
        string requestId = displayedRequestId;
        displayedRequestId = null;
        controller.CompleteNode(requestId);
    }

    public void SetMapVisible(bool visible)
    {
        if (canvas != null) canvas.gameObject.SetActive(visible);
    }

    RectTransform Rect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    Image Panel(string name, Transform parent, Color color)
    {
        var rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    TMP_Text Text(string name, string value, Transform parent, float size, Color color, Vector2 min, Vector2 max)
    {
        var rect = Rect(name, parent);
        Stretch(rect, min, max);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (runtimeFont != null) text.font = runtimeFont;
        else if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.margin = new Vector4(5, 3, 5, 3);
        return text;
    }

    TMP_Text TextAt(string name, string value, Transform parent, float size, Color color, Vector2 position, Vector2 dimensions)
    {
        var text = Text(name, value, parent, size, color, Vector2.one * 0.5f, Vector2.one * 0.5f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = dimensions;
        return text;
    }

    Button Command(string name, string value, Transform parent, UnityEngine.Events.UnityAction action, Vector2 min, Vector2 max)
    {
        var image = Panel(name, parent, green);
        Stretch(image.rectTransform, min, max);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        var label = Text("Label", value, image.transform, 20, Color.white, Vector2.zero, Vector2.one);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
