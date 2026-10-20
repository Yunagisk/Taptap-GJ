using System;

// Reproducible for the same map seed, node ID and progression tier.
public static class SmallMonsterEncounterRules
{
    static readonly SmallMonsterKind[][] Rosters =
    {
        new[] { SmallMonsterKind.Rat, SmallMonsterKind.Rat },
        new[] { SmallMonsterKind.Rat, SmallMonsterKind.Crow },
        new[] { SmallMonsterKind.Rat, SmallMonsterKind.Wraith },
        new[] { SmallMonsterKind.Crow, SmallMonsterKind.Crow },
        new[] { SmallMonsterKind.Crow, SmallMonsterKind.Wraith },
        new[] { SmallMonsterKind.Rat, SmallMonsterKind.Rat, SmallMonsterKind.Rat },
        new[] { SmallMonsterKind.Rat, SmallMonsterKind.Rat, SmallMonsterKind.Crow },
        new[] { SmallMonsterKind.Rat, SmallMonsterKind.Rat, SmallMonsterKind.Wraith },
        new[] { SmallMonsterKind.Rat, SmallMonsterKind.Crow, SmallMonsterKind.Wraith },
    };

    public static SmallMonsterKind[] Choose(int mapSeed, string nodeId, int clearedBattles)
    {
        if (clearedBattles <= 0) return (SmallMonsterKind[])Rosters[0].Clone();
        int[] weights = clearedBattles < 3 ? new[] { 40, 35, 25, 0, 0, 0, 0, 0, 0 }
            : clearedBattles < 6 ? new[] { 20, 25, 20, 15, 20, 0, 0, 0, 0 }
            : new[] { 10, 15, 15, 10, 10, 10, 10, 10, 10 };
        uint seed = unchecked((uint)mapSeed) ^ 2166136261u;
        foreach (char c in nodeId ?? "standalone") seed = unchecked((seed ^ c) * 16777619u);
        int roll = new Random(unchecked((int)seed)).Next(100);
        for (int i = 0; i < weights.Length; i++)
        { roll -= weights[i]; if (roll < 0) return (SmallMonsterKind[])Rosters[i].Clone(); }
        throw new InvalidOperationException("Encounter weights must sum to 100.");
    }
}
