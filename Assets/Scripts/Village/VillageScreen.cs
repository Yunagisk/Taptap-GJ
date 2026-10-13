using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class VillageScreen : MonoBehaviour
{
    [Header("Shared run and status")]
    public WorldMapTemplate template;
    public TMP_FontAsset font;
    public TMP_Text hpText;
    public TMP_Text lanternText;
    public TMP_Text sinText;
    public TMP_Text timeText;
    public TMP_Text weaponText;

    public GameObject panelRoot;
    public GameObject panelInn;
    public GameObject panelShop;
    public GameObject panelBag;
    public GameObject panelEvent;
    public GameObject panelConflict;

    public GameObject focusInn;
    public GameObject focusShop;
    public GameObject focusTavern;
    public GameObject focusInnConfirm;
    public GameObject focusBuy;
    public GameObject focusSlot;
    public GameObject focusOption;
    public GameObject focusConflict;

    public TMP_Text toast;
    public WeaponLibrary weaponLibrary;

    GameObject[] _panels;
    RectTransform shopContent;
    bool shopReady;
    TMP_FontAsset runtimeFont;
    public bool IsLoading { get; private set; }

    void Awake()
    {
        if (GameRoot.I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        var root = GameRoot.I;
        if (root.villageMapTemplate != null) template = root.villageMapTemplate;
        root.villageMapTemplate = template;
        root.enterVillageOnLoad = false;
        if (string.IsNullOrEmpty(root.villageReturnScene))
            root.villageReturnScene = GameFlowController.MapScene;
        try
        {
            if (template == null) throw new InvalidOperationException("Assign a WorldMapTemplate to VillageScreen.");
            // Reuse the existing run. Opening the village never starts a new map.
            var progression = new MapProgression(template.Load(), root.state.map, root.state.world,
                string.IsNullOrEmpty(root.state.map.templateId) ? Guid.NewGuid().GetHashCode() : root.state.map.randomSeed);
            MainStoryRules.EnsureInitialized(root.state, progression.Layout);
            EquipmentRules.EnsureInitialized(root.state);
        }
        catch (Exception error) { Debug.LogException(error, this); }
        if (font != null && font.sourceFontFile != null)
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
            runtimeFont.isMultiAtlasTexturesEnabled = true;
            var canvas = hpText != null ? hpText.GetComponentInParent<Canvas>()
                : GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
        }
        _panels = new[] { panelInn, panelShop, panelBag, panelEvent };
        PrepareShopList();
        ClosePanels();
        RefreshStatus();
    }

    public void RefreshStatus()
    {
        if (GameRoot.I == null) return;
        var state = GameRoot.I.state;
        if (hpText != null) hpText.text = "生命 " + state.player.hp + "/" + state.player.maxHp;
        if (lanternText != null) lanternText.text = "提灯 " + state.player.lanternCharges + "/" + state.player.maxLanternCharges;
        if (sinText != null) sinText.text = "罪骸 " + state.world.sinRemnant + "    罪恶 " + state.world.sin;
        string[] periods = { "早晨", "下午", "夜间" };
        if (timeText != null) timeText.text = "第 " + (state.world.time / 3 + 1) + " 天 " + periods[state.world.time % 3];
        if (weaponText != null) weaponText.text = "当前武器：" + NewRunFactory.WeaponName(state.player.currentWeaponId);
    }

    void Update()
    {
        RefreshStatus();
        if (IsLoading || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (panelConflict != null && panelConflict.activeSelf) CloseConflict();
        else if (panelRoot != null && panelRoot.activeSelf) ClosePanels();
    }

    public void OpenInn()
    {
        Show(panelInn, focusInnConfirm);
    }

    public void OpenShop()
    {
        Show(panelShop, focusBuy);
        RebuildShop();
    }

    public void OpenBag()
    {
        Show(panelBag, focusSlot);
        var equipment = panelBag != null ? panelBag.GetComponent<EquipmentPanelView>() : null;
        if (equipment != null) equipment.Open();
    }

    public void OpenEvent()
    {
        Show(panelEvent, focusOption);
    }

    public void ClosePanels()
    {
        SetBagButtons(true);
        if (panelConflict != null)
            panelConflict.SetActive(false);
        if (panelRoot != null)
            panelRoot.SetActive(false);
        Select(focusInn);
    }

    public void OpenConflict()
    {
        if (panelConflict != null)
            panelConflict.SetActive(true);
        SetBagButtons(false);
        Select(focusConflict);
    }

    public void CloseConflict()
    {
        if (panelConflict != null)
            panelConflict.SetActive(false);
        SetBagButtons(true);
        Select(focusSlot);
    }

    public void ConfirmInn()
    {
        // Ignore callbacks after closing the inn or while changing scenes.
        if (IsLoading || panelRoot == null || !panelRoot.activeInHierarchy
            || panelInn == null || !panelInn.activeInHierarchy) return;
        var player = GameRoot.I != null && GameRoot.I.state != null ? GameRoot.I.state.player : null;
        if (player == null) return;
        if (player.hp >= player.maxHp)
        {
            Say("生命已满，无需休息。");
            ClosePanels();
            return;
        }

        int recovered = player.maxHp - player.hp;
        player.hp = player.maxHp;
        RefreshStatus();
        Say("休息完成，恢复 " + recovered + " 点生命。");
        ClosePanels();
    }

    public void ConfirmBuy()
    {
        Say("点列表里的武器购买。买后不补货。");
    }

    public void ConfirmOptionA()
    {
        Say("选项甲已提交一次。关掉再开不会重抽。");
        ClosePanels();
    }

    public void ConfirmOptionB()
    {
        Say("选项乙已提交一次。关掉再开不会重抽。");
        ClosePanels();
    }

    public void ConfirmUseItem()
    {
        Say("使用道具已提交一次。");
    }

    public void ConfirmConflict()
    {
        Say("装备替换已提交一次。");
        CloseConflict();
    }

    public void LeaveToMap()
    {
        if (IsLoading) return;
        var root = GameRoot.I;
        string scene = root != null && !string.IsNullOrEmpty(root.villageReturnScene)
            ? root.villageReturnScene : GameFlowController.MapScene;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Say("世界地图场景未加入构建列表。");
            return;
        }
        try
        {
            IsLoading = true;
            if (root != null) root.enterVillageOnLoad = false;
            Say("正在进入世界地图……");
            SceneManager.LoadSceneAsync(scene);
        }
        catch (Exception error)
        {
            IsLoading = false;
            Say("进入地图失败，请重试。");
            Debug.LogException(error, this);
        }
    }

    void Show(GameObject panel, GameObject focus)
    {
        if (_panels != null)
        {
            foreach (var item in _panels)
            {
                if (item != null)
                    item.SetActive(item == panel);
            }
        }

        if (panelConflict != null)
            panelConflict.SetActive(false);
        SetBagButtons(true);
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }
        Select(focus);
    }

    void SetBagButtons(bool interactable)
    {
        if (panelBag == null)
            return;
        var buttons = panelBag.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].interactable = interactable;
    }

    void Say(string message)
    {
        if (toast != null)
            toast.text = message;
    }

    void Select(GameObject target)
    {
        if (target == null || EventSystem.current == null)
            return;
        EventSystem.current.SetSelectedGameObject(target);
    }

    void PrepareShopList()
    {
        if (panelShop == null || focusBuy == null) return;
        focusBuy.SetActive(false);
        var scrollGo = new GameObject("ShopScroll", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement), typeof(Image));
        scrollGo.transform.SetParent(panelShop.transform, false);
        scrollGo.transform.SetSiblingIndex(2);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        var element = scrollGo.GetComponent<LayoutElement>();
        element.minHeight = 240;
        element.preferredHeight = 280;
        element.flexibleHeight = 1;
        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        shopContent = contentRect;
        shopReady = true;
    }

    void RebuildShop()
    {
        if (!shopReady || GameRoot.I == null) return;
        var state = GameRoot.I.state;
        ShopRules.EnsureStock(state);
        for (int i = shopContent.childCount - 1; i >= 0; i--)
            Destroy(shopContent.GetChild(i).gameObject);

        var buttons = new List<Button>();
        var ids = state.shop.availableWeaponIds;
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            var button = Instantiate(focusBuy.GetComponent<Button>(), shopContent);
            button.gameObject.SetActive(true);
            button.name = "Buy_" + id;
            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            int price = weaponLibrary != null ? weaponLibrary.PriceOf(id) : WeaponTable.PriceOf(id);
            string weaponName = weaponLibrary != null ? weaponLibrary.NameOf(id) : WeaponTable.NameOf(id);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                if (runtimeFont != null) label.font = runtimeFont;
                label.text = weaponName + "    " + price + " 罪骸    库存 1";
            }
            string captured = id;
            int capturedPrice = price;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => BuyWeapon(captured, capturedPrice));
            buttons.Add(button);
        }
        for (int i = 0; i < buttons.Count; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            if (i > 0) nav.selectOnUp = buttons[i - 1];
            if (i + 1 < buttons.Count) nav.selectOnDown = buttons[i + 1];
            buttons[i].navigation = nav;
        }
        var body = panelShop.transform.Find("Txt_ShopBody");
        var bodyText = body != null ? body.GetComponent<TMP_Text>() : null;
        if (bodyText != null)
        {
            if (runtimeFont != null) bodyText.font = runtimeFont;
            bodyText.text = ids.Count == 0
                ? "武器已全部售出，不再补货。"
                : "每把库存 1，买后不补货。价格全关卡通用。";
        }
        if (buttons.Count > 0) Select(buttons[0].gameObject);
    }

    void BuyWeapon(string id, int price)
    {
        if (GameRoot.I == null) return;
        var result = ShopRules.TryBuy(GameRoot.I.state, id, price);
        Say(result.message);
        RefreshStatus();
        if (result.ok) RebuildShop();
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void OnDestroy()
    {
        if (runtimeFont == null) return;
        foreach (var texture in runtimeFont.atlasTextures)
            if (texture != null) Destroy(texture);
        Destroy(runtimeFont.material);
        Destroy(runtimeFont);
    }
}
