using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(WorldMapController))]
public class WorldMapSceneView : MonoBehaviour
{
    public RectTransform mapContent;
    public WorldMapVisualLayout visualLayout;
    public WorldMapViewport viewport;
    public Image villageArtwork;
    public MapCurrentLocationMarker currentLocationMarker;
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
<<<<<<< HEAD
    public MapSceneRoad repairedRoadPrefab;

    readonly Dictionary<string, MapSceneRoad> repairedRoads = new Dictionary<string, MapSceneRoad>();
    readonly Dictionary<string, RectTransform> nodeTransforms = new Dictionary<string, RectTransform>();
=======
    [Header("Discovery animation")]
    [Min(0.1f)] public float revealDuration = 1;
    [Tooltip("The crack and grey fade continue while the roads flow outward.")]
    [Range(0.1f, 1)] public float completionPortion = 0.9f;
    [Range(0, 0.4f)] public float roadRevealStartPortion = 0.12f;
    [Range(0.45f, 0.9f)] public float roadRevealPortion = 0.72f;

    public bool IsRevealing { get; private set; }
>>>>>>> origin/Feature/MapFunctional

    MapSceneNode[] nodes;
    MapSceneRoad[] roads;
    WorldMapController controller;
    TMP_FontAsset runtimeFont;
    string displayedRequestId;
    readonly Dictionary<string, MapSceneNode> nodeById = new Dictionary<string, MapSceneNode>();
    readonly Dictionary<string, MapSceneRoad> roadById = new Dictionary<string, MapSceneRoad>();
    readonly List<RoadAnimation> revealingRoads = new List<RoadAnimation>();
    readonly List<NodeAnimation> revealingNodes = new List<NodeAnimation>();
    float revealStartedAt;
    bool waitingForRevealPaths;
    MapSceneNode completingNode;
    string framedNodeId;

    sealed class RoadAnimation
    {
        public MapSceneRoad road;
        public string originId, targetId;
    }

    sealed class NodeAnimation
    {
        public MapSceneNode node;
        public RoadAnimation arrival;
    }

    void Start()
    {
        controller = GetComponent<WorldMapController>();
        if (controller.Progression == null) return;
        if (canvas == null) canvas = GetComponentInParent<Canvas>(true);
        if (visualLayout == null) visualLayout = controller.template.visualLayout;
        nodes = mapContent.GetComponentsInChildren<MapSceneNode>(true);
        roads = mapContent.GetComponentsInChildren<MapSceneRoad>(true);
<<<<<<< HEAD
        foreach (var node in nodes)
        {
            node.Bind(controller);
            nodeTransforms[node.nodeId] = (RectTransform)node.transform;
        }
        if (font != null && font.sourceFontFile != null)
=======
        foreach (var node in nodes) { node.Bind(controller); nodeById[node.nodeId] = node; }
        foreach (var road in roads) roadById[MapRevealTransition.RoadKey(road.fromNodeId, road.toNodeId)] = road;
        ApplyVisualLayout(controller.Progression.Layout);
        viewport = WorldMapViewport.Configure(this);
        if (canvas != null && font != null && font.sourceFontFile != null)
>>>>>>> origin/Feature/MapFunctional
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

    void Update()
    {
        if (!IsRevealing) return;
        if (waitingForRevealPaths)
        {
            foreach (var entry in revealingRoads)
                if (entry.road != null && !entry.road.IsGeometryReady) return;
            // Route planning is done before the animation clock starts. Slow scene
            // initialization must not skip the beginning of the flowing reveal.
            revealStartedAt = Time.unscaledTime;
            waitingForRevealPaths = false;
            FrameDiscovery();
        }
        float elapsed = Time.unscaledTime - revealStartedAt;
        float completionProgress = MapRevealMath.CompletionProgress(elapsed, revealDuration, completionPortion);
        float roadPhase = MapRevealMath.RoadPhase(elapsed, revealDuration, roadRevealPortion, roadRevealStartPortion);
        float roadProgress = MapRevealMath.RoadProgress(elapsed, revealDuration, roadRevealPortion, roadRevealStartPortion);
        float nodeProgress = MapRevealMath.NodeProgress(elapsed, revealDuration, roadRevealPortion);
        if (completingNode != null) completingNode.SetCompletionProgress(completionProgress);
        foreach (var entry in revealingRoads)
            if (entry.road != null) entry.road.SetRevealProgress(roadProgress, entry.originId, roadPhase);
        foreach (var entry in revealingNodes)
        {
            if (entry.node == null || nodeProgress <= 0) continue;
            Vector3 direction = Vector3.right;
            if (entry.arrival.road != null) entry.arrival.road.TryGetArrivalDirection(entry.node.nodeId, out direction);
            entry.node.SetRevealProgress(nodeProgress, direction);
        }
        bool completionOnly = revealingRoads.Count == 0 && revealingNodes.Count == 0;
        if (elapsed >= revealDuration || (completionOnly && completionProgress >= 1)) FinishReveal();
    }

    void BeginReveal(MapRevealTransition transition)
    {
        if (nodeById.TryGetValue(transition.sourceId, out completingNode)) completingNode.PrepareCompletion();
        foreach (var delta in transition.roads)
        {
            if (!roadById.TryGetValue(MapRevealTransition.RoadKey(delta.fromId, delta.toId), out var road)) continue;
            revealingRoads.Add(new RoadAnimation { road = road, originId = delta.originId, targetId = delta.TargetId });
            road.SetRevealProgress(0, delta.originId);
        }
        foreach (string id in transition.nodeIds)
        {
            if (!nodeById.TryGetValue(id, out var node)) continue;
            var arrival = revealingRoads.Find(entry => entry.targetId == id);
            if (arrival == null) continue;
            node.PrepareReveal();
            revealingNodes.Add(new NodeAnimation { node = node, arrival = arrival });
        }
        IsRevealing = completingNode != null || revealingRoads.Count > 0 || revealingNodes.Count > 0;
        if (!IsRevealing) return;
        foreach (var node in nodes) node.SetRevealInputBlocked(true);
        waitingForRevealPaths = true;
    }

    void FinishReveal()
    {
        if (completingNode != null) completingNode.SetCompletionProgress(1);
        completingNode = null;
        foreach (var entry in revealingRoads)
            if (entry.road != null) entry.road.SetRevealProgress(1, entry.originId);
        foreach (var entry in revealingNodes)
            if (entry.node != null) entry.node.CompleteReveal();
        if (nodes != null)
            foreach (var node in nodes) if (node != null) node.SetRevealInputBlocked(false);
        revealingRoads.Clear();
        revealingNodes.Clear();
        IsRevealing = false;
        waitingForRevealPaths = false;
    }

    void OnDisable() => FinishReveal();

    public RectTransform NodeTransform(string id) =>
        id != null && nodeById.TryGetValue(id, out var node) ? (RectTransform)node.transform : null;

    public void ApplyVisualLayout(MapLayout map)
    {
        if (visualLayout == null) return;
        visualLayout.Validate(map);
        foreach (var node in mapContent.GetComponentsInChildren<MapSceneNode>(true))
            ((RectTransform)node.transform).anchoredPosition = visualLayout.PositionFor(map.nodes.Find(entry => entry.id == node.nodeId));
    }

    void FrameDiscovery()
    {
        if (viewport == null) return;
        bool found = false;
        Rect bounds = default;
        if (completingNode != null)
        {
            bounds = MapUITransform.Bounds((RectTransform)completingNode.transform, mapContent);
            found = true;
        }
        foreach (var entry in revealingRoads)
        {
            if (entry.road == null || !entry.road.TryGetPathBounds(mapContent, out var roadBounds)) continue;
            bounds = found ? Union(bounds, roadBounds) : roadBounds;
            found = true;
        }
        foreach (var entry in revealingNodes)
        {
            if (entry.node == null) continue;
            Rect nodeBounds = MapUITransform.Bounds((RectTransform)entry.node.transform, mapContent);
            bounds = found ? Union(bounds, nodeBounds) : nodeBounds;
            found = true;
        }
        if (found) viewport.EnsureVisible(bounds);
    }

    static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
        Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    public void Refresh()
    {
        if (nodes == null) return;
        var map = controller.Progression;
        var root = GameRoot.I;
        var transition = root.pendingMapReveal;
        root.pendingMapReveal = null;
        if (transition != null && !transition.Matches(map)) transition = null;
        if (transition != null) FinishReveal();
        foreach (var node in nodes) node.Refresh(map);
        foreach (var road in roads) road.Refresh(map);
<<<<<<< HEAD
        RefreshRepairedRoads(map);
=======
        if (currentLocationMarker != null) currentLocationMarker.Refresh(this, map, transition != null);
        if (viewport != null && framedNodeId != map.State.currentNodeId)
        {
            var current = NodeTransform(map.State.currentNodeId);
            if (current != null) viewport.EnsureVisible(MapUITransform.Bounds(current, mapContent));
            framedNodeId = map.State.currentNodeId;
        }
        if (transition != null) BeginReveal(transition);
>>>>>>> origin/Feature/MapFunctional
        var player = GameRoot.I.state.player;
        if (hpText != null) hpText.text = player.hp + " / " + player.maxHp;
        string[] periods = { "早晨", "下午", "夜间" };
        if (timeText != null) timeText.text = "第 " + (map.World.time / 3 + 1) + " 天 · " + periods[map.World.time % 3] + "   T " + map.World.time;
        if (sinText != null) sinText.text = "罪恶 " + map.World.sin + " / " + map.Layout.sinThreshold;
        if (bossCountText != null) bossCountText.text = "原罪 " + map.State.defeatedBossNodes.Count + " / 3";
        noticeText.text = controller.Notice;
        var request = map.GetPendingRequest();
<<<<<<< HEAD
        bool showPlaceholder = request != null && request.kind != MapNodeKind.Battle
            && !(request.kind == MapNodeKind.Event && controller.WildernessCatalog != null);
=======
        bool showPlaceholder = request != null && request.kind != MapNodeKind.Battle && !controller.IsWaitingToEnter;
>>>>>>> origin/Feature/MapFunctional
        displayedRequestId = showPlaceholder ? request.requestId : null;
        nodePanel.SetActive(showPlaceholder);
        if (!showPlaceholder) return;
        var definition = map.GetNode(request.nodeId);
        nodeTitle.text = definition.displayName;
        string[] kinds = { "村庄", "普通战", "问号事件", "火堆", "原罪首领" };
        nodeDescription.text = kinds[(int)request.kind] + "\n处理中";
        completeButton.interactable = useWhiteboxCompletion && !controller.IsEnteringNode;
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
