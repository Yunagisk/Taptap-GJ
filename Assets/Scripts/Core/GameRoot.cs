using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class GameRoot : MonoBehaviour
{
    public static GameRoot I;
    public GameState state;
    public bool enterVillageOnLoad;
    [System.NonSerialized] public WorldMapTemplate villageMapTemplate;
    [System.NonSerialized] public string villageReturnScene;
    [System.NonSerialized] public WorldMapTemplate battleMapTemplate;
    [System.NonSerialized] public string battleRequestId;
    [System.NonSerialized] public string battleReturnScene;
    [System.NonSerialized] public MapRevealTransition pendingMapReveal;
    [System.NonSerialized] public MapBossIntroductionState mapBossIntroductions = new MapBossIntroductionState();
    [System.NonSerialized] public BattleFlowController activeBattle;
    public DayStartSaveStore SaveSlot { get; set; }
    public string LastSaveError { get; private set; }
    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        state = new GameState();
        SaveSlot = new DayStartSaveStore(System.IO.Path.Combine(Application.persistentDataPath, "day-start-save-v1.json"));
        WorldResourceRules.DawnReached += SaveDawn;
        DontDestroyOnLoad(gameObject);
    }

    void SaveDawn(MapLayout layout, MapState map, WorldState world, System.Action<GameState> completeCheckpoint)
    {
        // Preview tools and independent rule simulations must never overwrite the player's slot.
        if (state == null || !ReferenceEquals(state.map, map) || !ReferenceEquals(state.world, world)) return;
        try
        {
            var snapshot = DayStartSaveStore.Copy(state);
            completeCheckpoint?.Invoke(snapshot);
            SaveMorning(snapshot, layout);
        }
        catch (System.Exception error)
        {
            LastSaveError = "清晨存档保存失败：" + error.Message;
            Debug.LogWarning(LastSaveError, this);
        }
    }

    public bool SaveMorning(GameState morning, MapLayout layout)
    {
        bool saved = SaveSlot.TrySave(morning, layout, out var error);
        LastSaveError = error;
        if (!saved) Debug.LogWarning(error, this);
        return saved;
    }

    public void InstallMorning(GameState morning, WorldMapTemplate template)
    {
        state = morning;
        enterVillageOnLoad = false;
        villageMapTemplate = template;
        villageReturnScene = GameFlowController.MapScene;
        battleMapTemplate = null;
        battleRequestId = battleReturnScene = null;
        pendingMapReveal = null;
        mapBossIntroductions = new MapBossIntroductionState();
        activeBattle = null;
        MapTimeDialView.RememberPresentedTime(morning.world);
    }
    void OnDestroy()
    {
        WorldResourceRules.DawnReached -= SaveDawn;
        if (I == this) I = null;
    }
}
