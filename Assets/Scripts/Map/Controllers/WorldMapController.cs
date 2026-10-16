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
    public MapSceneTransition entryTransitionPrefab;
    bool isLoadingBattle;
    bool entryDispatched;
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
    public bool IsEnteringNode { get; private set; }
    public bool IsWaitingToEnter => IsEnteringNode && !entryDispatched;

    void Awake()
    {
        if (template == null) { Debug.LogError("Assign a WorldMapTemplate.", this); enabled = false; return; }
        if (GameRoot.I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        try
        {
            int mapSeed = seed == 0 ? Guid.NewGuid().GetHashCode() : seed;
            Progression = new MapProgression(template.Load(), GameRoot.I.state.map, GameRoot.I.state.world, mapSeed);
            MainStoryRules.EnsureInitialized(GameRoot.I.state, Progression.Layout);
            EquipmentRules.EnsureInitialized(GameRoot.I.state);
            Notice = "懒惰已出现";
        }
        catch (Exception error) { Debug.LogException(error, this); enabled = false; }
    }

    void Start()
    {
        var pending = Progression?.GetPendingRequest();
        if (pending != null && pending.kind == MapNodeKind.Battle) BeginEntry(pending);
    }

    AsyncOperation EnterBattle(MapNodeRequest request)
    {
        if (isLoadingBattle) return null;
        if (!Application.CanStreamedLevelBeLoaded(BattleScene))
        {
            Debug.LogError("CombatScene is missing from Build Settings.", this);
            return null;
        }
        var root = GameRoot.I;
        root.battleMapTemplate = template;
        root.battleRequestId = request.requestId;
        root.battleReturnScene = gameObject.scene.path;
        try
        {
            isLoadingBattle = true;
            return MapSceneTransition.LoadSceneAsync(BattleScene);
        }
        catch (Exception error)
        {
            isLoadingBattle = false;
            Debug.LogException(error, this);
            return null;
        }
    }

    public void MoveToNode(string nodeId)
    {
        if (isLoadingBattle || IsEnteringNode || MapSceneTransition.IsBusy || Progression == null) return;
        if (entryTransitionPrefab == null)
        {
            Debug.LogError("Assign the map scene transition prefab before entering nodes.", this);
            return;
        }
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
        if (destination != null && destination.kind == MapNodeKind.Battle
            && !Progression.State.clearedNodes.Contains(nodeId))
        {
            EquipmentRules.EnsureInitialized(GameRoot.I.state);
            if (GameRoot.I.state.inventory.equippedWeaponIds.TrueForAll(string.IsNullOrEmpty))
            {
                Notice = "\u8bf7\u5148\u5728\u80cc\u5305\u4e2d\u88c5\u5907\u81f3\u5c11\u4e00\u628a\u6b66\u5668";
                StateChanged?.Invoke();
                return;
            }
        }
        if (Progression.CanReenterVillage(nodeId))
        {
            Notice = destination.displayName;
            BeginEntry(null, true);
            return;
        }
        if (!Progression.TryMove(nodeId, out var request)) return;
        var node = Progression.GetNode(nodeId);
        Notice = node.displayName;
        if (Progression.LastSpawnedBossId != null)
            Notice += " · " + Progression.GetNode(Progression.LastSpawnedBossId).displayName + "已出现";
        if (request == null)
        {
            if (node.kind == MapNodeKind.Village) BeginEntry(null, true);
            else StateChanged?.Invoke();
            return;
        }
        BeginEntry(request);
    }

    void BeginEntry(MapNodeRequest request, bool village = false)
    {
        if (IsEnteringNode || MapSceneTransition.IsBusy) return;
        IsEnteringNode = true;
        entryDispatched = false;
        // Refresh moves the marker and starts its writing animation before the
        // transition coroutine waits for it. Gameplay callbacks stay deferred.
        StateChanged?.Invoke();
        if (!MapSceneTransition.Begin(entryTransitionPrefab, this, () => DispatchEntry(request, village), () =>
        {
            if (this == null) return;
            IsEnteringNode = false;
            entryDispatched = false;
            if (GameRoot.I != null) StateChanged?.Invoke();
        }))
        {
            IsEnteringNode = false;
            StateChanged?.Invoke();
        }
    }

    AsyncOperation DispatchEntry(MapNodeRequest request, bool village)
    {
        if (!village && (request == null || Progression.GetPendingRequest()?.requestId != request.requestId)) return null;
        entryDispatched = true;
        StateChanged?.Invoke();
        if (village) { onVillageEntered.Invoke(); return null; }
        NodeEntered?.Invoke(request);
        switch (request.kind)
        {
            case MapNodeKind.Battle: onBattleEntered.Invoke(request); return EnterBattle(request);
            case MapNodeKind.Event: onEventEntered.Invoke(request); break;
            case MapNodeKind.Camp: onCampEntered.Invoke(request); break;
            case MapNodeKind.Boss: onBossEntered.Invoke(request); break;
        }
        return null;
    }

    // External battle/event screens return the requestId they received on entry.
    public void CompleteNode(string requestId)
    {
        if (Progression == null || IsWaitingToEnter) return;
        var completed = Progression.GetPendingRequest();
        var before = MapRevealTransition.Capture(Progression);
        if (!Progression.TryCompleteNode(requestId)) return;
        MainStoryRules.OnNodeCompleted(GameRoot.I.state, completed);
        GameRoot.I.pendingMapReveal = MapRevealTransition.AfterCompletion(Progression, before, completed.nodeId);
        Notice = Progression.IsComplete ? "三个原罪区域已完成" : "节点已完成";
        StateChanged?.Invoke();
        if (Progression.IsComplete) onMapCompleted.Invoke();
    }
}
