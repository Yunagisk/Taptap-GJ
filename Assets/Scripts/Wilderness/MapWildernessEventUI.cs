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
    public bool IsChoosing => ActiveNode() != null && !ActiveNode().resolved && !ActiveNode().abandoned
        && !ActiveNode().encounterPending;
    public WildernessLeaveWarning LeaveWarning { get; private set; }
    public bool IsLeaveConfirmationOpen => LeaveWarning != null && LeaveWarning.IsOpen;

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
            && ValidButton(defeatButton, false) && resultScroll != null;
    }

    public bool Show()
    {
        if (!CanShow() || ActiveNode() == null || controller.WildernessCatalog == null
            || WildernessEventRules.FindDefinition(controller.WildernessCatalog, ActiveNode().templateId) == null) return false;
        if (!gameObject.activeInHierarchy && EventSystem.current != null)
            previousFocus = EventSystem.current.currentSelectedGameObject;
        renderedKey = null;
        if (LeaveWarning == null)
        {
            LeaveWarning = closeButton.GetComponent<WildernessLeaveWarning>()
                ?? closeButton.gameObject.AddComponent<WildernessLeaveWarning>();
            LeaveWarning.Bind(this);
        }
        LeaveWarning.Hide();
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
        if (LeaveWarning != null) LeaveWarning.Hide();
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
        return IsOpen && !IsLeaveConfirmationOpen && controller != null && controller.isActiveAndEnabled
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

    public void HandleBack()
    {
        if (IsLeaveConfirmationOpen) LeaveWarning.Cancel();
        else Close();
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
        state.player.NormalizeResolve();
        resourcesText.text = WorldResourceRules.TimeLabel(state.world) + "    生命 " + state.player.hp
            + "/" + state.player.maxHp + "    罪恶 " + state.world.sin + "/100    罪骸 " + state.world.sinRemnant
            + "    决心 " + state.player.resolve + "/" + state.player.maxResolve;
        resourcesText.fontSize = 23;
        eventTitle.text = definition.title;
        eventDescription.gameObject.SetActive(false);
        bool choosing = !node.resolved && !node.abandoned;
        bool pending = node.encounterPending;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            choiceButtons[i].gameObject.SetActive(choosing);
            choiceButtons[i].interactable = choosing;
            if (i < definition.choices.Count) choiceLabels[i].text = WildernessEventRules.DescribeChoice(definition.choices[i]);
        }
        victoryButton.gameObject.SetActive(pending);
        defeatButton.gameObject.SetActive(pending);
        victoryButton.interactable = defeatButton.interactable = pending;
        closeButton.interactable = !pending;
        ConfigureNavigation(choosing, pending);
        closeLabel.text = pending ? "先应对小怪" : choosing ? "离开此处" : "继续探索";
        eventMeta.rectTransform.anchoredPosition = new Vector2(48, -610);
        eventMeta.gameObject.SetActive(!choosing);
        eventMeta.text = pending ? "小怪拦住了去路，你打算怎么做？" : "事件已结束，可以继续探索。";
        string result = choosing ? definition.description : ResultSummary(level, definition, node);
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
        resultScroll.gameObject.SetActive(true);
        var scrollRect = (RectTransform)resultScroll.transform;
        scrollRect.anchoredPosition = new Vector2(48, -167);
        scrollRect.sizeDelta = new Vector2(1004, choosing ? 160 : 430);
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
        // Old saves may retain prototype-only reward and combat notes.
        var lines = text.Split('\n');
        var presented = new StringBuilder();
        foreach (string line in lines)
        {
            if (line.Contains("道具记录（文字") || line.Contains("效果记录（文字")) continue;
            string clean = line.Contains("战斗暂用文字结算")
                ? "一只小怪挡住了去路。你可以驱赶它，或绕路离开。"
                : line.Contains("战斗伤害、攻击和道具均为文字占位")
                    ? "你赶走了小怪，拾取了它落下的罪骸。罪骸 +1。"
                    : line.Contains("普通遭遇失败（文字占位）") ? "你绕过小怪，继续前行。"
                    : line.Replace("（文字占位）", "").Replace("（文字记录）", "");
            if (presented.Length > 0) presented.Append('\n');
            presented.Append(clean);
        }
        return presented.ToString();
    }

    string NodeName(string id)
    {
        var node = controller != null && controller.Progression != null ? controller.Progression.GetNode(id) : null;
        return node != null ? node.displayName : "相邻地点";
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
        var preferred = node != null && node.encounterPending ? victoryButton
            : node != null && !node.resolved && !node.abandoned ? choiceButtons[0] : closeButton;
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



