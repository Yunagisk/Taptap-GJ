using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

// A single clock and sin balance shared by the village, map and combat settlement.
public static class WorldResourceRules
{
    public const int InitialSin = 40;
    public const int MaxSin = 100;
    public const int UnitsPerDay = 3;
    static readonly string[] Periods = { "早晨", "下午", "夜间" };

    public static void Normalize(WorldState world)
    {
        if (world == null) return;
        world.time = Math.Max(0, world.time);
        world.sin = Math.Max(0, Math.Min(MaxSin, world.sin));
        VillageEventRules.EnsureInitialized(world);
    }

    public static int Day(WorldState world)
    {
        return Math.Max(0, world != null ? world.time : 0) / UnitsPerDay + 1;
    }

    public static string TimeLabel(WorldState world)
    {
        int time = Math.Max(0, world != null ? world.time : 0);
        return "第 " + (time / UnitsPerDay + 1) + " 天 " + Periods[time % UnitsPerDay];
    }

    // Returns the last newly spawned boss, if this action reaches the threshold.
    public static string SpendTime(MapLayout layout, MapState map, WorldState world, int units, bool inVillage)
    {
        if (layout == null || map == null || world == null)
            throw new ArgumentNullException("Time spending requires the shared map and world.");
        if (units < 0 || (long)world.time + units > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(units));
        Normalize(world);
        string spawned = null;
        for (int i = 0; i < units; i++)
        {
            world.time++;
            if (!inVillage)
            {
                int amount = NaturalSin(layout, map);
                string next = ChangeSin(layout, map, world, amount);
                if (next != null) spawned = next;
            }
            // Every dawn stores the current node, including dawn spent inside the village.
            if (world.time % UnitsPerDay == 0)
            {
                map.checkpointNodeId = map.currentNodeId;
                map.checkpointTime = world.time;
            }
            VillageEventRules.OnTimeAdvanced(world, map, layout);
        }
        return spawned;
    }

    public static string ChangeSin(MapLayout layout, MapState map, WorldState world, int delta)
    {
        if (world == null) return null;
        world.sin = (int)Math.Max(0L, Math.Min((long)MaxSin, (long)world.sin + delta));
        if (layout == null || map == null || world.sin < layout.sinThreshold || delta <= 0)
            return null;
        if (map.spawnedBossNodes == null) map.spawnedBossNodes = new List<string>();
        if (map.revealedNodes == null) map.revealedNodes = new List<string>();
        foreach (var region in layout.regions)
        {
            if (map.spawnedBossNodes.Contains(region.bossNodeId)) continue;
            map.spawnedBossNodes.Add(region.bossNodeId);
            if (!map.revealedNodes.Contains(region.bossNodeId)) map.revealedNodes.Add(region.bossNodeId);
            // Keep the existing threshold spawn/reset rule; clearing an enemy never subtracts sin.
            world.sin = 0;
            return region.bossNodeId;
        }
        // Natural growth continues after every boss has appeared and stops at the global cap.
        return null;
    }

    static int NaturalSin(MapLayout layout, MapState map)
    {
        byte[] bytes;
        using (var hash = SHA256.Create())
            bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(map.randomSeed + ":sin:" + map.sinRollCount));
        map.sinRollCount++;
        uint roll = (uint)bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16) | ((uint)bytes[3] << 24);
        return layout.sinPerMoveMin + (int)(roll % (uint)(layout.sinPerMoveMax - layout.sinPerMoveMin + 1));
    }

    public static int Heal(PlayerState player, int amount)
    {
        if (player == null || amount <= 0 || player.maxHp <= 0) return 0;
        int oldHp = Math.Max(0, Math.Min(player.maxHp, player.hp));
        player.hp = (int)Math.Min(player.maxHp, (long)oldHp + amount);
        return player.hp - oldHp;
    }

    public static int RecoverAtCamp(PlayerState player)
    {
        if (player == null || player.hp >= player.maxHp) return 0;
        // The prototype camp restores 25% max HP; its one-use bonus scales that recovery.
        int bonus = Math.Max(0, player.nextCampRecoveryBonusPercent);
        int amount = Math.Max(1, (int)Math.Ceiling(player.maxHp * 0.25d * (1d + bonus / 100d)));
        int recovered = Heal(player, amount);
        if (recovered > 0) player.nextCampRecoveryBonusPercent = 0;
        return recovered;
    }
}
