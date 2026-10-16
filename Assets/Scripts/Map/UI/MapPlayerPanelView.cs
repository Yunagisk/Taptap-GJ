using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapPlayerPanelView : MonoBehaviour
{
    public EquipmentPanelView equipmentPanel;
    public Canvas canvas;
    public WorldMapController mapController;
    public RectTransform hudRoot;
    [Tooltip("Uniform size of the portrait and its hover details. Shrinks to fit smaller windows.")]
    [Min(0.1f)] public float hudScale = 1;
    [Tooltip("Keep the complete HUD at a fixed position near the bottom-left corner.")]
    public bool alignHudPortraitToCorner;
    [Min(0)] public float hudEdgeMargin = 4;
    [Tooltip("Offset the complete HUD from the fitted corner, in Canvas units.")]
    public Vector2 hudOffset = new Vector2(55, 22);
    public RectTransform inventoryWindow;
    public PanelToggler inventoryPanel;
    public Image portrait;
    public TMP_Text hudHealthText;
    public RectTransform hudHealthFill;
    public TMP_Text inventoryHealthText;
    public RectTransform inventoryHealthFill;
    public TMP_Text resourcesText;
    public TMP_Text sinValueText;
    public TMP_Text itemsText;
    public ScrollRect itemsScroll;
    public TMP_Text[] weaponNames;
    public TMP_Text[] weaponStatuses;
    public Image[] weaponSlots;
    public TMP_Text sinStatusText;
    public TMP_Text virtueStatusText;
    public string sinStatus = "—";
    public string virtueStatus = "—";
    public bool manageEscape = true;

    float nextRefresh;
    MapPlayerInkHud inkHud;
    Material healthReadoutMaterial;

    public bool IsInventoryOpen => equipmentPanel != null ? equipmentPanel.IsOpen
        : inventoryPanel != null && inventoryPanel.target != null && inventoryPanel.target.alpha > 0;

    public void SetEquipmentPanel(EquipmentPanelView panel)
    {
        equipmentPanel = panel;
    }

    void Start()
    {
        CloseInventory();
        Refresh();
    }

    void Update()
    {
        if (manageEscape && IsInventoryOpen && Input.GetKeyDown(KeyCode.Escape)) CloseInventory();
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        Refresh();
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        Vector2 area = ((RectTransform)canvas.transform).rect.size;
        if (inventoryWindow != null && (equipmentPanel == null || equipmentPanel.parchment == null))
            inventoryWindow.localScale = Vector3.one * Mathf.Max(0.1f,
                Mathf.Min(1f, (area.x - 48) / 960f, (area.y - 48) / 640f));
        if (hudRoot != null)
        {
            float requested = Mathf.Max(0.1f, hudScale) * Mathf.Min(1f, area.x / 1280f);
            if (alignHudPortraitToCorner && portrait != null)
            {
                Rect expandedBounds = hudRoot.rect;
                if (inkHud == null) inkHud = hudRoot.GetComponent<MapPlayerInkHud>();
                if (inkHud != null && inkHud.TryGetExpandedBounds(hudRoot, out var contentBounds))
                    expandedBounds = contentBounds;
                var pose = MapHudScreenLayout.Fit(area, expandedBounds, requested, hudEdgeMargin, hudOffset);
                hudRoot.localScale = Vector3.one * pose.scale;
                hudRoot.anchoredPosition = pose.position;
            }
            else
            {
                Vector2 available = area - Vector2.Max(Vector2.zero, hudRoot.anchoredPosition) - Vector2.one * 24;
                float fitted = Mathf.Min(requested, available.x / Mathf.Max(1, hudRoot.rect.width),
                    available.y / Mathf.Max(1, hudRoot.rect.height));
                hudRoot.localScale = Vector3.one * Mathf.Max(0.01f, fitted);
            }
        }
    }

    public void OnPortraitClicked()
    {
        if (IsInventoryOpen) return;
        OpenInventory();
    }

    public void OpenInventory()
    {
        Refresh();
        if (equipmentPanel != null) { equipmentPanel.Open(); return; }
        if (inventoryPanel == null) return;
        inventoryPanel.Show();
        inventoryPanel.transform.SetAsLastSibling();
    }

    public void CloseInventory()
    {
        if (equipmentPanel != null) equipmentPanel.Close();
        else if (inventoryPanel != null) inventoryPanel.Hide();
    }

    public void Refresh()
    {
        if (GameRoot.I == null || GameRoot.I.state == null) return;
        var state = GameRoot.I.state;
        var player = state.player;
        string health = player.hp + " / " + player.maxHp;
        if (hudHealthText != null)
        {
            hudHealthText.text = health;
            // Use this label's material instance so the pale digits stay clear
            // over both the ink fill and empty track without changing the font.
            if (hudHealthText.fontSharedMaterial != null
                && hudHealthText.fontSharedMaterial != healthReadoutMaterial)
            {
                healthReadoutMaterial = hudHealthText.fontMaterial;
                healthReadoutMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(18, 18, 18, 255));
                healthReadoutMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                hudHealthText.UpdateMeshPadding();
                hudHealthText.havePropertiesChanged = true;
            }
        }
        float healthRatio = player.maxHp > 0 ? Mathf.Clamp01((float)player.hp / player.maxHp) : 0;
        SetFill(hudHealthFill, healthRatio);
        if (equipmentPanel != null)
        {
            equipmentPanel.Refresh();
            return;
        }
        inventoryHealthText.text = "生命值  " + health;
        float ratio = player.maxHp > 0 ? Mathf.Clamp01((float)player.hp / player.maxHp) : 0;
        SetFill(hudHealthFill, ratio);
        SetFill(inventoryHealthFill, ratio);
        resourcesText.text = "决心  " + player.resolve + " / " + player.maxResolve
            + "\n提灯  " + player.lanternCharges + " / " + player.maxLanternCharges
            + "\n罪骸  " + state.world.sinRemnant;
        int threshold = 100;
        if (mapController != null && mapController.Progression != null) threshold = mapController.Progression.Layout.sinThreshold;
        sinValueText.text = "罪恶值  " + state.world.sin + " / " + threshold;
        for (int i = 0; i < weaponNames.Length; i++)
        {
            string id = i < state.inventory.weapons.Count ? state.inventory.weapons[i] : null;
            bool equipped = !string.IsNullOrEmpty(id) && id == player.currentWeaponId;
            weaponNames[i].text = string.IsNullOrEmpty(id) ? "空槽" : NewRunFactory.WeaponName(id);
            weaponStatuses[i].text = equipped ? "当前武器" : "";
            weaponSlots[i].color = equipped ? new Color32(220, 235, 225, 255) : new Color32(232, 234, 239, 255);
        }
        var items = new StringBuilder();
        foreach (var item in state.inventory.items)
        {
            if (item == null || item.count <= 0 || string.IsNullOrEmpty(item.itemId)) continue;
            if (items.Length > 0) items.Append('\n');
            items.Append(item.itemId).Append("  × ").Append(item.count);
        }
        string itemValue = items.Length == 0 ? "暂无道具" : items.ToString();
        if (itemsText.text != itemValue)
        {
            itemsText.text = itemValue;
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemsText.rectTransform);
        }
        sinStatusText.text = "原罪  " + sinStatus;
        virtueStatusText.text = "美德  " + virtueStatus;
    }

    static void SetFill(RectTransform fill, float ratio)
    {
        if (fill != null) fill.anchorMax = new Vector2(ratio, 1);
    }

}
