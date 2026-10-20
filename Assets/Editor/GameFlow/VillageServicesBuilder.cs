using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit authoring: importing this file never edits an open scene.
public static class VillageServicesBuilder
{
    public const string ScenePath = "Assets/Scenes/Village.unity";
    public const string ShopPath = "Assets/Prefabs/UI/VillageShopPanel.prefab";
    public const string InnPath = "Assets/Prefabs/UI/InnPanel.prefab";
    public const string TransitionPath = "Assets/Prefabs/UI/VillageRestTransition.prefab";
    static readonly Color Ink = new Color32(58, 43, 29, 255);
    static readonly Color Muted = new Color32(110, 87, 59, 255);
    static TMP_FontAsset font;
    static Sprite paper, remnant, lantern;

    [MenuItem("Tools/Game Flow/Build Village Shop and Inn")]
    public static void Build()
    {
        RequireCleanEditor();
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            font = Load<TMP_FontAsset>("Assets/Fonts/Huiwen.asset");
            paper = Load<Sprite>("Assets/Sprite/UI/InventoryParchment2K/InventoryPaper.png");
            remnant = Load<Sprite>("Assets/Sprite/UI/ResourceIcons2K/SinRemnant.png");
            lantern = Load<Sprite>("Assets/Sprite/UI/ResourceIcons2K/Lantern.png");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var screen = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VillageScreen>(true)).Single();
            var parent = screen.panelRoot.transform;
            var canvas = parent.GetComponentInParent<Canvas>().rootCanvas;

            var shopAsset = Save(BuildShop(), ShopPath);
            var innAsset = Save(BuildInn(), InnPath);
            var transitionAsset = Save(BuildTransition(canvas.transform), TransitionPath);
            Object.DestroyImmediate(screen.panelShop);
            Object.DestroyImmediate(screen.panelInn);
            if (screen.restTransition != null) Object.DestroyImmediate(screen.restTransition.gameObject);
            screen.panelShop = (GameObject)PrefabUtility.InstantiatePrefab(shopAsset, parent);
            screen.panelShop.name = "Panel_Shop";
            screen.panelInn = (GameObject)PrefabUtility.InstantiatePrefab(innAsset, parent);
            screen.panelInn.name = "Panel_Inn";
            var transition = (GameObject)PrefabUtility.InstantiatePrefab(transitionAsset, canvas.transform);
            screen.restTransition = transition.GetComponent<VillageRestTransition>();
            screen.focusBuy = screen.panelShop.GetComponent<VillageShopUI>().cards[0].button.gameObject;
            screen.focusInnConfirm = screen.panelInn.GetComponent<VillageInnUI>().confirmButton.gameObject;
            StyleHotspot(screen.focusInn, "旅店", VillageServiceIcon.Symbol.Bed, -60);
            StyleHotspot(screen.focusShop, "商店", VillageServiceIcon.Symbol.Bag, -35);
            StyleHotspot(screen.focusTavern, "酒馆", VillageServiceIcon.Symbol.Mug, -60);
            screen.panelShop.SetActive(false); screen.panelInn.SetActive(false); transition.SetActive(false);
            screen.panelRoot.SetActive(false);
            foreach (var changed in new[] {screen.panelShop,screen.panelInn,transition})
                PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
            EditorUtility.SetDirty(screen);
            ValidateScreen(screen);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save village services.");
            AssetDatabase.SaveAssets();
            Debug.Log("Village shop, inn confirmation and overnight transition installed in Village.unity.");
        }
        finally
        {
            if (previous.Length > 0 && previous.All(item => !string.IsNullOrEmpty(item.path))
                && previous.Any(item => item.isLoaded && item.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    [MenuItem("Tools/Game Flow/Validate Village Shop and Inn")]
    public static void Validate()
    {
        RequireCleanEditor();
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ValidateScreen(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VillageScreen>(true)).Single());
        }
        finally
        {
            if (previous.Length > 0 && previous.All(item => !string.IsNullOrEmpty(item.path))
                && previous.Any(item => item.isLoaded && item.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    static void ValidateScreen(VillageScreen screen)
    {
        var shop = screen.panelShop.GetComponent<VillageShopUI>();
        var inn = screen.panelInn.GetComponent<VillageInnUI>();
        if (shop == null || inn == null || screen.restTransition == null || shop.cards.Length != 6
            || shop.offers.Length != 14 || screen.focusBuy != shop.cards[0].button.gameObject
            || screen.focusInnConfirm != inn.confirmButton.gameObject)
            throw new InvalidOperationException("Village service bindings are incomplete.");
        if (shop.offers.Any(offer => string.IsNullOrWhiteSpace(offer.description))
            || shop.offers.Where(offer => !offer.consumable).Any(offer => offer.artwork == null))
            throw new InvalidOperationException("Shop artwork or one-sentence description is missing.");
        foreach (var label in screen.panelShop.GetComponentsInChildren<TMP_Text>(true))
            if (label.text.Contains("旅途所需")) throw new InvalidOperationException("Removed shop subtitle is still present.");
        var sorted = new SerializedObject(screen.restTransition.GetComponent<Canvas>());
        if (!sorted.FindProperty("m_OverrideSorting").boolValue
            || sorted.FindProperty("m_SortingOrder").intValue < 100)
            throw new InvalidOperationException("Blackout must sort above every village panel.");
        Debug.Log("Village service prefab references are valid.");
    }

    static GameObject BuildShop()
    {
        var root = Overlay("VillageShopPanel");
        var ui = root.AddComponent<VillageShopUI>();
        var window = Window(root.transform, "ShopWindow", new Vector2(1580, 940));
        Label(window, "Title", "商店", 52, 64, 38, 450, 70);
        Sprite(window, "BalanceIcon", remnant, 1356, 48, 46, 46);
        ui.balance = Label(window, "Balance", "4", 40, 1412, 44, 68, 54);
        ui.closeButton = IconButton(window, "Close", VillageServiceIcon.Symbol.Close, 1490, 42, 44, 44);
        Rule(window, "HeaderRule", 58, 122, 1464, 2);
        ui.weaponsTab = Button(window, "WeaponsTab", "武器", 28, 60, 146, 154, 52);
        ui.itemsTab = Button(window, "ItemsTab", "道具", 28, 238, 146, 154, 52);
        Icon(ui.weaponsTab.transform, "Icon", VillageServiceIcon.Symbol.Swords, 10, 10, 30, 30);
        Icon(ui.itemsTab.transform, "Icon", VillageServiceIcon.Symbol.Bottle, 10, 10, 30, 30);
        Tab(ui.weaponsTab); Tab(ui.itemsTab);
        ui.weaponsUnderline = Image(window, "WeaponsUnderline", Ink, 60, 199, 154, 3);
        ui.itemsUnderline = Image(window, "ItemsUnderline", Ink, 238, 199, 154, 3);
        ui.cards = new VillageShopCard[6];
        for (int i = 0; i < ui.cards.Length; i++)
        {
            var face = Image(window, "Offer_" + i, new Color(1, .94f, .8f, .18f),
                66 + i % 3 * 284, 220 + i / 3 * 294, 264, 274);
            var card = face.gameObject.AddComponent<VillageShopCard>();
            face.raycastTarget = true;
            card.backing = face; card.button = face.gameObject.AddComponent<Button>();
            card.button.targetGraphic = face;
            SetButtonColors(card.button);
            card.frame = Frame(face.transform, "InkFrame", 0, false);
            Artwork(face.transform, 20, 15, 224, 184, out card.artwork, out card.fallback);
            card.title = Label(face.transform, "Name", "剑", 28, 16, 204, 232, 34, TextAlignmentOptions.Center);
            card.stock = Label(face.transform, "Stock", "×1", 20, 190, 8, 62, 28, TextAlignmentOptions.Right);
            card.price = Label(face.transform, "Price", "4", 26, 136, 239, 60, 30);
            Sprite(face.transform, "Currency", remnant, 100, 239, 28, 28);
            ui.cards[i] = card;
        }
        var paging = Rect("Paging", window, 290, 812, 340, 36);
        ui.previousButton = IconButton(paging, "Previous", VillageServiceIcon.Symbol.Left, 24, 0, 44, 36);
        ui.pageLabel = Label(paging, "Page", "1 / 2", 24, 94, 0, 140, 36, TextAlignmentOptions.Center);
        ui.nextButton = IconButton(paging, "Next", VillageServiceIcon.Symbol.Right, 258, 0, 44, 36);
        Rule(window, "VerticalRule", 948, 154, 2, 666, true);
        Artwork(window, 996, 154, 510, 376, out ui.artwork, out ui.fallback);
        ui.itemName = Label(window, "SelectedName", "剑", 42, 996, 548, 300, 56);
        ui.stock = Label(window, "SelectedStock", "库存 1", 24, 1326, 556, 180, 40, TextAlignmentOptions.Right);
        Rule(window, "DescriptionRule", 996, 610, 510, 2);
        ui.description = Label(window, "Description", "", 28, 996, 628, 510, 90, TextAlignmentOptions.TopLeft);
        ui.buyButton = Button(window, "Purchase", "", 30, 996, 738, 510, 72);
        Icon(ui.buyButton.transform, "Bag", VillageServiceIcon.Symbol.Bag, 95, 20, 30, 30);
        ui.buyLabel = Label(ui.buyButton.transform, "Action", "购买", 30, 138, 14, 170, 44, TextAlignmentOptions.Center);
        Sprite(ui.buyButton.transform, "Currency", remnant, 318, 20, 30, 30);
        ui.price = Label(ui.buyButton.transform, "Price", "4", 30, 358, 14, 75, 44);
        ui.message = Label(window, "PurchaseMessage", "", 21, 996, 818, 510, 34, TextAlignmentOptions.Center);
        ui.message.color = Muted;
        Rule(window, "FooterRule", 58, 856, 1464, 1);
        var footer = Label(window, "StockPolicy", "武器买后不补货 · 道具每日补货", 21, 64, 868, 1100, 28);
        footer.color = Muted;
        Label(window, "EscapeHint", "Esc 关闭", 21, 1312, 868, 194, 28, TextAlignmentOptions.Right).color = Muted;
        ui.offers = Offers();
        return root;
    }

    static VillageShopUI.Offer[] Offers()
    {
        string[] ids = { "sword", "axe", "hammer", "torch", "greatsword", "spear", "dual_swords", "sickle", "flail", "poleaxe", "flame_tongue", "heaven_fire" };
        string[] art = { "Sword", "Axe", "Hammer", "Torch", "Greatsword", "Spear", "DualSwords", "Sickle", "Flail", "Poleaxe", "FlameTongue", "HeavenFire" };
        string[] descriptions = {
            "轻巧的直剑，锋利的剑尖适合精准穿刺。",
            "厚重的斧刃横向挥砍，能同时劈向面前的敌人。",
            "沉实的铁锤以钝击砸碎敌人的护甲。",
            "摇曳的火焰灼烧敌人，留下持续燃烧的余烬。",
            "沉重的双手大剑，以强力突刺压垮敌人的防线。",
            "修长的矛尖直指要害，刺中弱点时更易击溃敌人。",
            "双刃交错连刺，以连续命中撕开敌人的防线。",
            "弯曲的镰刃连续收割，对破防的敌人尤为致命。",
            "铁链牵动沉重锤头，在连击中削弱敌人的力量。",
            "长柄将尖刃与斧刃合为一体，兼具突刺与横扫。",
            "赤热的剑锋吐出火舌，在刺击中重燃持剑者的意志。",
            "熔火在锤头裂隙中流淌，重击兼具破甲与灼烧之力。"
        };
        var offers = new List<VillageShopUI.Offer>();
        for (int i = 0; i < ids.Length; i++)
        {
            var sprite = Load<Sprite>("Assets/Sprite/Items/WeaponIllustrations/" + art[i] + ".png");
            offers.Add(new VillageShopUI.Offer { id = ids[i], artwork = sprite,
                artworkUv = ArtworkBounds(sprite), description = descriptions[i] });
        }
        offers.Add(new VillageShopUI.Offer { id = ItemTable.Bandage, consumable = true,
            description = "恢复 15 点生命，战内外均可使用，最多携带 1 件。" });
        offers.Add(new VillageShopUI.Offer { id = ItemTable.HolyWater, consumable = true,
            description = "清除全部普通负面状态，保留增益与原罪效果，最多携带 1 件。" });
        return offers.ToArray();
    }

    static Rect ArtworkBounds(Sprite sprite)
    {
        // Crop only the displayed UVs. Source art, imports and animation registration stay intact.
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)))) return new Rect(0,0,1,1);
            var pixels = texture.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = 0, maxY = 0;
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a >= 24)
                { minX = Math.Min(minX,x); minY = Math.Min(minY,y); maxX = Math.Max(maxX,x); maxY = Math.Max(maxY,y); }
            minX = Math.Max(0,minX-10); minY = Math.Max(0,minY-10);
            maxX = Math.Min(texture.width,maxX+11); maxY = Math.Min(texture.height,maxY+11);
            return new Rect((float)minX/texture.width, (float)minY/texture.height,
                (float)(maxX-minX)/texture.width, (float)(maxY-minY)/texture.height);
        }
        finally { Object.DestroyImmediate(texture); }
    }

    static GameObject BuildInn()
    {
        var root = Overlay("InnPanel");
        var ui = root.AddComponent<VillageInnUI>();
        var window = Window(root.transform, "InnWindow", new Vector2(780, 520));
        Icon(window, "Bed", VillageServiceIcon.Symbol.Bed, 364, 48, 52, 52);
        Label(window, "Title", "在此歇息一晚？", 40, 60, 130, 660, 64, TextAlignmentOptions.Center);
        ui.timeRange = Label(window, "TimeRange", "", 26, 40, 212, 700, 46, TextAlignmentOptions.Center);
        ui.timeRange.color = Muted;
        Icon(window, "HealthIcon", VillageServiceIcon.Symbol.Heart, 175, 298, 32, 32);
        ui.health = Label(window, "Health", "56 → 100", 28, 220, 292, 166, 48);
        Sprite(window, "LanternIcon", lantern, 434, 292, 38, 44);
        ui.lantern = Label(window, "Lantern", "1 → 2", 28, 488, 292, 136, 48);
        Rule(window, "ActionRule", 60, 376, 660, 2);
        ui.cancelButton = Button(window, "Btn_InnClose", "离开", 30, 64, 410, 300, 68);
        ui.confirmButton = Button(window, "Btn_InnConfirm", "歇息", 30, 416, 410, 300, 68);
        ui.confirmLabel = ui.confirmButton.GetComponentInChildren<TMP_Text>();
        Icon(ui.cancelButton.transform, "Close", VillageServiceIcon.Symbol.Close, 56, 21, 26, 26);
        Icon(ui.confirmButton.transform, "Confirm", VillageServiceIcon.Symbol.Check, 56, 21, 26, 26);
        ui.message = Label(window, "Message", "", 20, 60, 480, 660, 26, TextAlignmentOptions.Center);
        return root;
    }

    static GameObject BuildTransition(Transform canvas)
    {
        var root = Overlay("VillageRestTransition");
        root.transform.SetParent(canvas, false);
        var sorted = root.AddComponent<Canvas>();
        sorted.renderMode = RenderMode.ScreenSpaceOverlay;
        sorted.overrideSorting = true; sorted.sortingOrder = 200;
        root.AddComponent<GraphicRaycaster>();
        var serialized = new SerializedObject(sorted);
        serialized.FindProperty("m_RenderMode").intValue = (int)RenderMode.ScreenSpaceOverlay;
        serialized.FindProperty("m_OverrideSorting").boolValue = true;
        serialized.FindProperty("m_SortingOrder").intValue = 200;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var ui = root.AddComponent<VillageRestTransition>();
        ui.blackout = root.GetComponent<Image>(); ui.blackout.color = Color.clear;
        ui.blackout.canvasRenderer.cullTransparentMesh = false;
        var map = Load<GameObject>("Assets/Prefabs/MapPrefabs/WorldMapHUD.prefab");
        var original = map.GetComponentsInChildren<MapTimeDialView>(true).Single();
        var astrolabe = Object.Instantiate(original.gameObject, root.transform, false);
        astrolabe.name = "TimeAstrolabe";
        var mapView = astrolabe.GetComponent<MapTimeDialView>();
        ui.dial = mapView.dial; ui.periodLabel = mapView.periodLabel;
        Object.DestroyImmediate(mapView);
        ui.dialVisibility = astrolabe.AddComponent<CanvasGroup>();
        ui.dialVisibility.alpha = 0; ui.dialVisibility.blocksRaycasts = false;
        foreach (var graphic in astrolabe.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        return root;
    }

    static void StyleHotspot(GameObject target, string text, VillageServiceIcon.Symbol symbol, float verticalOffset)
    {
        if (target == null) return;
        var image = target.GetComponent<Image>();
        if (image == null) return;
        var targetRect = (RectTransform)target.transform;
        targetRect.anchoredPosition = new Vector2(targetRect.anchoredPosition.x, verticalOffset);
        image.sprite = paper; image.type = UnityEngine.UI.Image.Type.Simple; image.color = Color.white;
        var label = target.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text; label.color = Ink;
            label.rectTransform.offsetMin = new Vector2(42, 0);
            label.rectTransform.offsetMax = Vector2.zero;
        }
        var existing = target.transform.Find("ServiceIcon");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var icon = Icon(target.transform, "ServiceIcon", symbol, 22, 18, 32, 32);
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0,.5f);
        icon.rectTransform.anchoredPosition = new Vector2(22,16);
        EditorUtility.SetDirty(target);
    }

    static GameObject Save(GameObject root, string path)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        root.SetActive(false);
        try
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path)
                ?? throw new InvalidOperationException("Could not save " + path);
            var sorted = prefab.GetComponent<Canvas>();
            if (sorted != null)
            {
                // A detached prefab Canvas defaults to WorldSpace unless these native values are persisted.
                var data = new SerializedObject(sorted);
                data.FindProperty("m_RenderMode").intValue = (int)RenderMode.ScreenSpaceOverlay;
                data.FindProperty("m_OverrideSorting").boolValue = true;
                data.FindProperty("m_SortingOrder").intValue = 200;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SavePrefabAsset(prefab);
            }
            return prefab;
        }
        finally { Object.DestroyImmediate(root); }
    }

    static GameObject Overlay(string name)
    {
        var image = Image(null, name, new Color32(21, 25, 18, 36), 0, 0, 0, 0);
        Stretch(image.rectTransform); image.raycastTarget = true;
        return image.gameObject;
    }

    static RectTransform Window(Transform parent, string name, Vector2 size)
    {
        var image = Image(parent, name, Color.white, 0, 0, size.x, size.y);
        image.sprite = paper; image.raycastTarget = true;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = Vector2.zero;
        image.gameObject.AddComponent<VillageServiceWindow>();
        Frame(rect, "ParchmentInkFrame", 22, true);
        return rect;
    }

    static void Artwork(Transform parent, float x, float y, float width, float height,
        out RawImage art, out VillageServiceIcon fallback)
    {
        var slot = Rect("ArtworkSlot", parent, x, y, width, height);
        var raw = new GameObject("OriginalArtwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
        raw.transform.SetParent(slot, false);
        var rect = (RectTransform)raw.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        art = raw.GetComponent<RawImage>(); art.raycastTarget = false;
        raw.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fallback = Icon(slot, "ItemSign", VillageServiceIcon.Symbol.Bandage,
            (width-100)*.5f, (height-100)*.5f, 100, 100);
    }

    static Button Button(Transform parent, string name, string caption, float fontSize, float x, float y, float width, float height)
    {
        var image = Image(parent, name, new Color32(180, 147, 95, 45), x,y,width,height);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        SetButtonColors(button); Frame(image.transform, "ButtonFrame", 1, true);
        var text = Label(image.transform, "Label", caption, fontSize, 12, 3, width-24, height-6, TextAlignmentOptions.Center);
        text.enableWordWrapping = false;
        return button;
    }

    static void Tab(Button button)
    {
        button.GetComponent<Image>().color = Color.clear;
        Object.DestroyImmediate(button.transform.Find("ButtonFrame").gameObject);
        var text = button.GetComponentInChildren<TMP_Text>();
        text.rectTransform.anchoredPosition += Vector2.right * 15;
    }

    static void SetButtonColors(Button button)
    {
        var colors = button.colors;
        colors.highlightedColor = new Color32(210, 184, 137, 255);
        colors.selectedColor = new Color32(217, 190, 141, 255);
        colors.pressedColor = new Color32(156, 124, 79, 255);
        colors.disabledColor = new Color32(179, 169, 149, 130);
        colors.fadeDuration = .1f; button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
    }

    static Button IconButton(Transform parent, string name, VillageServiceIcon.Symbol symbol, float x, float y, float width, float height)
    {
        var image = Image(parent,name,new Color(1,1,1,0),x,y,width,height);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; SetButtonColors(button);
        Icon(image.transform,"Icon",symbol,(width-26)*.5f,(height-26)*.5f,26,26);
        return button;
    }

    static VillageServiceIcon Icon(Transform parent, string name, VillageServiceIcon.Symbol symbol, float x, float y, float width, float height)
    {
        var rect = Rect(name,parent,x,y,width,height);
        var icon = rect.gameObject.AddComponent<VillageServiceIcon>();
        icon.symbol = symbol; icon.color = Ink; icon.raycastTarget = false;
        return icon;
    }

    static void Sprite(Transform parent, string name, Sprite sprite, float x, float y, float width, float height)
    {
        var image = Image(parent,name,Color.white,x,y,width,height);
        image.sprite = sprite; image.preserveAspect = true;
    }

    static TMP_Text Label(Transform parent, string name, string text, float fontSize,
        float x, float y, float width, float height, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var rect = Rect(name,parent,x,y,width,height);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.fontSize = fontSize; label.color = Ink; label.text = text;
        label.alignment = alignment; label.raycastTarget = false; label.enableWordWrapping = true;
        return label;
    }

    static Image Image(Transform parent, string name, Color color, float x, float y, float width, float height)
    {
        var rect = Rect(name,parent,x,y,width,height);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return image;
    }

    static void Rule(Transform parent,string name,float x,float y,float width,float height,bool vertical=false)
    {
        var rect = Rect(name,parent,x,y,width,height);
        var rule = rect.gameObject.AddComponent<InventoryInkGraphic>(); rule.raycastTarget = false;
        rule.shape = vertical ? InventoryInkGraphic.Shape.VerticalRule : InventoryInkGraphic.Shape.Rule;
        rule.color = new Color(.45f,.34f,.2f,.45f); rule.stroke = 1.2f;
    }

    static InventoryInkGraphic Frame(Transform parent,string name,float inset,bool ornate)
    {
        var rect = Rect(name,parent,0,0,0,0); Stretch(rect);
        rect.offsetMin = Vector2.one*inset; rect.offsetMax = -Vector2.one*inset;
        var frame = rect.gameObject.AddComponent<InventoryInkGraphic>(); frame.raycastTarget = false;
        frame.color = new Color(.34f,.24f,.13f,.72f); frame.stroke = 1.3f;
        frame.shape = ornate ? InventoryInkGraphic.Shape.OrnateFrame : InventoryInkGraphic.Shape.Frame;
        return frame;
    }

    static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
    {
        var root = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer)); root.layer = 5;
        root.transform.SetParent(parent,false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height);
        return rect;
    }

    static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException("Missing village service asset: " + path);
    static void RequireCleanEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before authoring village services.");
        for (int i=0;i<SceneManager.sceneCount;i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before authoring village services.");
    }
}
