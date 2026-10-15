using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Displays a generated question event; WorldMapController owns all game-state changes.</summary>
public class MapWildernessEventUI : MonoBehaviour
{
    public WorldMapController controller;
    public GameFlowController flow;
    public TextAsset catalogAsset;
    public RectTransform window;
    public TMP_Text resourcesText, eventTitle, eventDescription, eventMeta, resultText;
    public Button[] choiceButtons = new Button[2];
    public TMP_Text[] choiceLabels = new TMP_Text[2];
    public Button closeButton, victoryButton, defeatButton;
    public TMP_Text closeLabel;
    public ScrollRect resultScroll;

    public bool IsOpen => enabled && gameObject.activeInHierarchy;

    GameObject previousFocus;
    UnityAction[] chooseActions;
    bool listenersBound;
    string renderedKey;

    public bool CanShow()
    {
        var canvas = GetComponentInParent<Canvas>(true);
        return enabled && controller != null && controller.isActiveAndEnabled
            && (flow == null || !flow.IsLoading) && canvas != null && canvas.isActiveAndEnabled
            && catalogAsset != null && window != null && resourcesText != null && eventTitle != null
            && eventDescription != null && eventMeta != null && resultText != null && closeLabel != null
            && choiceButtons != null && choiceButtons.Length == 2
            && choiceLabels != null && choiceLabels.Length == 2
            && ValidButton(choiceButtons[0], false) && ValidButton(choiceButtons[1], false)
            && choiceLabels[0] != null && choiceLabels[1] != null
            && ValidButton(closeButton, false) && ValidButton(victoryButton, false)
            && ValidButton(defeatButton, false);
    }

    public bool Show()
    {
        if (!CanShow() || ActiveNode() == null || controller.WildernessCatalog == null
            || WildernessEventRules.FindDefinition(controller.WildernessCatalog, ActiveNode().templateId) == null) return false;
        if (!gameObject.activeInHierarchy && EventSystem.current != null)
            previousFocus = EventSystem.current.currentSelectedGameObject;
        renderedKey = null;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy) return false;
        Refresh();
        FitWindow();
        SelectPreferred();
        return true;
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
        if (previousFocus != null && EventSystem.current != null)
        {
            var selectable = previousFocus.GetComponent<Selectable>();
            if (selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable())
                EventSystem.current.SetSelectedGameObject(previousFocus);
        }
        previousFocus = null;
        renderedKey = null;
    }

    void OnEnable()
    {
        if (listenersBound) return;
        if (chooseActions == null)
        {
            chooseActions = new UnityAction[2];
            chooseActions[0] = () => Choose(0);
            chooseActions[1] = () => Choose(1);
        }
        for (int i = 0; i < 2 && choiceButtons != null && i < choiceButtons.Length; i++)
            Listen(choiceButtons[i], chooseActions[i], true);
        Listen(closeButton, Close, true);
        Listen(victoryButton, Victory, true);
        Listen(defeatButton, Defeat, true);
        listenersBound = true;
    }

    void OnDisable()
    {
        if (!listenersBound) return;
        for (int i = 0; i < 2 && choiceButtons != null && i < choiceButtons.Length; i++)
            Listen(choiceButtons[i], chooseActions[i], false);
        Listen(closeButton, Close, false);
        Listen(victoryButton, Victory, false);
        Listen(defeatButton, Defeat, false);
        listenersBound = false;
    }

    static void Listen(Button button, UnityAction action, bool add)
    {
        if (button == null) return;
        if (add) button.onClick.AddListener(action); else button.onClick.RemoveListener(action);
    }

    bool CanAct(Button button)
    {
        return IsOpen && controller != null && controller.isActiveAndEnabled
            && (flow == null || !flow.IsLoading) && ValidButton(button, true) && button.IsInteractable();
    }

    void Choose(int index)
    {
        if (index < 0 || index >= choiceButtons.Length || !CanAct(choiceButtons[index])) return;
        controller.ChooseWildernessEvent(index);
        Refresh();
        SelectPreferred();
    }
    void Victory() { Resolve(true, victoryButton); }
    void Defeat() { Resolve(false, defeatButton); }
    void Resolve(bool victory, Button button)
    {
        if (!CanAct(button)) return;
        controller.ResolveWildernessEncounter(victory);
        Refresh();
        SelectPreferred();
    }
    void Close()
    {
        if (!CanAct(closeButton)) return;
        var node = ActiveNode();
        if (node != null && node.encounterPending) return;
        controller.CloseWildernessEvent();
    }

    WildernessLevelState Level => GameRoot.I != null && GameRoot.I.state != null
        && GameRoot.I.state.map != null ? GameRoot.I.state.map.wildernessEvents : null;
    WildernessNodeState ActiveNode()
    {
        var level = Level;
        return level == null ? null : WildernessEventRules.FindNode(level, level.activeNodeId);
    }

    public void Refresh()
    {
        if (controller == null || GameRoot.I == null || GameRoot.I.state == null) return;
        var state = GameRoot.I.state;
        var level = Level;
        var node = ActiveNode();
        var catalog = controller.WildernessCatalog;
        var definition = node == null ? null : WildernessEventRules.FindDefinition(catalog, node.templateId);
        if (node == null || definition == null || state.world == null || state.player == null) return;
        resourcesText.text = WorldResourceRules.TimeLabel(state.world) + "    生命 " + state.player.hp
            + "/" + state.player.maxHp + "    罪恶 " + state.world.sin + "/100    罪骸 " + state.world.sinRemnant;
        eventTitle.text = definition.title;
        string bridge = !string.IsNullOrEmpty(node.bridgeFrom) && !string.IsNullOrEmpty(node.bridgeTo)
            ? "\n可修复通道：" + NodeName(node.bridgeFrom) + " — " + NodeName(node.bridgeTo) : "";
        eventDescription.text = definition.description + bridge;
        bool choosing = !node.resolved && !node.abandoned;
        bool pending = node.encounterPending;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            choiceButtons[i].gameObject.SetActive(choosing);
            choiceButtons[i].interactable = choosing;
            if (i < definition.choices.Count) choiceLabels[i].text = ChoiceSummary(definition.choices[i]);
        }
        victoryButton.gameObject.SetActive(pending);
        defeatButton.gameObject.SetActive(pending);
        victoryButton.interactable = defeatButton.interactable = pending;
        closeButton.interactable = !pending;
        ConfigureNavigation(choosing, pending);
        closeLabel.text = pending ? "先结算遭遇" : choosing ? "离开此处" : "继续探索";
        eventMeta.text = choosing ? "选择后结算；离开后，这次事件将不再出现。"
            : pending ? "遭遇一只小怪，使用下方按钮进行文字结算。" : "事件已结束，可以继续探索。";
        string result = choosing ? "" : ResultSummary(level, definition, node);
        if (pending)
        {
            string enemy = "小怪";
            if (catalog != null && catalog.enemyVariants != null)
            {
                var variant = catalog.enemyVariants.Find(value => value != null && value.id == node.enemyId);
                if (variant != null) enemy = variant.name;
            }
            result += "\n\n当前遭遇：" + enemy + " × 1";
        }
        resultText.text = Present(result);
        if (resultScroll != null) resultScroll.gameObject.SetActive(!choosing);
        var descriptionRect = eventDescription.rectTransform;
        descriptionRect.sizeDelta = new Vector2(descriptionRect.sizeDelta.x, 115);
        string key = node.id + ":" + node.chosenIndex + ":" + node.encounterResolved;
        if (key != renderedKey)
        {
            renderedKey = key;
            if (resultScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                resultScroll.verticalNormalizedPosition = 1;
            }
        }
    }

    string ResultSummary(WildernessLevelState level, WildernessEventDefinition definition, WildernessNodeState node)
    {
        if (level == null || level.history == null) return "事件已结束。";
        int choiceIndex = -1;
        for (int i = level.history.Count - 1; i >= 0; i--)
            if (level.history[i] != null && level.history[i].StartsWith(definition.title + " · ", StringComparison.Ordinal))
            { choiceIndex = i; break; }
        if (choiceIndex < 0) return "事件已结束。";
        var text = new StringBuilder(level.history[choiceIndex]);
        if (node.encounterResolved && choiceIndex + 1 < level.history.Count)
            text.Append("\n\n").Append(level.history[choiceIndex + 1]);
        return text.ToString();
    }

    string Present(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var layout = controller != null && controller.Progression != null ? controller.Progression.Layout : null;
        if (layout != null && layout.nodes != null)
            foreach (var node in layout.nodes)
                if (node != null && !string.IsNullOrEmpty(node.id) && !string.IsNullOrEmpty(node.displayName))
                    text = text.Replace(node.id, NodeName(node.id));
        return text.Replace("（文字占位）", "（文字记录）");
    }

    string NodeName(string id)
    {
        var node = controller != null && controller.Progression != null ? controller.Progression.GetNode(id) : null;
        if (node == null) return "相邻地点";
        if (node.kind != MapNodeKind.Event) return node.displayName;
        var layout = controller.Progression.Layout;
        var region = layout.regions != null ? layout.regions.Find(value => value != null && value.id == node.region) : null;
        int ordinal = 0;
        foreach (var candidate in layout.nodes)
        {
            if (candidate == null || candidate.kind != MapNodeKind.Event || candidate.region != node.region) continue;
            ordinal++;
            if (candidate.id == id) break;
        }
        return (region != null ? region.displayName : "野外") + "·问号 " + ordinal;
    }

    static string ChoiceSummary(WildernessChoiceDefinition choice)
    {
        var text = new StringBuilder(choice.label).Append("  ·  额外 ").Append(choice.timeCost).Append(" 单位时间\n");
        long total = 0;
        foreach (var outcome in choice.outcomes) total += outcome.weight;
        for (int i = 0; i < choice.outcomes.Count; i++)
        {
            var outcome = choice.outcomes[i];
            if (i != 0) text.Append(" / ");
            if (choice.outcomes.Count > 1) text.Append(Math.Round(outcome.weight * 100d / total)).Append("% ");
            if (outcome.sinDelta != 0) text.Append("罪恶 ").Append(outcome.sinDelta > 0 ? "+" : "").Append(outcome.sinDelta).Append("  ");
            if (outcome.healAmount > 0) text.Append("恢复 ").Append(outcome.healAmount).Append(" 生命  ");
            if (!string.IsNullOrEmpty(outcome.rewardText)) text.Append(outcome.rewardText).Append("  ");
            if (!string.IsNullOrEmpty(outcome.effectText)) text.Append(outcome.effectText).Append("  ");
            if (outcome.campRecoveryBonusPercent > 0) text.Append("下次营地恢复 +").Append(outcome.campRecoveryBonusPercent).Append("%  ");
            if (outcome.repairBridge) text.Append("修复通道  ");
            if (outcome.encounter) text.Append("普通遭遇（另耗时 1）");
            else if (outcome.sinDelta == 0 && outcome.healAmount == 0 && string.IsNullOrEmpty(outcome.rewardText)
                && string.IsNullOrEmpty(outcome.effectText) && !outcome.repairBridge && outcome.campRecoveryBonusPercent == 0)
                text.Append("无额外战斗");
        }
        return text.ToString().TrimEnd();
    }

    void ConfigureNavigation(bool choosing, bool pending)
    {
        var buttons = choosing ? new[] { choiceButtons[0], choiceButtons[1], closeButton }
            : pending ? new[] { victoryButton, defeatButton } : new[] { closeButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            var previous = buttons[(i + buttons.Length - 1) % buttons.Length];
            var next = buttons[(i + 1) % buttons.Length];
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = previous, selectOnLeft = previous, selectOnDown = next, selectOnRight = next };
        }
    }
    void SelectPreferred()
    {
        if (!IsOpen || EventSystem.current == null) return;
        var node = ActiveNode();
        var preferred = node != null && node.encounterPending ? victoryButton : closeButton;
        if (ValidButton(preferred, true) && preferred.IsInteractable())
            EventSystem.current.SetSelectedGameObject(preferred.gameObject);
    }

    static bool ValidButton(Button button, bool requireActive)
    {
        return button != null && button.enabled && (!requireActive || button.gameObject.activeInHierarchy);
    }

    void LateUpdate() { FitWindow(); }
    void FitWindow()
    {
        if (window == null) return;
        var canvas = GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRect == null || window.sizeDelta.x <= 0 || window.sizeDelta.y <= 0) return;
        float scale = Mathf.Max(0.1f, Mathf.Min(1f, (canvasRect.rect.width - 48) / window.sizeDelta.x,
            (canvasRect.rect.height - 48) / window.sizeDelta.y));
        window.localScale = Vector3.one * scale;
    }
}



