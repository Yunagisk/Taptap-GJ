using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(WorldMapController))]
public class WorldMapSceneView : MonoBehaviour
{
    public RectTransform mapContent;
    public Canvas canvas;
    public TMP_FontAsset font;
    public TMP_Text hpText;
    public TMP_Text timeText;
    public TMP_Text sinText;
    public TMP_Text bossCountText;
    public TMP_Text noticeText;
    public GameObject nodePanel;
    public TMP_Text nodeTitle;
    public TMP_Text nodeDescription;
    public Button completeButton;
    public bool useWhiteboxCompletion = true;
    public Image originalBackground;
    public bool showOriginalBackground;
    public MapSceneRoad repairedRoadPrefab;

    readonly Dictionary<string, MapSceneRoad> repairedRoads = new Dictionary<string, MapSceneRoad>();
    readonly Dictionary<string, RectTransform> nodeTransforms = new Dictionary<string, RectTransform>();

    MapSceneNode[] nodes;
    MapSceneRoad[] roads;
    WorldMapController controller;
    TMP_FontAsset runtimeFont;
    string displayedRequestId;

    void Start()
    {
        controller = GetComponent<WorldMapController>();
        if (controller.Progression == null) return;
        nodes = mapContent.GetComponentsInChildren<MapSceneNode>(true);
        roads = mapContent.GetComponentsInChildren<MapSceneRoad>(true);
        foreach (var node in nodes)
        {
            node.Bind(controller);
            nodeTransforms[node.nodeId] = (RectTransform)node.transform;
        }
        if (font != null && font.sourceFontFile != null)
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
            runtimeFont.isMultiAtlasTexturesEnabled = true;
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
        }
        if (originalBackground != null) originalBackground.enabled = showOriginalBackground;
        completeButton.onClick.AddListener(CompleteDisplayedNode);
        controller.StateChanged += Refresh;
        Refresh();
    }

    void LateUpdate()
    {
        if (canvas == null || mapContent == null) return;
        var area = ((RectTransform)canvas.transform).rect.size;
        mapContent.localScale = Vector3.one * Mathf.Min(area.x / 1920f, area.y / 1080f);
    }

    public void Refresh()
    {
        if (nodes == null) return;
        var map = controller.Progression;
        foreach (var node in nodes) node.Refresh(map);
        foreach (var road in roads) road.Refresh(map);
        RefreshRepairedRoads(map);
        var player = GameRoot.I.state.player;
        hpText.text = "HP " + player.hp + " / " + player.maxHp;
        string[] periods = { "早晨", "下午", "夜间" };
        timeText.text = "第 " + (map.World.time / 3 + 1) + " 天 · " + periods[map.World.time % 3] + "   T " + map.World.time;
        sinText.text = "罪恶 " + map.World.sin + " / " + map.Layout.sinThreshold;
        bossCountText.text = "原罪 " + map.State.defeatedBossNodes.Count + " / 3";
        noticeText.text = controller.Notice;
        var request = map.GetPendingRequest();
        bool showPlaceholder = request != null && request.kind != MapNodeKind.Battle
            && !(request.kind == MapNodeKind.Event && controller.WildernessCatalog != null);
        displayedRequestId = showPlaceholder ? request.requestId : null;
        nodePanel.SetActive(showPlaceholder);
        if (!showPlaceholder) return;
        var definition = map.GetNode(request.nodeId);
        nodeTitle.text = definition.displayName;
        string[] kinds = { "村庄", "普通战", "问号事件", "火堆", "原罪首领" };
        nodeDescription.text = kinds[(int)request.kind] + "\n处理中";
        completeButton.interactable = useWhiteboxCompletion;
    }

    void RefreshRepairedRoads(MapProgression map)
    {
        foreach (var road in repairedRoads.Values) road.gameObject.SetActive(false);
        if (repairedRoadPrefab == null || map.State.wildernessEvents == null
            || map.State.wildernessEvents.repairedRoads == null) return;
        foreach (var repaired in map.State.wildernessEvents.repairedRoads)
        {
            if (repaired == null || !nodeTransforms.ContainsKey(repaired.from) || !nodeTransforms.ContainsKey(repaired.to)
                || !map.GetNeighbors(repaired.from).Contains(repaired.to)) continue;
            string key = string.CompareOrdinal(repaired.from, repaired.to) < 0
                ? repaired.from + ":" + repaired.to : repaired.to + ":" + repaired.from;
            MapSceneRoad road;
            if (!repairedRoads.TryGetValue(key, out road))
            {
                road = Instantiate(repairedRoadPrefab, mapContent, false);
                road.name = "RepairedRoad";
                road.transform.SetAsFirstSibling();
                road.fromNodeId = repaired.from;
                road.toNodeId = repaired.to;
                road.from = nodeTransforms[repaired.from];
                road.to = nodeTransforms[repaired.to];
                repairedRoads.Add(key, road);
            }
            road.Refresh(map);
        }
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
        canvas.gameObject.SetActive(visible);
    }

    void OnDestroy()
    {
        if (controller != null) controller.StateChanged -= Refresh;
        if (completeButton != null) completeButton.onClick.RemoveListener(CompleteDisplayedNode);
        if (runtimeFont == null) return;
        foreach (var texture in runtimeFont.atlasTextures)
            if (texture != null) Destroy(texture);
        Destroy(runtimeFont.material);
        Destroy(runtimeFont);
    }
}
