using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[Serializable]
public class MapNodeTransitionEvent : UnityEvent<MapNodeRequest> { }

[DefaultExecutionOrder(-100)]
public class WorldMapController : MonoBehaviour
{
    public const string BattleScene = "CombatScene";
    public WorldMapTemplate template;
    bool isLoadingBattle;
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

    void Start()
    {
        var pending = Progression?.GetPendingRequest();
        if (pending != null && pending.kind == MapNodeKind.Battle) EnterBattle(pending);
    }

    void EnterBattle(MapNodeRequest request)
    {
        if (isLoadingBattle) return;
        if (!Application.CanStreamedLevelBeLoaded(BattleScene))
        {
            Debug.LogError("CombatScene is missing from Build Settings.", this);
            return;
        }
        var root = GameRoot.I;
        root.battleMapTemplate = template;
        root.battleRequestId = request.requestId;
        root.battleReturnScene = gameObject.scene.path;
        try
        {
            isLoadingBattle = true;
            SceneManager.LoadSceneAsync(BattleScene);
        }
        catch (Exception error)
        {
            isLoadingBattle = false;
            Debug.LogException(error, this);
        }
    }

    public void MoveToNode(string nodeId)
    {
        if (isLoadingBattle || Progression == null) return;
        var destination = Progression.GetNode(nodeId);
        if (destination != null && destination.kind == MapNodeKind.Village
            && !Application.CanStreamedLevelBeLoaded(GameFlowController.VillageScene))
        {
            Notice = "村庄场景未加入构建列表";
            Debug.LogError("Village is missing from Build Settings.", this);
            StateChanged?.Invoke();
            return;
        }
        if (destination != null && destination.kind == MapNodeKind.Battle
            && !Progression.State.clearedNodes.Contains(nodeId)
            && !Application.CanStreamedLevelBeLoaded(BattleScene))
        {
            Notice = "战斗场景未加入构建列表";
            Debug.LogError("CombatScene is missing from Build Settings.", this);
            StateChanged?.Invoke();
            return;
        }
        if (Progression.CanReenterVillage(nodeId))
        {
            Notice = destination.displayName;
            StateChanged?.Invoke();
            onVillageEntered.Invoke();
            return;
        }
        if (!Progression.TryMove(nodeId, out var request)) return;
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
            case MapNodeKind.Battle: onBattleEntered.Invoke(request); EnterBattle(request); break;
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
