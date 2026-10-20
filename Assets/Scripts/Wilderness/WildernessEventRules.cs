using System;
using System.Collections.Generic;

// Shared question-mark content; an empty questionNodeIds list preserves the standalone seven-location preview.
[Serializable]
public class WildernessEventCatalog
{
    public List<WildernessEventDefinition> templates = new List<WildernessEventDefinition>();
    public List<WildernessEnemyVariant> enemyVariants = new List<WildernessEnemyVariant>();
    public WildernessGraph graph = new WildernessGraph();
    public List<string> questionNodeIds = new List<string>();
}

[Serializable]
public class WildernessEventDefinition
{
    public string id;
    public string title;
    public string description;
    public int weight;
    public List<WildernessChoiceDefinition> choices = new List<WildernessChoiceDefinition>();
}

[Serializable]
public class WildernessChoiceDefinition
{
    public string label;
    public int timeCost;
    public List<WildernessOutcomeDefinition> outcomes = new List<WildernessOutcomeDefinition>();
}

[Serializable]
public class WildernessOutcomeDefinition
{
    public int weight;
    public int sinDelta;
    public int healAmount;
    public string rewardText;
    public string effectText;
    public int campRecoveryBonusPercent;
    public bool encounter;
    public bool repairBridge;
}

[Serializable]
public class WildernessEnemyVariant
{
    public string id;
    public string name;
    public int stage;
}

[Serializable]
public class WildernessGraph
{
    public string villageNodeId = "village";
    public List<WildernessGraphNode> nodes = new List<WildernessGraphNode>();
    public List<WildernessRoad> repairableRoads = new List<WildernessRoad>();
}

[Serializable]
public class WildernessGraphNode
{
    public string id;
    public List<string> neighbors = new List<string>();
    public float x;
    public float y;
}

[Serializable]
public class WildernessRoad
{
    public string from;
    public string to;
}

[Serializable]
public class WildernessLevelState
{
    public int seed;
    public int stage;
    public string currentNodeId;
    public string activeNodeId;
    public List<WildernessNodeState> nodes = new List<WildernessNodeState>();
    public List<WildernessRoad> repairedRoads = new List<WildernessRoad>();
    public List<string> history = new List<string>();
}

[Serializable]
public class WildernessNodeState
{
    public string id;
    public string templateId;
    public bool shown;
    public bool resolved;
    public bool abandoned;
    public int chosenIndex = -1;
    public List<WildernessOptionRoll> rolls = new List<WildernessOptionRoll>();
    public string bridgeFrom;
    public string bridgeTo;
    public bool encounterPending;
    public bool encounterResolved;
    public string enemyId;
}

[Serializable]
public class WildernessOptionRoll
{
    public int outcomeIndex;
    public string enemyId;
}

[Serializable]
public class WildernessChoiceResult
{
    public bool success;
    public string message;
    public bool encounterPending;
}

public static class WildernessEventRules
{
    const int DefaultQuestionCount = 7;
    const int MaximumDegree = 4;

    // Validation is read-only, so invalid content never partially spends resources.
    public static void ValidateCatalog(WildernessEventCatalog catalog)
    {
        Require(catalog != null, "缺少野外配置。");
        var questionIds = QuestionIds(catalog);
        Require(catalog.templates != null && catalog.templates.Count >= questionIds.Count,
            "野外配置的模板数量不足。");
        Require(catalog.enemyVariants != null && catalog.enemyVariants.Count > 0, "缺少隐藏遭遇敌人配置。");
        var enemyIds = new HashSet<string>();
        foreach (var enemy in catalog.enemyVariants)
            Require(enemy != null && HasText(enemy.id) && HasText(enemy.name) && enemy.stage > 0
                && enemyIds.Add(enemy.id), "敌人 ID、名称和阶段必须有效且 ID 不重复。");

        long totalWeight = 0;
        var ids = new HashSet<string>();
        foreach (var definition in catalog.templates)
        {
            Require(definition != null && HasText(definition.id) && ids.Add(definition.id)
                && HasText(definition.title) && HasText(definition.description)
                && definition.weight > 0, "模板 ID、说明和权重必须有效。");
            Require(definition.choices != null && definition.choices.Count == 2, "每个问号模板必须配置两个选项。");
            totalWeight += definition.weight;
            foreach (var choice in definition.choices)
            {
                Require(choice != null && HasText(choice.label) && choice.timeCost >= 0
                    && choice.outcomes != null && choice.outcomes.Count > 0, "选项及额外耗时配置无效。");
                long outcomeWeight = 0;
                foreach (var outcome in choice.outcomes)
                {
                    Require(outcome != null && outcome.weight > 0 && outcome.healAmount >= 0
                        && outcome.campRecoveryBonusPercent >= 0, "选项结果配置无效。");
                    Require(!outcome.encounter || !outcome.repairBridge, "隐藏遭遇不能同时修桥。");
                    outcomeWeight += outcome.weight;
                }
                Require(outcomeWeight <= int.MaxValue, "选项结果权重溢出。");
            }
        }
        Require(totalWeight <= int.MaxValue, "模板总权重溢出。");

        var graph = catalog.graph;
        Require(graph != null && HasText(graph.villageNodeId) && graph.nodes != null
            && graph.nodes.Count >= questionIds.Count + 1 && graph.repairableRoads != null
            && (HasConfiguredQuestions(catalog) || graph.nodes.Count == DefaultQuestionCount + 1),
            "关卡必须包含村庄及全部问号节点。");
        var graphNodes = new Dictionary<string, WildernessGraphNode>();
        foreach (var node in graph.nodes)
        {
            Require(node != null && HasText(node.id) && !graphNodes.ContainsKey(node.id)
                && node.neighbors != null && node.neighbors.Count > 0
                && node.neighbors.Count <= (HasConfiguredQuestions(catalog) ? 5 : MaximumDegree)
                && !float.IsNaN(node.x) && !float.IsInfinity(node.x)
                && !float.IsNaN(node.y) && !float.IsInfinity(node.y), "关卡节点或基础道路配置无效。");
            graphNodes.Add(node.id, node);
        }
        Require(graphNodes.ContainsKey(graph.villageNodeId), "关卡缺少村庄。");
        var configuredQuestionIds = new HashSet<string>();
        foreach (var questionId in questionIds)
            Require(HasText(questionId) && configuredQuestionIds.Add(questionId)
                && questionId != graph.villageNodeId && graphNodes.ContainsKey(questionId),
                "问号节点必须存在、不得重复且不能是村庄。");
        foreach (var node in graph.nodes)
        {
            var neighbors = new HashSet<string>();
            foreach (var next in node.neighbors)
                Require(next != node.id && neighbors.Add(next) && graphNodes.ContainsKey(next)
                    && graphNodes[next].neighbors.Contains(node.id), "基础道路必须是双向、有效且不重复的。");
        }
        Require(Reachable(graph, graph.villageNodeId).Count == graph.nodes.Count,
            "修桥之前所有节点必须能够回到村庄。");
        foreach (var road in graph.repairableRoads)
            Require(road != null && HasText(road.from) && HasText(road.to) && road.from != road.to
                && graphNodes.ContainsKey(road.from) && graphNodes.ContainsKey(road.to),
                "可修复缺边必须有两个已生成的端点。");
    }

    public static WildernessLevelState Generate(WildernessEventCatalog catalog, int seed, int stage)
    {
        ValidateCatalog(catalog);
        Require(stage > 0, "阶段必须为正数。");
        var enemies = new List<WildernessEnemyVariant>();
        foreach (var enemy in catalog.enemyVariants)
            if (enemy.stage == stage) enemies.Add(enemy);
        Require(enemies.Count > 0, "当前阶段没有隐藏遭遇敌人配置。");

        var random = new StableRandom(seed, stage);
        var level = new WildernessLevelState {
            seed = seed, stage = stage, currentNodeId = catalog.graph.villageNodeId
        };
        var bridgeCandidates = RepairCandidates(level, catalog);
        var pool = new List<WildernessEventDefinition>();
        foreach (var template in catalog.templates)
            if (!HasBridgeOutcome(template) || bridgeCandidates.Count > 0) pool.Add(template);
        var questionIds = QuestionIds(catalog);
        Require(pool.Count >= questionIds.Count, "排除无法修复的断桥后，问号模板数量不足。");

        foreach (var questionId in questionIds)
        {
            int selectedIndex = SelectDefinition(pool, random);
            var template = pool[selectedIndex];
            pool.RemoveAt(selectedIndex);
            var node = new WildernessNodeState {
                id = questionId, templateId = template.id
            };
            if (HasBridgeOutcome(template))
            {
                var localCandidates = HasConfiguredQuestions(catalog)
                    ? bridgeCandidates.FindAll(value => value.from == questionId || value.to == questionId)
                    : bridgeCandidates;
                if (localCandidates.Count == 0) localCandidates = bridgeCandidates;
                var road = localCandidates[random.Next(localCandidates.Count)];
                node.bridgeFrom = road.from;
                node.bridgeTo = road.to;
            }
            foreach (var choice in template.choices)
            {
                int outcomeIndex = SelectOutcome(choice.outcomes, random);
                var outcome = choice.outcomes[outcomeIndex];
                node.rolls.Add(new WildernessOptionRoll {
                    outcomeIndex = outcomeIndex,
                    enemyId = outcome.encounter ? enemies[random.Next(enemies.Count)].id : null
                });
            }
            level.nodes.Add(node);
        }
        if (HasConfiguredQuestions(catalog))
            foreach (var node in level.nodes)
                if (HasText(node.bridgeFrom) && node.id != node.bridgeFrom && node.id != node.bridgeTo)
                {
                    // A bridge event belongs at one of its own endpoints, even if some regions have no legal shortcut.
                    var endpoint = level.nodes.Find(value => value != node
                        && (value.id == node.bridgeFrom || value.id == node.bridgeTo)
                        && !HasText(value.bridgeFrom));
                    Require(endpoint != null, "断桥事件缺少可用的问号端点。");
                    string previousId = node.id;
                    node.id = endpoint.id;
                    endpoint.id = previousId;
                }
        return level;
    }

    // Save restoration validates the generated results instead of generating new rolls.
    public static void ValidateLevel(WildernessLevelState level, WildernessEventCatalog catalog)
    {
        ValidateCatalog(catalog);
        var questionIds = QuestionIds(catalog);
        Require(ValidLevelLists(level) && level.nodes.Count == questionIds.Count && level.stage > 0,
            "关卡存档必须保留全部已配置的问号。");
        Require(catalog.enemyVariants.Exists(enemy => enemy.stage == level.stage), "关卡阶段没有对应敌人配置。");
        Require(catalog.graph.nodes.Exists(node => node.id == level.currentNodeId), "当前位置不在独立关卡中。");
        var nodeIds = new HashSet<string>();
        var templateIds = new HashSet<string>();
        var selectedRepairs = new List<WildernessRoad>();
        foreach (var node in level.nodes)
        {
            Require(node != null && nodeIds.Add(node.id), "问号节点 ID 不得重复。");
            Require(questionIds.Contains(node.id), "问号节点 ID 无效。");
            var definition = FindDefinition(catalog, node.templateId);
            Require(definition != null && templateIds.Add(node.templateId), "模板缺失或重复。");
            Require(node.rolls != null && node.rolls.Count == definition.choices.Count, "选项预置结果缺失。");
            for (int i = 0; i < node.rolls.Count; i++)
            {
                var roll = node.rolls[i];
                var outcomes = definition.choices[i].outcomes;
                Require(roll != null && roll.outcomeIndex >= 0 && roll.outcomeIndex < outcomes.Count, "选项预置结果越界。");
                var outcome = outcomes[roll.outcomeIndex];
                Require(outcome.encounter
                    ? catalog.enemyVariants.Exists(enemy => enemy.id == roll.enemyId && enemy.stage == level.stage)
                    : !HasText(roll.enemyId), "预置敌人与结果或阶段不匹配。");
            }
            if (HasBridgeOutcome(definition))
            {
                Require(HasText(node.bridgeFrom) && HasText(node.bridgeTo)
                    && catalog.graph.repairableRoads.Exists(road => SameRoad(road, node.bridgeFrom, node.bridgeTo))
                    && IsLegalMissingRoad(new WildernessLevelState(), catalog, node.bridgeFrom, node.bridgeTo),
                    "断桥预置端点不是可修复的缺边。");
                Require(!HasConfiguredQuestions(catalog) || node.id == node.bridgeFrom || node.id == node.bridgeTo,
                    "大地图断桥事件必须位于所修桥的端点。");
            }
            else Require(!HasText(node.bridgeFrom) && !HasText(node.bridgeTo), "非断桥事件不应包含道路端点。");

            Require(node.chosenIndex >= -1 && node.chosenIndex < definition.choices.Count, "选择记录越界。");
            if (!node.shown)
            {
                Require(!node.resolved && !node.abandoned && node.chosenIndex == -1
                    && !node.encounterPending && !node.encounterResolved && !HasText(node.enemyId)
                    && level.activeNodeId != node.id, "未展示事件包含结算状态。");
                continue;
            }
            Require(!(node.resolved && node.abandoned), "事件不能同时完成和放弃。");
            if (node.abandoned)
            {
                Require(!node.resolved && node.chosenIndex == -1 && !node.encounterPending
                    && !node.encounterResolved && !HasText(node.enemyId) && level.activeNodeId != node.id,
                    "放弃事件包含结算状态。");
                continue;
            }
            if (!node.resolved)
            {
                Require(node.chosenIndex == -1 && !node.encounterPending && !node.encounterResolved
                    && !HasText(node.enemyId) && level.activeNodeId == node.id
                    && level.currentNodeId == node.id, "未回答事件缺少当前活动状态。");
                continue;
            }
            Require(node.chosenIndex >= 0, "已结算事件缺少选项记录。");
            var selectedRoll = node.rolls[node.chosenIndex];
            var selectedOutcome = definition.choices[node.chosenIndex].outcomes[selectedRoll.outcomeIndex];
            if (selectedOutcome.encounter)
            {
                Require(node.enemyId == selectedRoll.enemyId && (node.encounterPending != node.encounterResolved),
                    "隐藏遭遇的敌人或结算标记不一致。");
                Require(!node.encounterPending || (level.activeNodeId == node.id && level.currentNodeId == node.id),
                    "待结算遭遇必须留在当前事件。");
            }
            else Require(!node.encounterPending && !node.encounterResolved && !HasText(node.enemyId),
                "普通结果包含隐藏遭遇状态。");
            if (selectedOutcome.repairBridge)
                selectedRepairs.Add(new WildernessRoad { from = node.bridgeFrom, to = node.bridgeTo });
        }

        if (HasText(level.activeNodeId))
        {
            var active = FindNode(level, level.activeNodeId);
            Require(active != null && active.shown && !active.abandoned && level.currentNodeId == active.id,
                "活动事件不在当前位置。");
        }
        var checkedRoads = new List<WildernessRoad>();
        foreach (var road in level.repairedRoads)
        {
            Require(road != null && catalog.graph.repairableRoads.Exists(value => SameRoad(value, road.from, road.to))
                && IsLegalMissingRoad(new WildernessLevelState(), catalog, road.from, road.to)
                && !checkedRoads.Exists(value => SameRoad(value, road.from, road.to))
                && selectedRepairs.Exists(value => SameRoad(value, road.from, road.to)),
                "修复道路必须对应已选择修复的预设缺边，且不得重复。");
            checkedRoads.Add(road);
        }
        foreach (var road in selectedRepairs)
            Require(checkedRoads.Exists(value => SameRoad(value, road.from, road.to)), "修桥结算缺少已修道路。");
        foreach (var node in catalog.graph.nodes)
            Require(GetNeighbors(level, catalog, node.id).Count
                <= (HasConfiguredQuestions(catalog) ? Math.Max(MaximumDegree, node.neighbors.Count) : MaximumDegree),
                "修桥不能使端点度数超过 4。");
    }

    public static WildernessEventDefinition FindDefinition(WildernessEventCatalog catalog, string id)
    {
        if (catalog == null || catalog.templates == null || !HasText(id)) return null;
        return catalog.templates.Find(value => value != null && value.id == id);
    }

    public static WildernessNodeState FindNode(WildernessLevelState level, string nodeId)
    {
        if (level == null || level.nodes == null || !HasText(nodeId)) return null;
        return level.nodes.Find(value => value != null && value.id == nodeId);
    }

    // Movement and its ordinary 1-unit road cost belong to the independent level controller.
    public static bool TryEnter(WildernessLevelState level, string nodeId)
    {
        if (!ValidLevelLists(level) || HasText(level.activeNodeId) || level.currentNodeId != nodeId) return false;
        var node = FindNode(level, nodeId);
        if (node == null || node.shown || node.resolved || node.abandoned || node.chosenIndex != -1
            || node.encounterPending || node.encounterResolved) return false;
        node.shown = true;
        level.activeNodeId = node.id;
        return true;
    }

    // An unanswered question is lost when dismissed. A pending encounter must first be settled.
    public static void Leave(WildernessLevelState level)
    {
        if (!ValidLevelLists(level) || !HasText(level.activeNodeId)) return;
        var node = FindNode(level, level.activeNodeId);
        if (node == null || node.encounterPending) return;
        if (!node.resolved && !node.abandoned)
        {
            node.abandoned = true;
            level.history.Add("已离开：" + LocationLabel(node.id) + "（未选择，本次不再提示）");
        }
        level.activeNodeId = null;
    }

    public static WildernessChoiceResult TryChoose(WildernessLevelState level, WildernessEventCatalog catalog,
        GameState resources, MapLayout resourceLayout, int choiceIndex)
    {
        string error;
        if (!ValidateForChoice(level, catalog, resources, resourceLayout, out error)) return Fail(error);
        var node = FindNode(level, level.activeNodeId);
        if (node == null || !node.shown || node.resolved || node.abandoned || node.chosenIndex != -1
            || node.encounterPending || node.encounterResolved || level.currentNodeId != node.id)
            return Fail("当前没有可选择的问号事件。");
        var definition = FindDefinition(catalog, node.templateId);
        if (definition == null || choiceIndex < 0 || choiceIndex >= definition.choices.Count
            || node.rolls == null || node.rolls.Count != definition.choices.Count)
            return Fail("选项或生成时保存的结果无效。");
        var roll = node.rolls[choiceIndex];
        var choice = definition.choices[choiceIndex];
        if (roll == null || roll.outcomeIndex < 0 || roll.outcomeIndex >= choice.outcomes.Count)
            return Fail("生成时保存的选项结果无效。");
        var outcome = choice.outcomes[roll.outcomeIndex];
        WildernessEnemyVariant enemy = null;
        if (outcome.encounter)
        {
            enemy = catalog.enemyVariants.Find(value => value.id == roll.enemyId && value.stage == level.stage);
            if (enemy == null) return Fail("生成时保存的隐藏遭遇敌人无效。");
        }
        if (outcome.repairBridge && !CanRepairBridge(level, catalog, node.id))
            return Fail("当前缺边不能修复。");
        long cost = (long)choice.timeCost + (outcome.encounter ? 1 : 0);
        if ((long)resources.world.time + cost > int.MaxValue
            || (long)resources.map.sinRollCount + cost > int.MaxValue)
            return Fail("世界时间或时间记录已达上限，无法结算。");

        // All validation completes before any mutation, and the chosen flag is set before payment.
        node.chosenIndex = choiceIndex;
        node.resolved = true;
        WorldResourceRules.SpendTime(resourceLayout, resources.map, resources.world, (int)cost, false);
        WorldResourceRules.ChangeSin(resourceLayout, resources.map, resources.world, outcome.sinDelta);
        int healed = WorldResourceRules.Heal(resources.player, outcome.healAmount);
        if (outcome.campRecoveryBonusPercent > 0)
            resources.player.nextCampRecoveryBonusPercent = Math.Max(resources.player.nextCampRecoveryBonusPercent,
                outcome.campRecoveryBonusPercent);

        var parts = new List<string> { definition.title + " · " + choice.label };
        if (choice.timeCost > 0) parts.Add("选项额外耗时 " + choice.timeCost + " 单位");
        if (outcome.sinDelta != 0) parts.Add("事件罪恶 " + Signed(outcome.sinDelta));
        if (healed > 0) parts.Add("恢复 " + healed + " 生命");
        else if (outcome.healAmount > 0) parts.Add("生命已满");
        if (outcome.campRecoveryBonusPercent > 0)
            parts.Add("下次营地恢复 +" + outcome.campRecoveryBonusPercent + "%（同类只保留最高值）");
        if (outcome.repairBridge)
        {
            level.repairedRoads.Add(new WildernessRoad { from = node.bridgeFrom, to = node.bridgeTo });
            parts.Add("已修复通道：" + LocationLabel(node.bridgeFrom) + " — " + LocationLabel(node.bridgeTo));
        }
        if (outcome.encounter)
        {
            node.encounterPending = true;
            node.enemyId = enemy.id;
            parts.Add("一只" + enemy.name + "挡住了去路。你可以驱赶它，或绕路离开。\n本次探索额外耗时 1 单位。");
        }
        else parts.Add("事件已结束");
        parts.Add("当前时间 " + resources.world.time + "，罪恶 " + resources.world.sin
            + "，生命 " + resources.player.hp + "/" + resources.player.maxHp);
        string message = string.Join("\n", parts.ToArray());
        level.history.Add(message);
        return new WildernessChoiceResult { success = true, message = message, encounterPending = node.encounterPending };
    }

    public static WildernessChoiceResult ResolveEncounter(WildernessLevelState level, GameState resources, bool victory)
    {
        if (!ValidLevelLists(level) || resources == null || resources.world == null
            || resources.world.sinRemnant < 0 || resources.world.time < 0
            || resources.world.sin < 0 || resources.world.sin > WorldResourceRules.MaxSin)
            return Fail("关卡或资源状态不可用。");
        var node = FindNode(level, level.activeNodeId);
        if (node == null || level.currentNodeId != node.id || !node.shown || !node.resolved
            || node.chosenIndex < 0 || !node.encounterPending || node.encounterResolved || !HasText(node.enemyId))
            return Fail("当前没有待结算的隐藏遭遇。");
        if (victory && resources.world.sinRemnant == int.MaxValue)
            return Fail("罪骸已达上限，无法结算。");
        node.encounterResolved = true;
        node.encounterPending = false;
        if (victory) resources.world.sinRemnant++;
        string message = victory
            ? "你赶走了小怪，拾取了它落下的罪骸。罪骸 +1。"
            : "你绕过小怪，继续前行。";
        level.history.Add(message);
        return new WildernessChoiceResult { success = true, message = message };
    }

    public static List<string> GetNeighbors(WildernessLevelState level, WildernessEventCatalog catalog, string nodeId)
    {
        var result = new List<string>();
        if (level == null || catalog == null || catalog.graph == null || catalog.graph.nodes == null) return result;
        var graphNode = catalog.graph.nodes.Find(value => value != null && value.id == nodeId);
        if (graphNode == null || graphNode.neighbors == null) return result;
        result.AddRange(graphNode.neighbors);
        if (level.repairedRoads == null) return result;
        foreach (var road in level.repairedRoads)
        {
            if (road == null) continue;
            string other = road.from == nodeId ? road.to : road.to == nodeId ? road.from : null;
            if (HasText(other) && !result.Contains(other)
                && catalog.graph.nodes.Exists(value => value != null && value.id == other))
                result.Add(other);
        }
        return result;
    }

    public static bool CanRepairBridge(WildernessLevelState level, WildernessEventCatalog catalog, string nodeId)
    {
        try { ValidateCatalog(catalog); }
        catch (Exception) { return false; }
        var node = FindNode(level, nodeId);
        if (!ValidLevelLists(level) || node == null || catalog == null || catalog.graph == null
            || catalog.graph.nodes == null || catalog.graph.repairableRoads == null
            || !HasText(node.bridgeFrom) || !HasText(node.bridgeTo)
            || node.bridgeFrom == node.bridgeTo) return false;
        var definition = FindDefinition(catalog, node.templateId);
        if (definition == null || !HasBridgeOutcome(definition)) return false;
        bool configured = catalog.graph.repairableRoads.Exists(road => SameRoad(road, node.bridgeFrom, node.bridgeTo));
        if (!configured) return false;
        return IsLegalMissingRoad(level, catalog, node.bridgeFrom, node.bridgeTo);
    }

    static bool ValidateForChoice(WildernessLevelState level, WildernessEventCatalog catalog, GameState resources,
        MapLayout layout, out string error)
    {
        error = null;
        if (!ValidLevelLists(level) || resources == null || resources.world == null
            || resources.player == null || resources.map == null)
        {
            error = "关卡或资源状态不可用。";
            return false;
        }
        try
        {
            ValidateLevel(level, catalog);
            MapLayoutValidator.Validate(layout);
            Require(resources.world.time >= 0 && resources.world.sin >= 0 && resources.world.sin <= WorldResourceRules.MaxSin
                && resources.world.sinRemnant >= 0 && resources.world.stage > 0
                && resources.map.sinRollCount >= 0 && resources.player.nextCampRecoveryBonusPercent >= 0
                && resources.player.maxHp > 0 && resources.player.hp >= 0
                && resources.player.hp <= resources.player.maxHp, "世界资源数值无效。");
            Require(resources.map.spawnedBossNodes != null && resources.map.revealedNodes != null
                && resources.map.defeatedBossNodes != null, "资源地图状态无效。");
        }
        catch (Exception exception)
        {
            error = "配置或状态无效：" + exception.Message;
            return false;
        }
        return true;
    }

    static List<WildernessRoad> RepairCandidates(WildernessLevelState level, WildernessEventCatalog catalog)
    {
        var result = new List<WildernessRoad>();
        foreach (var road in catalog.graph.repairableRoads)
            if (IsLegalMissingRoad(level, catalog, road.from, road.to)
                && !result.Exists(value => SameRoad(value, road.from, road.to)))
                result.Add(road);
        return result;
    }

    static bool IsLegalMissingRoad(WildernessLevelState level, WildernessEventCatalog catalog, string from, string to)
    {
        var graph = catalog.graph;
        if (from == to || !graph.nodes.Exists(value => value.id == from) || !graph.nodes.Exists(value => value.id == to))
            return false;
        var fromNeighbors = GetNeighbors(level, catalog, from);
        var toNeighbors = GetNeighbors(level, catalog, to);
        if (fromNeighbors.Contains(to) || toNeighbors.Contains(from)
            || fromNeighbors.Count >= MaximumDegree || toNeighbors.Count >= MaximumDegree) return false;
        var reachable = Reachable(graph, graph.villageNodeId);
        return reachable.Contains(from) && reachable.Contains(to);
    }

    static HashSet<string> Reachable(WildernessGraph graph, string start)
    {
        var visited = new HashSet<string>();
        var pending = new Queue<string>();
        visited.Add(start);
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            var node = graph.nodes.Find(value => value.id == current);
            foreach (var next in node.neighbors)
                if (visited.Add(next)) pending.Enqueue(next);
        }
        return visited;
    }

    static bool SameRoad(WildernessRoad road, string from, string to)
    {
        return road != null && ((road.from == from && road.to == to) || (road.from == to && road.to == from));
    }

    static bool ValidLevelLists(WildernessLevelState level)
    {
        return level != null && level.nodes != null && level.repairedRoads != null && level.history != null;
    }

    static bool HasBridgeOutcome(WildernessEventDefinition definition)
    {
        if (definition == null || definition.choices == null) return false;
        foreach (var choice in definition.choices)
            if (choice != null && choice.outcomes != null && choice.outcomes.Exists(value => value != null && value.repairBridge))
                return true;
        return false;
    }

    static int SelectDefinition(List<WildernessEventDefinition> values, StableRandom random)
    {
        int total = 0;
        foreach (var value in values) total += value.weight;
        int cursor = random.Next(total);
        for (int i = 0; i < values.Count; i++)
        {
            cursor -= values[i].weight;
            if (cursor < 0) return i;
        }
        throw new InvalidOperationException("模板权重无效。");
    }

    static int SelectOutcome(List<WildernessOutcomeDefinition> values, StableRandom random)
    {
        int total = 0;
        foreach (var value in values) total += value.weight;
        int cursor = random.Next(total);
        for (int i = 0; i < values.Count; i++)
        {
            cursor -= values[i].weight;
            if (cursor < 0) return i;
        }
        throw new InvalidOperationException("结果权重无效。");
    }

    static string LocationLabel(string nodeId)
    {
        int number;
        if (nodeId != null && nodeId.StartsWith("field", StringComparison.Ordinal)
            && int.TryParse(nodeId.Substring(5), out number)) return "地点 " + number;
        if (nodeId != null && nodeId.Length > 3 && nodeId[0] == 'r')
        {
            int separator = nodeId.IndexOf('_');
            int region;
            if (separator > 1 && int.TryParse(nodeId.Substring(1, separator - 1), out region))
            {
                string suffix = nodeId.Substring(separator + 1);
                string prefix = "区域 " + region + " · ";
                if (suffix == "y") return prefix + "问号 1";
                if (suffix == "w") return prefix + "问号 2";
                if (suffix == "camp") return prefix + "营地";
                if (suffix == "boss") return prefix + "首领";
                return prefix + "路口";
            }
        }
        return "村庄入口";
    }

    static bool HasConfiguredQuestions(WildernessEventCatalog catalog)
    {
        return catalog.questionNodeIds != null && catalog.questionNodeIds.Count > 0;
    }

    static List<string> QuestionIds(WildernessEventCatalog catalog)
    {
        if (HasConfiguredQuestions(catalog)) return catalog.questionNodeIds;
        var result = new List<string>();
        for (int i = 1; i <= DefaultQuestionCount; i++) result.Add("field" + i.ToString("00"));
        return result;
    }

    static string Signed(int value) { return value > 0 ? "+" + value : value.ToString(); }
    static bool HasText(string value) { return !string.IsNullOrEmpty(value); }
    static WildernessChoiceResult Fail(string message) { return new WildernessChoiceResult { message = message }; }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // Xorshift32 with rejection sampling; generated rolls are persisted rather than replayed on load.
    sealed class StableRandom
    {
        uint state;
        public StableRandom(int seed, int stage)
        {
            state = unchecked((uint)seed ^ ((uint)stage * 0x9E3779B9u) ^ 0xA341316Cu);
            if (state == 0) state = 0x6D2B79F5u;
            for (int i = 0; i < 8; i++) NextUInt();
        }
        uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
        public int Next(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            uint limit = uint.MaxValue - uint.MaxValue % (uint)count;
            uint value;
            do { value = NextUInt(); } while (value >= limit);
            return (int)(value % (uint)count);
        }
    }
}
