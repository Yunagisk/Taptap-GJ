using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

[Serializable]
public sealed class VillageEventState
{
    public bool initialized;
    public int randomSeed;
    public int rollCount;
    public int lastProcessedTime = -1;
    // Retain all occurrences, including expired and resolved ones: each definition appears only once per run.
    public List<VillageEventOccurrence> occurrences = new List<VillageEventOccurrence>();
    // Temporary item rewards are descriptive records, never actual inventory grants.
    public List<string> rewardTexts = new List<string>();
}

[Serializable]
public sealed class VillageEventOccurrence
{
    public string id;
    public string definitionId;
    public int appearedTime;
    public int expiresTime;
    public bool resolved;
    public int chosenIndex = -1;
    public bool expired;
}

public sealed class VillageEventDefinition
{
    public string id;
    public string title;
    public string description;
    public int earliestDay;
    public int durationDays;
    public int weight;
    public bool requiresLivingSloth;
    public VillageEventChoice[] choices;
}

public sealed class VillageEventChoice
{
    public string label;
    public int timeCost;
    public int sinDelta;
    public int healAmount;
    public string rewardText;
    public int nextDirectHitReductionPercent;
    public int nextAttackBonusPercent;
    public int nextCampRecoveryBonusPercent;
}

public sealed class VillageEventChoiceResult
{
    public bool success;
    public string message;
    public string occurrenceId;
    public string spawnedBossId;
}

// All tavern choices settle through shared world/player resources. Views may inspect, but never roll events.
public static class VillageEventRules
{
    public const int TimeUnitsPerDay = 3;
    public const int AttemptsPerTimeUnit = 2;
    public const int AppearanceChancePercent = 55;

    static readonly VillageEventDefinition[] definitions =
    {
        Define("v01", "粮仓分配", "粮仓前排起了长队，有限的补给需要重新分配。", 1, 3, 3,
            Choice("协助分配", 1, -15),
            Choice("独占补给", 1, 20, reward: "绷带 ×1")),
        Define("v02", "告解争执", "求助的人与村民争执不下，双方都希望有人出面。", 1, 2, 3,
            Choice("耐心调解", 1, -12, heal: 10),
            Choice("驱逐求助者", 1, 15, directHitReduction: 15)),
        Define("v03", "安葬无名者", "一名无人认领的死者仍停留在村边，尚未得到安葬。", 3, 3, 2,
            Choice("协助安葬", 2, -20, reward: "净化剂 ×1"),
            Choice("夺取遗物", 1, 20, reward: "绷带 ×1")),
        Define("v04", "药房求援", "药房人手不足，救济药品还没有分发给需要的人。", 4, 3, 2,
            Choice("整理救济药品", 1, -15, reward: "绷带 ×1"),
            Choice("优先获得自己用药", 1, 15, heal: 25)),
        Define("v05", "邻里告密", "一则未经证实的传言，让邻里间的猜疑越来越深。", 6, 2, 2,
            Choice("查清谣言", 1, -15),
            Choice("附和指控", 1, 20, attackBonus: 15)),
        Define("v06", "守夜缺席", "今晚的守夜人没有到场，村口还缺一个值守的人。", 7, 3, 2,
            Choice("承担守夜", 2, -20, reward: "坚定药剂 ×1"),
            Choice("推诿他人", 1, 15)),
        Define("v07", "宴席余粮", "宴席散去后还剩下一些食物，门外有人仍在挨饿。", 9, 2, 2,
            Choice("分给饥饿者", 1, -15, heal: 10),
            Choice("独自享用", 1, 20, heal: 25)),
        Define("v08", "替罪羊", "惊恐的村民围住一名被指控的人，却没有拿出证据。", 10, 2, 2,
            Choice("保护被指控者", 2, -20, reward: "浓缩药剂 ×1"),
            Choice("任由围攻", 1, 25)),
        Define("v09", "封闭的仓门", "仓门紧闭，囤积的物资迟迟没有分给村民。", 12, 3, 1,
            Choice("说服囤积者", 1, -18, reward: "绷带 ×1"),
            Choice("抢取材料", 1, 20, reward: "浓缩药剂 ×1")),
        Define("v10", "破损的钟绳", "村中的警钟绳索已经破损，需要有人花时间修复。", 14, 4, 1,
            Choice("修复钟绳", 2, -20, campRecoveryBonus: 20),
            Choice("放弃修复", 1, 12)),
        Define("v11", "未寄的家书", "一封没有寄出的家书与几件遗物，被留在酒馆的角落。", 16, 3, 1,
            Choice("送还遗物", 1, -15, reward: "净化剂 ×1"),
            Choice("取走药布", 1, 15, reward: "绷带 ×1")),
        Define("v12", "最后的互助", "村民希望设立临时救济点，但仅存的人手和物资都很有限。", 20, 4, 1,
            Choice("建立救济点", 2, -25, reward: "浓缩药剂 ×1"),
            Choice("只顾自己", 1, 20, attackBonus: 15)),
        Define("v13", "酒馆密谋", "有人在酒馆里商量趁乱抢劫。懒惰首领仍在，恐惧正让人们失去克制。", 1, 2, 2,
            Choice("阻止两人", 1, -15),
            Choice("无动于衷", 1, 0),
            Choice("参与抢劫", 1, 20), true)
    };

    // Repairs missing fields without changing the clock, sin, health, or an existing run's event history.
    public static void EnsureInitialized(WorldState world)
    {
        if (world == null) return;
        if (world.villageEvents == null) world.villageEvents = new VillageEventState();
        if (world.villageEvents.occurrences == null)
            world.villageEvents.occurrences = new List<VillageEventOccurrence>();
        if (world.villageEvents.rewardTexts == null)
            world.villageEvents.rewardTexts = new List<string>();
    }

    // Called once from run initialization, never from opening a view. Migrated runs do not roll for past time.
    public static void InitializeForRun(GameState state, MapLayout layout)
    {
        if (state == null || state.world == null || state.map == null) return;
        EnsureInitialized(state.world);
        var events = state.world.villageEvents;
        Expire(state.world, state.map, layout);
        if (events.initialized) return;
        events.randomSeed = state.map.randomSeed;
        events.initialized = true;
        int now = Math.Max(0, state.world.time);
        events.lastProcessedTime = Math.Max(events.lastProcessedTime, now);
        GenerateAt(state.world, state.map, layout, now);
    }

    // SpendTime invokes this for each elapsed world unit, whether in the village or outside it.
    public static void OnTimeAdvanced(WorldState world, MapState map, MapLayout layout)
    {
        if (world == null || map == null) return;
        EnsureInitialized(world);
        var events = world.villageEvents;
        int now = Math.Max(0, world.time);
        if (!events.initialized)
        {
            events.initialized = true;
            events.randomSeed = map.randomSeed;
            // An old save is migrated at its current tick, without generating a backlog.
            events.lastProcessedTime = now - 1;
        }
        for (long tick = (long)events.lastProcessedTime + 1; tick <= now; tick++)
        {
            ExpireAt(events, map, layout, (int)tick);
            // Commit the tick before rolling so reentrant or repeated callbacks cannot reroll it.
            events.lastProcessedTime = (int)tick;
            GenerateAt(world, map, layout, (int)tick);
        }
        Expire(world, map, layout);
    }

    // Reading the tavern is free: this only invalidates existing occurrences and returns the live list.
    public static List<VillageEventOccurrence> GetActiveEvents(GameState state, MapLayout layout)
    {
        var active = new List<VillageEventOccurrence>();
        if (state == null || state.world == null || state.map == null) return active;
        EnsureInitialized(state.world);
        Expire(state.world, state.map, layout);
        foreach (var occurrence in state.world.villageEvents.occurrences)
            if (occurrence != null && !occurrence.resolved && !occurrence.expired
                && FindDefinition(occurrence.definitionId) != null)
                active.Add(occurrence);
        return active;
    }

    public static VillageEventDefinition FindDefinition(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var definition in definitions)
            if (definition.id == id) return definition;
        return null;
    }

    public static VillageEventChoiceResult TryChoose(GameState state, MapLayout layout,
        string occurrenceId, int choiceIndex)
    {
        var result = new VillageEventChoiceResult { occurrenceId = occurrenceId };
        if (state == null || state.world == null || state.map == null || state.player == null || layout == null)
            return Fail(result, "当前无法处理这件事。");
        if (state.map.currentNodeId != layout.villageNodeId
            || !string.IsNullOrEmpty(state.map.pendingRequestId) || !string.IsNullOrEmpty(state.map.pendingNodeId))
            return Fail(result, "回到村庄后才能处理酒馆事件。");
        // Validate before even repairing/expiring event state: a malformed template must have no side effects.
        try
        {
            MapLayoutValidator.Validate(layout);
        }
        catch (Exception)
        {
            // The validator can throw for incomplete lists as well as an explicitly invalid configuration.
            // Only configuration validation is caught; settlement failures remain visible to the caller.
            return Fail(result, "当前世界配置不可用，无法处理这件事。");
        }
        EnsureInitialized(state.world);
        Expire(state.world, state.map, layout);
        var occurrence = state.world.villageEvents.occurrences.Find(
            item => item != null && item.id == occurrenceId);
        if (occurrence == null || occurrence.resolved || occurrence.expired)
            return Fail(result, "这件事已经结束或已过期。");
        var definition = FindDefinition(occurrence.definitionId);
        if (definition == null || definition.choices == null
            || choiceIndex < 0 || choiceIndex >= definition.choices.Length)
            return Fail(result, "请选择有效的处理方式。");
        var choice = definition.choices[choiceIndex];
        if (choice == null || choice.timeCost < 1
            || (long)Math.Max(0, state.world.time) + choice.timeCost > int.MaxValue)
            return Fail(result, "这件事暂时无法处理。");

        // Commit the one-time choice before any resource mutation or elapsed-time callbacks.
        occurrence.resolved = true;
        occurrence.chosenIndex = choiceIndex;
        result.spawnedBossId = WorldResourceRules.SpendTime(layout, state.map, state.world,
            choice.timeCost, true);
        string spawnedByChoice = WorldResourceRules.ChangeSin(layout, state.map, state.world, choice.sinDelta);
        if (!string.IsNullOrEmpty(spawnedByChoice)) result.spawnedBossId = spawnedByChoice;
        int healed = WorldResourceRules.Heal(state.player, choice.healAmount);
        state.player.nextDirectHitReductionPercent = Math.Max(state.player.nextDirectHitReductionPercent,
            choice.nextDirectHitReductionPercent);
        state.player.nextAttackBonusPercent = Math.Max(state.player.nextAttackBonusPercent,
            choice.nextAttackBonusPercent);
        state.player.nextCampRecoveryBonusPercent = Math.Max(state.player.nextCampRecoveryBonusPercent,
            choice.nextCampRecoveryBonusPercent);

        var message = new StringBuilder();
        message.Append(definition.title).Append("：").Append(choice.label).Append("。\n");
        message.Append("消耗 ").Append(choice.timeCost).Append(" 单位时间；罪恶 ")
            .Append(choice.sinDelta >= 0 ? "+" : "").Append(choice.sinDelta)
            .Append("（当前 ").Append(state.world.sin).Append("/100）");
        if (choice.healAmount > 0)
            message.Append("；恢复生命 ").Append(healed);
        if (choice.nextDirectHitReductionPercent > 0)
            message.Append("；下次直接受击伤害 -").Append(choice.nextDirectHitReductionPercent).Append("%");
        if (choice.nextAttackBonusPercent > 0)
            message.Append("；下次完整攻击伤害 +").Append(choice.nextAttackBonusPercent).Append("%");
        if (choice.nextCampRecoveryBonusPercent > 0)
            message.Append("；下次营地恢复 +").Append(choice.nextCampRecoveryBonusPercent).Append("%");
        message.Append("。");
        if (!string.IsNullOrEmpty(choice.rewardText))
        {
            string reward = WorldResourceRules.TimeLabel(state.world) + " · " + definition.title
                + "：" + choice.rewardText;
            state.world.villageEvents.rewardTexts.Add(reward);
        }
        if (!string.IsNullOrEmpty(result.spawnedBossId))
            message.Append("\n罪恶达到 100，新的原罪首领已经出现，罪恶归零。");
        result.success = true;
        result.message = message.ToString();
        return result;
    }

    static VillageEventChoiceResult Fail(VillageEventChoiceResult result, string message)
    {
        result.message = message;
        return result;
    }

    static void Expire(WorldState world, MapState map, MapLayout layout)
    {
        ExpireAt(world.villageEvents, map, layout, Math.Max(0, world.time));
    }

    static void ExpireAt(VillageEventState events, MapState map, MapLayout layout, int time)
    {
        foreach (var occurrence in events.occurrences)
        {
            if (occurrence == null || occurrence.resolved || occurrence.expired) continue;
            var definition = FindDefinition(occurrence.definitionId);
            if (definition == null || time >= occurrence.expiresTime
                || (definition.requiresLivingSloth && SlothIsDefeated(map, layout)))
                occurrence.expired = true;
        }
    }

    static void GenerateAt(WorldState world, MapState map, MapLayout layout, int time)
    {
        var events = world.villageEvents;
        for (int attempt = 0; attempt < AttemptsPerTimeUnit; attempt++)
        {
            if (Next(events, 100) >= AppearanceChancePercent) continue;
            var eligible = new List<VillageEventDefinition>();
            int totalWeight = 0;
            int day = time / TimeUnitsPerDay + 1;
            foreach (var definition in definitions)
            {
                if (day < definition.earliestDay
                    || (definition.requiresLivingSloth && !SlothIsAlive(map, layout))
                    || HasAppeared(events, definition.id)) continue;
                eligible.Add(definition);
                totalWeight += definition.weight;
            }
            if (eligible.Count == 0 || totalWeight <= 0) continue;
            int roll = Next(events, totalWeight);
            VillageEventDefinition selected = null;
            foreach (var definition in eligible)
            {
                roll -= definition.weight;
                if (roll >= 0) continue;
                selected = definition;
                break;
            }
            if (selected == null) continue;
            long expires = (long)time + selected.durationDays * TimeUnitsPerDay;
            events.occurrences.Add(new VillageEventOccurrence
            {
                id = "village:" + selected.id + ":" + time + ":" + events.occurrences.Count,
                definitionId = selected.id,
                appearedTime = time,
                expiresTime = (int)Math.Min(int.MaxValue, expires)
            });
        }
    }

    static bool HasAppeared(VillageEventState events, string definitionId)
    {
        foreach (var occurrence in events.occurrences)
            if (occurrence != null && occurrence.definitionId == definitionId)
                return true;
        return false;
    }

    static bool SlothIsAlive(MapState map, MapLayout layout)
    {
        if (map == null || layout == null || layout.regions == null) return false;
        foreach (var region in layout.regions)
            if (region != null && region.bossKey == "sloth")
                return map.defeatedBossNodes == null || !map.defeatedBossNodes.Contains(region.bossNodeId);
        return false;
    }

    static bool SlothIsDefeated(MapState map, MapLayout layout)
    {
        // A temporarily unavailable layout is not evidence that a previously active event has expired.
        if (map == null || map.defeatedBossNodes == null || layout == null || layout.regions == null)
            return false;
        foreach (var region in layout.regions)
            if (region != null && region.bossKey == "sloth")
                return map.defeatedBossNodes.Contains(region.bossNodeId);
        return false;
    }

    static int Next(VillageEventState events, int exclusiveMax)
    {
        byte[] bytes;
        using (var hash = SHA256.Create())
            bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(events.randomSeed + ":village:" + events.rollCount));
        unchecked { events.rollCount++; }
        uint value = (uint)bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16) | ((uint)bytes[3] << 24);
        return (int)(value % (uint)exclusiveMax);
    }

    static VillageEventChoice Choice(string label, int time, int sin, int heal = 0, string reward = null,
        int directHitReduction = 0, int attackBonus = 0, int campRecoveryBonus = 0)
    {
        return new VillageEventChoice
        {
            label = label, timeCost = time, sinDelta = sin, healAmount = heal, rewardText = reward,
            nextDirectHitReductionPercent = directHitReduction,
            nextAttackBonusPercent = attackBonus,
            nextCampRecoveryBonusPercent = campRecoveryBonus
        };
    }

    static VillageEventDefinition Define(string id, string title, string description, int earliestDay,
        int durationDays, int weight, VillageEventChoice first, VillageEventChoice second)
    {
        return new VillageEventDefinition
        {
            id = id, title = title, description = description,
            earliestDay = earliestDay, durationDays = durationDays, weight = weight,
            choices = new[] { first, second }
        };
    }

    static VillageEventDefinition Define(string id, string title, string description, int earliestDay,
        int durationDays, int weight, VillageEventChoice first, VillageEventChoice second,
        VillageEventChoice third, bool requiresLivingSloth)
    {
        var definition = Define(id, title, description, earliestDay, durationDays, weight, first, second);
        definition.choices = new[] { first, second, third };
        definition.requiresLivingSloth = requiresLivingSloth;
        return definition;
    }
}
