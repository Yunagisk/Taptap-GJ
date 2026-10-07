using System;
using System.Collections.Generic;

//物品定义
[Serializable]
public class ItemStack
{
    public string itemId;
    public int count;
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
    public string currentWeaponId = "sword"; // 当前武器ID
}

//背包状态
[Serializable]
public class InventoryState
{
    //消耗品
    public List<ItemStack> items = new List<ItemStack>();
    //武器
    public List<string> weapons = new List<string>() { "sword", "axe", "hammer", "torch" };
}

[Serializable]
public class ShopState
{
    public List<string> availableWeaponIds = new List<string> { "sword", "axe", "hammer", "dual_swords" };
}

//世界状态
[Serializable]
public class WorldState
{
    public int time = 0;                    //世界时间
    public int sin = 0;
    public int stage = 1;//罪恶值
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
    public string pendingNodeId;
    public string pendingRequestId;
    public List<string> revealedNodes = new List<string>();
    public List<string> spawnedBossNodes = new List<string>();
    public List<string> defeatedBossNodes = new List<string>();
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
}
