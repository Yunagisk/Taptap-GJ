using System;
using System.Collections.Generic;

public enum MapNodeKind { Village, Battle, Event, Camp, Boss }

[Serializable]
public class MapNodeDefinition
{
    public string id;
    public string displayName;
    public MapNodeKind kind;
    public int region;
    public float x;
    public float y;
    public List<string> neighbors = new List<string>();
}

[Serializable]
public class MapRegionDefinition
{
    public int id;
    public string displayName;
    public string bossNodeId;
    public string bossKey;
    public bool startsSpawned;
}

[Serializable]
public class MapLayout
{
    public string id = "three-sins-whitebox-v1";
    public string villageNodeId = "village";
    public int initialSin = 40;
    public int sinThreshold = 100;
    public int sinPerMoveMin = 8;
    public int sinPerMoveMax = 12;
    public float bossNodeClearance;
    public float bossApproachDistance;
    public List<MapRegionDefinition> regions = new List<MapRegionDefinition>();
    public List<MapNodeDefinition> nodes = new List<MapNodeDefinition>();
}

[Serializable]
public class MapNodeRequest
{
    public string requestId;
    public string nodeId;
    public MapNodeKind kind;
    public int region;
    public string bossKey;
}
