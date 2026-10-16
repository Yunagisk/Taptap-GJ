using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/World Map Visual Layout")]
public class WorldMapVisualLayout : ScriptableObject
{
    [Serializable]
    public class NodePosition
    {
        public string nodeId;
        public Vector2 position;
    }

    public string mapId;
    public Vector2 mapSize = new Vector2(3840, 2160);
    public Vector2 villageArtworkSize = new Vector2(1920, 1080);
    public List<NodePosition> nodes = new List<NodePosition>();

    public Vector2 PositionFor(MapNodeDefinition node)
    {
        foreach (var entry in nodes) if (entry.nodeId == node.id) return entry.position;
        return new Vector2(node.x, node.y);
    }

    public void Validate(MapLayout map)
    {
        if (mapId != map.id) throw new InvalidOperationException("Visual layout belongs to another map.");
        if (mapSize.x < 1920 || mapSize.y < 1080 || villageArtworkSize.x <= 0 || villageArtworkSize.y <= 0)
            throw new InvalidOperationException("Invalid visual map dimensions.");
        var ids = new HashSet<string>();
        foreach (var node in nodes)
            if (string.IsNullOrEmpty(node.nodeId) || !ids.Add(node.nodeId)
                || !IsFinite(node.position.x) || !IsFinite(node.position.y)
                || Mathf.Abs(node.position.x) > mapSize.x * 0.5f - 70
                || Mathf.Abs(node.position.y) > mapSize.y * 0.5f - 70)
                throw new InvalidOperationException("Invalid or duplicate visual node position: " + node.nodeId);
        if (ids.Count != map.nodes.Count) throw new InvalidOperationException("Visual layout must include every map node.");
        foreach (var node in map.nodes)
            if (!ids.Contains(node.id)) throw new InvalidOperationException("Missing visual node: " + node.id);
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
