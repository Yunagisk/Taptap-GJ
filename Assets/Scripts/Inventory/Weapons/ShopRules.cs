public readonly struct PurchaseResult
{
    public readonly bool ok;
    public readonly string message;
    public readonly bool shortRemnant;

    public PurchaseResult(bool ok, string message, bool shortRemnant = false)
    {
        this.ok = ok;
        this.message = message;
        this.shortRemnant = shortRemnant;
    }
}

// 全局商店规则：价格全关卡共用，库存 1，买后不补货。花钱不改罪恶。
public static class ShopRules
{
    public static void StockNewRun(ShopState shop, string startingWeaponId)
    {
        shop.availableWeaponIds.Clear();
        for (int i = 0; i < WeaponTable.All.Length; i++)
        {
            string id = WeaponTable.All[i].id;
            if (id != startingWeaponId) shop.availableWeaponIds.Add(id);
        }
        shop.stocked = true;
    }

    public static void EnsureStock(GameState state)
    {
        if (state == null || state.shop.stocked) return;
        state.shop.availableWeaponIds.Clear();
        for (int i = 0; i < WeaponTable.All.Length; i++)
        {
            string id = WeaponTable.All[i].id;
            if (!state.inventory.weapons.Contains(id)) state.shop.availableWeaponIds.Add(id);
        }
        state.shop.stocked = true;
    }

    public static PurchaseResult TryBuy(GameState state, string weaponId, int price)
    {
        if (state == null) return new PurchaseResult(false, "没有进行中的局。");
        var offer = WeaponTable.Find(weaponId);
        if (offer == null) return new PurchaseResult(false, "商店没有这把武器。");
        EnsureStock(state);
        if (state.inventory.weapons.Contains(weaponId))
            return new PurchaseResult(false, offer.displayName + "已经解锁。");
        if (!state.shop.availableWeaponIds.Contains(weaponId))
            return new PurchaseResult(false, offer.displayName + "已售罄，不再补货。");
        if (price < 0) return new PurchaseResult(false, "价格无效。");
        if (state.inventory.weapons.Count >= WeaponTable.Capacity)
            return new PurchaseResult(false, "武器背包已满。");
        if (state.world.sinRemnant < price)
            return new PurchaseResult(false, "罪骸不足。需要 " + price + "，当前 " + state.world.sinRemnant + "。", true);

        state.world.sinRemnant -= price;
        state.shop.availableWeaponIds.Remove(weaponId);
        state.inventory.weapons.Add(weaponId);
        return new PurchaseResult(true, "买下" + offer.displayName + "，已放入背包。花费 " + price + " 罪骸。");
    }

    public static void GrantRemnant(WorldState world, int amount)
    {
        if (world == null || amount <= 0) return;
        world.sinRemnant += amount;
    }
}
