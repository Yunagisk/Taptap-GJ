using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(BattleFlowController))]
public class BattleResultUI : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    private BattleFlowController battleFlow;
    private GameObject overlay;
    private TMP_Text title;
    private TMP_Text message;
    private Button returnButton;
    private Button restartButton;
    private TMP_Text returnLabel;
    private TMP_FontAsset runtimeFont;
    private bool returning;

    private void Awake()
    {
        battleFlow = GetComponent<BattleFlowController>();
        if (font != null && font.sourceFontFile != null)
            runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
        BuildUI();
    }

    private void OnEnable() { battleFlow.OnBattleStateChanged += ShowResult; }
    private void OnDisable() { battleFlow.OnBattleStateChanged -= ShowResult; }
    private void Start() { ShowResult(battleFlow.CurrentState); }

    private void ShowResult(BattleState state)
    {
        if (state != BattleState.Victory && state != BattleState.Defeat) return;
        bool victory = state == BattleState.Victory;
        title.text = victory ? "\u6218\u6597\u80dc\u5229" : "\u6218\u6597\u5931\u8d25";
        title.color = victory ? new Color32(125, 213, 158, 255) : new Color32(237, 130, 130, 255);
        var root = GameRoot.I;
        bool canRevive = root != null && root.battleMapTemplate != null
            && !string.IsNullOrEmpty(root.battleRequestId);
        returnButton.interactable = victory || canRevive;
        returnLabel.text = victory ? "\u8fd4\u56de\u5927\u5730\u56fe" : "\u5728\u5b58\u6863\u70b9\u590d\u6d3b";
        restartButton.gameObject.SetActive(!victory);
        returnButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(victory ? 0 : -150, -110);
        message.text = victory ? "\u6218\u6597\u5df2\u7ed3\u675f\uff0c\u8fd4\u56de\u5927\u5730\u56fe\u7ee7\u7eed\u63a2\u7d22\u3002"
            : "\u53ef\u9009\u62e9\u5728\u5b58\u6863\u70b9\u590d\u6d3b\uff0c\u6216\u91cd\u5f00\u6e38\u620f\u3002";
        if (!victory && canRevive)
        {
            var layout = root.battleMapTemplate.Load();
            var checkpoint = layout.nodes.Find(node => node.id == root.state.map.checkpointNodeId);
            if (checkpoint != null) message.text += "\n\u5b58\u6863\u70b9\uff1a" + checkpoint.displayName
                + "\uff08\u7b2c " + (root.state.map.checkpointTime / 3 + 1) + " \u5929\u65e9\u6668\uff09";
        }
        overlay.SetActive(true);
    }

    public void ReturnToWorldMap()
    {
        if (returning || (battleFlow.CurrentState != BattleState.Victory
            && battleFlow.CurrentState != BattleState.Defeat)) return;
        var root = GameRoot.I;
        string scene = root != null && !string.IsNullOrEmpty(root.battleReturnScene)
            ? root.battleReturnScene : GameFlowController.MapScene;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            message.text = "\u5927\u5730\u56fe\u573a\u666f\u672a\u52a0\u5165\u6784\u5efa\u5217\u8868\u3002";
            Debug.LogError("The return map scene is missing from Build Settings.", this);
            return;
        }
        try
        {
            bool showVillage = false;
            if (root != null && root.battleMapTemplate != null
                && !string.IsNullOrEmpty(root.battleRequestId))
            {
                var layout = root.battleMapTemplate.Load();
                var map = new MapProgression(layout, root.state.map, root.state.world,
                    root.state.map.randomSeed);
                var pending = map.GetPendingRequest();
                if (pending == null || pending.kind != MapNodeKind.Battle
                    || pending.requestId != root.battleRequestId)
                    throw new InvalidOperationException("The battle request no longer matches the map.");
                if (battleFlow.CurrentState == BattleState.Victory)
                    map.TryCompleteNode(root.battleRequestId);
                else
                {
                    if (!map.TryReviveAtCheckpoint(root.battleRequestId))
                        throw new InvalidOperationException("Cannot revive at the checkpoint.");
                    root.state.player.hp = root.state.player.maxHp;
                    showVillage = map.State.currentNodeId == layout.villageNodeId;
                }
            }
            returning = true;
            returnButton.interactable = restartButton.interactable = false;
            SceneManager.LoadSceneAsync(scene);
            if (root != null)
            {
                root.battleMapTemplate = null;
                root.battleRequestId = null;
                root.battleReturnScene = null;
                root.enterVillageOnLoad = showVillage;
            }
        }
        catch (Exception error)
        {
            returning = false;
            returnButton.interactable = restartButton.interactable = true;
            message.text = "\u8fd4\u56de\u5931\u8d25\uff0c\u8bf7\u91cd\u8bd5\u3002";
            Debug.LogException(error, this);
        }
    }

    public void RestartGame()
    {
        if (returning || battleFlow.CurrentState != BattleState.Defeat) return;
        if (!Application.CanStreamedLevelBeLoaded(GameFlowController.MenuScene))
        {
            message.text = "\u4e3b\u83dc\u5355\u573a\u666f\u672a\u52a0\u5165\u6784\u5efa\u5217\u8868\u3002";
            return;
        }
        try
        {
            returning = true;
            returnButton.interactable = restartButton.interactable = false;
            SceneManager.LoadSceneAsync(GameFlowController.MenuScene);
            var root = GameRoot.I;
            if (root == null) return;
            root.state = new GameState();
            root.enterVillageOnLoad = false;
            root.battleMapTemplate = null;
            root.battleRequestId = null;
            root.battleReturnScene = null;
        }
        catch (Exception error)
        {
            returning = false;
            returnButton.interactable = restartButton.interactable = true;
            message.text = "\u91cd\u5f00\u5931\u8d25\uff0c\u8bf7\u91cd\u8bd5\u3002";
            Debug.LogException(error, this);
        }
    }

    private void BuildUI()
    {
        overlay = new GameObject("BattleResultCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var shade = Panel("Shade", overlay.transform, new Color(0, 0, 0, 0.72f));
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = Vector2.one;
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
        var card = Panel("ResultPanel", shade.transform, new Color32(35, 40, 48, 255));
        card.rectTransform.sizeDelta = new Vector2(640, 380);
        title = Label("Title", card.transform, "", 48, new Vector2(0, 105), new Vector2(580, 80));
        message = Label("Message", card.transform, "", 24, new Vector2(0, 15), new Vector2(580, 90));
        var buttonImage = Panel("ReturnButton", card.transform, new Color32(69, 104, 88, 255));
        buttonImage.rectTransform.anchoredPosition = new Vector2(0, -110);
        buttonImage.rectTransform.sizeDelta = new Vector2(280, 64);
        returnButton = buttonImage.gameObject.AddComponent<Button>();
        returnButton.targetGraphic = buttonImage;
        returnButton.onClick.AddListener(ReturnToWorldMap);
        returnLabel = Label("Label", buttonImage.transform, "\u8fd4\u56de\u5927\u5730\u56fe", 26, Vector2.zero, new Vector2(280, 64));
        var restartImage = Panel("RestartButton", card.transform, new Color32(132, 65, 65, 255));
        restartImage.rectTransform.anchoredPosition = new Vector2(150, -110);
        restartImage.rectTransform.sizeDelta = new Vector2(280, 64);
        restartButton = restartImage.gameObject.AddComponent<Button>();
        restartButton.targetGraphic = restartImage;
        restartButton.onClick.AddListener(RestartGame);
        Label("Label", restartImage.transform, "\u91cd\u5f00\u6e38\u620f", 26, Vector2.zero, new Vector2(280, 64));
        restartButton.gameObject.SetActive(false);
        overlay.SetActive(false);
    }

    private Image Panel(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private TMP_Text Label(string name, Transform parent, string text, float size,
        Vector2 position, Vector2 dimensions)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var label = obj.GetComponent<TextMeshProUGUI>();
        label.font = runtimeFont != null ? runtimeFont : font;
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = dimensions;
        return label;
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay);
        if (runtimeFont == null) return;
        foreach (var texture in runtimeFont.atlasTextures)
            if (texture != null) Destroy(texture);
        Destroy(runtimeFont.material);
        Destroy(runtimeFont);
    }
}
