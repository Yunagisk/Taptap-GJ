using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private bool playingSlothAftermath;
    private Coroutine pendingResult;
    private RectTransform resultCard;
    private CanvasGroup resultVisibility;
    private BattleHudGraphic cardFrame, primaryFrame;
    private BattleResultEmblem emblem;
    private readonly TMP_Text[] statLabels = new TMP_Text[3];
    private readonly TMP_Text[] statValues = new TMP_Text[3];
    private int startingRemnant;
    private bool rewardBaselineCaptured;
    private float presentedAt;
    private static readonly Color Gold = new Color32(232, 202, 145, 255);
    private static readonly Color DefeatInk = new Color32(211, 151, 131, 255);

    private void Awake()
    {
        battleFlow = GetComponent<BattleFlowController>();
        CaptureRewardBaseline();
        if (font != null)
            runtimeFont = SharedUIFont.Get(font);
        BuildUI();
    }

    private void OnEnable() { battleFlow.OnBattleStateChanged += ShowResult; }
    private void OnDisable()
    {
        battleFlow.OnBattleStateChanged -= ShowResult;
        if (pendingResult != null) StopCoroutine(pendingResult);
        pendingResult = null;
    }
    private void Start() { ShowResult(battleFlow.CurrentState); }

    private void ShowResult(BattleState state)
    {
        if (pendingResult != null) StopCoroutine(pendingResult);
        pendingResult = null;
        if (state != BattleState.Victory && state != BattleState.Defeat)
        {
            CaptureRewardBaseline();
            overlay.SetActive(false);
            return;
        }
        if (state == BattleState.Victory && EnemyDefeatSpritePlayback.AnyPlayingIn(gameObject.scene))
        {
            overlay.SetActive(false);
            pendingResult = StartCoroutine(ShowAfterDeath(state));
            return;
        }
        PresentResult(state);
    }

    private void CaptureRewardBaseline()
    {
        var world = GameRoot.I?.state?.world;
        if (rewardBaselineCaptured || world == null) return;
        startingRemnant = world.sinRemnant;
        rewardBaselineCaptured = true;
    }

    private IEnumerator ShowAfterDeath(BattleState state)
    {
        while (battleFlow.CurrentState == state && EnemyDefeatSpritePlayback.AnyPlayingIn(gameObject.scene))
            yield return null;
        pendingResult = null;
        if (isActiveAndEnabled && battleFlow.CurrentState == state) PresentResult(state);
    }

    private void PresentResult(BattleState state)
    {
        bool victory = state == BattleState.Victory;
        title.text = victory ? "\u6218\u6597\u80dc\u5229" : "\u6218\u6597\u5931\u8d25";
        Color accent = victory ? Gold : DefeatInk;
        title.color = emblem.color = accent;
        emblem.SetVictory(victory);
        cardFrame.ink = victory ? new Color32(151, 127, 87, 255) : new Color32(141, 98, 82, 255);
        cardFrame.SetVerticesDirty();
        primaryFrame.fill = victory ? new Color32(177, 148, 92, 255) : new Color32(151, 103, 82, 255);
        primaryFrame.ink = accent;
        primaryFrame.SetVerticesDirty();
        var root = GameRoot.I;
        GameState morning = null;
        bool canRevive = root != null && root.battleMapTemplate != null
            && root.SaveSlot.TryLoad(root.battleMapTemplate.Load(), out morning, out _);
        bool slothVictory = victory && GetComponent<SmallMonsterEncounterSetup>()?.IsSlothEncounter == true;
        returnButton.interactable = victory || canRevive;
        returnLabel.color = victory ? new Color32(30, 24, 18, 255)
            : canRevive ? new Color32(244, 226, 193, 255) : new Color32(141, 128, 109, 255);
        returnLabel.text = victory ? "\u8fd4\u56de\u5927\u5730\u56fe" : "回到今天早晨";
        if (slothVictory) returnLabel.text = "继续";
        restartButton.gameObject.SetActive(!victory);
        returnButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(victory ? 0 : -158, -186);
        message.text = victory ? "\u6218\u6597\u5df2\u7ed3\u675f\uff0c\u8fd4\u56de\u5927\u5730\u56fe\u7ee7\u7eed\u63a2\u7d22\u3002"
            : canRevive ? "读取今天早晨的自动存档，重新开始这一天。" : "暂无可用的清晨存档，请重开游戏。";
        if (slothVictory) message.text = "尘雾渐渐散去。西奥多倒在碎屑间，仍有呼吸。";
        if (victory && battleFlow.Guide != null)
            message.text = "至少这条路上的麻烦已经解决了。\n返回埃尔南多，向哈罗德复命。";
        if (!victory && canRevive)
        {
            var layout = root.battleMapTemplate.Load();
            var checkpoint = layout.nodes.Find(node => node.id == morning.map.currentNodeId);
            if (checkpoint != null) message.text += "\n第 " + WorldResourceRules.Day(morning.world)
                + " 天早晨 · " + checkpoint.displayName;
        }
        var player = battleFlow.Player;
        int maximumHp = player != null ? player.MaxHP : root?.state?.player?.maxHp ?? 100;
        statLabels[0].text = "交战回合";
        statValues[0].text = Mathf.Max(1, battleFlow.RoundNumber) + " 回合";
        statLabels[1].text = "剩余生命";
        statValues[1].text = (victory && player != null ? player.CurrentHP : 0) + " / " + maximumHp;
        statLabels[2].text = "获得罪骸";
        statValues[2].text = victory ? "+ " + Mathf.Max(0, (root?.state?.world?.sinRemnant ?? 0) - startingRemnant) : "";
        statValues[2].color = accent;
        statLabels[2].gameObject.SetActive(victory);
        statValues[2].gameObject.SetActive(victory);
        for (int i = 0; i < (victory ? 3 : 2); i++)
        {
            float x = victory ? (i - 1) * 218 : (i - .5f) * 280;
            statLabels[i].rectTransform.anchoredPosition = new Vector2(x, 27);
            statValues[i].rectTransform.anchoredPosition = new Vector2(x, -10);
        }
        ((RectTransform)resultCard.Find("LeftStatRule")).anchoredPosition = new Vector2(victory ? -109 : 0, 5);
        resultCard.Find("RightStatRule").gameObject.SetActive(victory);
        presentedAt = Time.unscaledTime;
        resultVisibility.alpha = 0;
        resultVisibility.interactable = false;
        overlay.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(returnButton.interactable ? returnButton.gameObject : restartButton.gameObject);
    }

    private void Update()
    {
        if (overlay == null || !overlay.activeSelf) return;
        float phase = Mathf.Clamp01((Time.unscaledTime - presentedAt) / .24f);
        float eased = phase * phase * (3 - 2 * phase);
        resultVisibility.alpha = eased;
        resultVisibility.interactable = phase >= 1;
        resultCard.anchoredPosition = new Vector2(0, Mathf.Lerp(-14, 0, eased));
        var area = ((RectTransform)overlay.transform).rect.size;
        resultCard.localScale = Vector3.one * Mathf.Min(1, Mathf.Max(.1f, (area.x - 48) / resultCard.sizeDelta.x),
            Mathf.Max(.1f, (area.y - 48) / resultCard.sizeDelta.y));
    }

    public void ReturnToWorldMap()
    {
        if (returning || playingSlothAftermath || (battleFlow.CurrentState != BattleState.Victory
            && battleFlow.CurrentState != BattleState.Defeat)) return;
        var root = GameRoot.I;
        if (battleFlow.CurrentState == BattleState.Defeat)
        {
            bool started = DayStartLoadTransition.TryBegin(root != null ? root.battleMapTemplate : null, error =>
            {
                returning = false;
                returnButton.interactable = restartButton.interactable = true;
                message.text = error;
            });
            if (started)
            {
                returning = true;
                returnButton.interactable = restartButton.interactable = false;
            }
            return;
        }
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
            if (root != null && root.battleMapTemplate != null
                && !string.IsNullOrEmpty(root.battleRequestId))
            {
                var layout = root.battleMapTemplate.Load();
                var map = new MapProgression(layout, root.state.map, root.state.world,
                    root.state.map.randomSeed);
                var pending = map.GetPendingRequest();
                if (pending == null || (pending.kind != MapNodeKind.Battle && pending.kind != MapNodeKind.Boss)
                    || pending.requestId != root.battleRequestId)
                    throw new InvalidOperationException("The battle request no longer matches the map.");
                if (battleFlow.CurrentState == BattleState.Victory && SlothChapterRules.IsSlothRequest(pending)
                    && root.state.story.slothFate == SlothFate.Undecided)
                {
                    PlaySlothAftermath();
                    return;
                }
                if (battleFlow.CurrentState == BattleState.Victory)
                {
                    var before = MapRevealTransition.Capture(map);
                    if (!map.TryCompleteNode(root.battleRequestId))
                        throw new InvalidOperationException("Cannot complete the battle request.");
                    MainStoryRules.OnNodeCompleted(root.state, pending);
                    root.pendingMapReveal = MapRevealTransition.AfterCompletion(map, before, pending.nodeId);
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
                root.enterVillageOnLoad = false;
            }
        }
        catch (Exception error)
        {
            returning = false;
            playingSlothAftermath = false;
            returnButton.interactable = restartButton.interactable = true;
            overlay.SetActive(true);
            message.text = "\u8fd4\u56de\u5931\u8d25\uff0c\u8bf7\u91cd\u8bd5\u3002";
            Debug.LogException(error, this);
        }
    }

    private void PlaySlothAftermath()
    {
        playingSlothAftermath = true;
        returnButton.interactable = restartButton.interactable = false;
        overlay.SetActive(false);
        bool started = SlothChapterPresentation.PlayAfterBattle(transform, () =>
        {
            if (this == null) return;
            playingSlothAftermath = false;
            var root = GameRoot.I;
            if (root == null || root.state.story.slothFate == SlothFate.Undecided)
            {
                RestoreAftermathFailure();
                return;
            }
            // The decision and its complete dialogue finish before paying time or clearing the node.
            ReturnToWorldMap();
        });
        if (!started) RestoreAftermathFailure();
    }

    private void RestoreAftermathFailure()
    {
        playingSlothAftermath = false;
        returnButton.interactable = restartButton.interactable = true;
        overlay.SetActive(true);
        message.text = "未能继续战后对话，请重试。";
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
            root.pendingMapReveal = null;
            root.enterVillageOnLoad = false;
            root.villageMapTemplate = null;
            root.villageReturnScene = null;
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
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        resultVisibility = overlay.GetComponent<CanvasGroup>();
        var shade = Panel("Shade", overlay.transform, new Color32(14, 12, 10, 180));
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = Vector2.one;
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
        var shadow = Frame("CardShadow", shade.transform, new Vector2(0, -10), new Vector2(818, 532),
            new Color32(0, 0, 0, 85), Color.clear);
        shadow.stroke = 0;
        cardFrame = Frame("ResultPanel", shade.transform, Vector2.zero, new Vector2(800, 520),
            new Color32(39, 32, 26, 255), new Color32(151, 127, 87, 255));
        resultCard = cardFrame.rectTransform;
        var inner = Frame("InnerFrame", resultCard, Vector2.zero, new Vector2(768, 488),
            Color.clear, new Color32(115, 95, 68, 140));
        inner.stroke = 1.15f;
        var heading = Label("Heading", resultCard, "战 斗 结 算", 17, new Vector2(0, 226), new Vector2(360, 26));
        heading.color = new Color32(156, 138, 108, 255);
        var crest = new GameObject("BattleEmblem", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleResultEmblem));
        crest.transform.SetParent(resultCard, false);
        emblem = crest.GetComponent<BattleResultEmblem>();
        emblem.rectTransform.anchoredPosition = new Vector2(0, 174);
        emblem.rectTransform.sizeDelta = new Vector2(70, 70);
        emblem.raycastTarget = false;
        Rule(resultCard, "LeftCrestRule", new Vector2(-190, 174), new Vector2(210, 1));
        Rule(resultCard, "RightCrestRule", new Vector2(190, 174), new Vector2(210, 1));
        title = Label("Title", resultCard, "", 52, new Vector2(0, 105), new Vector2(660, 72));
        title.characterSpacing = 7;
        Rule(resultCard, "SummaryRule", new Vector2(0, 55), new Vector2(640, 1));
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 218;
            statLabels[i] = Label("StatLabel_" + i, resultCard, "", 18, new Vector2(x, 27), new Vector2(204, 26));
            statLabels[i].color = new Color32(158, 143, 118, 255);
            statValues[i] = Label("StatValue_" + i, resultCard, "", 28, new Vector2(x, -10), new Vector2(204, 42));
        }
        Rule(resultCard, "LeftStatRule", new Vector2(-109, 5), new Vector2(1, 42));
        Rule(resultCard, "RightStatRule", new Vector2(109, 5), new Vector2(1, 42));
        message = Label("Message", resultCard, "", 23, new Vector2(0, -84), new Vector2(656, 82));
        message.enableAutoSizing = true; message.fontSizeMin = 19; message.fontSizeMax = 23;
        message.color = new Color32(199, 186, 161, 255);
        Rule(resultCard, "ActionRule", new Vector2(0, -139), new Vector2(640, 1));
        primaryFrame = Frame("ReturnButton", resultCard, new Vector2(0, -186), new Vector2(284, 62),
            new Color32(177, 148, 92, 255), Gold);
        returnButton = MakeButton(primaryFrame);
        returnButton.onClick.AddListener(ReturnToWorldMap);
        returnLabel = Label("Label", primaryFrame.transform, "\u8fd4\u56de\u5927\u5730\u56fe", 26, Vector2.zero, new Vector2(260, 52));
        returnLabel.color = new Color32(30, 24, 18, 255);
        var restartFrame = Frame("RestartButton", resultCard, new Vector2(158, -186), new Vector2(284, 62),
            new Color32(48, 39, 31, 255), new Color32(139, 117, 84, 255));
        restartButton = MakeButton(restartFrame);
        restartButton.onClick.AddListener(RestartGame);
        Label("Label", restartFrame.transform, "\u91cd\u5f00\u6e38\u620f", 26, Vector2.zero, new Vector2(260, 52));
        restartButton.gameObject.SetActive(false);
        overlay.SetActive(false);
    }

    private BattleHudGraphic Frame(string name, Transform parent, Vector2 position, Vector2 size, Color fill, Color ink)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic));
        obj.transform.SetParent(parent, false);
        var graphic = obj.GetComponent<BattleHudGraphic>();
        graphic.rectTransform.anchoredPosition = position; graphic.rectTransform.sizeDelta = size;
        graphic.fill = fill; graphic.ink = ink; graphic.stroke = 1.4f; graphic.raycastTarget = false;
        return graphic;
    }

    private void Rule(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(InventoryInkGraphic));
        obj.transform.SetParent(parent, false);
        var line = obj.GetComponent<InventoryInkGraphic>();
        line.rectTransform.anchoredPosition = position; line.rectTransform.sizeDelta = size;
        line.shape = size.x > size.y ? InventoryInkGraphic.Shape.Rule : InventoryInkGraphic.Shape.VerticalRule;
        line.color = new Color32(132, 112, 80, 175); line.stroke = 1.4f; line.raycastTarget = false;
    }

    private Button MakeButton(BattleHudGraphic graphic)
    {
        graphic.raycastTarget = true;
        var button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        var colors = button.colors;
        colors.highlightedColor = new Color32(255, 236, 197, 255);
        colors.pressedColor = new Color32(178, 153, 110, 255);
        colors.selectedColor = new Color32(255, 236, 197, 255);
        colors.disabledColor = new Color32(126, 118, 104, 255);
        colors.fadeDuration = .12f; button.colors = colors;
        return button;
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
        label.color = new Color32(227, 214, 188, 255);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = dimensions;
        return label;
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay);
    }
}
