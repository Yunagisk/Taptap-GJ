using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// A self-contained content preview: this never reads or replaces GameRoot.I or the main save.
public class WildernessPreviewUI : MonoBehaviour
{
    public TextAsset catalogAsset;
    public WorldMapTemplate resourceTemplate;
    [Range(1, 3)] public int stage = 1;
    public int initialSeed = 73105;
    public TMP_Text resourcesText, currentNodeText, eventTitle, eventDescription, eventMeta, roadsText, historyText, messageText;
    public TMP_InputField seedInput;
    public Button[] nodeButtons = new Button[8];
    public TMP_Text[] nodeLabels = new TMP_Text[8];
    public Button[] choiceButtons = new Button[2];
    public TMP_Text[] choiceLabels = new TMP_Text[2];
    public Button leaveButton, victoryButton, defeatButton, regenerateButton, saveButton, loadButton;

    public WildernessLevelState Level { get { return level; } }
    public GameState Resources { get { return resources; } }
    public WildernessEventCatalog Catalog { get { return catalog; } }
    public string SavePath { get { return Path.Combine(Application.persistentDataPath, "wilderness-preview-v1.json"); } }

    [Serializable]
    public sealed class PreviewSave
    {
        public int version = 1;
        public WildernessLevelState level;
        public GameState resources;
    }

    WildernessEventCatalog catalog;
    WildernessLevelState level;
    GameState resources;
    MapLayout resourceLayout;
    UnityAction[] nodeActions, choiceActions;
    bool initialized;
    ScrollRect storyScroll;
    string renderedStory;
    string message = "选择相邻地点开始探索。这里的进度与正式游戏分开保存。";

    void OnEnable()
    {
        EnsureActions();
        Bind(true);
        if (!initialized) Initialize();
        Refresh();
    }

    void OnDisable() { Bind(false); }

    void EnsureActions()
    {
        if (nodeActions != null) return;
        nodeActions = new UnityAction[nodeButtons.Length];
        for (int i = 0; i < nodeActions.Length; i++)
        {
            int index = i;
            nodeActions[i] = () => TravelToIndex(index);
        }
        choiceActions = new UnityAction[choiceButtons.Length];
        for (int i = 0; i < choiceActions.Length; i++)
        {
            int index = i;
            choiceActions[i] = () => Choose(index);
        }
    }

    void Bind(bool add)
    {
        if (nodeActions == null) return;
        for (int i = 0; i < nodeButtons.Length && i < nodeActions.Length; i++) Listen(nodeButtons[i], nodeActions[i], add);
        for (int i = 0; i < choiceButtons.Length && i < choiceActions.Length; i++) Listen(choiceButtons[i], choiceActions[i], add);
        Listen(leaveButton, LeaveCurrent, add);
        Listen(victoryButton, ResolveVictory, add);
        Listen(defeatButton, ResolveDefeat, add);
        Listen(regenerateButton, Regenerate, add);
        Listen(saveButton, Save, add);
        Listen(loadButton, Load, add);
    }

    static void Listen(Button button, UnityAction action, bool add)
    {
        if (button == null) return;
        if (add) button.onClick.AddListener(action); else button.onClick.RemoveListener(action);
    }

    void Initialize()
    {
        try
        {
            if (catalogAsset == null || resourceTemplate == null) throw new InvalidOperationException("缺少野外配置或资源规则模板。");
            catalog = JsonUtility.FromJson<WildernessEventCatalog>(catalogAsset.text);
            WildernessEventRules.ValidateCatalog(catalog);
            resourceLayout = resourceTemplate.Load();
            BuildRun(initialSeed);
            initialized = true;
        }
        catch (Exception error) { Fail(error); }
    }

    void BuildRun(int seed)
    {
        level = WildernessEventRules.Generate(catalog, seed, stage);
        resources = NewRunFactory.Create(resourceLayout, "sword", seed);
        resources.player.hp = Math.Min(65, resources.player.maxHp);
        resources.world.stage = stage;
        if (seedInput != null) seedInput.text = seed.ToString();
    }

    public void Regenerate()
    {
        if (catalog == null || resourceLayout == null) { Initialize(); Refresh(); return; }
        int seed;
        if (seedInput == null || !int.TryParse(seedInput.text, out seed))
        {
            message = "请输入整数种子。"; Refresh(); return;
        }
        try
        {
            BuildRun(seed);
            message = "已生成新的独立关卡：从 " + WildernessEventRules.GenerationTemplateCount(catalog) + " 个模板不放回抽取 7 个地点。";
        }
        catch (Exception error) { Fail(error); }
        Refresh();
    }

    public void TravelToIndex(int index)
    {
        if (catalog == null || catalog.graph == null || index < 0 || index >= catalog.graph.nodes.Count) return;
        TravelTo(catalog.graph.nodes[index].id);
    }

    public void TravelTo(string nodeId)
    {
        if (level == null || resources == null) return;
        var active = WildernessEventRules.FindNode(level, level.activeNodeId);
        if (active != null && active.encounterPending)
        {
            message = "先结算当前的单只小怪遭遇，再继续前进。"; Refresh(); return;
        }
        if (nodeId == level.currentNodeId) { Refresh(); return; }
        if (!WildernessEventRules.GetNeighbors(level, catalog, level.currentNodeId).Contains(nodeId))
        {
            message = "只能沿已开放的道路移动。"; Refresh(); return;
        }
        try
        {
            ValidateResources(resources, level.seed);
            MapLayoutValidator.Validate(resourceLayout);
            if (resources.world.time == int.MaxValue || resources.map.sinRollCount == int.MaxValue)
                throw new InvalidOperationException("世界计时已达上限，无法移动。");
            WildernessEventRules.Leave(level);
            level.currentNodeId = nodeId;
            resources.map.currentNodeId = nodeId;
            WorldResourceRules.SpendTime(resourceLayout, resources.map, resources.world, 1, false);
            bool revealed = WildernessEventRules.TryEnter(level, nodeId);
            message = revealed ? "抵达一个新的问号地点。选择一次后，本地点不再刷新。"
                : nodeId == catalog.graph.villageNodeId ? "回到村庄入口。" : "此地点已探索，往返仍消耗移动时间。";
        }
        catch (Exception error) { Fail(error); }
        Refresh();
    }

    public void Choose(int index)
    {
        if (level == null || resources == null) return;
        try
        {
            var result = WildernessEventRules.TryChoose(level, catalog, resources, resourceLayout, index);
            message = result.message;
        }
        catch (Exception error) { Fail(error); }
        Refresh();
    }

    public void LeaveCurrent()
    {
        if (level == null) return;
        var active = WildernessEventRules.FindNode(level, level.activeNodeId);
        if (active == null) return;
        if (active.encounterPending) { message = "先结算当前遭遇。"; Refresh(); return; }
        WildernessEventRules.Leave(level);
        message = active.resolved ? "已收起事件结果。" : "你放弃了这次事件；再次经过时不会重新开放选项。";
        Refresh();
    }

    public void ResolveVictory() { Resolve(true); }
    public void ResolveDefeat() { Resolve(false); }

    void Resolve(bool victory)
    {
        if (level == null || resources == null) return;
        try { message = WildernessEventRules.ResolveEncounter(level, resources, victory).message; }
        catch (Exception error) { Fail(error); }
        Refresh();
    }

    public void Save()
    {
        if (level == null || resources == null) return;
        try
        {
            var payload = JsonUtility.ToJson(new PreviewSave { level = level, resources = resources }, true);
            string temporaryPath = SavePath + ".tmp";
            File.WriteAllText(temporaryPath, payload, Encoding.UTF8);
            File.Copy(temporaryPath, SavePath, true);
            File.Delete(temporaryPath);
            message = "独立预览存档已保存，包含预抽结果、修桥状态与资源。";
        }
        catch (Exception error) { Fail(error); }
        Refresh();
    }

    public void Load()
    {
        if (catalog == null) return;
        try
        {
            if (!File.Exists(SavePath)) { message = "尚无独立预览存档，请先保存。"; Refresh(); return; }
            var loaded = JsonUtility.FromJson<PreviewSave>(File.ReadAllText(SavePath, Encoding.UTF8));
            ValidateSave(loaded);
            level = loaded.level;
            resources = loaded.resources;
            stage = level.stage;
            if (seedInput != null) seedInput.text = level.seed.ToString();
            message = "已读取独立预览存档；事件随机结果保持不变。";
        }
        catch (Exception error) { Fail(error); }
        Refresh();
    }

    void ValidateSave(PreviewSave saved)
    {
        if (saved == null || saved.version != 1)
            throw new InvalidOperationException("预览存档格式无效，当前关卡保持不变。");
        WildernessEventRules.ValidateLevel(saved.level, catalog);
        ValidateResources(saved.resources, saved.level.seed);
        if (saved.resources.world.stage != saved.level.stage
            || saved.resources.map.currentNodeId != saved.level.currentNodeId)
            throw new InvalidOperationException("预览存档的阶段或位置不一致，当前关卡保持不变。");
    }

    void ValidateResources(GameState state, int expectedSeed)
    {
        if (state == null || state.player == null || state.world == null || state.map == null
            || state.inventory == null || state.shop == null || state.story == null
            || state.world.time < 0 || state.world.sin < 0 || state.world.sin > 100 || state.world.sinRemnant < 0
            || state.player.maxHp <= 0 || state.player.hp < 0 || state.player.hp > state.player.maxHp
            || state.player.nextAttackBonusPercent < 0 || state.player.nextDirectHitReductionPercent < 0
            || state.player.nextCampRecoveryBonusPercent < 0 || state.map.templateId != resourceLayout.id
            || state.map.randomSeed != expectedSeed || state.map.sinRollCount < 0
            || state.map.clearedNodes == null || state.map.visitedNodes == null || state.map.revealedNodes == null
            || state.map.spawnedBossNodes == null || state.map.defeatedBossNodes == null
            || state.inventory.items == null || state.inventory.weapons == null || state.inventory.equippedWeaponIds == null
            || state.shop.availableWeaponIds == null || state.story.completedDialogueIds == null
            || state.world.villageEvents == null || state.world.villageEvents.occurrences == null
            || state.world.villageEvents.rewardTexts == null)
            throw new InvalidOperationException("预览资源数据无效，当前关卡保持不变。");
    }
    public void Refresh()
    {
        bool ready = catalog != null && level != null && resources != null;
        if (resourcesText != null) resourcesText.text = ready
            ? WorldResourceRules.TimeLabel(resources.world) + "    生命 " + resources.player.hp + "/" + resources.player.maxHp
                + "    罪恶 " + resources.world.sin + "/100    罪骸 " + resources.world.sinRemnant
                + "    阶段 " + level.stage + "    种子 " + level.seed
            : "野外内容预览未就绪";
        if (currentNodeText != null) currentNodeText.text = ready ? "当前位置：" + NodeName(level.currentNodeId, true) : "";
        if (messageText != null) messageText.text = message ?? "";
        for (int i = 0; i < choiceButtons.Length; i++) if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(false);
        if (victoryButton != null) victoryButton.gameObject.SetActive(false);
        if (defeatButton != null) defeatButton.gameObject.SetActive(false);
        if (leaveButton != null) leaveButton.interactable = false;
        if (saveButton != null) saveButton.interactable = ready;
        if (loadButton != null) loadButton.interactable = catalog != null;
        if (!ready)
        {
            if (eventTitle != null) eventTitle.text = "配置检查";
            if (eventDescription != null) eventDescription.text = message;
            for (int i = 0; i < nodeButtons.Length; i++) if (nodeButtons[i] != null) nodeButtons[i].interactable = false;
            return;
        }
        var neighbors = WildernessEventRules.GetNeighbors(level, catalog, level.currentNodeId);
        var active = WildernessEventRules.FindNode(level, level.activeNodeId);
        bool pending = active != null && active.encounterPending;
        for (int i = 0; i < nodeButtons.Length; i++)
        {
            if (nodeButtons[i] == null) continue;
            bool exists = i < catalog.graph.nodes.Count;
            nodeButtons[i].gameObject.SetActive(exists);
            if (!exists) continue;
            string id = catalog.graph.nodes[i].id;
            bool current = id == level.currentNodeId;
            nodeButtons[i].interactable = !pending && (current || neighbors.Contains(id));
            if (i < nodeLabels.Length && nodeLabels[i] != null)
                nodeLabels[i].text = (current ? "● " : neighbors.Contains(id) ? "→ " : "   ") + NodeName(id, true);
        }
        var currentNode = WildernessEventRules.FindNode(level, level.currentNodeId);
        var definition = currentNode == null ? null : WildernessEventRules.FindDefinition(catalog, currentNode.templateId);
        if (eventTitle != null) eventTitle.text = definition == null ? "村庄入口" : definition.title;
        bool expanded = currentNode == null || currentNode.resolved || currentNode.abandoned;
        if (eventDescription != null)
        {
            EnsureStoryScroll();
            var descriptionRect = (RectTransform)storyScroll.transform;
            descriptionRect.sizeDelta = new Vector2(descriptionRect.sizeDelta.x, expanded ? 390 : 110);
        }
        if (eventMeta != null) eventMeta.gameObject.SetActive(!expanded);
        if (eventDescription != null) eventDescription.text = definition == null
            ? "从左侧选择相邻的问号地点。\n\n每条道路移动消耗 1 单位时间；选择的额外耗时另算。\n往返不会重新生成或重开事件。"
            : currentNode.resolved ? CurrentResultText(definition, currentNode)
            : currentNode.abandoned ? definition.description + "\n\n你已离开这次事件，选项不会再次开放。" : definition.description;
        if (eventMeta != null) eventMeta.text = currentNode == null ? "7 个问号 · " + WildernessEventRules.GenerationTemplateCount(catalog) + " 个模板 · 每关不重复"
            : pending ? "隐藏遭遇：单只小怪。攻击、受击与道具效果以文字展示。"
            : currentNode.resolved ? "已完成：本地点不再开放选项。"
            : currentNode.abandoned ? "已离开：本地点不再开放选项。"
            : active == currentNode ? "请选择你的行动。" : "此地点已探索。";
        if (active == currentNode && definition != null && !currentNode.resolved && !currentNode.abandoned && !pending)
        {
            for (int i = 0; i < choiceButtons.Length && i < definition.choices.Count; i++)
            {
                if (choiceButtons[i] == null) continue;
                choiceButtons[i].gameObject.SetActive(true);
                choiceButtons[i].interactable = true;
                if (i < choiceLabels.Length && choiceLabels[i] != null) choiceLabels[i].text = WildernessEventRules.DescribeChoice(definition.choices[i]);
            }
        }
        if (leaveButton != null) leaveButton.interactable = active != null && !pending;
        if (pending)
        {
            string enemyName = currentNode.enemyId;
            foreach (var enemy in catalog.enemyVariants) if (enemy.id == currentNode.enemyId) enemyName = enemy.name;
            if (eventDescription != null) eventDescription.text = CurrentResultText(definition, currentNode) + "\n\n遭遇：" + enemyName + " × 1\n使用下方按钮模拟胜负，暂不进入正式战斗。";
            if (victoryButton != null) victoryButton.gameObject.SetActive(true);
            if (defeatButton != null) defeatButton.gameObject.SetActive(true);
        }
        if (storyScroll != null && renderedStory != eventDescription.text)
        {
            renderedStory = eventDescription.text;
            Canvas.ForceUpdateCanvases();
            storyScroll.verticalNormalizedPosition = 1;
        }
        if (roadsText != null) roadsText.text = RoadsSummary();
        if (historyText != null)
        {
            int start = Math.Max(0, level.history.Count - 4);
            var builder = new StringBuilder();
            for (int i = start; i < level.history.Count; i++)
            {
                string entry = level.history[i] ?? "";
                int newline = entry.IndexOf('\n');
                builder.AppendLine(newline < 0 ? entry : entry.Substring(0, newline));
            }
            historyText.text = builder.Length == 0 ? "尚无事件结算记录。" : builder.ToString().TrimEnd();
        }
    }

    string CurrentResultText(WildernessEventDefinition definition, WildernessNodeState node)
    {
        for (int i = level.history.Count - 1; i >= 0; i--)
            if (level.history[i] != null && level.history[i].StartsWith(definition.title + " · ", StringComparison.Ordinal))
                return level.history[i] + (node.encounterResolved ? "\n遭遇已结算。" : "");
        return definition.description + "\n\n本次事件已经结束。";
    }
    string NodeName(string id, bool showStatus)
    {
        if (id == catalog.graph.villageNodeId) return "村庄入口";
        var node = WildernessEventRules.FindNode(level, id);
        if (node == null) return id;
        int ordinal = 1;
        foreach (var candidate in catalog.graph.nodes)
        {
            if (candidate.id == id) break;
            if (candidate.id != catalog.graph.villageNodeId) ordinal++;
        }
        string title = node.shown ? WildernessEventRules.FindDefinition(catalog, node.templateId).title : "问号地点 " + ordinal;
        if (showStatus && node.encounterPending) title += " [遭遇中]";
        else if (showStatus && node.resolved) title += " [完成]";
        else if (showStatus && node.abandoned) title += " [离开]";
        return title;
    }

    string RoadsSummary()
    {
        var text = new StringBuilder("已开放道路\n");
        var seen = new HashSet<string>();
        foreach (var from in catalog.graph.nodes)
        {
            foreach (var to in WildernessEventRules.GetNeighbors(level, catalog, from.id))
            {
                string key = string.CompareOrdinal(from.id, to) < 0 ? from.id + ":" + to : to + ":" + from.id;
                if (seen.Add(key)) text.Append(NodeShort(from.id)).Append(" — ").Append(NodeShort(to)).Append('\n');
            }
        }
        text.Append("\n断桥横路\n");
        bool hasBridge = false;
        foreach (var node in level.nodes)
        {
            if (string.IsNullOrEmpty(node.bridgeFrom) || string.IsNullOrEmpty(node.bridgeTo)) continue;
            hasBridge = true;
            bool repaired = false;
            foreach (var road in level.repairedRoads)
                if ((road.from == node.bridgeFrom && road.to == node.bridgeTo) || (road.to == node.bridgeFrom && road.from == node.bridgeTo)) repaired = true;
            text.Append(NodeShort(node.bridgeFrom)).Append(" — ").Append(NodeShort(node.bridgeTo)).Append(repaired ? " [已修复]" : " [待修复]").Append('\n');
        }
        if (!hasBridge) text.Append("本关未抽到断桥事件。");
        return text.ToString().TrimEnd();
    }

    string NodeShort(string id)
    {
        if (id == catalog.graph.villageNodeId) return "村庄";
        int ordinal = 1;
        foreach (var node in catalog.graph.nodes)
        {
            if (node.id == catalog.graph.villageNodeId) continue;
            if (node.id == id) return "地点 " + ordinal;
            ordinal++;
        }
        return id;
    }

    void EnsureStoryScroll()
    {
        if (storyScroll != null) return;
        var textRect = eventDescription.rectTransform;
        var go = new GameObject("StoryScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        var rect = (RectTransform)go.transform;
        rect.SetParent(textRect.parent, false); rect.SetSiblingIndex(textRect.GetSiblingIndex());
        rect.anchorMin = textRect.anchorMin; rect.anchorMax = textRect.anchorMax;
        rect.pivot = textRect.pivot; rect.anchoredPosition = textRect.anchoredPosition; rect.sizeDelta = textRect.sizeDelta;
        go.GetComponent<Image>().color = new Color(0, 0, 0, .001f);
        storyScroll = go.GetComponent<ScrollRect>(); storyScroll.horizontal = false;
        storyScroll.movementType = ScrollRect.MovementType.Clamped; storyScroll.scrollSensitivity = 35;
        storyScroll.viewport = rect; storyScroll.content = textRect;
        textRect.SetParent(rect, false); textRect.anchorMin = new Vector2(0, 1); textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(.5f, 1); textRect.anchoredPosition = Vector2.zero; textRect.sizeDelta = Vector2.zero;
        eventDescription.overflowMode = TextOverflowModes.Overflow;
        var fit = eventDescription.GetComponent<ContentSizeFitter>() ?? eventDescription.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void Fail(Exception error)
    {
        message = error.Message;
        Debug.LogError("Wilderness preview: " + error.Message, this);
    }
}





