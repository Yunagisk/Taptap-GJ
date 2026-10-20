using TMPro;
using UnityEngine;

// Colored markers sit beneath the portrait panel; every active status keeps its
// own name and stack count even when the health bar uses one dominant color.
public sealed class BattlePlayerStatusView : MonoBehaviour
{
    BattleHudView hud;
    PlayerCombatant player;
    RectTransform markers;
    BattleHudGraphic healthFrame;
    Color normalHealth, normalInk;
    readonly GameObject[] badges = new GameObject[9];
    readonly TMP_Text[] labels = new TMP_Text[9];

    public void Bind(BattleHudView owner)
    {
        hud = owner; player = hud.Flow.Player;
        normalHealth = hud.playerHealth.color;
        healthFrame = hud.playerHealth.GetComponentInParent<BattleHudGraphic>();
        if (healthFrame != null) normalInk = healthFrame.ink;
        var panel = (RectTransform)hud.resolve[0].transform.parent;
        var root = new GameObject("PlayerStatusMarkers", typeof(RectTransform));
        root.layer = hud.gameObject.layer;
        markers = (RectTransform)root.transform;
        markers.SetParent(panel, false);
        markers.anchorMin = markers.anchorMax = new Vector2(0, 0);
        markers.pivot = new Vector2(0, 1);
        markers.anchoredPosition = new Vector2(0, -8);
        markers.sizeDelta = new Vector2(405, 100);
        for (int i = 0; i < badges.Length; i++)
        {
            var id = (StatusId)i;
            var badge = new GameObject(id.ToString(), typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic));
            badge.layer = root.layer; badge.transform.SetParent(markers, false);
            var rect = (RectTransform)badge.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(126, 30);
            var frame = badge.GetComponent<BattleHudGraphic>();
            frame.raycastTarget = false; frame.stroke = .8f; frame.ink = StatusAppearance.ColorOf(id) * .7f;
            var iconRoot = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(StatusIconGraphic));
            iconRoot.layer = root.layer; iconRoot.transform.SetParent(rect, false);
            var icon = iconRoot.GetComponent<StatusIconGraphic>();
            icon.Status = id; icon.color = StatusAppearance.ColorOf(id); icon.raycastTarget = false;
            icon.rectTransform.anchoredPosition = new Vector2(-46, 0); icon.rectTransform.sizeDelta = new Vector2(20, 20);
            var textRoot = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textRoot.layer = root.layer; textRoot.transform.SetParent(rect, false);
            var label = textRoot.GetComponent<TextMeshProUGUI>();
            label.font = hud.statusText.font; label.fontSize = 18; label.raycastTarget = false;
            label.color = Color.Lerp(StatusAppearance.ColorOf(id), Color.white, .35f);
            label.alignment = TextAlignmentOptions.MidlineLeft; label.enableWordWrapping = false;
            label.rectTransform.anchoredPosition = new Vector2(15, 0); label.rectTransform.sizeDelta = new Vector2(87, 28);
            badges[i] = badge; labels[i] = label;
        }
        if (player != null) player.OnMarksChanged += Changed;
        Refresh();
    }
    void Changed(Combatant actor) => Refresh();
    public void Refresh()
    {
        if (player == null) return;
        int slot = 0;
        for (int i = 0; i < badges.Length; i++)
        {
            int stacks = player.Statuses.Stacks((StatusId)i);
            bool visible = stacks > 0 && !player.IsDead;
            badges[i].SetActive(visible);
            if (!visible) continue;
            labels[i].text = StatusRules.DisplayName((StatusId)i) + " ×" + stacks;
            ((RectTransform)badges[i].transform).anchoredPosition = new Vector2(slot % 3 * 134, -(slot / 3) * 36);
            slot++;
        }
        UpdatePalette();
    }
    void Update() => UpdatePalette();
    void UpdatePalette()
    {
        if (player == null || hud == null) return;
        var primary = StatusAppearance.Primary(player.Statuses);
        Color tint = StatusAppearance.HealthTint(player.Statuses, normalHealth);
        hud.playerHealth.color = primary.HasValue ? Color.Lerp(tint, StatusAppearance.ColorOf(primary.Value),
            .05f + .04f * Mathf.Sin(Time.unscaledTime * 3.1f)) : normalHealth;
        if (healthFrame == null) return;
        healthFrame.ink = primary.HasValue ? Color.Lerp(normalInk, StatusAppearance.ColorOf(primary.Value), .6f) : normalInk;
        healthFrame.SetVerticesDirty();
    }
    void OnDestroy()
    {
        if (!ReferenceEquals(player, null)) player.OnMarksChanged -= Changed;
        if (hud != null && hud.playerHealth != null) hud.playerHealth.color = normalHealth;
        if (healthFrame != null) { healthFrame.ink = normalInk; healthFrame.SetVerticesDirty(); }
        if (markers != null) Destroy(markers.gameObject);
    }
}
