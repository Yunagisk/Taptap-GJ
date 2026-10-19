using System;
using System.Collections.Generic;

public interface IItemUser
{
    int CurrentHp { get; }
    int MaxHp { get; }
    StatusSet Statuses { get; }
    int Heal(int amount);
}

public interface IItemWeaknessTarget
{
    bool IsAlive { get; }
    int UnknownWeaknessCount { get; }
    int RevealWeaknesses();
}

public readonly struct ItemResult
{
    public readonly bool ok;
    public readonly string message;
    public readonly int amount;
    public ItemResult(bool ok, string message, int amount = 0)
    { this.ok = ok; this.message = message; this.amount = amount; }
}

// All checks precede payment. Failed use never consumes an item or a lantern charge.
public static class ItemRules
{
    public static int Count(InventoryState inventory, string id)
    {
        id = ItemTable.CanonicalId(id);
        long count = 0;
        if (inventory?.items != null)
            foreach (var stack in inventory.items)
                if (stack != null && stack.count > 0 && ItemTable.CanonicalId(stack.itemId) == id)
                    count += stack.count;
        return (int)Math.Min(int.MaxValue, count);
    }

    public static ItemResult TryGrant(GameState state, string id, int amount = 1)
    {
        var item = ItemTable.Find(id);
        if (state?.inventory == null || item == null || item.battleOnly || amount <= 0)
            return new ItemResult(false, "无法获得该道具。");
        int room = Math.Max(0, item.carryLimit - Count(state.inventory, item.id));
        if (room < amount) return new ItemResult(false, item.displayName + "携带上限为 " + item.carryLimit + "。");
        if (state.inventory.items == null) state.inventory.items = new List<ItemStack>();
        var stack = state.inventory.items.Find(x => x != null && x.itemId == item.id && x.count > 0);
        if (stack == null) state.inventory.items.Add(new ItemStack(item.id, amount));
        else stack.count += amount;
        return new ItemResult(true, "获得" + item.displayName + " × " + amount + "。", amount);
    }

    public static int HealingAmount(PlayerState player, ItemDef item)
    {
        if (player == null || item == null || float.IsNaN(player.healingMultiplier)) return 0;
        double amount = Math.Floor(item.healAmount * (double)player.healingMultiplier);
        return (int)Math.Max(0, Math.Min(int.MaxValue, amount));
    }

    public static ItemResult Validate(GameState state, string id, bool inBattle,
        IItemUser user = null, IItemWeaknessTarget target = null, bool selectingTarget = false)
    {
        var item = ItemTable.Find(id);
        if (state?.player == null || state.inventory == null || item == null)
            return new ItemResult(false, "没有可用的道具或玩家状态。");
        if (user == null)
        {
            if (inBattle) return new ItemResult(false, "没有可行动的玩家。");
            user = new WorldItemUser(state.player);
        }
        if (user.CurrentHp <= 0) return new ItemResult(false, "死亡后无法使用道具。");
        if (item.battleOnly && !inBattle) return new ItemResult(false, "提灯只能在战斗中使用。");
        if (item.effect == ItemEffect.RevealWeaknesses)
        {
            if (state.player.lanternCharges <= 0) return new ItemResult(false, "提灯次数不足，可在旅馆补充。");
            if (!selectingTarget)
            {
                if (target == null || !target.IsAlive) return new ItemResult(false, "请选择一名存活敌人。");
                if (target.UnknownWeaknessCount == 0) return new ItemResult(false, "该敌人当前没有尚未揭露的弱点。");
            }
        }
        else
        {
            if (Count(state.inventory, item.id) == 0) return new ItemResult(false, "没有" + item.displayName + "。");
            if (item.effect == ItemEffect.Heal)
            {
                if (user.CurrentHp >= user.MaxHp) return new ItemResult(false, "生命已满，无需使用绷带。");
                if (HealingAmount(state.player, item) <= 0) return new ItemResult(false, "当前治疗修正下无法恢复生命。");
            }
            if (item.effect == ItemEffect.Cleanse && (user.Statuses == null || user.Statuses.DebuffCount == 0))
                return new ItemResult(false, "当前没有可净化的普通负面状态。");
        }
        return new ItemResult(true, "可使用");
    }

    public static ItemResult TryUse(GameState state, string id, bool inBattle,
        IItemUser user = null, IItemWeaknessTarget target = null)
    {
        var check = Validate(state, id, inBattle, user, target);
        if (!check.ok) return check;
        var item = ItemTable.Find(id);
        if (user == null) user = new WorldItemUser(state.player);
        int applied;
        string message;
        switch (item.effect)
        {
            case ItemEffect.Heal:
                applied = user.Heal(Math.Min(HealingAmount(state.player, item), user.MaxHp - user.CurrentHp));
                message = "使用绷带，恢复 " + applied + " 点生命。";
                break;
            case ItemEffect.Cleanse:
                applied = user.Statuses.PurgeAllDebuffs();
                message = "使用圣水，清除 " + applied + " 种普通负面状态及全部层数。";
                break;
            default:
                applied = target.RevealWeaknesses();
                message = "使用提灯，揭露 " + applied + " 种弱点。";
                break;
        }
        if (applied <= 0) return new ItemResult(false, "本次使用未产生效果，道具未消耗。");
        if (item.effect == ItemEffect.RevealWeaknesses) state.player.lanternCharges--;
        else ConsumeOne(state.inventory, item.id);
        state.player.hp = user.CurrentHp;
        state.player.maxHp = user.MaxHp;
        if (state.player.ordinaryStatuses == null) state.player.ordinaryStatuses = new List<StatusSnapshot>();
        user.Statuses?.WriteSnapshot(state.player.ordinaryStatuses);
        return new ItemResult(true, message, applied);
    }

    static void ConsumeOne(InventoryState inventory, string id)
    {
        for (int i = 0; i < inventory.items.Count; i++)
        {
            var stack = inventory.items[i];
            if (stack == null || stack.count <= 0 || ItemTable.CanonicalId(stack.itemId) != id) continue;
            stack.count--;
            if (stack.count == 0) inventory.items.RemoveAt(i);
            return;
        }
    }

    public static void EnsureShopStock(GameState state)
    {
        if (state?.shop == null || state.world == null) return;
        int day = Math.Max(0, state.world.time) / WorldResourceRules.UnitsPerDay;
        if (state.shop.itemStockDay == day && state.shop.itemStock != null && state.shop.itemStock.Count > 0) return;
        state.shop.itemStockDay = day;
        state.shop.itemStock = new List<ItemStack>();
        foreach (var item in ItemTable.All)
            if (item.dailyStock > 0) state.shop.itemStock.Add(new ItemStack(item.id, item.dailyStock));
    }

    public static int ShopStock(GameState state, string id)
    {
        EnsureShopStock(state);
        var stack = state?.shop?.itemStock?.Find(x => x != null && x.itemId == ItemTable.CanonicalId(id));
        return stack != null ? Math.Max(0, stack.count) : 0;
    }

    public static ItemResult TryBuy(GameState state, string id)
    {
        var item = ItemTable.Find(id);
        if (state?.player == null || state.player.hp <= 0 || state.world == null || state.inventory == null
            || state.shop == null || item == null || item.dailyStock <= 0)
            return new ItemResult(false, "无法购买该道具。");
        if (ShopStock(state, item.id) <= 0) return new ItemResult(false, item.displayName + "今日已售罄。");
        if (Count(state.inventory, item.id) >= item.carryLimit)
            return new ItemResult(false, item.displayName + "已达携带上限。");
        if (state.world.sinRemnant < item.price) return new ItemResult(false, "罪骸不足，需要 " + item.price + "。");
        var granted = TryGrant(state, item.id);
        if (!granted.ok) return granted;
        state.world.sinRemnant -= item.price;
        state.shop.itemStock.Find(x => x.itemId == item.id).count--;
        return new ItemResult(true, "买下" + item.displayName + "，花费 " + item.price + " 罪骸。", 1);
    }

    sealed class WorldItemUser : IItemUser
    {
        readonly PlayerState player;
        public WorldItemUser(PlayerState player)
        { this.player = player; Statuses = new StatusSet(); Statuses.RestoreSnapshot(player.ordinaryStatuses); }
        public int CurrentHp => player.hp;
        public int MaxHp => player.maxHp;
        public StatusSet Statuses { get; }
        public int Heal(int amount) => WorldResourceRules.Heal(player, amount);
    }
}
