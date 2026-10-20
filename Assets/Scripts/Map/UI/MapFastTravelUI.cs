using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The map stays visible above a small confirmation card. Only the controller
// can commit a quote; this view owns camera framing and temporary route ink.
public sealed class MapFastTravelUI : MonoBehaviour
{
    WorldMapController controller;
    WorldMapSceneView view;
    GameFlowController navigationFlow;
    RectTransform window;
    TMP_Text title, cost, arrival, departure;
    readonly Dictionary<MapSceneRoad, Color> roadColors = new Dictionary<MapSceneRoad, Color>();
    readonly List<GameObject> endpoints = new List<GameObject>();
    GameObject previousFocus;
    bool hiding;
    public Button ConfirmButton { get; private set; }
    public Button CancelButton { get; private set; }
    public bool IsOpen => gameObject.activeInHierarchy && controller != null && controller.IsFastTravelConfirmationOpen;

    public static MapFastTravelUI Create(WorldMapController controller, WorldMapSceneView view)
    {
        var root = new GameObject("FastTravelConfirmation", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(MapFastTravelUI));
        root.layer = view.canvas.gameObject.layer;
        root.SetActive(false);
        root.transform.SetParent(view.canvas.transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        // The transparent surface blocks map clicks without covering the route.
        root.GetComponent<Image>().color = new Color(0, 0, 0, .008f);
        var ui = root.GetComponent<MapFastTravelUI>();
        ui.controller = controller; ui.view = view;
        ui.navigationFlow = FindObjectOfType<GameFlowController>();
        var panel = new GameObject("TravelCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic));
        panel.layer = root.layer; panel.transform.SetParent(rect, false);
        ui.window = (RectTransform)panel.transform;
        ui.window.anchorMin = ui.window.anchorMax = new Vector2(.5f, 0);
        ui.window.pivot = new Vector2(.5f, 0); ui.window.anchoredPosition = new Vector2(0, 24);
        ui.window.sizeDelta = new Vector2(690, 222);
        var frame = panel.GetComponent<BattleHudGraphic>();
        frame.fill = new Color32(231, 219, 191, 255); frame.ink = new Color32(99, 77, 48, 255); frame.stroke = 1.5f;
        ui.title = ui.Label("Destination", new Vector2(24, -14), new Vector2(642, 34), 26);
        ui.departure = ui.Label("Departure", new Vector2(24, -55), new Vector2(642, 26), 18);
        ui.cost = ui.Label("Cost", new Vector2(24, -86), new Vector2(280, 30), 23);
        ui.arrival = ui.Label("Arrival", new Vector2(310, -86), new Vector2(356, 30), 23);
        var hint = ui.Label("Hint", new Vector2(24, -124), new Vector2(642, 26), 17);
        hint.text = "仅经过已到访且已完成的节点 · 不会重进战斗或事件";
        ui.CancelButton = ui.Button("Cancel", "取消", new Vector2(-106, 34));
        ui.ConfirmButton = ui.Button("Confirm", "确认移动", new Vector2(106, 34));
        ui.CancelButton.onClick.AddListener(controller.CancelFastTravel);
        ui.ConfirmButton.onClick.AddListener(controller.ConfirmFastTravel);
        return ui;
    }

    public bool Show(MapTravelPlan plan)
    {
        if (view == null || view.viewport == null || plan == null) return false;
        ClearInk();
        bool found = false;
        Rect bounds = default;
        foreach (string id in plan.NodeIds)
        {
            var node = view.NodeTransform(id);
            if (node == null) return false;
            Rect area = MapUITransform.Bounds(node, view.mapContent);
            bounds = found ? Union(bounds, area) : area; found = true;
        }
        foreach (var road in view.mapContent.GetComponentsInChildren<MapSceneRoad>())
        {
            bool selected = false;
            for (int i = 1; i < plan.NodeIds.Count; i++)
                if (MapRevealTransition.RoadKey(road.fromNodeId, road.toNodeId)
                    == MapRevealTransition.RoadKey(plan.NodeIds[i - 1], plan.NodeIds[i])) { selected = true; break; }
            if (!selected) continue;
            roadColors[road] = road.color;
            road.color = new Color32(174, 112, 30, 255);
            if (road.TryGetPathBounds(view.mapContent, out var path)) bounds = Union(bounds, path);
        }
        bounds = Rect.MinMaxRect(bounds.xMin - 100, bounds.yMin - 75, bounds.xMax + 100, bounds.yMax + 100);
        if (!view.viewport.BeginTravelPreview(bounds)) { ClearInk(); return false; }
        Endpoint(plan.OriginId, "当前位置", new Color32(48, 104, 78, 255));
        Endpoint(plan.DestinationId, "目标位置", new Color32(166, 104, 24, 255));
        string destination = PlaceName(plan.DestinationId);
        title.text = "是否移动到「" + destination + "」？";
        departure.text = "当前位置：" + PlaceName(plan.OriginId)
            + "   ·   " + WorldResourceRules.TimeLabel(plan.DepartureTime);
        cost.text = "消耗 " + plan.TimeCost + " 单位时间";
        arrival.text = "抵达：" + WorldResourceRules.TimeLabel(plan.ArrivalTime);
        if (EventSystem.current != null) previousFocus = EventSystem.current.currentSelectedGameObject;
        transform.SetAsLastSibling(); gameObject.SetActive(true); FitWindow();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(CancelButton.gameObject);
        return gameObject.activeInHierarchy;
    }

    string PlaceName(string id)
    {
        var map = controller.Progression;
        var node = map.GetNode(id);
        var region = map.Layout.regions.Find(value => value.id == node.region);
        return region != null ? region.displayName + " · " + node.displayName : node.displayName;
    }

    void Endpoint(string id, string caption, Color ink)
    {
        var node = view.NodeTransform(id);
        var root = new GameObject("TravelEndpoint_" + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic));
        root.layer = view.gameObject.layer; root.transform.SetParent(view.mapContent, false);
        var rect = (RectTransform)root.transform;
        var area = MapUITransform.Bounds(node, view.mapContent);
        rect.anchoredPosition = area.center; rect.sizeDelta = area.size + Vector2.one * 26;
        var graphic = root.GetComponent<BattleHudGraphic>();
        graphic.shape = BattleHudGraphic.Shape.Diamond; graphic.fill = Color.clear; graphic.ink = ink; graphic.stroke = 3;
        graphic.raycastTarget = false;
        var labelRoot = new GameObject("Location", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelRoot.layer = root.layer; labelRoot.transform.SetParent(rect, false);
        var label = labelRoot.GetComponent<TextMeshProUGUI>();
        label.font = title.font; label.fontSize = 25; label.color = ink; label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center; label.text = caption;
        label.rectTransform.anchoredPosition = new Vector2(0, rect.sizeDelta.y * .5f + 22);
        label.rectTransform.sizeDelta = new Vector2(200, 38);
        endpoints.Add(root);
    }
    public void Hide(bool restoreView)
    {
        if (hiding) return;
        hiding = true;
        ClearInk();
        if (view != null && view.viewport != null) view.viewport.EndTravelPreview(restoreView);
        gameObject.SetActive(false);
        if (restoreView && previousFocus != null && previousFocus.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousFocus);
        previousFocus = null; hiding = false;
    }
    void ClearInk()
    {
        foreach (var pair in roadColors) if (pair.Key != null) pair.Key.color = pair.Value;
        roadColors.Clear();
        foreach (var endpoint in endpoints) if (endpoint != null) { endpoint.SetActive(false); Destroy(endpoint); }
        endpoints.Clear();
    }
    void Update()
    {
        if (IsOpen && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)
            && (navigationFlow == null || !navigationFlow.isActiveAndEnabled))) controller.CancelFastTravel();
    }
    void LateUpdate()
    {
        FitWindow();
        if (view == null || view.mapContent == null) return;
        float scale = Mathf.Max(.01f, Mathf.Abs(view.mapContent.localScale.x));
        foreach (var endpoint in endpoints)
        {
            if (endpoint == null) continue;
            var label = endpoint.GetComponentInChildren<TMP_Text>();
            label.rectTransform.localScale = Vector3.one / scale;
            label.rectTransform.anchoredPosition = new Vector2(0, ((RectTransform)endpoint.transform).sizeDelta.y * .5f + 22 / scale);
        }
    }
    void OnDisable() { if (!hiding && controller != null) controller.CancelFastTravel(); }
    void OnDestroy() => ClearInk();
    void FitWindow()
    {
        if (window == null || view == null || view.canvas == null) return;
        var area = ((RectTransform)view.canvas.transform).rect.size;
        window.localScale = Vector3.one * Mathf.Min(1, Mathf.Max(.1f, (area.x - 48) / window.sizeDelta.x),
            Mathf.Max(.1f, area.y * .29f / window.sizeDelta.y));
    }
    TMP_Text Label(string name, Vector2 position, Vector2 size, float fontSize)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.layer = gameObject.layer; root.transform.SetParent(window, false);
        var label = root.GetComponent<TextMeshProUGUI>();
        label.font = SharedUIFont.Get(view.font); label.fontSize = fontSize; label.color = new Color32(51, 41, 29, 255);
        label.raycastTarget = false; label.enableWordWrapping = false;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 1);
        label.rectTransform.pivot = new Vector2(0, 1); label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
        return label;
    }
    Button Button(string name, string caption, Vector2 position)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic), typeof(Button));
        root.layer = gameObject.layer; root.transform.SetParent(window, false);
        var rect = (RectTransform)root.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(188, 44);
        var graphic = root.GetComponent<BattleHudGraphic>(); graphic.fill = new Color32(218, 203, 171, 255);
        graphic.ink = new Color32(105, 80, 45, 255); graphic.stroke = 1;
        var button = root.GetComponent<Button>(); button.targetGraphic = graphic;
        var labelRoot = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelRoot.layer = root.layer; labelRoot.transform.SetParent(rect, false);
        var label = labelRoot.GetComponent<TextMeshProUGUI>(); label.font = SharedUIFont.Get(view.font);
        label.fontSize = 23; label.color = new Color32(51, 41, 29, 255); label.text = caption;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.rectTransform.sizeDelta = rect.sizeDelta;
        return button;
    }
    static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
}
