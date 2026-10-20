using System;
using System.Collections.Generic;

//物品定义
[Serializable]
public class ItemStack
{
    public string itemId;
    public int count;
    public ItemStack() { }
    public ItemStack(string id, int n)
    {
        itemId = id;
        count = n;
    }
}

//玩家状态
[Serializable]
public class PlayerState
{
    public int hp = 100;
    public int maxHp = 100;                  
    public int resolve = 1;                  // 决心
    public int maxResolve = 3;               // 决心上限
    public int lanternCharges = 2;           // 提灯次数
    public int maxLanternCharges = 2;        // 提灯上限
    public float healingMultiplier = 1f;
    public List<StatusSnapshot> ordinaryStatuses = new List<StatusSnapshot>();
    public string currentWeaponId = "sword"; // 当前武器ID
    public int nextDirectHitReductionPercent;
    public int nextAttackBonusPercent;
    public int nextCampRecoveryBonusPercent;
}

//背包状态
[Serializable]
public class InventoryState
{
    //消耗品
    public List<ItemStack> items = new List<ItemStack>();
    //武器
    public List<string> weapons = new List<string>() { "sword" };
    // 四个装备槽。空列表用于迁移旧局；四个空槽表示主动卸下。
    public List<string> equippedWeaponIds = new List<string>();
    // Per-run notification history; unopened categories retain their new-weapon dot.
    public List<string> seenWeaponIds = new List<string>() { "sword" };
}

[Serializable]
public class ShopState
{
    public bool stocked;
    public List<string> availableWeaponIds = new List<string>();
    public int itemStockDay = -1;
    public List<ItemStack> itemStock = new List<ItemStack>();
}

//世界状态
[Serializable]
public class WorldState
{
    public int time = 0;                    //世界时间
    public int sin = 40;         // 罪恶，范围 0~100；与罪骸货币独立。
    public int sinRemnant = WeaponTable.StartingRemnant; // 罪骸。全局货币。
    public int stage = 1;
    public VillageEventState villageEvents = new VillageEventState();
}

//地图状态
[Serializable]
public class MapState
{
    public string currentNodeId = "village";                                // 当前节点ID
    public List<string> clearedNodes = new List<string>();                  // 已清理的节点
    public List<string> visitedNodes = new List<string>() { "village" };    // 已访问的节点
    public string templateId;
    public int randomSeed;
    public int sinRollCount;
    public int requestSequence;
    public string checkpointNodeId;
    public int checkpointTime;
    public bool revivedAtCheckpoint;
    public string pendingNodeId;
    public string pendingRequestId;
    public List<string> revealedNodes = new List<string>();
    public List<string> spawnedBossNodes = new List<string>();
    public List<string> defeatedBossNodes = new List<string>();
    public WildernessLevelState wildernessEvents;
}

public enum MainStoryStage
{
    ArrivedVillage,
    VillageIntroductionComplete,
    CommissionAccepted,
    TutorialCompleted,
    SlothDefeated,
    CommissionTurnedIn
}

public enum StoryDialogueEffect { None, VillageIntroduction, AcceptCommission, TurnInCommission }

[Serializable]
public class MainStoryState
{
    public MainStoryStage stage = MainStoryStage.ArrivedVillage;
    public string tutorialNodeId;
    public string slothNodeId;
    public List<string> completedDialogueIds = new List<string>();
}

//游戏总状态
[Serializable]
public class GameState
{
    public PlayerState player = new PlayerState();
    public InventoryState inventory = new InventoryState();
    public WorldState world = new WorldState();
    public MapState map = new MapState();
    public ShopState shop = new ShopState();
    public MainStoryState story = new MainStoryState();
}
