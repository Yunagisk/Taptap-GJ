using UnityEditor;
using UnityEngine;

public static class ItemSystemTools
{
    [MenuItem("Tools/Items/Prepare Test Supplies (Play Mode)")]
    public static void PrepareTestSupplies()
    {
        if (!EditorApplication.isPlaying || GameRoot.I == null) return;
        var state = GameRoot.I.state;
        ItemRules.TryGrant(state, ItemTable.Bandage);
        ItemRules.TryGrant(state, ItemTable.HolyWater);
        state.player.lanternCharges = state.player.maxLanternCharges;
        var player = GameRoot.I.activeBattle != null ? GameRoot.I.activeBattle.Player : null;
        if (player != null && !player.IsDead)
        {
            int desiredHp = Mathf.Max(1, player.MaxHP / 2);
            if (player.CurrentHP > desiredHp) player.LoseRaw(player.CurrentHP - desiredHp);
            player.ApplyStatus(StatusId.Poison, 2);
            player.ApplyStatus(StatusId.Weak, 2);
        }
        else if (state.player.hp > 0)
        {
            state.player.hp = Mathf.Max(1, state.player.maxHp / 2);
            state.player.ordinaryStatuses.Clear();
            state.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Poison, 2));
            state.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Weak, 2));
        }
        Debug.Log("道具测试已准备：绷带、圣水、满提灯次数、半血、中毒和衰弱。仅修改当前 Play 会话。");
    }

    [MenuItem("Tools/Items/Prepare Test Supplies (Play Mode)", true)]
    static bool CanPrepare() => EditorApplication.isPlaying && GameRoot.I != null;
}
