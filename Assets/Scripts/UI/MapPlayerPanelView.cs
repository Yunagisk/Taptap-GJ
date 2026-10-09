using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapPlayerPanelView : MonoBehaviour
{
    public Canvas canvas;
    public WorldMapController mapController;
    public RectTransform hudRoot;
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

    public bool IsInventoryOpen => inventoryPanel != null && inventoryPanel.target != null
        && inventoryPanel.target.alpha > 0;

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
        if (inventoryWindow != null)
            inventoryWindow.localScale = Vector3.one * Mathf.Max(0.1f,
                Mathf.Min(1f, (area.x - 48) / 960f, (area.y - 48) / 640f));
        if (hudRoot != null)
            hudRoot.localScale = Vector3.one * Mathf.Min(1f, area.x / 1280f);
    }

    public void OpenInventory()
    {
        Refresh();
        inventoryPanel.Show();
        inventoryPanel.transform.SetAsLastSibling();
    }

    public void CloseInventory()
    {
        if (inventoryPanel != null) inventoryPanel.Hide();
    }

    public void Refresh()
    {
        if (GameRoot.I == null || GameRoot.I.state == null) return;
        var state = GameRoot.I.state;
        var player = state.player;
        string health = player.hp + " / " + player.maxHp;
        hudHealthText.text = "HP " + health;
        inventoryHealthText.text = "生命值  " + health;
        float ratio = player.maxHp > 0 ? Mathf.Clamp01((float)player.hp / player.maxHp) : 0;
        SetFill(hudHealthFill, ratio);
        SetFill(inventoryHealthFill, ratio);
        resourcesText.text = "决心  " + player.resolve + " / " + player.maxResolve
            + "\n提灯  " + player.lanternCharges + " / " + player.maxLanternCharges;
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
