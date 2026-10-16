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
<<<<<<< HEAD
    public VillageEntryConfirmationUI villageEntryConfirmation;
    public MapWildernessEventUI wildernessEventUI;
    public WildernessEventCatalog WildernessCatalog { get; private set; }
    public bool IsWildernessEventOpen => wildernessEventUI != null && wildernessEventUI.IsOpen;
    GameState wildernessRun;
    bool isLoadingBattle;
    string pendingVillageNodeId;
    string pendingVillageOriginId;
    GameState pendingVillageRun;

    public bool IsVillageEntryConfirmationOpen => !string.IsNullOrEmpty(pendingVillageNodeId);

    bool IsChangingScene => isLoadingBattle || villageEntryConfirmation != null
        && villageEntryConfirmation.flow != null && villageEntryConfirmation.flow.IsLoading;
=======
    public MapSceneTransition entryTransitionPrefab;
    bool isLoadingBattle;
    bool entryDispatched;
>>>>>>> origin/Feature/MapFunctional
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
            VillageEventRules.InitializeForRun(GameRoot.I.state, Progression.Layout);
            InitializeWilderness();
            Notice = "懒惰已出现";
        }
        catch (Exception error) { Debug.LogException(error, this); enabled = false; }
    }

    void Start()
    {
        var pending = Progression?.GetPendingRequest();
<<<<<<< HEAD
        if (pending != null && pending.kind == MapNodeKind.Battle) EnterBattle(pending);
        else if (pending != null && pending.kind == MapNodeKind.Event) OpenWildernessEvent(pending);
=======
        if (pending != null && pending.kind == MapNodeKind.Battle) BeginEntry(pending);
>>>>>>> origin/Feature/MapFunctional
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
<<<<<<< HEAD
        MoveToNode(nodeId, false);
    }

    void MoveToNode(string nodeId, bool villageConfirmed)
    {
        if (IsChangingScene || Progression == null || IsVillageEntryConfirmationOpen || IsWildernessEventOpen) return;
=======
        if (isLoadingBattle || IsEnteringNode || MapSceneTransition.IsBusy || Progression == null) return;
        if (entryTransitionPrefab == null)
        {
            Debug.LogError("Assign the map scene transition prefab before entering nodes.", this);
            return;
        }
>>>>>>> origin/Feature/MapFunctional
        var destination = Progression.GetNode(nodeId);
        if (destination != null && destination.kind == MapNodeKind.Event
            && !Progression.State.clearedNodes.Contains(nodeId)
            && (!HasWildernessContext() || wildernessEventUI == null || !wildernessEventUI.CanShow()))
        {
            Notice = "野外事件界面尚未就绪。";
            StateChanged?.Invoke();
            return;
        }
        if (destination != null && destination.kind == MapNodeKind.Village
            && !Application.CanStreamedLevelBeLoaded(GameFlowController.VillageScene))
        {
            Notice = "村庄场景未加入构建列表";
            Debug.LogError("Village is missing from Build Settings.", this);
            StateChanged?.Invoke();
            return;
        }
        if (!villageConfirmed && destination != null && destination.kind == MapNodeKind.Village
            && !Progression.CanReenterVillage(nodeId))
        {
            if (!Progression.CanMove(nodeId)) return;
            RequestVillageEntry(nodeId, destination.displayName);
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
<<<<<<< HEAD
            case MapNodeKind.Battle: onBattleEntered.Invoke(request); EnterBattle(request); break;
            case MapNodeKind.Event: onEventEntered.Invoke(request); OpenWildernessEvent(request); break;
=======
            case MapNodeKind.Battle: onBattleEntered.Invoke(request); return EnterBattle(request);
            case MapNodeKind.Event: onEventEntered.Invoke(request); break;
>>>>>>> origin/Feature/MapFunctional
            case MapNodeKind.Camp: onCampEntered.Invoke(request); break;
            case MapNodeKind.Boss: onBossEntered.Invoke(request); break;
        }
        return null;
    }

    void InitializeWilderness()
    {
        if (wildernessEventUI == null || wildernessEventUI.catalogAsset == null) return;
        var source = JsonUtility.FromJson<WildernessEventCatalog>(wildernessEventUI.catalogAsset.text);
        WildernessCatalog = MapWildernessRules.BuildCatalog(source, Progression.Layout);
        MapWildernessRules.EnsureInitialized(GameRoot.I.state, Progression.Layout, WildernessCatalog);
        wildernessRun = GameRoot.I.state;
    }

    bool HasWildernessContext()
    {
        var run = GameRoot.I != null ? GameRoot.I.state : null;
        return isActiveAndEnabled && Progression != null && WildernessCatalog != null
            && run != null && run == wildernessRun && run.map == Progression.State
            && run.world == Progression.World && run.map.wildernessEvents != null;
    }

    bool HasPendingWildernessEvent()
    {
        if (!HasWildernessContext() || IsChangingScene) return false;
        var pending = Progression.GetPendingRequest();
        var level = wildernessRun.map.wildernessEvents;
        return pending != null && pending.kind == MapNodeKind.Event
            && pending.nodeId == Progression.State.currentNodeId
            && level.currentNodeId == pending.nodeId && level.activeNodeId == pending.nodeId;
    }

    void OpenWildernessEvent(MapNodeRequest request)
    {
        if (!HasWildernessContext() || request == null || request.kind != MapNodeKind.Event
            || request.requestId != Progression.State.pendingRequestId
            || request.nodeId != Progression.State.currentNodeId) return;
        MapWildernessRules.EnsureInitialized(wildernessRun, Progression.Layout, WildernessCatalog);
        var level = wildernessRun.map.wildernessEvents;
        var question = WildernessEventRules.FindNode(level, request.nodeId);
        // Previously visited placeholder nodes migrate as spent, without reopening or paying again.
        if (question != null && (question.resolved || question.abandoned)
            && !question.encounterPending && level.activeNodeId != request.nodeId)
        {
            if (Progression.FinishEvent(request.requestId))
            {
                Notice = "此处事件已经结束。";
                StateChanged?.Invoke();
            }
            return;
        }
        if (wildernessEventUI == null || !wildernessEventUI.CanShow()) return;
        bool resume = level.activeNodeId == request.nodeId && question != null && question.shown;
        if (!resume && !MapWildernessRules.TryOpen(wildernessRun, Progression.Layout, WildernessCatalog, request)) return;
        if (wildernessEventUI.Show()) return;
        // A disabled UI must never strand a newly entered node in a pending request.
        WildernessEventRules.Leave(level);
        Progression.FinishEvent(request.requestId);
        Notice = "暂时无法显示野外事件。";
        StateChanged?.Invoke();
    }

    public void ChooseWildernessEvent(int choiceIndex)
    {
        if (!HasPendingWildernessEvent() || !IsWildernessEventOpen) return;
        var result = WildernessEventRules.TryChoose(wildernessRun.map.wildernessEvents,
            WildernessCatalog, wildernessRun, Progression.Layout, choiceIndex);
        Notice = result.message;
        if (result.success) Notice = result.message.Split('\n')[0];
        wildernessEventUI.Refresh();
        StateChanged?.Invoke();
    }

    public void ResolveWildernessEncounter(bool victory)
    {
        if (!HasPendingWildernessEvent() || !IsWildernessEventOpen) return;
        var result = WildernessEventRules.ResolveEncounter(wildernessRun.map.wildernessEvents, wildernessRun, victory);
        Notice = result.message;
        wildernessEventUI.Refresh();
        StateChanged?.Invoke();
    }

    public void CloseWildernessEvent()
    {
        if (!HasPendingWildernessEvent() || !IsWildernessEventOpen) return;
        var level = wildernessRun.map.wildernessEvents;
        var question = WildernessEventRules.FindNode(level, level.activeNodeId);
        if (question == null) return;
        if (question.encounterPending)
        {
            Notice = "请先结算当前遭遇。";
            wildernessEventUI.Refresh();
            return;
        }
        string requestId = Progression.State.pendingRequestId;
        WildernessEventRules.Leave(level);
        if (!Progression.FinishEvent(requestId)) return;
        wildernessEventUI.Hide();
        Notice = question.abandoned ? "你离开了这次事件。" : "事件已结束，可以继续探索。";
        StateChanged?.Invoke();
    }

    void RequestVillageEntry(string nodeId, string destinationName)
    {
        var run = GameRoot.I != null ? GameRoot.I.state : null;
        if (run == null || run.map != Progression.State || run.world != Progression.World) return;
        if (villageEntryConfirmation == null)
        {
            Notice = "村庄进入确认界面尚未配置。";
            StateChanged?.Invoke();
            return;
        }
        pendingVillageNodeId = nodeId;
        pendingVillageOriginId = Progression.State.currentNodeId;
        pendingVillageRun = run;
        if (villageEntryConfirmation.Show(destinationName)) return;
        ClearVillageEntry();
        Notice = "暂时无法打开村庄进入确认界面。";
        StateChanged?.Invoke();
    }

    public void ConfirmVillageEntry()
    {
        if (!IsVillageEntryConfirmationOpen) return;
        string destinationId = pendingVillageNodeId;
        var run = GameRoot.I != null ? GameRoot.I.state : null;
        bool valid = !IsChangingScene && isActiveAndEnabled && Progression != null
            && villageEntryConfirmation != null && villageEntryConfirmation.IsOpen
            && run != null && run == pendingVillageRun
            && run.map == Progression.State && run.world == Progression.World
            && Progression.State.currentNodeId == pendingVillageOriginId
            && Progression.CanMove(destinationId);
        // Clear before callbacks so a second confirmation cannot spend another unit.
        ClearVillageEntry();
        if (villageEntryConfirmation != null) villageEntryConfirmation.Hide(!valid);
        if (valid) MoveToNode(destinationId, true);
    }

    public void CancelVillageEntry()
    {
        if (!IsVillageEntryConfirmationOpen) return;
        ClearVillageEntry();
        if (villageEntryConfirmation != null) villageEntryConfirmation.Hide(true);
    }

    void ClearVillageEntry()
    {
        pendingVillageNodeId = null;
        pendingVillageOriginId = null;
        pendingVillageRun = null;
    }

    void OnDisable()
    {
        CancelVillageEntry();
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
        if (completed != null && completed.kind == MapNodeKind.Camp)
        {
            int recovered = WorldResourceRules.RecoverAtCamp(GameRoot.I.state.player);
            Notice += recovered > 0 ? " · 营地休息恢复 " + recovered + " 点生命" : " · 生命已满";
        }
        StateChanged?.Invoke();
        if (Progression.IsComplete) onMapCompleted.Invoke();
    }
}
