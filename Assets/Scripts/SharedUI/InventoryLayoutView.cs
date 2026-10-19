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
    public string SelectedItemId => selectedItem;

    readonly List<InventoryDisplayRules.WeaponEntry> weapons = new List<InventoryDisplayRules.WeaponEntry>();
    readonly List<InventoryDisplayRules.ItemEntry> items = new List<InventoryDisplayRules.ItemEntry>();
    readonly List<InventoryEntryView> weaponRows = new List<InventoryEntryView>(), itemRows = new List<InventoryEntryView>();
    readonly StringBuilder signature = new StringBuilder();
    TMP_FontAsset runtimeFont;
    string weaponSignature, itemSignature, selectedItem;

    public void Bind(EquipmentPanelView panel, TMP_FontAsset font)
    {
        owner=panel;runtimeFont=font;
        if (useItemButton == null && itemDetails != null)
        {
            useItemButton = ItemUi.Button(itemDetails.transform.parent, font, "UseSelectedItem",
                new Vector2(1516, -757), new Vector2(112, 30), panel.UseSelectedItem);
            var rect = (RectTransform)useItemButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            useItemButton.GetComponentInChildren<TMP_Text>().text = "使用道具";
        }
        else if (useItemButton != null) useItemButton.onClick.AddListener(panel.UseSelectedItem);
    }

    public void Refresh(GameState state)
    {
        if(owner==null || state==null)return;
        var player=state.player;
        if(healthReadout!=null)healthReadout.text="血量  " + player.hp + " / " + player.maxHp;
        if(healthFill!=null)healthFill.fillAmount=player.maxHp>0?Mathf.Clamp01((float)player.hp/player.maxHp):0;
        if(resolveReadout!=null)resolveReadout.text=player.resolve+" / "+player.maxResolve;
        for(int i=0;resolveFills!=null && i<resolveFills.Length;i++)
        {
            if(resolveFills[i]!=null)resolveFills[i].enabled=i<player.resolve && i<player.maxResolve;
            if(resolvePips!=null && i<resolvePips.Length && resolvePips[i]!=null)resolvePips[i].alpha=i<player.maxResolve?1:.18f;
        }
        if(currency!=null)currency.text=state.world.sinRemnant.ToString();
        if(currentWeapon!=null)currentWeapon.text=string.IsNullOrEmpty(player.currentWeaponId)?"未装备武器":Name(player.currentWeaponId);

        InventoryDisplayRules.Weapons(state.inventory.weapons,Damage,weapons);
        if(!weapons.Exists(entry=>entry.id==owner.SelectedWeaponId))
        {
            string first=weapons.Exists(entry=>entry.id==player.currentWeaponId)?player.currentWeaponId:weapons.Count>0?weapons[0].id:null;
            owner.SetWeaponSelection(first);
        }
        UpdateWeaponRows(state);
        UpdateSlots(state);
        UpdateWeaponDetails(state);

        InventoryDisplayRules.Items(state.inventory.items,items);
        items.RemoveAll(entry => entry.id == ItemTable.Lantern);
        items.Add(new InventoryDisplayRules.ItemEntry(ItemTable.Lantern, state.player.lanternCharges));
        if(!items.Exists(entry=>entry.id==selectedItem))selectedItem=items.Count>0?items[0].id:null;
        UpdateItemRows(state);
        UpdateItemDetails(state);
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

    void UpdateWeaponRows(GameState state)
    {
        signature.Clear();foreach(var entry in weapons)signature.Append(entry.id).Append(':').Append(entry.column).Append('|');
        string next=signature.ToString();
        if(next!=weaponSignature)
        {
            Clear(weaponRows);int[] counts=new int[4];
            foreach(var entry in weapons)
            {
                if(weaponTemplate==null || weaponContent==null)break;
                var row=Create(weaponTemplate,weaponContent);row.name="Weapon_"+entry.id;
                var rect=(RectTransform)row.transform;
                rect.anchoredPosition=new Vector2(entry.column*196+6,-counts[entry.column]++*224-4);
                rect.sizeDelta=new Vector2(184,212);
                string id=entry.id;row.button.onClick.AddListener(()=>owner.SelectOwnedWeapon(id));
                Sprite sprite=Art(id);
                row.icon.sprite=sprite;row.icon.enabled=sprite!=null;
                row.fallback.gameObject.SetActive(sprite==null);row.fallback.text=Name(id);
                row.title.text=Name(id);
                weaponRows.Add(row);
            }
            int maximum=0;foreach(int count in counts)maximum=Mathf.Max(maximum,count);
            if(weaponContent!=null)weaponContent.sizeDelta=new Vector2(784,Mathf.Max(224,maximum*224));
            weaponSignature=next;
        }
        for(int i=0;i<weaponRows.Count;i++)
        {
            string id=weapons[i].id;int slot=state.inventory.equippedWeaponIds.IndexOf(id);
            weaponRows[i].annotation.text=id==state.player.currentWeaponId?"当前 · 已装备":slot>=0?"已装备 · "+(slot+1):"";
            weaponRows[i].Select(id==owner.SelectedWeaponId);
        }
        if(emptyWeapons!=null)emptyWeapons.gameObject.SetActive(weapons.Count==0);
    }

    void UpdateSlots(GameState state)
    {
        for(int i=0;owner.slotButtons!=null && i<owner.slotButtons.Length;i++)
        {
            string id=i<state.inventory.equippedWeaponIds.Count?state.inventory.equippedWeaponIds[i]:null;
            if(owner.slotLabels!=null && i<owner.slotLabels.Length && owner.slotLabels[i]!=null)
                owner.slotLabels[i].text=(i+1)+" · "+(string.IsNullOrEmpty(id)?"空槽":Name(id));
            var image=owner.slotButtons[i]!=null?owner.slotButtons[i].targetGraphic as Image:null;
            if(image!=null)image.color=new Color(0,0,0,i==owner.SelectedSlot ? .12f : .025f);
        }
    }

    void UpdateWeaponDetails(GameState state)
    {
        string id=owner.SelectedWeaponId;
        bool owned=!string.IsNullOrEmpty(id),equipped=owned&&state.inventory.equippedWeaponIds.Contains(id);
        if(owner.equipButton!=null)owner.equipButton.interactable=owned;
        if(owner.unequipButton!=null)owner.unequipButton.interactable=!string.IsNullOrEmpty(state.inventory.equippedWeaponIds[owner.SelectedSlot]);
        if(owner.selectWeaponButton!=null)owner.selectWeaponButton.interactable=equipped&&id!=state.player.currentWeaponId;
        if(weaponDetails==null)return;
        if(!owned){weaponDetails.text="选择持有武器，查看伤害类型与技能。";return;}
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
                row.icon.sprite=info.icon;row.icon.enabled=info.icon!=null;
                row.fallback.gameObject.SetActive(info.icon==null);row.fallback.text=info.displayName;
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
        itemDetails.enableAutoSizing=true;itemDetails.fontSizeMin=12;itemDetails.fontSizeMax=20;
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
