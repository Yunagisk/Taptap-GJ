using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryLayoutView : MonoBehaviour
{
    [Serializable] public struct WeaponArt { public string id; public Sprite sprite; }
    [Serializable] public struct ItemInfo { public string id, displayName; [TextArea] public string description; public Sprite icon; }
    [Serializable] public struct CategorySlot { public InventoryEntryView card; public InventoryInkGraphic newDot; }
    public EquipmentPanelView owner;
    public Image portrait, healthFill;
    public TMP_Text healthReadout, resolveReadout, currency, currentWeapon;
    public Image[] resolveFills;
    public CanvasGroup[] resolvePips;
    public ScrollRect weaponScroll, itemScroll;
    public RectTransform weaponContent, itemContent;
    public InventoryEntryView weaponTemplate, itemTemplate;
    public TMP_Text weaponDetails, itemDetails, emptyWeapons, emptyItems;
    public WeaponArt[] weaponArt;
    public ItemInfo[] itemInfo;
    public Button useItemButton;
    public CategorySlot[] categorySlots;
    public RectTransform weaponPicker;
    public CanvasGroup pickerVisibility;
    public TMP_Text pickerTitle;
    public Button closePickerButton;
    public string SelectedItemId => selectedItem;
    public bool IsWeaponPickerOpen => openCategory >= 0;

    readonly List<InventoryDisplayRules.WeaponEntry> weapons = new List<InventoryDisplayRules.WeaponEntry>();
    readonly List<InventoryDisplayRules.ItemEntry> items = new List<InventoryDisplayRules.ItemEntry>();
    readonly List<InventoryEntryView> weaponRows = new List<InventoryEntryView>(), itemRows = new List<InventoryEntryView>();
    readonly StringBuilder signature = new StringBuilder();
    TMP_FontAsset runtimeFont;
    string weaponSignature, itemSignature, selectedItem;
    readonly List<InventoryDisplayRules.WeaponEntry> choices = new List<InventoryDisplayRules.WeaponEntry>();
    int openCategory = -1;
    float pickerPhase;
    Vector2 pickerPosition;
    Color normalHealthColor;
    string previousWeaponDetails, previousItemDetails;

    public void Bind(EquipmentPanelView panel, TMP_FontAsset font)
    {
        owner=panel;runtimeFont=font;
        if (healthFill != null) normalHealthColor = healthFill.color;
        for (int i = 0; categorySlots != null && i < categorySlots.Length; i++)
        {
            int category = i;
            categorySlots[i].card.button.onClick.AddListener(() => ToggleWeaponPicker(category));
        }
        if (closePickerButton != null) closePickerButton.onClick.AddListener(() => CloseWeaponPicker());
        CloseWeaponPicker(true);
        if (useItemButton == null && itemDetails != null)
        {
            useItemButton = ItemUi.Button(itemDetails.transform.parent, font, "UseSelectedItem",
                new Vector2(1516, -757), new Vector2(112, 30), panel.UseSelectedItem);
            var rect = (RectTransform)useItemButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            useItemButton.GetComponentInChildren<TMP_Text>().text = "使用道具";
        }
        else if (useItemButton != null) useItemButton.onClick.AddListener(panel.UseSelectedItem);
        ApplyReadableLayout();
    }

    void ApplyReadableLayout()
    {
        // The parchment is scaled to fit a 4:3 window. A 26-unit baseline keeps
        // essential labels above fourteen screen pixels at 1024 x 768.
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            label.enableAutoSizing = false;
            label.fontSize = Mathf.Max(26, label.fontSize);
        }
        for (int i = 0; categorySlots != null && i < categorySlots.Length; i++)
        {
            var card = categorySlots[i].card;
            if (card == null) continue;
            Place(card.title.rectTransform, new Vector2(8, -142), new Vector2(168, 34));
            Place(card.annotation.rectTransform, new Vector2(4, -178), new Vector2(176, 32));
            var size = card.icon.rectTransform.sizeDelta;
            card.icon.rectTransform.sizeDelta = new Vector2(size.x, Mathf.Min(size.y, 128));
        }
        if (weaponTemplate != null)
            weaponTemplate.annotation.rectTransform.sizeDelta = new Vector2(204, 34);
        if (weaponPicker != null)
            Place(weaponPicker.Find("PickerHint") as RectTransform, new Vector2(16, -46), new Vector2(300, 32));
        var window = owner.window;
        if (window == null) return;
        Place(window.Find("WeaponDetailsFrame") as RectTransform, new Vector2(16, -664), new Vector2(784, 184));
        Place(window.Find("ItemDetailsFrame") as RectTransform, new Vector2(848, -664), new Vector2(736, 184));
        Place(window.Find("WeaponDetailsTitle") as RectTransform, new Vector2(32, -674), new Vector2(360, 40));
        Place(window.Find("ItemDetailsTitle") as RectTransform, new Vector2(864, -674), new Vector2(500, 40));
        Place(owner.unequipButton != null ? owner.unequipButton.transform as RectTransform : null,
            new Vector2(482, -674), new Vector2(120, 40));
        Place(owner.selectWeaponButton != null ? owner.selectWeaponButton.transform as RectTransform : null,
            new Vector2(612, -674), new Vector2(172, 40));
        Place(useItemButton != null ? useItemButton.transform as RectTransform : null,
            new Vector2(1400, -674), new Vector2(168, 40));
        Place(weaponDetails != null ? weaponDetails.rectTransform : null, new Vector2(32, -720), new Vector2(752, 126));
        Place(itemDetails != null ? itemDetails.rectTransform : null, new Vector2(864, -720), new Vector2(704, 126));
        if (itemScroll != null)
        {
            var rect = (RectTransform)itemScroll.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 328);
        }
        if (owner.messageText != null)
            Place(owner.messageText.rectTransform, new Vector2(20, -852), new Vector2(1560, 28));
        ScrollableTextRegion.Ensure(weaponDetails);
        ScrollableTextRegion.Ensure(itemDetails);
    }

    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    public void Refresh(GameState state)
    {
        if(owner==null || state==null)return;
        var player=state.player;
        player.NormalizeResolve();
        if(healthReadout!=null)healthReadout.text="血量  " + player.hp + " / " + player.maxHp;
        if(healthFill!=null)healthFill.fillAmount=player.maxHp>0?Mathf.Clamp01((float)player.hp/player.maxHp):0;
        if(healthFill!=null)healthFill.color=StatusAppearance.HealthTint(player.ordinaryStatuses,normalHealthColor);
        if(resolveReadout!=null)resolveReadout.text=player.resolve+" / "+player.maxResolve;
        for(int i=0;resolveFills!=null && i<resolveFills.Length;i++)
        {
            if(resolveFills[i]!=null)resolveFills[i].enabled=i<player.resolve && i<player.maxResolve;
            if(resolvePips!=null && i<resolvePips.Length && resolvePips[i]!=null)
            {
                resolvePips[i].alpha=1;
                resolvePips[i].gameObject.SetActive(i<player.maxResolve);
            }
        }
        if(currency!=null)currency.text=state.world.sinRemnant.ToString();
        if(currentWeapon!=null)currentWeapon.text=string.IsNullOrEmpty(player.currentWeaponId)?"未装备武器":Name(player.currentWeaponId);

        InventoryDisplayRules.Weapons(state.inventory.weapons,Damage,weapons);
        InventoryEquipmentRules.Normalize(state, CategoryOf);
        if(!weapons.Exists(entry=>entry.id==owner.SelectedWeaponId))
        {
            string first=weapons.Exists(entry=>entry.id==player.currentWeaponId)?player.currentWeaponId:weapons.Count>0?weapons[0].id:null;
            if (openCategory >= 0) first = state.inventory.equippedWeaponIds[openCategory];
            owner.SetWeaponSelection(first);
        }
        if (!string.IsNullOrEmpty(owner.SelectedWeaponId)) owner.SetSlotSelection(CategoryOf(owner.SelectedWeaponId));
        if (openCategory >= 0) InventoryEquipmentRules.Acknowledge(state.inventory, openCategory, CategoryOf);
        UpdateWeaponRows(state);
        UpdateCategorySlots(state);
        UpdateWeaponDetails(state);

        InventoryDisplayRules.Items(state.inventory.items,items);
        items.RemoveAll(entry => entry.id == ItemTable.Lantern);
        items.Add(new InventoryDisplayRules.ItemEntry(ItemTable.Lantern, state.player.lanternCharges));
        if(!items.Exists(entry=>entry.id==selectedItem))selectedItem=items.Count>0?items[0].id:null;
        UpdateItemRows(state);
        UpdateItemDetails(state);
        if (weaponDetails != null && previousWeaponDetails != weaponDetails.text)
        { previousWeaponDetails = weaponDetails.text; ScrollableTextRegion.Reset(weaponDetails); }
        if (itemDetails != null && previousItemDetails != itemDetails.text)
        { previousItemDetails = itemDetails.text; ScrollableTextRegion.Reset(itemDetails); }
    }

    IReadOnlyList<DamageComponent> Damage(string id)
    {
        var data=owner.weaponLibrary!=null?owner.weaponLibrary.Find(id):null;
        return data!=null?data.DamageComponents:null;
    }
    string Name(string id)=>owner.weaponLibrary!=null?owner.weaponLibrary.NameOf(id):WeaponTable.NameOf(id);
    Sprite Art(string id)
    {
        if(weaponArt!=null)foreach(var art in weaponArt)if(art.id==id)return art.sprite;
        return null;
    }

    int CategoryOf(string id)
    {
        foreach (var entry in weapons) if (entry.id == id) return entry.column;
        return (int)MapHudWeaponDisplay.PrimaryDamage(Damage(id));
    }

    public bool EquipSelected(GameState state)
    {
        string id = owner.SelectedWeaponId;
        return InventoryEquipmentRules.TryEquip(state, owner.SelectedSlot, id, CategoryOf);
    }

    void ToggleWeaponPicker(int column)
    {
        if (owner == null || !owner.IsOpen || (owner.parchment != null && !owner.parchment.CanInteract)) return;
        if (column == openCategory) { CloseWeaponPicker(); owner.Refresh(); return; }
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state == null || weaponPicker == null) return;
        openCategory = column;
        InventoryEquipmentRules.Acknowledge(state.inventory, column, CategoryOf);
        owner.SetWeaponSelection(state.inventory.equippedWeaponIds[column]);
        pickerTitle.text = InventoryDisplayRules.DamageNames[column] + "武器";
        PositionWeaponPicker(column);
        weaponPicker.gameObject.SetActive(true);
        weaponPicker.SetAsLastSibling();
        owner.SelectSlot(column);
        if (weaponScroll != null) { weaponScroll.StopMovement(); weaponScroll.verticalNormalizedPosition = 1; }
    }

    void PositionWeaponPicker(int column)
    {
        var parent = (RectTransform)weaponPicker.parent;
        var card = (RectTransform)categorySlots[column].card.transform;
        Vector3 topRight = parent.InverseTransformPoint(card.TransformPoint(new Vector3(card.rect.xMax, card.rect.yMax)));
        float x = topRight.x - parent.rect.xMin + 14;
        float y = topRight.y - parent.rect.yMax + 28;
        pickerPosition = new Vector2(Mathf.Clamp(x, 16, parent.rect.width - weaponPicker.rect.width - 16),
            Mathf.Clamp(y, -parent.rect.height + weaponPicker.rect.height + 16, -16));
    }

    public void CloseWeaponPicker(bool immediate = false)
    {
        openCategory = -1;
        if (pickerVisibility != null) pickerVisibility.blocksRaycasts = pickerVisibility.interactable = false;
        if (!immediate) return;
        pickerPhase = 0;
        if (pickerVisibility != null) pickerVisibility.alpha = 0;
        if (weaponPicker != null) weaponPicker.gameObject.SetActive(false);
    }

    void Update()
    {
        if (weaponPicker == null) return;
        if (owner == null || !owner.IsOpen) { CloseWeaponPicker(true); return; }
        if (IsWeaponPickerOpen && Input.GetMouseButtonDown(0))
        {
            var canvas = weaponPicker.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(weaponPicker, Input.mousePosition, camera);
            for (int i = 0; !inside && categorySlots != null && i < categorySlots.Length; i++)
                inside = RectTransformUtility.RectangleContainsScreenPoint((RectTransform)categorySlots[i].card.transform, Input.mousePosition, camera);
            if (!inside) { CloseWeaponPicker(); owner.Refresh(); }
        }
        pickerPhase = Mathf.MoveTowards(pickerPhase, IsWeaponPickerOpen ? 1 : 0, Time.unscaledDeltaTime / .18f);
        float ease = Mathf.SmoothStep(0, 1, pickerPhase);
        weaponPicker.anchoredPosition = pickerPosition + Vector2.right * (1 - ease) * 14;
        if (pickerVisibility != null)
        {
            pickerVisibility.alpha = ease;
            pickerVisibility.blocksRaycasts = IsWeaponPickerOpen;
            pickerVisibility.interactable = IsWeaponPickerOpen && ease > .8f;
        }
        if (pickerPhase <= 0 && weaponPicker.gameObject.activeSelf) weaponPicker.gameObject.SetActive(false);
    }

    void OnDisable() => CloseWeaponPicker(true);

    void EquipChoice(string id)
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        int column = openCategory;
        if (!InventoryEquipmentRules.TryEquip(state, column, id, CategoryOf)) return;
        owner.SetWeaponSelection(id);
        CloseWeaponPicker();
        owner.SelectSlot(column);
    }

    void UpdateWeaponRows(GameState state)
    {
        if (openCategory < 0) return;
        choices.Clear();
        foreach (var entry in weapons) if (entry.column == openCategory) choices.Add(entry);
        signature.Clear();signature.Append(openCategory).Append('|');
        foreach(var entry in choices)signature.Append(entry.id).Append('|');
        string next=signature.ToString();
        if(next!=weaponSignature)
        {
            Clear(weaponRows);
            for (int i = 0; i < choices.Count; i++)
            {
                var entry = choices[i];
                if(weaponTemplate==null || weaponContent==null)break;
                var row=Create(weaponTemplate,weaponContent);row.name="Weapon_"+entry.id;
                var rect=(RectTransform)row.transform;
                rect.anchoredPosition=new Vector2(0,-i*114);
                rect.sizeDelta=new Vector2(328,106);
                string id=entry.id;row.button.onClick.AddListener(()=>EquipChoice(id));
                Sprite sprite=Art(id);
                row.icon.sprite=sprite;row.icon.enabled=sprite!=null;
                row.fallback.gameObject.SetActive(sprite==null);row.fallback.text=Name(id);
                row.title.text=Name(id);
                weaponRows.Add(row);
            }
            if(weaponContent!=null)weaponContent.sizeDelta=new Vector2(328,Mathf.Max(326,choices.Count*114));
            weaponSignature=next;
        }
        for(int i=0;i<weaponRows.Count;i++)
        {
            string id=choices[i].id;int slot=state.inventory.equippedWeaponIds.IndexOf(id);
            weaponRows[i].annotation.text=id==state.player.currentWeaponId?"当前 · 已装备":slot>=0?"已装备":"点击装备";
            weaponRows[i].Select(slot>=0);
        }
        if(emptyWeapons!=null)emptyWeapons.gameObject.SetActive(choices.Count==0);
    }

    void UpdateCategorySlots(GameState state)
    {
        for(int i=0;categorySlots!=null && i<categorySlots.Length;i++)
        {
            string id=i<state.inventory.equippedWeaponIds.Count?state.inventory.equippedWeaponIds[i]:null;
            var card = categorySlots[i].card;
            Sprite sprite = string.IsNullOrEmpty(id) ? null : Art(id);
            card.icon.sprite = sprite; card.icon.enabled = sprite != null;
            card.fallback.gameObject.SetActive(sprite == null);
            card.fallback.text = string.IsNullOrEmpty(id) ? "＋" : Name(id);
            card.title.text = string.IsNullOrEmpty(id) ? "未装备" : Name(id);
            card.annotation.text = string.IsNullOrEmpty(id) ? "点击选择" : id == state.player.currentWeaponId ? "当前武器" : "点击更换";
            bool selected = i == openCategory || (!string.IsNullOrEmpty(id) && id == owner.SelectedWeaponId);
            card.Select(selected);
            if (!selected && card.border != null) card.border.color = new Color(.12f, .08f, .04f, .44f);
            if (categorySlots[i].newDot != null)
                categorySlots[i].newDot.gameObject.SetActive(InventoryEquipmentRules.HasUnread(state.inventory, i, CategoryOf));
        }
    }

    void UpdateWeaponDetails(GameState state)
    {
        string id=owner.SelectedWeaponId;
        bool owned=!string.IsNullOrEmpty(id),equipped=owned&&state.inventory.equippedWeaponIds.Contains(id);
        if(owner.equipButton!=null)owner.equipButton.interactable=owned && !equipped;
        if(owner.unequipButton!=null)owner.unequipButton.interactable=!string.IsNullOrEmpty(state.inventory.equippedWeaponIds[owner.SelectedSlot]);
        if(owner.selectWeaponButton!=null)owner.selectWeaponButton.interactable=equipped&&id!=state.player.currentWeaponId;
        if(weaponDetails==null)return;
        if(!owned){weaponDetails.text="点击上方属性框，选择要装备的武器。";return;}
        var data=owner.weaponLibrary!=null?owner.weaponLibrary.Find(id):null;
        var damage=Damage(id);var line=new StringBuilder(Name(id));line.Append("   ·   ");
        if(damage==null || damage.Count==0)line.Append("—");
        else
        {
            bool has=false;
            foreach(var part in damage)if((uint)part.damageType<4)
            {if(has)line.Append(" / ");line.Append(InventoryDisplayRules.DamageNames[(int)part.damageType]).Append(' ').Append(part.damage);has=true;}
        }
        var skill=data!=null?data.Skill:null;
        line.Append("\n技能：").Append(skill!=null?skill.SkillName:"无")
            .Append("   ·   决心消耗：").Append(skill!=null?skill.DeterminationCost.ToString():"—");
        if(skill!=null && !string.IsNullOrEmpty(skill.Description))line.Append("\n").Append(skill.Description);
        weaponDetails.text=line.ToString();
    }

    ItemInfo Info(string id)
    {
        var result = new ItemInfo{id=id,displayName=InventoryDisplayRules.ItemName(id)};
        if(itemInfo!=null)foreach(var item in itemInfo)
            if(ItemTable.CanonicalId(item.id)==id) { result=item; break; }
        var definition = ItemTable.Find(id);
        if(string.IsNullOrEmpty(result.displayName))result.displayName=definition?.displayName ?? id;
        if(string.IsNullOrEmpty(result.description))result.description=definition?.description;
        return result;
    }
    void UpdateItemRows(GameState state)
    {
        signature.Clear();foreach(var entry in items)signature.Append(entry.id).Append('|');
        string next=signature.ToString();
        if(next!=itemSignature)
        {
            Clear(itemRows);
            for(int i=0;i<items.Count;i++)
            {
                if(itemTemplate==null || itemContent==null)break;
                string id=items[i].id;var info=Info(id);
                var row=Create(itemTemplate,itemContent);row.name="Item_"+id;
                var rect=(RectTransform)row.transform;rect.anchoredPosition=new Vector2(0,-i*100);rect.sizeDelta=new Vector2(736,100);
                row.button.onClick.AddListener(()=>{selectedItem=id;owner.Refresh();});
                row.title.text=string.IsNullOrEmpty(info.displayName)?id:info.displayName;
                row.PresentItemIcon(info.icon, id);
                itemRows.Add(row);
            }
            if(itemContent!=null)itemContent.sizeDelta=new Vector2(736,Mathf.Max(400,items.Count*100));
            itemSignature=next;
        }
        for(int i=0;i<itemRows.Count;i++)
        {
            itemRows[i].annotation.text=items[i].id==ItemTable.Lantern
                ? "次数 "+items[i].count+" / "+state.player.maxLanternCharges : "× "+items[i].count;
            itemRows[i].Select(items[i].id==selectedItem);
        }
        if(emptyItems!=null)emptyItems.gameObject.SetActive(items.Count==0);
    }
    void UpdateItemDetails(GameState state)
    {
        var check = GameRoot.I != null && GameRoot.I.activeBattle != null
            ? new ItemResult(false,"请在战斗道具菜单中使用。") : ItemRules.Validate(state,selectedItem,false);
        if(useItemButton!=null)useItemButton.interactable=check.ok;
        if(itemDetails==null)return;
        int at=items.FindIndex(entry=>entry.id==selectedItem);
        if(at<0){itemDetails.text="暂无道具";return;}
        var info=Info(selectedItem);
        itemDetails.text=(string.IsNullOrEmpty(info.displayName)?selectedItem:info.displayName)
            +(selectedItem==ItemTable.Lantern?"   ·   剩余次数：":"   ·   持有数量：")+items[at].count
            +"\n"+(string.IsNullOrEmpty(info.description)?"暂无效果说明":info.description)
            +(check.ok?"":"\n"+check.message);
        itemDetails.enableAutoSizing=false;itemDetails.fontSize=26;
    }

    InventoryEntryView Create(InventoryEntryView template,RectTransform parent)
    {
        var row=Instantiate(template,parent);row.button.onClick=new Button.ButtonClickedEvent();
        if(runtimeFont!=null)foreach(var label in row.GetComponentsInChildren<TMP_Text>(true))label.font=runtimeFont;
        row.gameObject.SetActive(true);return row;
    }
    void Clear(List<InventoryEntryView> rows)
    {
        foreach(var row in rows)if(row!=null){row.gameObject.SetActive(false);Destroy(row.gameObject);}
        rows.Clear();
    }
}
