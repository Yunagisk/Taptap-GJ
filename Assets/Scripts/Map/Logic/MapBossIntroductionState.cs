using System.Collections.Generic;

// Presentation memory belongs to a run, and survives returning from another scene.
// Replacing MapState for a new run gives it a fresh introduction without touching progression.
public sealed class MapBossIntroductionState
{
    MapState owner;
    readonly HashSet<string> shown = new HashSet<string>();

    public bool HasShown(MapState state, string nodeId) => ReferenceEquals(owner, state) && shown.Contains(nodeId);

    public void MarkShown(MapState state, string nodeId)
    {
        if (!ReferenceEquals(owner, state)) { owner = state; shown.Clear(); }
        shown.Add(nodeId);
    }

    public MapNodeDefinition NextSloth(MapProgression map)
    {
        foreach (var region in map.Layout.regions)
            if (region.bossKey == "sloth" && map.IsVisible(region.bossNodeId)
                && !map.State.clearedNodes.Contains(region.bossNodeId) && !HasShown(map.State, region.bossNodeId))
                return map.GetNode(region.bossNodeId);
        return null;
    }
}
