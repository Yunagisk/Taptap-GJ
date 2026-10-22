using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared village/map equipment window. Layout and reusable rows are authored in prefabs.
public class EquipmentPanelView : MonoBehaviour
{
    public RectTransform window;
    public CanvasGroup visibility;
    public PanelToggler panelToggler;
    public InventoryParchmentView parchment;
    public InventoryLayoutView inventoryLayout;
    public TMP_FontAsset font;
    public WeaponLibrary weaponLibrary;
    public TMP_Text healthText, statusText, sinText, itemsText, questText, selectionText, messageText;
    public Button[] slotButtons;
    public TMP_Text[] slotLabels;
    public RectTransform ownedRowsParent;
    public Button rowButtonPrefab;
    public ScrollRect ownedWeaponsScroll;
    public Button equipButton, unequipButton, selectWeaponButton, closeButton;
    public bool manageEscape;
    [SerializeField] int selectedSlot;
    [SerializeField] string selectedOwnedWeaponId;

    readonly List<Button> ownedRows = new List<Button>();
    readonly List<string> rowIds = new List<string>();
    string ownedSignature;
    float nextRefresh;
    TMP_FontAsset runtimeFont;

    public bool IsOpen => gameObject.activeInHierarchy && (parchment != null ? parchment.IsVisible
        : visibility == null || visibility.alpha > 0.5f);
    public int SelectedSlot => selectedSlot;
    public string SelectedWeaponId => selectedOwnedWeaponId;

    void Awake()
    {
        if (font != null)
        {
            runtimeFont = SharedUIFont.Get(font);
            runtimeFont.isMultiAtlasTexturesEnabled = true;
            foreach (var label in GetComponentsInChildren<TMP_Text>(true)) label.font = runtimeFont;
        }
        if (slotButtons != null)
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                if (slotButtons[i] != null) slotButtons[i].onClick.AddListener(() => SelectSlot(slot));
            }
        if (equipButton != null) equipButton.onClick.AddListener(EquipSelected);
        if (unequipButton != null) unequipButton.onClick.AddListener(UnequipSelected);
        if (selectWeaponButton != null) selectWeaponButton.onClick.AddListener(UseSelectedSlot);
        selectedSlot = Mathf.Clamp(selectedSlot, 0, EquipmentRules.SlotCount - 1);
        if (inventoryLayout != null) inventoryLayout.Bind(this, runtimeFont);
    }

    void OnEnable() { nextRefresh = 0; Refresh(); }

    void Update()
    {
        if (manageEscape && IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        if (!IsOpen || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.15f;
        Refresh();
    }

    void LateUpdate()
    {
        if (parchment != null) { parchment.FitContent(window); return; }
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null || window == null) return;
        var area = ((RectTransform)canvas.transform).rect.size;
        window.localScale = Vector3.one * Mathf.Max(0.1f,
            Mathf.Min(1f, (area.x - 48) / 960f, (area.y - 48) / 640f));
    }

    public void Open()
    {
        if (inventoryLayout != null) inventoryLayout.CloseWeaponPicker(true);
        gameObject.SetActive(true);
        if (parchment != null) parchment.Open();
        else if (panelToggler != null) panelToggler.Show();
        else SetVisibility(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Close()
    {
        if (inventoryLayout != null) inventoryLayout.CloseWeaponPicker(true);
        if (parchment != null) parchment.Close();
        else if (panelToggler != null) panelToggler.Hide();
        else SetVisibility(false);
    }

    void SetVisibility(bool show)
    {
        if (visibility == null) return;
        visibility.alpha = show ? 1 : 0;
        visibility.blocksRaycasts = show;
        visibility.interactable = show;
    }

    public void SelectSlot(int slot)
    {
        if (slot < 0 || slot >= EquipmentRules.SlotCount) return;
        selectedSlot = slot;
        Refresh();
    }

    public void SelectOwnedWeapon(string id)
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state == null || !state.inventory.weapons.Contains(id)) return;
        selectedOwnedWeaponId = id;
        Refresh();
    }

    public void SetWeaponSelection(string id) { selectedOwnedWeaponId = id; }
    public void SetSlotSelection(int slot) { selectedSlot = Mathf.Clamp(slot, 0, EquipmentRules.SlotCount - 1); }

    public void EquipSelected()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        bool ok = !string.IsNullOrEmpty(selectedOwnedWeaponId) && state != null
            && (inventoryLayout != null ? inventoryLayout.EquipSelected(state) : EquipmentRules.TryEquip(state, selectedSlot, selectedOwnedWeaponId));
        Say(ok ? "已装备：" + NameOf(selectedOwnedWeaponId) : "请先选择该类已拥有的武器。");
        Refresh();
    }

    public void UnequipSelected()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        bool ok = state != null && EquipmentRules.TryEquip(state, selectedSlot, null);
        Say(ok ? "已卸下武器。" : "无法卸下该武器。");
        Refresh();
    }

    public void UseSelectedSlot()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state == null) return;
        EquipmentRules.EnsureInitialized(state);
        string id = inventoryLayout != null ? selectedOwnedWeaponId : state.inventory.equippedWeaponIds[selectedSlot];
        bool ok = EquipmentRules.TrySelectWeapon(state, id);
        Say(ok ? "当前武器：" + NameOf(state.player.currentWeaponId) : "当前槽位没有武器。");
        Refresh();
    }

    public void UseSelectedItem()
    {
        if (!IsOpen || inventoryLayout == null || GameRoot.I == null) return;
        if (GameRoot.I.activeBattle != null) { Say("请在战斗道具菜单中使用。"); return; }
        var result = ItemRules.TryUse(GameRoot.I.state, inventoryLayout.SelectedItemId, false);
        Say(result.message);
        Refresh();
    }

    public void Refresh()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state == null) return;
        EquipmentRules.EnsureInitialized(state);
        if (inventoryLayout != null) { inventoryLayout.Refresh(state); return; }
        var player = state.player;
        if (healthText != null) healthText.text = "生命  " + player.hp + " / " + player.maxHp;
        if (statusText != null) statusText.text = "决心  " + player.resolve + " / " + player.maxResolve
            + "    提灯  " + player.lanternCharges + " / " + player.maxLanternCharges
            + "\n罪骸  " + state.world.sinRemnant;
        if (sinText != null) sinText.text = "罪恶值  " + state.world.sin + " / " + WorldResourceRules.MaxSin;
        if (questText != null) questText.text = "当前目标  " + MainStoryRules.GoalText(state);
        var itemLines = new StringBuilder();
        foreach (var item in state.inventory.items)
        {
            if (item == null || item.count <= 0 || string.IsNullOrEmpty(item.itemId)) continue;
            if (itemLines.Length > 0) itemLines.Append('\n');
            itemLines.Append(item.itemId).Append("  × ").Append(item.count);
        }
        if (itemsText != null) itemsText.text = itemLines.Length == 0 ? "暂无道具" : itemLines.ToString();
        if (!state.inventory.weapons.Contains(selectedOwnedWeaponId)) selectedOwnedWeaponId = null;
        for (int i = 0; slotButtons != null && i < slotButtons.Length; i++)
        {
            string id = i < state.inventory.equippedWeaponIds.Count ? state.inventory.equippedWeaponIds[i] : null;
            bool current = !string.IsNullOrEmpty(id) && id == player.currentWeaponId;
            if (slotLabels != null && i < slotLabels.Length && slotLabels[i] != null)
                slotLabels[i].text = "槽 " + (i + 1) + "  " + (string.IsNullOrEmpty(id) ? "空槽" : NameOf(id))
                    + (current ? "\n当前武器" : "");
            var image = slotButtons[i] != null ? slotButtons[i].targetGraphic as Image : null;
            if (image != null) image.color = parchment != null
                ? new Color(0, 0, 0, i == selectedSlot ? .14f : .045f)
                : i == selectedSlot ? new Color32(198, 218, 202, 255) : new Color32(234, 232, 224, 255);
        }
        string signature = string.Join("|", state.inventory.weapons.ToArray());
        if (signature != ownedSignature) RebuildOwnedRows(state, signature);
        for (int i = 0; i < ownedRows.Count; i++)
        {
            string id = rowIds[i];
            var label = ownedRows[i].GetComponentInChildren<TMP_Text>(true);
            int equippedSlot = state.inventory.equippedWeaponIds.IndexOf(id);
            if (label != null) label.text = NameOf(id) + (equippedSlot >= 0 ? "  [槽 " + (equippedSlot + 1) + "]" : "");
            var image = ownedRows[i].targetGraphic as Image;
            if (image != null) image.color = parchment != null
                ? new Color(0, 0, 0, id == selectedOwnedWeaponId ? .14f : .045f)
                : id == selectedOwnedWeaponId ? new Color32(198, 218, 202, 255) : new Color32(245, 243, 235, 255);
        }
        if (selectionText != null) selectionText.text = "选中槽 " + (selectedSlot + 1)
            + "    武器：" + (string.IsNullOrEmpty(selectedOwnedWeaponId) ? "未选择" : NameOf(selectedOwnedWeaponId));
        if (equipButton != null) equipButton.interactable = !string.IsNullOrEmpty(selectedOwnedWeaponId);
        bool hasWeapon = !string.IsNullOrEmpty(state.inventory.equippedWeaponIds[selectedSlot]);
        if (unequipButton != null) unequipButton.interactable = hasWeapon;
        if (selectWeaponButton != null) selectWeaponButton.interactable = hasWeapon;
    }

    void RebuildOwnedRows(GameState state, string signature)
    {
        if (ownedRowsParent == null || rowButtonPrefab == null) return;
        float position = ownedWeaponsScroll != null ? ownedWeaponsScroll.verticalNormalizedPosition : 1;
        foreach (var row in ownedRows)
            if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        ownedRows.Clear();
        rowIds.Clear();
        foreach (string id in state.inventory.weapons)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var row = Instantiate(rowButtonPrefab, ownedRowsParent);
            row.gameObject.SetActive(true);
            row.name = "Owned_" + id;
            string captured = id;
            row.onClick = new Button.ButtonClickedEvent();
            row.onClick.AddListener(() => SelectOwnedWeapon(captured));
            if (runtimeFont != null)
                foreach (var label in row.GetComponentsInChildren<TMP_Text>(true)) label.font = runtimeFont;
            ownedRows.Add(row);
            rowIds.Add(id);
        }
        ownedSignature = signature;
        LayoutRebuilder.ForceRebuildLayoutImmediate(ownedRowsParent);
        if (ownedWeaponsScroll != null) ownedWeaponsScroll.verticalNormalizedPosition = position;
    }

    string NameOf(string id) => weaponLibrary != null ? weaponLibrary.NameOf(id) : WeaponTable.NameOf(id);
    void Say(string message) { if (messageText != null) messageText.text = message; }

}
