using System;

// 武器目录。availableInShop 区分商店与事件奖励；价格供没有 SO 时回退。
public sealed class WeaponOffer
{
    public readonly string id;
    public readonly string displayName;
    public readonly int price;
    public readonly bool availableInShop;

    public WeaponOffer(string id, string displayName, int price, bool availableInShop = true)
    {
        this.id = id;
        this.displayName = displayName;
        this.price = price;
        this.availableInShop = availableInShop;
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
        new WeaponOffer("sickle", "镰刀", 8),
        new WeaponOffer("flail", "链枷", 8),
        new WeaponOffer("poleaxe", "斧枪", 12),
        new WeaponOffer("flame_tongue", "焰舌", 12, false),
        new WeaponOffer("heaven_fire", "天火", 12, false),
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

    public static bool IsSoldInShop(string id) => Find(id)?.availableInShop == true;

    public static string NameOf(string id)
    {
        var offer = Find(id);
        return offer != null ? offer.displayName : id;
    }
}
