// Configuration shared by the shop, inventory and battle actions.
public enum ItemEffect { Heal, Cleanse, RevealWeaknesses }

public sealed class ItemDef
{
    public readonly string id, displayName, description;
    public readonly ItemEffect effect;
    public readonly int healAmount, price, dailyStock, carryLimit;
    public readonly bool battleOnly;

    public ItemDef(string id, string name, string description, ItemEffect effect,
        int healAmount = 0, int price = 0, int dailyStock = 0, int carryLimit = 0, bool battleOnly = false)
    {
        this.id = id; displayName = name; this.description = description; this.effect = effect;
        this.healAmount = healAmount; this.price = price; this.dailyStock = dailyStock;
        this.carryLimit = carryLimit; this.battleOnly = battleOnly;
    }
}

public static class ItemTable
{
    public const string Bandage = "bandage", HolyWater = "holy_water", Lantern = "lantern";
    public static readonly ItemDef[] All =
    {
        new ItemDef(Bandage, "绷带", "恢复 15 点生命，适用治疗修正并向下取整。战内外可用。",
            ItemEffect.Heal, healAmount: 15, price: 2, dailyStock: 1, carryLimit: 1),
        new ItemDef(HolyWater, "圣水", "清除自身全部普通负面状态及其全部层数，保留增益和原罪效果。战内外可用。",
            ItemEffect.Cleanse, price: 6, dailyStock: 1, carryLimit: 1),
        new ItemDef(Lantern, "提灯", "选择一名存活敌人，揭露其当前阶段的全部弱点，不揭露抗性或免疫。消耗一次提灯次数。",
            ItemEffect.RevealWeaknesses, battleOnly: true)
    };

    public static string CanonicalId(string id)
    {
        string key = (id ?? "").Replace("_", "").Replace("-", "").ToLowerInvariant();
        switch (key)
        {
            case "bandage": case "绷带": return Bandage;
            case "holywater": case "圣水": return HolyWater;
            case "lantern": case "提灯": return Lantern;
            default: return id;
        }
    }

    public static ItemDef Find(string id)
    {
        id = CanonicalId(id);
        foreach (var item in All) if (item.id == id) return item;
        return null;
    }
}
