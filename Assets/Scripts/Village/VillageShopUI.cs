using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Saved, editable six-card shelf. Only explicit purchase calls change the run.
public sealed class VillageShopUI : MonoBehaviour
{
    [Serializable] public sealed class Offer
    {
        public string id;
        public bool consumable;
        public Sprite artwork;
        public Rect artworkUv = new Rect(0, 0, 1, 1);
        [TextArea] public string description;
    }

    public Offer[] offers;
    public VillageShopCard[] cards;
    public Button weaponsTab, itemsTab, previousButton, nextButton, closeButton, buyButton;
    public Image weaponsUnderline, itemsUnderline;
    public TMP_Text balance, pageLabel, itemName, stock, description, price, buyLabel, message;
    public RawImage artwork;
    public VillageServiceIcon fallback;
    readonly List<Offer> visible = new List<Offer>();
    readonly HashSet<string> shownWeapons = new HashSet<string>();
    VillageScreen owner;
    GameState run;
    string selected;
    int page;
    bool items, bound, purchasing;
    float nextRefresh;

    public string SelectedId => selected;
    public bool ShowingItems => items;
    public int Page => page;
    public GameObject PreferredFocus => cards != null && cards.Length > 0 && cards[0].gameObject.activeSelf
        ? cards[0].button.gameObject : closeButton.gameObject;

    public void Bind(VillageScreen screen, TMP_FontAsset font)
    {
        owner = screen;
        if (font != null) foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
        if (bound) return;
        bound = true;
        weaponsTab.onClick.AddListener(() => SetCategory(false));
        itemsTab.onClick.AddListener(() => SetCategory(true));
        previousButton.onClick.AddListener(() => ChangePage(-1));
        nextButton.onClick.AddListener(() => ChangePage(1));
        closeButton.onClick.AddListener(owner.ClosePanels);
        buyButton.onClick.AddListener(Purchase);
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            cards[i].button.onClick.AddListener(() => SelectCard(index));
        }
    }

    void OnEnable() { nextRefresh = 0; if (bound) Refresh(); }
    void Update()
    {
        if (!bound || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .15f;
        Refresh();
    }

    public void Open() { Refresh(); Focus(PreferredFocus); }

    public void SetCategory(bool consumables)
    {
        if (!isActiveAndEnabled || owner == null || !owner.CanUseFacilities) return;
        items = consumables; page = 0; selected = null;
        message.text = "";
        Refresh();
    }

    public void ChangePage(int direction)
    {
        if (!isActiveAndEnabled || owner == null || !owner.CanUseFacilities) return;
        page = Mathf.Clamp(page + direction, 0, Mathf.Max(0, (visible.Count - 1) / cards.Length));
        selected = null; message.text = "";
        Refresh();
    }

    public void SelectCard(int index)
    {
        int at = page * cards.Length + index;
        if (!isActiveAndEnabled || owner == null || !owner.CanUseFacilities || at < 0 || at >= visible.Count) return;
        selected = visible[at].id; message.text = ""; Refresh();
    }

    public void Refresh()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state?.world == null || owner == null || offers == null) return;
        ShopRules.EnsureStock(state);
        ItemRules.EnsureShopStock(state);
        if (!ReferenceEquals(run, state))
        {
            run = state; shownWeapons.Clear(); selected = null; page = 0;
            foreach (string id in state.shop.availableWeaponIds) shownWeapons.Add(id);
        }
        foreach (string id in state.shop.availableWeaponIds) shownWeapons.Add(id);
        visible.Clear();
        foreach (var offer in offers)
            if (offer != null && offer.consumable == items && (items || shownWeapons.Contains(offer.id))) visible.Add(offer);
        page = Mathf.Clamp(page, 0, Mathf.Max(0, (visible.Count - 1) / cards.Length));
        int start = page * cards.Length;
        Offer chosen = null;
        for (int i = start; i < Math.Min(visible.Count, start + cards.Length); i++)
            if (visible[i].id == selected) chosen = visible[i];
        if (chosen == null && visible.Count > start) { chosen = visible[start]; selected = chosen.id; }
        balance.text = state.world.sinRemnant.ToString();
        weaponsUnderline.enabled = !items; itemsUnderline.enabled = items;
        int pages = Math.Max(1, (visible.Count + cards.Length - 1) / cards.Length);
        pageLabel.text = (page + 1) + " / " + pages;
        previousButton.interactable = page > 0;
        nextButton.interactable = page + 1 < pages;
        pageLabel.transform.parent.gameObject.SetActive(pages > 1);
        for (int i = 0; i < cards.Length; i++)
        {
            int index = start + i;
            cards[i].gameObject.SetActive(index < visible.Count);
            if (index < visible.Count)
            {
                var offer = visible[index];
                cards[i].Present(offer, NameOf(offer), PriceOf(offer), offer == chosen, Available(offer));
            }
        }
        if (chosen == null)
        {
            itemName.text = "暂无商品"; description.text = ""; stock.text = ""; price.text = "";
            artwork.gameObject.SetActive(false); fallback.gameObject.SetActive(false);
            buyButton.interactable = false; buyLabel.text = "暂无商品"; return;
        }
        itemName.text = NameOf(chosen);
        description.text = chosen.description;
        price.text = PriceOf(chosen).ToString();
        SetArtwork(artwork, fallback, chosen);
        bool available = Available(chosen);
        bool full = chosen.consumable
            ? ItemRules.Count(state.inventory, chosen.id) >= ItemTable.Find(chosen.id).carryLimit
            : state.inventory.weapons.Count >= WeaponTable.Capacity;
        bool poor = state.world.sinRemnant < PriceOf(chosen);
        stock.text = available ? "库存 1" : "已售罄";
        buyButton.interactable = available && !full && !poor && owner.CanUseFacilities && !purchasing;
        buyLabel.text = !available ? "已售罄" : full ? (chosen.consumable ? "携带已满" : "背包已满") : poor ? "罪骸不足" : "购买";
    }

    bool Available(Offer offer) => offer.consumable ? ItemRules.ShopStock(run, offer.id) > 0
        : run.shop.availableWeaponIds.Contains(offer.id) && !run.inventory.weapons.Contains(offer.id);
    string NameOf(Offer offer) => offer.consumable ? ItemTable.Find(offer.id).displayName
        : owner.weaponLibrary != null ? owner.weaponLibrary.NameOf(offer.id) : WeaponTable.NameOf(offer.id);
    int PriceOf(Offer offer) => offer.consumable ? ItemTable.Find(offer.id).price
        : owner.weaponLibrary != null ? owner.weaponLibrary.PriceOf(offer.id) : WeaponTable.PriceOf(offer.id);

    public void Purchase()
    {
        if (purchasing || !isActiveAndEnabled || owner == null || !owner.CanUseFacilities
            || GameRoot.I == null || !ReferenceEquals(run, GameRoot.I.state) || !buyButton.interactable) return;
        var offer = visible.Find(candidate => candidate.id == selected);
        if (offer == null) return;
        purchasing = true;
        try
        {
            if (offer.consumable)
            {
                var result = ItemRules.TryBuy(run, offer.id);
                message.text = result.message;
            }
            else
            {
                var result = ShopRules.TryBuy(run, offer.id, PriceOf(offer));
                message.text = result.message;
            }
            owner.RefreshStatus();
        }
        finally { purchasing = false; Refresh(); }
        if (!buyButton.interactable) Focus(PreferredFocus);
    }

    public static void SetArtwork(RawImage image, VillageServiceIcon icon, Offer offer)
    {
        bool hasArt = offer.artwork != null;
        image.gameObject.SetActive(hasArt); icon.gameObject.SetActive(!hasArt);
        if (hasArt)
        {
            image.texture = offer.artwork.texture; image.uvRect = offer.artworkUv;
            var fit = image.GetComponent<AspectRatioFitter>();
            if (fit != null) fit.aspectRatio = image.texture.width * offer.artworkUv.width
                / Mathf.Max(1, image.texture.height * offer.artworkUv.height);
        }
        else { icon.symbol = offer.id == ItemTable.Bandage ? VillageServiceIcon.Symbol.Bandage : VillageServiceIcon.Symbol.Bottle; icon.SetVerticesDirty(); }
    }

    static void Focus(GameObject target)
    { if (EventSystem.current != null && target != null) EventSystem.current.SetSelectedGameObject(target); }
}
