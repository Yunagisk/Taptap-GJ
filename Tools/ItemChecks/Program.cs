using System;
using System.Collections.Generic;
using System.Text.Json;

static class Program
{
    static int checks;
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    static void Check(bool ok, string name)
    { checks++; if (!ok) throw new Exception("FAILED: " + name); }
    static GameState State() => new GameState();

    static void Main()
    {
        var state = State(); state.player.hp = 50;
        Check(ItemRules.TryGrant(state, ItemTable.Bandage).ok, "grant bandage");
        Check(!ItemRules.TryGrant(state, ItemTable.Bandage).ok, "carry cap is enforced");
        var result = ItemRules.TryUse(state, "绷带", false);
        Check(result.ok && result.amount == 15 && state.player.hp == 65, "outdoor bandage heals 15");
        Check(ItemRules.Count(state.inventory, ItemTable.Bandage) == 0, "successful use removes empty stack");
        Check(!ItemRules.TryUse(state, ItemTable.Bandage, false).ok && state.player.hp == 65, "repeat use cannot heal without stock");
        Check(state.world.time == 0 && state.world.sin == 40, "outdoor use costs no time or sin");

        ItemRules.TryGrant(state, ItemTable.Bandage); state.player.healingMultiplier = .75f;
        result = ItemRules.TryUse(state, ItemTable.Bandage, false);
        Check(result.ok && result.amount == 11 && state.player.hp == 76, "healing modifier rounds down");
        ItemRules.TryGrant(state, ItemTable.Bandage); state.player.hp = 98; state.player.healingMultiplier = 1;
        Check(ItemRules.TryUse(state, ItemTable.Bandage, false).amount == 2 && state.player.hp == 100, "healing caps at max HP");
        ItemRules.TryGrant(state, ItemTable.Bandage);
        Check(!ItemRules.TryUse(state, ItemTable.Bandage, false).ok && ItemRules.Count(state.inventory, ItemTable.Bandage) == 1, "full HP does not consume");
        state.player.hp = 50; state.player.healingMultiplier = 0;
        Check(!ItemRules.TryUse(state, ItemTable.Bandage, false).ok && ItemRules.Count(state.inventory, ItemTable.Bandage) == 1, "zero healing does not consume");
        state.player.healingMultiplier = float.NaN;
        Check(!ItemRules.TryUse(state, ItemTable.Bandage, false).ok, "invalid multiplier rejected");
        state.player.healingMultiplier = float.PositiveInfinity;
        Check(ItemRules.TryUse(state, ItemTable.Bandage, false).ok && state.player.hp == 100, "large modifier cannot overflow HP");
        state.player.hp = 0; ItemRules.TryGrant(state, ItemTable.Bandage);
        Check(!ItemRules.TryUse(state, ItemTable.Bandage, false).ok && state.player.hp == 0, "items do not revive");

        state = State(); ItemRules.TryGrant(state, ItemTable.HolyWater);
        state.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Poison, 3));
        state.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Ignite, 2));
        state.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Regen, 2));
        result = ItemRules.TryUse(state, "holy-water", false);
        Check(result.ok && result.amount == 2, "holy water purges every debuff kind");
        Check(state.player.ordinaryStatuses.Count == 1 && state.player.ordinaryStatuses[0].id == StatusId.Regen
            && state.player.ordinaryStatuses[0].stacks == 2, "holy water preserves buffs");
        ItemRules.TryGrant(state, ItemTable.HolyWater);
        Check(!ItemRules.TryUse(state, ItemTable.HolyWater, false).ok && ItemRules.Count(state.inventory, ItemTable.HolyWater) == 1,
            "no debuff does not consume holy water");

        var user = new User { CurrentHp = 30, MaxHp = 100 };
        foreach (StatusId id in Enum.GetValues(typeof(StatusId))) user.Statuses.Apply(id, 2, 1);
        result = ItemRules.TryUse(state, ItemTable.HolyWater, true, user);
        Check(result.ok && result.amount == 6 && user.Statuses.DebuffCount == 0, "battle holy water removes all six debuffs and layers");
        Check(user.Statuses.Has(StatusId.Regen) && user.Statuses.Has(StatusId.Haste) && user.Statuses.Has(StatusId.Steadfast), "all buffs survive cleansing");
        Check(state.player.ordinaryStatuses.Count == 3, "battle effects sync to serializable state");
        ItemRules.TryGrant(state, ItemTable.Bandage);
        result = ItemRules.TryUse(state, ItemTable.Bandage, true, user);
        Check(result.ok && user.CurrentHp == 45 && state.player.hp == 45, "battle heals actual combatant and syncs shared HP");
        ItemRules.TryGrant(state, ItemTable.Bandage); user.IgnoreHealing = true;
        Check(!ItemRules.TryUse(state, ItemTable.Bandage, true, user).ok && ItemRules.Count(state.inventory, ItemTable.Bandage) == 1, "no actual effect never consumes");

        var target = new Target(); int charges = state.player.lanternCharges;
        Check(!ItemRules.TryUse(state, ItemTable.Lantern, false).ok && state.player.lanternCharges == charges, "lantern forbidden out of battle");
        Check(!ItemRules.TryUse(state, ItemTable.Lantern, true, user).ok, "missing target rejected");
        target.IsAlive = false;
        Check(!ItemRules.TryUse(state, ItemTable.Lantern, true, user, target).ok && state.player.lanternCharges == charges, "dead target preserves charge");
        target.IsAlive = true;
        result = ItemRules.TryUse(state, ItemTable.Lantern, true, user, target);
        Check(result.ok && result.amount == 2 && state.player.lanternCharges == charges - 1, "lantern reveals all unknown weaknesses for one charge");
        Check(!ItemRules.TryUse(state, ItemTable.Lantern, true, user, target).ok && state.player.lanternCharges == charges - 1, "already revealed enemy does not consume");
        state.player.lanternCharges = 0; target.UnknownWeaknessCount = 1;
        Check(!ItemRules.TryUse(state, ItemTable.Lantern, true, user, target).ok && target.UnknownWeaknessCount == 1, "empty lantern rejected before reveal");
        Check(!ItemRules.TryGrant(state, ItemTable.Lantern).ok && !ItemRules.TryBuy(state, ItemTable.Lantern).ok, "lantern uses charges rather than item stock");
        Check(!ItemRules.TryUse(state, "haste_potion", true, user).ok, "excluded potions have no implementation");

        state = State(); state.world.sinRemnant = 20;
        Check(ItemRules.TryBuy(state, ItemTable.Bandage).ok && state.world.sinRemnant == 18, "bandage purchase pays correct price");
        Check(!ItemRules.TryBuy(state, ItemTable.Bandage).ok && state.world.sinRemnant == 18, "purchase cannot repeat or exceed daily stock");
        ItemRules.EnsureShopStock(state);
        Check(ItemRules.ShopStock(state, ItemTable.Bandage) == 0, "reopening shop never restocks");
        Check(ItemRules.TryBuy(state, ItemTable.HolyWater).ok && state.world.sinRemnant == 12, "holy water costs six");
        state.world.time = 3;
        Check(ItemRules.ShopStock(state, ItemTable.Bandage) == 1, "new day restocks");
        Check(!ItemRules.TryBuy(state, ItemTable.Bandage).ok && ItemRules.ShopStock(state, ItemTable.Bandage) == 1 && state.world.sinRemnant == 12,
            "carry cap does not charge money or stock");
        state.player.hp = 50; ItemRules.TryUse(state, ItemTable.Bandage, false);
        Check(ItemRules.TryBuy(state, ItemTable.Bandage).ok && state.world.sinRemnant == 10, "can buy after freeing capacity on later day");
        state.inventory.items.Clear(); state.world.sinRemnant = 0;
        Check(!ItemRules.TryBuy(state, ItemTable.HolyWater).ok && ItemRules.ShopStock(state, ItemTable.HolyWater) == 1, "insufficient money preserves stock");
        Check(state.world.time == 3 && state.world.sin == 40, "shopping does not advance world resources");

        state.inventory.items.Add(new ItemStack("绷带", 1));
        Check(ItemRules.Count(state.inventory, "bandage") == 1 && !ItemRules.TryGrant(state, "bandage").ok, "legacy aliases share carry cap");
        Check(!ItemRules.TryGrant(state, ItemTable.Bandage, -1).ok && !ItemRules.TryGrant(state, ItemTable.Bandage, int.MaxValue).ok, "invalid grants do not overflow");
        state.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Poison, 2));
        string saved = JsonSerializer.Serialize(state, Json);
        var loaded = JsonSerializer.Deserialize<GameState>(saved, Json);
        Check(ItemRules.ShopStock(loaded, ItemTable.Bandage) == 0 && loaded.player.ordinaryStatuses[0].stacks == 2,
            "JSON roundtrip preserves stock and status layers");
        Check(ItemRules.Count(loaded.inventory, ItemTable.Bandage) == 1, "JSON roundtrip preserves inventory");
        Check(!ItemRules.TryUse(null, ItemTable.Bandage, false).ok && !ItemRules.TryBuy(null, ItemTable.HolyWater).ok, "null state fails safely");
        Console.WriteLine($"PASS: {checks} item use, healing, cleansing, lantern, stock and persistence checks");
    }

    sealed class User : IItemUser
    {
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public StatusSet Statuses { get; } = new StatusSet();
        public bool IgnoreHealing;
        public int Heal(int amount) { if (IgnoreHealing) return 0; int n = Math.Min(amount, MaxHp - CurrentHp); CurrentHp += n; return n; }
    }
    sealed class Target : IItemWeaknessTarget
    {
        public bool IsAlive { get; set; } = true;
        public int UnknownWeaknessCount { get; set; } = 2;
        public int RevealWeaknesses() { int n = UnknownWeaknessCount; UnknownWeaknessCount = 0; return n; }
    }
}
