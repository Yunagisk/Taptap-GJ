using System;

// The village uses the shared world clock; the presentation never writes time directly.
public static class VillageInnRules
{
    public static int TimeCost(int time) => WorldResourceRules.UnitsPerDay
        - Math.Max(0, time) % WorldResourceRules.UnitsPerDay;

    public static bool CanRest(GameState state) => state?.player != null && state.world != null
        && state.player.hp > 0 && state.player.maxHp > 0
        && (state.player.hp < state.player.maxHp
            || state.player.lanternCharges < state.player.maxLanternCharges)
        && state.world.time >= 0 && (long)state.world.time + TimeCost(state.world.time) <= int.MaxValue;

    public static bool TryRest(GameState state, MapLayout layout, int departureTime, out string message)
    {
        if (layout == null || state?.map == null || state.world == null || state.world.time != departureTime)
        { message = "当前时间已变化，请重新确认歇息。"; return false; }
        if (!CanRest(state))
        { message = "生命和提灯次数已满，无需休息。"; return false; }
        int hp = Math.Max(0, state.player.maxHp - state.player.hp);
        int lantern = Math.Max(0, state.player.maxLanternCharges - state.player.lanternCharges);
        WorldResourceRules.SpendTime(layout, state.map, state.world, TimeCost(departureTime), true, saved =>
        {
            saved.player.hp = saved.player.maxHp;
            saved.player.lanternCharges = saved.player.maxLanternCharges;
            ItemRules.EnsureShopStock(saved);
        });
        state.player.hp = state.player.maxHp;
        state.player.lanternCharges = state.player.maxLanternCharges;
        ItemRules.EnsureShopStock(state);
        message = "歇息完成，恢复 " + hp + " 点生命，补充 " + lantern + " 次提灯。";
        return true;
    }
}
