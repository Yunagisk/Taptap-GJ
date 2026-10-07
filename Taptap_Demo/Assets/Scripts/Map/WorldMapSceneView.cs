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
        foreach (var node in nodes) node.Bind(controller);
        if (font != null && font.sourceFontFile != null)
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
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
        var player = GameRoot.I.state.player;
        hpText.text = "HP " + player.hp + " / " + player.maxHp;
        string[] periods = { "早晨", "下午", "夜间" };
        timeText.text = "第 " + (map.World.time / 3 + 1) + " 天 · " + periods[map.World.time % 3] + "   T " + map.World.time;
        sinText.text = "罪恶 " + map.World.sin + " / " + map.Layout.sinThreshold;
        bossCountText.text = "原罪 " + map.State.defeatedBossNodes.Count + " / 3";
        noticeText.text = controller.Notice;
        var request = map.GetPendingRequest();
        displayedRequestId = request?.requestId;
        nodePanel.SetActive(request != null);
        if (request == null) return;
        var definition = map.GetNode(request.nodeId);
        nodeTitle.text = definition.displayName;
        string[] kinds = { "村庄", "普通战", "问号事件", "火堆", "原罪首领" };
        nodeDescription.text = kinds[(int)request.kind] + "\n处理中";
        completeButton.interactable = useWhiteboxCompletion;
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
