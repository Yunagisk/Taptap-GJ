using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MapPlayerPanelBuilder
{
    const string PortraitPath = "Assets/Sprite/UI/PlayerPortraitPlaceholder.png";
    static readonly Color Ink = new Color32(37, 40, 46, 255);
    static readonly Color Muted = new Color32(95, 100, 110, 255);

    [MenuItem("Tools/Map Whitebox/Update Player HUD And Inventory")]
    public static void UpdateCurrentScene()
    {
        if (EditorApplication.isPlaying) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MapSceneBuilder.ScenePath)
        {
            Debug.LogWarning("Open the copied map scene before updating its player UI.");
            return;
        }
        if (!EditorUtility.DisplayDialog("Update Player UI", "Replace the player HUD and inventory layout in this copied scene? Map and background transforms are preserved.", "Update", "Cancel")) return;
        Apply();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void Apply()
    {
        var map = Object.FindObjectOfType<WorldMapSceneView>();
        if (map == null) throw new System.InvalidOperationException("The native map scene is required.");
        var canvas = map.canvas;
        var rects = canvas.GetComponentsInChildren<RectTransform>(true);
        RectTransform Find(string name) => rects.First(rect => rect.name == name);
        var hud = Find("HUDPanel");
        var inventory = Find("InventoryPanel");
        var button = Find("Btn_Inventory").GetComponent<Button>();
        var close = Find("Btn_Close").GetComponent<Button>();
        var portraitSprite = CreatePortrait();
        var view = hud.GetComponent<MapPlayerPanelView>() ?? hud.gameObject.AddComponent<MapPlayerPanelView>();
        view.canvas = canvas;
        view.mapController = map.GetComponent<WorldMapController>();
        view.inventoryPanel = inventory.GetComponent<PanelToggler>();

        // Keep existing map and background transforms; only replace the player UI roots.
        button.transform.SetParent(hud, false);
        map.hpText.transform.SetParent(hud, false);
        close.transform.SetParent(inventory, false);
        map.sinText.transform.SetParent(inventory, false);
        foreach (string name in new[] { "PlayerHUD", "PlayerInventoryWindow", "InventoryBackdrop" })
        {
            var old = rects.FirstOrDefault(rect => rect != null && rect.name == name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var legacy = inventory.Find("LegacyInventoryContent");
        if (legacy == null) legacy = Rect("LegacyInventoryContent", inventory);
        foreach (Transform child in inventory.Cast<Transform>().ToArray())
            if (child != close.transform && child != map.sinText.transform && child != legacy)
                child.SetParent(legacy, false);
        legacy.gameObject.SetActive(false);

        view.hudRoot = Rect("PlayerHUD", hud);
        Box(view.hudRoot, new Vector2(0, 0), new Vector2(32, 32), new Vector2(384, 96), Vector2.zero);
        button.transform.SetParent(view.hudRoot, false);
        var portrait = button.GetComponent<Image>();
        portrait.sprite = portraitSprite;
        portrait.type = UnityEngine.UI.Image.Type.Simple;
        portrait.preserveAspect = true;
        portrait.color = Color.white;
        foreach (Transform child in button.transform) child.gameObject.SetActive(false);
        Box((RectTransform)button.transform, Vector2.zero, Vector2.zero, new Vector2(96, 96), Vector2.zero);
        view.portrait = portrait;
        var border = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        border.effectColor = new Color32(57, 62, 69, 255);
        border.effectDistance = new Vector2(2, -2);
        map.hpText.transform.SetParent(view.hudRoot, false);
        Style(map.hpText, map.font, 22, Ink);
        Box(map.hpText.rectTransform, Vector2.zero, new Vector2(116, 53), new Vector2(252, 35), Vector2.zero);
        var shadow = map.hpText.GetComponent<Shadow>() ?? map.hpText.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(255, 255, 255, 230);
        shadow.effectDistance = new Vector2(1, -1);
        view.hudHealthText = map.hpText;
        view.hudHealthFill = HealthBar("HUDHealthBar", view.hudRoot, new Vector2(116, 23), 252, true);

        var backdrop = Image("InventoryBackdrop", inventory, new Color32(13, 16, 22, 175));
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        backdrop.transform.SetAsFirstSibling();
        var window = Image("PlayerInventoryWindow", inventory, new Color32(248, 249, 251, 255));
        window.raycastTarget = true;
        Box(window.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(960, 640), Vector2.one * 0.5f);
        view.inventoryWindow = window.rectTransform;
        Label("InventoryTitle", window.transform, map.font, "背包", new Vector2(36, -26), new Vector2(220, 44), 30);
        Line(window.transform, new Vector2(36, -88), new Vector2(888, 2));

        close.transform.SetParent(window.transform, false);
        Box((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-24, -22), new Vector2(44, 44), new Vector2(1, 1));
        var closeImage = close.GetComponent<Image>();
        closeImage.sprite = null;
        closeImage.type = UnityEngine.UI.Image.Type.Simple;
        closeImage.color = new Color32(232, 234, 239, 255);
        var closeLabel = close.GetComponentInChildren<TMP_Text>(true);
        closeLabel.gameObject.SetActive(true);
        closeLabel.text = "×";
        Style(closeLabel, map.font, 30, Ink);
        closeLabel.alignment = TextAlignmentOptions.Center;
        Stretch(closeLabel.rectTransform);

        var profile = Image("InventoryPortrait", window.transform, Color.white);
        profile.sprite = portraitSprite;
        Box(profile.rectTransform, new Vector2(0, 1), new Vector2(36, -116), new Vector2(80, 80), new Vector2(0, 1));
        Label("PlayerName", window.transform, map.font, "旅人", new Vector2(136, -110), new Vector2(310, 40), 24);
        view.inventoryHealthText = Label("InventoryHealth", window.transform, map.font, "生命值  100 / 100", new Vector2(136, -150), new Vector2(310, 38), 20);
        view.inventoryHealthFill = HealthBar("InventoryHealthBar", window.transform, new Vector2(136, -194), 310, false);
        view.resourcesText = Label("PlayerResources", window.transform, map.font, "决心  1 / 3\n提灯  2 / 2", new Vector2(36, -220), new Vector2(410, 80), 20);
        view.resourcesText.lineSpacing = 6;
        Label("WeaponHeading", window.transform, map.font, "武器", new Vector2(36, -318), new Vector2(410, 40), 22);
        view.weaponNames = new TMP_Text[4];
        view.weaponStatuses = new TMP_Text[4];
        view.weaponSlots = new Image[4];
        string[] names = { "剑", "斧", "锤", "火把" };
        for (int i = 0; i < 4; i++)
        {
            var slot = Image("WeaponSlot_" + (i + 1), window.transform, i == 0 ? new Color32(220, 235, 225, 255) : new Color32(232, 234, 239, 255));
            Box(slot.rectTransform, new Vector2(0, 1), new Vector2(36 + i * 108, -366), new Vector2(96, 92), new Vector2(0, 1));
            var name = Label("WeaponName", slot.transform, map.font, names[i], new Vector2(6, -8), new Vector2(84, 44), 24);
            name.alignment = TextAlignmentOptions.Center;
            var status = Label("WeaponStatus", slot.transform, map.font, i == 0 ? "当前武器" : "", new Vector2(6, -58), new Vector2(84, 28), 15);
            status.alignment = TextAlignmentOptions.Center;
            status.color = new Color32(43, 97, 64, 255);
            view.weaponSlots[i] = slot;
            view.weaponNames[i] = name;
            view.weaponStatuses[i] = status;
        }

        Line(window.transform, new Vector2(486, -116), new Vector2(2, 342));
        Label("ItemHeading", window.transform, map.font, "道具", new Vector2(524, -116), new Vector2(360, 40), 22);
        var list = Image("ItemList", window.transform, new Color(1, 1, 1, 0.001f));
        list.raycastTarget = true;
        Box(list.rectTransform, new Vector2(0, 1), new Vector2(524, -168), new Vector2(394, 290), new Vector2(0, 1));
        var viewport = Rect("Viewport", list.transform);
        Stretch(viewport);
        viewport.offsetMax = new Vector2(-18, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        view.itemsText = Label("ItemContent", viewport, map.font, "暂无道具", Vector2.zero, new Vector2(376, 290), 20);
        view.itemsText.lineSpacing = 14;
        view.itemsText.rectTransform.anchorMin = new Vector2(0, 1);
        view.itemsText.rectTransform.anchorMax = Vector2.one;
        view.itemsText.rectTransform.sizeDelta = new Vector2(0, 0);
        view.itemsText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        view.itemsScroll = list.gameObject.AddComponent<ScrollRect>();
        view.itemsScroll.viewport = viewport;
        view.itemsScroll.content = view.itemsText.rectTransform;
        view.itemsScroll.horizontal = false;
        view.itemsScroll.movementType = ScrollRect.MovementType.Clamped;
        var track = Image("ItemScrollbar", list.transform, new Color32(225, 228, 234, 255));
        Box(track.rectTransform, new Vector2(1, 0), new Vector2(-4, 0), new Vector2(6, 290), new Vector2(1, 0));
        var handle = Image("Handle", track.transform, new Color32(113, 122, 135, 255));
        track.raycastTarget = handle.raycastTarget = true;
        Stretch(handle.rectTransform);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        view.itemsScroll.verticalScrollbar = scrollbar;
        view.itemsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        Line(window.transform, new Vector2(36, -492), new Vector2(888, 2));
        view.sinStatusText = Label("SinStatus", window.transform, map.font, "原罪  —", new Vector2(36, -524), new Vector2(410, 38), 20);
        view.virtueStatusText = Label("VirtueStatus", window.transform, map.font, "美德  —", new Vector2(36, -570), new Vector2(410, 38), 20);
        map.sinText.transform.SetParent(window.transform, false);
        Style(map.sinText, map.font, 20, Muted);
        Box(map.sinText.rectTransform, new Vector2(0, 1), new Vector2(524, -524), new Vector2(394, 38), new Vector2(0, 1));
        view.sinValueText = map.sinText;

        while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
        while (close.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(close.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, view.OpenInventory);
        UnityEventTools.AddPersistentListener(close.onClick, view.CloseInventory);
        view.CloseInventory();
        inventory.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static RectTransform HealthBar(string name, Transform parent, Vector2 position, float width, bool bottom)
    {
        var track = Image(name, parent, new Color32(57, 62, 69, 255));
        Box(track.rectTransform, bottom ? Vector2.zero : new Vector2(0, 1), position, new Vector2(width, 18), bottom ? Vector2.zero : new Vector2(0, 1));
        var fill = Image("Fill", track.transform, new Color32(176, 58, 68, 255));
        Stretch(fill.rectTransform);
        return fill.rectTransform;
    }

    static void Line(Transform parent, Vector2 position, Vector2 size)
    {
        var line = Image("Divider", parent, new Color32(215, 219, 226, 255));
        Box(line.rectTransform, new Vector2(0, 1), position, size, new Vector2(0, 1));
    }

    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        Style(text, font, fontSize, Ink);
        text.text = value;
        Box(text.rectTransform, new Vector2(0, 1), position, size, new Vector2(0, 1));
        return text;
    }

    static void Style(TMP_Text text, TMP_FontAsset font, float size, Color color)
    {
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = false;
        text.richText = false;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
    }

    static Image Image(string name, Transform parent, Color color)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Box(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static Sprite CreatePortrait()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
        if (existing != null) return existing;
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                Color32 color = new Color32(65, 74, 84, 255);
                if (y < 13 && x >= 5 && x <= 26) color = new Color32(171, 181, 189, 255);
                if (y >= 10 && y < 29 && x >= 8 && x <= 23) color = new Color32(39, 44, 52, 255);
                if (y >= 13 && y < 25 && x >= 11 && x <= 20) color = new Color32(214, 218, 218, 255);
                if (y == 21 && (x == 13 || x == 18)) color = new Color32(50, 55, 64, 255);
                if (y >= 9 && y <= 13 && x >= 9 && x <= 23) color = new Color32(151, 61, 74, 255);
                if (y >= 3 && y <= 10 && x >= 18 && x <= 21) color = new Color32(151, 61, 74, 255);
                texture.SetPixel(x, y, color);
            }
        texture.Apply();
        System.IO.File.WriteAllBytes(PortraitPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(PortraitPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(PortraitPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
    }
}
