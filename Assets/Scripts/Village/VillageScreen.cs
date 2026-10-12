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
    RectTransform bagContent;
    bool shopReady;
    bool bagReady;
    Button[] slotButtons;
    TMP_FontAsset runtimeFont;
    GameObject detailRoot;
    TMP_Text detailText;
    RectTransform detailRect;
    string shownWeaponId;
    GameObject dragGhost;
    string pendingEquipId;
    int pendingEquipSlot = -1;
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
            new MapProgression(template.Load(), root.state.map, root.state.world,
                string.IsNullOrEmpty(root.state.map.templateId) ? Guid.NewGuid().GetHashCode() : root.state.map.randomSeed);
        }
        catch (Exception error) { Debug.LogException(error, this); }
        EquipRules.Ensure(root.state.player);
        if (font != null && font.sourceFontFile != null)
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
            var canvas = hpText != null ? hpText.GetComponentInParent<Canvas>()
                : GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
        }
        _panels = new[] { panelInn, panelShop, panelBag, panelEvent };
        PrepareShopList();
        PrepareBag();
        PrepareDetail();
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
        RebuildBag();
    }

    public void OpenEvent()
    {
        Show(panelEvent, focusOption);
    }

    public void ClosePanels()
    {
        HideWeaponDetail(shownWeaponId);
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
        pendingEquipId = null;
        pendingEquipSlot = -1;
        if (panelConflict != null)
            panelConflict.SetActive(false);
        SetBagButtons(true);
        Select(focusSlot);
    }

    public void ConfirmInn()
    {
        Say("旅馆已提交一次。关闭后再开不会重复结算。未写存档。");
        ClosePanels();
    }

    public void ConfirmBuy()
    {
        Say("点列表里的武器购买。鼠标停在武器上可看详情。买后进入背包，不补货。");
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
        if (!string.IsNullOrEmpty(pendingEquipId) && pendingEquipSlot >= 0 && GameRoot.I != null)
        {
            string id = pendingEquipId;
            int slot = pendingEquipSlot;
            pendingEquipId = null;
            pendingEquipSlot = -1;
            var result = EquipRules.TryEquip(GameRoot.I.state, id, slot, true);
            Say(result.message);
            RefreshStatus();
            RebuildBag();
            if (panelConflict != null) panelConflict.SetActive(false);
            SetBagButtons(true);
            Select(focusSlot);
            return;
        }
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
            panelRoot.SetActive(true);
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
                label.text = weaponName + "    " + price + " 罪骸    " + WeaponStatTable.AttackBrief(id);
            }
            var inspect = button.gameObject.AddComponent<WeaponInspectable>();
            inspect.screen = this;
            inspect.weaponId = id;
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
                : "鼠标停在武器上查看详情。每把库存 1，买后进入背包，不补货。";
        }
        if (buttons.Count > 0) Select(buttons[0].gameObject);
    }

    void BuyWeapon(string id, int price)
    {
        if (GameRoot.I == null) return;
        var result = ShopRules.TryBuy(GameRoot.I.state, id, price);
        Say(result.message);
        RefreshStatus();
        if (!result.ok) return;
        RebuildShop();
        RebuildBag();
    }

    void PrepareBag()
    {
        if (panelBag == null || focusSlot == null) return;
        var bagRect = panelBag.GetComponent<RectTransform>();
        if (bagRect != null) bagRect.sizeDelta = new Vector2(760f, 680f);

        slotButtons = new Button[EquipRules.SlotCount];
        var bagButtons = panelBag.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < EquipRules.SlotCount; i++)
        {
            Button button = null;
            for (int n = 0; n < bagButtons.Length; n++)
                if (bagButtons[n] != null && bagButtons[n].name == "Btn_Slot_" + (i + 1)) button = bagButtons[n];
            if (button == null) continue;
            button.onClick = new Button.ButtonClickedEvent();
            slotButtons[i] = button;
            var drop = button.gameObject.AddComponent<WeaponSlotDrop>();
            drop.screen = this;
            drop.slotIndex = i;
            var drag = button.gameObject.AddComponent<WeaponDragSource>();
            drag.screen = this;
            drag.fromSlot = i;
            var inspect = button.gameObject.AddComponent<WeaponInspectable>();
            inspect.screen = this;
        }

        var scrollGo = new GameObject("BagScroll", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement), typeof(Image), typeof(WeaponBagDrop));
        scrollGo.transform.SetParent(panelBag.transform, false);
        scrollGo.transform.SetSiblingIndex(2);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
        scrollGo.GetComponent<WeaponBagDrop>().screen = this;
        var element = scrollGo.GetComponent<LayoutElement>();
        element.minHeight = 180;
        element.preferredHeight = 240;
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
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        bagContent = contentRect;
        bagReady = true;
    }

    void RebuildBag()
    {
        if (!bagReady || GameRoot.I == null) return;
        var state = GameRoot.I.state;
        EquipRules.Ensure(state.player);
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;
            string id = EquipRules.Slot(state.player, i);
            var label = slotButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                if (runtimeFont != null) label.font = runtimeFont;
                label.text = string.IsNullOrEmpty(id) ? "空槽" : WeaponTable.NameOf(id);
            }
            var image = slotButtons[i].GetComponent<Image>();
            if (image != null)
                image.color = !string.IsNullOrEmpty(id) && id == state.player.currentWeaponId
                    ? new Color(0.45f, 0.55f, 0.42f, 1f)
                    : new Color(0.3f, 0.3f, 0.34f, 1f);
            var drag = slotButtons[i].GetComponent<WeaponDragSource>();
            if (drag != null) drag.weaponId = id;
            var inspect = slotButtons[i].GetComponent<WeaponInspectable>();
            if (inspect != null) inspect.weaponId = id;
        }

        for (int i = bagContent.childCount - 1; i >= 0; i--)
            Destroy(bagContent.GetChild(i).gameObject);
        if (state.inventory.weapons.Count == 0)
        {
            var empty = MakeBagRow(bagContent, "背包是空的");
            empty.GetComponent<Button>().interactable = false;
            return;
        }
        for (int i = 0; i < state.inventory.weapons.Count; i++)
        {
            string id = state.inventory.weapons[i];
            bool equipped = EquipRules.IndexOf(state.player, id) >= 0;
            string caption = WeaponTable.NameOf(id) + "    " + WeaponStatTable.AttackBrief(id)
                + (equipped ? "    已装配" : "");
            var button = MakeBagRow(bagContent, caption);
            button.name = "Bag_" + id;
            var drag = button.gameObject.AddComponent<WeaponDragSource>();
            drag.screen = this;
            drag.fromSlot = -1;
            drag.weaponId = id;
            var inspect = button.gameObject.AddComponent<WeaponInspectable>();
            inspect.screen = this;
            inspect.weaponId = id;
        }
    }

    Button MakeBagRow(Transform parent, string caption)
    {
        var go = new GameObject("BagRow", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.34f, 1f);
        image.sprite = focusSlot != null ? focusSlot.GetComponent<Image>().sprite : null;
        image.type = Image.Type.Sliced;
        var element = go.GetComponent<LayoutElement>();
        element.minHeight = 52;
        element.preferredHeight = 56;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>());
        var text = textGo.AddComponent<TextMeshProUGUI>();
        if (runtimeFont != null) text.font = runtimeFont;
        else if (toast != null) text.font = toast.font;
        text.text = caption;
        text.fontSize = 24;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.margin = new Vector4(12f, 4f, 12f, 4f);
        text.color = new Color(0.96f, 0.93f, 0.88f, 1f);
        text.raycastTarget = false;
        return button;
    }

    public void DropOnSlot(string weaponId, int slot)
    {
        if (GameRoot.I == null) return;
        if (panelConflict != null && panelConflict.activeSelf) return;
        var result = EquipRules.TryEquip(GameRoot.I.state, weaponId, slot, false);
        if (result.needConfirm)
        {
            pendingEquipId = weaponId;
            pendingEquipSlot = slot;
            var label = panelConflict != null ? panelConflict.transform.Find("Txt_Conflict") : null;
            var text = label != null ? label.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                if (runtimeFont != null) text.font = runtimeFont;
                text.text = result.message;
            }
            OpenConflict();
            return;
        }
        Say(result.message);
        RefreshStatus();
        RebuildBag();
    }

    public void DropOnBag(int slot)
    {
        if (GameRoot.I == null) return;
        var result = EquipRules.TryUnequip(GameRoot.I.state, slot);
        Say(result.message);
        RefreshStatus();
        RebuildBag();
    }

    void PrepareDetail()
    {
        Canvas canvas = hpText != null ? hpText.canvas : null;
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        detailRoot = new GameObject("WeaponDetail", typeof(RectTransform), typeof(Image));
        detailRoot.transform.SetParent(canvas.transform, false);
        detailRect = detailRoot.GetComponent<RectTransform>();
        detailRect.sizeDelta = new Vector2(420f, 280f);
        detailRect.pivot = new Vector2(0f, 1f);
        var background = detailRoot.GetComponent<Image>();
        background.color = new Color(0.08f, 0.07f, 0.06f, 0.94f);
        background.raycastTarget = false;
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(detailRoot.transform, false);
        Stretch(textGo.GetComponent<RectTransform>());
        detailText = textGo.AddComponent<TextMeshProUGUI>();
        if (runtimeFont != null) detailText.font = runtimeFont;
        else if (toast != null) detailText.font = toast.font;
        detailText.fontSize = 22;
        detailText.alignment = TextAlignmentOptions.TopLeft;
        detailText.color = new Color(0.96f, 0.93f, 0.88f, 1f);
        detailText.margin = new Vector4(16f, 12f, 16f, 12f);
        detailText.raycastTarget = false;
        detailText.enableWordWrapping = true;
        detailRoot.SetActive(false);

        dragGhost = new GameObject("DragGhost", typeof(RectTransform));
        dragGhost.transform.SetParent(canvas.transform, false);
        var ghostText = dragGhost.AddComponent<TextMeshProUGUI>();
        if (runtimeFont != null) ghostText.font = runtimeFont;
        else if (toast != null) ghostText.font = toast.font;
        ghostText.fontSize = 24;
        ghostText.alignment = TextAlignmentOptions.Center;
        ghostText.color = new Color(1f, 0.92f, 0.7f, 1f);
        ghostText.raycastTarget = false;
        dragGhost.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 48f);
        dragGhost.SetActive(false);
    }

    public void ShowWeaponDetail(string weaponId, Vector2 screenPosition)
    {
        if (detailRoot == null || string.IsNullOrEmpty(weaponId)) return;
        shownWeaponId = weaponId;
        detailText.text = WeaponStatTable.Detail(weaponId);
        detailRoot.SetActive(true);
        detailRoot.transform.SetAsLastSibling();
        MoveOnCanvas(detailRect, screenPosition, new Vector2(18f, -18f));
    }

    public void HideWeaponDetail(string weaponId)
    {
        if (shownWeaponId != weaponId && !string.IsNullOrEmpty(weaponId)) return;
        shownWeaponId = null;
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    public void BeginDragGhost(string weaponId, Vector2 screenPosition)
    {
        HideWeaponDetail(shownWeaponId);
        if (dragGhost == null) return;
        var text = dragGhost.GetComponent<TMP_Text>();
        if (text != null) text.text = WeaponTable.NameOf(weaponId);
        dragGhost.SetActive(true);
        dragGhost.transform.SetAsLastSibling();
        MoveDragGhost(screenPosition);
    }

    public void MoveDragGhost(Vector2 screenPosition)
    {
        if (dragGhost == null || !dragGhost.activeSelf) return;
        MoveOnCanvas(dragGhost.GetComponent<RectTransform>(), screenPosition, new Vector2(12f, -12f));
    }

    public void EndDragGhost()
    {
        if (dragGhost != null) dragGhost.SetActive(false);
    }

    void MoveOnCanvas(RectTransform rect, Vector2 screenPosition, Vector2 offset)
    {
        if (rect == null) return;
        var canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var canvasRect = canvas.transform as RectTransform;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out var local))
            return;
        rect.localPosition = local + offset;
        Vector2 size = canvasRect.rect.size;
        Vector2 pivot = rect.pivot;
        Vector2 half = rect.sizeDelta;
        float minX = -size.x * 0.5f + half.x * pivot.x;
        float maxX = size.x * 0.5f - half.x * (1f - pivot.x);
        float minY = -size.y * 0.5f + half.y * pivot.y;
        float maxY = size.y * 0.5f - half.y * (1f - pivot.y);
        var pos = rect.localPosition;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        rect.localPosition = pos;
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
