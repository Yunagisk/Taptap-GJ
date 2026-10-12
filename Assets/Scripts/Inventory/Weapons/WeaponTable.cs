using System;

// 商店表。价格以 WeaponData.price 为准，这里是同一份全局档位，供开局库存和没有 SO 时回退。
public sealed class WeaponOffer
{
    public readonly string id;
    public readonly string displayName;
    public readonly int price;

    public WeaponOffer(string id, string displayName, int price)
    {
        this.id = id;
        this.displayName = displayName;
        this.price = price;
    }
}

public static class WeaponTable
{
    public const int StartingRemnant = 4;
    public const int SmallMonsterRemnant = 1;
    public const int BossRemnant = 10;
    public const int Capacity = 12;

    public static readonly WeaponOffer[] All =
    {
        new WeaponOffer("sword", "剑", 4),
        new WeaponOffer("axe", "斧", 4),
        new WeaponOffer("hammer", "锤", 4),
        new WeaponOffer("torch", "火把", 4),
        new WeaponOffer("greatsword", "大剑", 8),
        new WeaponOffer("spear", "长矛", 8),
        new WeaponOffer("dual_swords", "双剑", 8),
        new WeaponOffer("sickle", "镰刀", 8),
        new WeaponOffer("flail", "链枷", 8),
        new WeaponOffer("poleaxe", "斧枪", 12),
        new WeaponOffer("flame_tongue", "焰舌", 12),
        new WeaponOffer("heaven_fire", "天火", 12),
    };

    public static WeaponOffer Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id) return All[i];
        return null;
    }

    public static int PriceOf(string id)
    {
        var offer = Find(id);
        return offer != null ? offer.price : 0;
    }

    public static string NameOf(string id)
    {
        var offer = Find(id);
        return offer != null ? offer.displayName : id;
    }
}
