using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class VillageInnUI : MonoBehaviour
{
    public TMP_Text timeRange, health, lantern, confirmLabel, message;
    public Button confirmButton, cancelButton;
    VillageScreen owner;
    bool bound;
    public GameObject PreferredFocus => confirmButton.interactable ? confirmButton.gameObject : cancelButton.gameObject;

    public void Bind(VillageScreen screen, TMP_FontAsset font)
    {
        owner = screen;
        if (font != null) foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
        if (bound) return;
        bound = true;
        confirmButton.onClick.AddListener(owner.ConfirmInn);
        cancelButton.onClick.AddListener(owner.ClosePanels);
    }

    void OnEnable() { if (message != null) message.text = ""; Refresh(); }
    void Update() => Refresh();
    public void Refresh()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state?.player == null || state.world == null || owner == null) return;
        int time = Mathf.Max(0, state.world.time);
        long target = (long)time + VillageInnRules.TimeCost(time);
        timeRange.text = MapTimeDialMotion.FormatLabel(time) + " → "
            + (target <= int.MaxValue ? MapTimeDialMotion.FormatLabel((int)target) : "时间已达上限");
        health.text = state.player.hp + " → " + state.player.maxHp;
        lantern.text = state.player.lanternCharges + " → " + state.player.maxLanternCharges;
        bool recover = VillageInnRules.CanRest(state);
        confirmButton.interactable = recover && owner.CanUseFacilities;
        cancelButton.interactable = !owner.IsResting;
        confirmLabel.text = recover ? "歇息" : "无需休息";
    }
}
