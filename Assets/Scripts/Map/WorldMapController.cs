using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class MapNodeTransitionEvent : UnityEvent<MapNodeRequest> { }

[DefaultExecutionOrder(-100)]
public class WorldMapController : MonoBehaviour
{
    public WorldMapTemplate template;
    [Tooltip("Zero chooses a new seed when starting a new map.")]
    public int seed;
    [Header("External screen entry slots")]
    public MapNodeTransitionEvent onBattleEntered = new MapNodeTransitionEvent();
    public MapNodeTransitionEvent onEventEntered = new MapNodeTransitionEvent();
    public MapNodeTransitionEvent onCampEntered = new MapNodeTransitionEvent();
    public MapNodeTransitionEvent onBossEntered = new MapNodeTransitionEvent();
    public UnityEvent onVillageEntered = new UnityEvent();
    public UnityEvent onMapCompleted = new UnityEvent();

    public MapProgression Progression { get; private set; }
    public event Action StateChanged;
    public event Action<MapNodeRequest> NodeEntered;
    public string Notice { get; private set; }

    void Awake()
    {
        if (template == null) { Debug.LogError("Assign a WorldMapTemplate.", this); enabled = false; return; }
        if (GameRoot.I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        try
        {
            int mapSeed = seed == 0 ? Guid.NewGuid().GetHashCode() : seed;
            Progression = new MapProgression(template.Load(), GameRoot.I.state.map, GameRoot.I.state.world, mapSeed);
            Notice = "懒惰已出现";
        }
        catch (Exception error) { Debug.LogException(error, this); enabled = false; }
    }

    public void MoveToNode(string nodeId)
    {
        if (Progression == null || !Progression.TryMove(nodeId, out var request)) return;
        var node = Progression.GetNode(nodeId);
        Notice = node.displayName;
        if (Progression.LastSpawnedBossId != null)
            Notice += " · " + Progression.GetNode(Progression.LastSpawnedBossId).displayName + "已出现";
        StateChanged?.Invoke();
        if (request == null)
        {
            if (node.kind == MapNodeKind.Village) onVillageEntered.Invoke();
            return;
        }
        NodeEntered?.Invoke(request);
        switch (request.kind)
        {
            case MapNodeKind.Battle: onBattleEntered.Invoke(request); break;
            case MapNodeKind.Event: onEventEntered.Invoke(request); break;
            case MapNodeKind.Camp: onCampEntered.Invoke(request); break;
            case MapNodeKind.Boss: onBossEntered.Invoke(request); break;
        }
    }

    // External battle/event screens return the requestId they received on entry.
    public void CompleteNode(string requestId)
    {
        if (Progression == null || !Progression.TryCompleteNode(requestId)) return;
        Notice = Progression.IsComplete ? "三个原罪区域已完成" : "节点已完成";
        StateChanged?.Invoke();
        if (Progression.IsComplete) onMapCompleted.Invoke();
    }
}
