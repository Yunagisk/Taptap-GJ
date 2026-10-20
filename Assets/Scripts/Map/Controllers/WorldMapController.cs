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
    public VillageEntryConfirmationUI villageEntryConfirmation;
    public MapWildernessEventUI wildernessEventUI;
    public MapCampUI campUIPrefab;
    MapCampUI campUI;
    GameState campRun;
    string campRequestId;
    public MapCampUI CampUI => campUI;
    public bool IsCampOpen => campUI != null && campUI.IsOpen;
    public WildernessEventCatalog WildernessCatalog { get; private set; }
    public bool IsWildernessEventOpen => wildernessEventUI != null && wildernessEventUI.IsOpen;
    GameState wildernessRun;
    bool isLoadingBattle;
    bool entryDispatched;
    string pendingVillageNodeId;
    string pendingVillageOriginId;
    GameState pendingVillageRun;
    MapTravelPlan pendingTravel;
    GameState pendingTravelRun;
    MapFastTravelUI fastTravelUI;
    public MapTravelPlan PendingTravel => pendingTravel;
    public MapFastTravelUI FastTravelUI => fastTravelUI;
    public bool IsFastTravelConfirmationOpen => pendingTravel != null;

    public bool IsVillageEntryConfirmationOpen => !string.IsNullOrEmpty(pendingVillageNodeId);

    bool IsChangingScene => isLoadingBattle || IsEnteringNode || MapSceneTransition.IsBusy
        || villageEntryConfirmation != null
        && villageEntryConfirmation.flow != null && villageEntryConfirmation.flow.IsLoading;
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
    public GuideMapController Guide { get; private set; }
    public bool GuideAllowsNode(string nodeId) => VillageIntroRules.CanMoveTo(GameRoot.I != null ? GameRoot.I.state : null,
        Progression != null ? Progression.Layout : null, nodeId) && (Guide == null || !Guide.IsOpen)
        && !SlothChapterPresentation.IsBlocking && !SlothChapterRules.IsFinished(GameRoot.I?.state);
    public void RefreshGuide() { StateChanged?.Invoke(); }
    public void EnterPreparedGuideBattle(MapNodeRequest request)
    {
        if (request != null && Progression?.GetPendingRequest()?.requestId == request.requestId
            && VillageIntroRules.IsTutorialBattle(GameRoot.I.state, request.nodeId)) EnterBattle(request);
    }

    void Awake()
    {
        if (template == null) { Debug.LogError("Assign a WorldMapTemplate.", this); enabled = false; return; }
        if (GameRoot.I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        try
        {
            int mapSeed = seed == 0 ? Guid.NewGuid().GetHashCode() : seed;
            GameRoot.I.state.map.slothChapterOnly = true;
            Progression = new MapProgression(template.Load(), GameRoot.I.state.map, GameRoot.I.state.world, mapSeed);
            MainStoryRules.EnsureInitialized(GameRoot.I.state, Progression.Layout);
            EquipmentRules.EnsureInitialized(GameRoot.I.state);
            VillageEventRules.InitializeForRun(GameRoot.I.state, Progression.Layout);
            InitializeWilderness();
            Notice = GameRoot.I.state.story.stage == MainStoryStage.SlothDefeated
                ? "懒惰已平息 · 返回埃尔南多，向哈罗德复命。" : "懒惰已出现";
        }
        catch (Exception error) { Debug.LogException(error, this); enabled = false; }
    }

    void Start()
    {
        Guide = GuideMapController.Attach(this);
        var pending = Progression?.GetPendingRequest();
        if (pending != null && (pending.kind == MapNodeKind.Battle || pending.kind == MapNodeKind.Event
            || pending.kind == MapNodeKind.Camp || SlothChapterRules.IsSlothRequest(pending)))
            BeginEntry(pending);
    }

    AsyncOperation EnterBattle(MapNodeRequest request)
    {
        if (isLoadingBattle || request == null
            || Progression?.GetPendingRequest()?.requestId != request.requestId) return null;
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
        MoveToNode(nodeId, false);
    }

    void MoveToNode(string nodeId, bool villageConfirmed)
    {
        if (!isActiveAndEnabled || IsChangingScene || Progression == null || IsVillageEntryConfirmationOpen || IsWildernessEventOpen || IsCampOpen
            || IsFastTravelConfirmationOpen) return;
        var mapView = GetComponent<WorldMapSceneView>();
        if (mapView != null && mapView.IsRevealing) return;
        if (!GuideAllowsNode(nodeId))
        {
            Notice = MainStoryRules.GoalText(GameRoot.I.state);
            StateChanged?.Invoke();
            return;
        }
        var destination = Progression.GetNode(nodeId);
        if (Progression.TryPlanFastTravel(nodeId, out var travel))
        {
            if (Progression.State.clearedNodes.Contains(nodeId) || PrepareNodeEntry(destination))
                RequestFastTravel(travel);
            return;
        }
        if (!PrepareNodeEntry(destination)) return;
        if (!villageConfirmed && destination.kind == MapNodeKind.Village
            && !Progression.CanReenterVillage(nodeId))
        {
            if (!Progression.CanMove(nodeId)) return;
            RequestVillageEntry(nodeId, destination.displayName);
            return;
        }
        if (Progression.CanReenterVillage(nodeId))
        {
            Notice = destination.displayName;
            BeginEntry(null, true);
            return;
        }
        if (!Progression.TryMove(nodeId, out var request)) return;
        Notice = destination.displayName;
        if (Progression.LastSpawnedBossId != null && Progression.IsBossAvailable(Progression.LastSpawnedBossId))
            Notice += " · " + Progression.GetNode(Progression.LastSpawnedBossId).displayName + "已出现";
        // Cleared nodes only move the location marker; there is no encounter to fade into.
        BeginEntry(request, destination.kind == MapNodeKind.Village);
    }

    bool PrepareNodeEntry(MapNodeDefinition destination)
    {
        if (destination == null) return false;
        if (entryTransitionPrefab == null)
        {
            Notice = "节点进入转场尚未配置。";
            Debug.LogError("Assign the map scene transition prefab before entering nodes.", this);
            StateChanged?.Invoke();
            return false;
        }
        if (destination.kind == MapNodeKind.Camp
            && !Progression.State.clearedNodes.Contains(destination.id) && !EnsureCampUI())
        {
            Notice = "火堆休息界面尚未就绪。";
            StateChanged?.Invoke();
            return false;
        }
        if (destination.kind == MapNodeKind.Event
            && !Progression.State.clearedNodes.Contains(destination.id)
            && (!HasWildernessContext() || wildernessEventUI == null || !wildernessEventUI.CanShow()))
        {
            Notice = "野外事件界面尚未就绪。";
            StateChanged?.Invoke();
            return false;
        }
        if (destination.kind == MapNodeKind.Village
            && !Application.CanStreamedLevelBeLoaded(GameFlowController.VillageScene))
        {
            Notice = "村庄场景未加入构建列表";
            Debug.LogError("Village is missing from Build Settings.", this);
            StateChanged?.Invoke();
            return false;
        }
        if ((destination.kind == MapNodeKind.Battle || destination.kind == MapNodeKind.Boss)
            && !Progression.State.clearedNodes.Contains(destination.id)
            && !Application.CanStreamedLevelBeLoaded(BattleScene))
        {
            Notice = "战斗场景未加入构建列表";
            Debug.LogError("CombatScene is missing from Build Settings.", this);
            StateChanged?.Invoke();
            return false;
        }
        if ((destination.kind == MapNodeKind.Battle || destination.kind == MapNodeKind.Boss)
            && !Progression.State.clearedNodes.Contains(destination.id))
        {
            EquipmentRules.EnsureInitialized(GameRoot.I.state);
            if (GameRoot.I.state.inventory.equippedWeaponIds.TrueForAll(string.IsNullOrEmpty))
            {
                Notice = "\u8bf7\u5148\u5728\u80cc\u5305\u4e2d\u88c5\u5907\u81f3\u5c11\u4e00\u628a\u6b66\u5668";
                StateChanged?.Invoke();
                return false;
            }
        }
        return true;
    }

    void RequestFastTravel(MapTravelPlan plan)
    {
        var run = GameRoot.I != null ? GameRoot.I.state : null;
        var view = GetComponent<WorldMapSceneView>();
        if (run == null || run.player.hp <= 0 || run.map != Progression.State || run.world != Progression.World
            || view == null || view.canvas == null || view.viewport == null) return;
        if (fastTravelUI == null) fastTravelUI = MapFastTravelUI.Create(this, view);
        pendingTravel = plan;
        pendingTravelRun = run;
        if (fastTravelUI.Show(plan)) { StateChanged?.Invoke(); return; }
        ClearFastTravel(true);
        Notice = "暂时无法预览这条路线。";
        StateChanged?.Invoke();
    }

    public void ConfirmFastTravel()
    {
        if (pendingTravel == null || IsChangingScene) return;
        var quote = pendingTravel;
        var run = GameRoot.I != null ? GameRoot.I.state : null;
        bool valid = isActiveAndEnabled && fastTravelUI != null && fastTravelUI.IsOpen
            && run != null && run == pendingTravelRun && run.player.hp > 0
            && run.map == Progression.State && run.world == Progression.World
            && !IsVillageEntryConfirmationOpen && !IsWildernessEventOpen && !IsCampOpen
            && GuideAllowsNode(quote.DestinationId);
        // Clear first so reentrant clicks and state-change callbacks cannot pay twice.
        pendingTravel = null;
        pendingTravelRun = null;
        if (valid && !Progression.State.clearedNodes.Contains(quote.DestinationId)
            && !PrepareNodeEntry(Progression.GetNode(quote.DestinationId)))
        {
            ClearFastTravel(true);
            StateChanged?.Invoke();
            return;
        }
        if (!valid || !Progression.TryFastTravel(quote, out var request))
        {
            ClearFastTravel(true);
            Notice = "当前位置或路线已变化，请重新选择目的地。";
            StateChanged?.Invoke();
            return;
        }
        ClearFastTravel(false);
        Notice = "已抵达" + Progression.GetNode(quote.DestinationId).displayName
            + " · 耗时 " + quote.TimeCost + " 单位 · " + WorldResourceRules.TimeLabel(Progression.World);
        if (Progression.LastSpawnedBossId != null && Progression.IsBossAvailable(Progression.LastSpawnedBossId))
            Notice += " · " + Progression.GetNode(Progression.LastSpawnedBossId).displayName + "已出现";
        if (request != null) BeginEntry(request);
        else if (SlothChapterRules.CanEndAtVillage(run)) BeginEntry(null, true);
        else StateChanged?.Invoke();
    }

    public void CancelFastTravel()
    {
        if (pendingTravel == null) return;
        ClearFastTravel(true);
        if (isActiveAndEnabled) StateChanged?.Invoke();
    }

    void ClearFastTravel(bool restoreView)
    {
        pendingTravel = null;
        pendingTravelRun = null;
        if (fastTravelUI != null) fastTravelUI.Hide(restoreView);
    }


    void BeginEntry(MapNodeRequest request, bool village = false)
    {
        if (request == null && !village)
        {
            StateChanged?.Invoke();
            return;
        }
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
        if (Progression == null || request != null
            && Progression.GetPendingRequest()?.requestId != request.requestId) return null;
        if (village && Progression.GetNode(Progression.State.currentNodeId)?.kind != MapNodeKind.Village) return null;
        entryDispatched = true;
        StateChanged?.Invoke();
        if (village) { onVillageEntered.Invoke(); return null; }
        if (request == null) return null;
        NodeEntered?.Invoke(request);
        switch (request.kind)
        {
            case MapNodeKind.Battle:
                if (Guide != null && Guide.PresentEncounter(request)) return null;
                onBattleEntered.Invoke(request); return EnterBattle(request);
            case MapNodeKind.Event: onEventEntered.Invoke(request); OpenWildernessEvent(request); break;
            case MapNodeKind.Camp: onCampEntered.Invoke(request); OpenCamp(request); break;
            case MapNodeKind.Boss:
                if (!SlothChapterRules.IsSlothRequest(request)) break;
                onBossEntered.Invoke(request);
                if (GameRoot.I.state.story.slothConfronted) return EnterBattle(request);
                var run = GameRoot.I.state;
                if (!SlothChapterPresentation.PlayConfrontation(transform, () =>
                {
                    if (this == null || GameRoot.I == null || GameRoot.I.state != run
                        || Progression.GetPendingRequest()?.requestId != request.requestId) return;
                    run.story.slothConfronted = true;
                    EnterBattle(request);
                }))
                {
                    Notice = "暂时无法开始对话，请重新进入地图。";
                    StateChanged?.Invoke();
                }
                break;
        }
        return null;
    }

    bool EnsureCampUI()
    {
        if (campUI != null) return campUI.CanShow();
        if (campUIPrefab == null) return false;
        var view = GetComponent<WorldMapSceneView>();
        var canvas = view != null && view.canvas != null ? view.canvas : GetComponentInParent<Canvas>(true);
        if (canvas == null || !canvas.isActiveAndEnabled) return false;
        campUI = Instantiate(campUIPrefab, canvas.transform, false);
        campUI.name = "MapCampPanel";
        campUI.Bind(this);
        return campUI.CanShow();
    }

    void OpenCamp(MapNodeRequest request)
    {
        var run = GameRoot.I != null ? GameRoot.I.state : null;
        if (request == null || request.kind != MapNodeKind.Camp || run == null
            || run.map != Progression.State || run.world != Progression.World
            || Progression.GetPendingRequest()?.requestId != request.requestId || !EnsureCampUI()) return;
        campRun = run;
        campRequestId = request.requestId;
        campUI.Show();
    }

    public void RestAtCamp()
    {
        var run = GameRoot.I != null ? GameRoot.I.state : null;
        var pending = Progression?.GetPendingRequest();
        if (!isActiveAndEnabled || IsChangingScene || !IsCampOpen || !campUI.CanRest || run == null || run != campRun
            || run.map != Progression.State || run.world != Progression.World || run.player.hp <= 0
            || pending == null || pending.kind != MapNodeKind.Camp || pending.requestId != campRequestId
            || pending.nodeId != Progression.State.currentNodeId) return;
        CompleteNode(campRequestId, true);
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
            if (FinishWildernessEvent(request.requestId))
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
        FinishWildernessEvent(request.requestId);
        Notice = "暂时无法显示野外事件。";
        StateChanged?.Invoke();
    }

    public void ChooseWildernessEvent(int choiceIndex)
    {
        if (!HasPendingWildernessEvent() || !IsWildernessEventOpen || wildernessEventUI.IsLeaveConfirmationOpen) return;
        var result = WildernessEventRules.TryChoose(wildernessRun.map.wildernessEvents,
            WildernessCatalog, wildernessRun, Progression.Layout, choiceIndex);
        Notice = result.message;
        if (result.success) Notice = result.message.Split('\n')[0];
        wildernessEventUI.Refresh();
        StateChanged?.Invoke();
    }

    public void ResolveWildernessEncounter(bool victory)
    {
        if (!HasPendingWildernessEvent() || !IsWildernessEventOpen || wildernessEventUI.IsLeaveConfirmationOpen) return;
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
        if (!question.resolved && !question.abandoned)
        {
            wildernessEventUI.LeaveWarning?.Show();
            return;
        }
        if (question.encounterPending)
        {
            Notice = "请先结算当前遭遇。";
            wildernessEventUI.Refresh();
            return;
        }
        FinishDisplayedWildernessEvent(question);
    }

    public void ConfirmLeaveWildernessEvent()
    {
        if (!HasPendingWildernessEvent() || !IsWildernessEventOpen || !wildernessEventUI.IsLeaveConfirmationOpen
            || !wildernessEventUI.LeaveWarning.MatchesCurrentEvent()) return;
        var question = WildernessEventRules.FindNode(wildernessRun.map.wildernessEvents,
            wildernessRun.map.wildernessEvents.activeNodeId);
        if (question == null || question.resolved || question.abandoned || question.encounterPending) return;
        FinishDisplayedWildernessEvent(question);
    }

    void FinishDisplayedWildernessEvent(WildernessNodeState question)
    {
        var level = wildernessRun.map.wildernessEvents;
        string requestId = Progression.State.pendingRequestId;
        WildernessEventRules.Leave(level);
        if (!FinishWildernessEvent(requestId)) return;
        wildernessEventUI.Hide();
        Notice = question.abandoned ? "你离开了这次事件。" : "事件已结束，可以继续探索。";
        StateChanged?.Invoke();
    }

    bool FinishWildernessEvent(string requestId)
    {
        var completed = Progression.GetPendingRequest();
        var before = MapRevealTransition.Capture(Progression);
        if (!Progression.FinishEvent(requestId)) return false;
        GameRoot.I.pendingMapReveal = MapRevealTransition.AfterCompletion(Progression, before, completed.nodeId);
        return true;
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
        if (!IsVillageEntryConfirmationOpen || IsChangingScene) return;
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
        if (!IsVillageEntryConfirmationOpen || isActiveAndEnabled && IsChangingScene) return;
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
        CancelFastTravel();
        CancelVillageEntry();
        if (campUI != null) campUI.Hide();
        campRun = null;
        campRequestId = null;
    }

    void OnEnable()
    {
        var pending = Progression?.GetPendingRequest();
        if (pending != null && pending.kind == MapNodeKind.Camp) BeginEntry(pending);
    }

    // External battle/event screens return the requestId they received on entry.
    public void CompleteNode(string requestId) => CompleteNode(requestId, false);

    void CompleteNode(string requestId, bool restingAtCamp)
    {
        if (Progression == null || IsWaitingToEnter) return;
        var completed = Progression.GetPendingRequest();
        if (completed != null && completed.kind == MapNodeKind.Camp && !restingAtCamp) return;
        // Boss completion belongs to the real combat result, never the old debug panel.
        if (completed != null && completed.kind == MapNodeKind.Boss && Progression.State.slothChapterOnly) return;
        var before = MapRevealTransition.Capture(Progression);
        if (!Progression.TryCompleteNode(requestId)) return;
        MainStoryRules.OnNodeCompleted(GameRoot.I.state, completed);
        GameRoot.I.pendingMapReveal = MapRevealTransition.AfterCompletion(Progression, before, completed.nodeId);
        Notice = Progression.IsComplete ? "三个原罪区域已完成" : "节点已完成";
        if (completed != null && completed.kind == MapNodeKind.Camp)
        {
            int recovered = WorldResourceRules.RecoverAtCamp(GameRoot.I.state.player);
            Notice += recovered > 0 ? " · 营地休息恢复 " + recovered + " 点生命" : " · 生命已满";
            campRun = null;
            campRequestId = null;
            if (campUI != null) campUI.Hide();
        }
        StateChanged?.Invoke();
        if (Progression.IsComplete) onMapCompleted.Invoke();
    }
}
