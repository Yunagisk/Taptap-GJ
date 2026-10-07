using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GameFlowSceneBuilder
{
    public const string MenuPath = "Assets/Scenes/MainMenu.unity";
    static readonly Color Ink = new Color32(38, 42, 48, 255);
    static readonly Color Red = new Color32(151, 51, 64, 255);
    static readonly Color Paper = new Color32(248, 249, 251, 255);
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/SiYuanHeiTi-Regular/SourceHanSansSC-Regular-2 SDF.asset");

    [MenuItem("Tools/Game Flow/Open Main Menu")]
    public static void OpenMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<Object>(MenuPath) == null) Build();
        else EditorSceneManager.OpenScene(MenuPath);
    }

    public static void Build()
    {
        BuildMenu();
        EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath);
        AddMapFlow();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        var previous = EditorBuildSettings.scenes.Where(scene => scene.path != MenuPath && scene.path != MapSceneBuilder.ScenePath);
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene(MenuPath, true),
            new EditorBuildSettingsScene(MapSceneBuilder.ScenePath, true)
        }.Concat(previous).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MenuPath);
    }

    static void BuildMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(MenuPath) != null)
            throw new System.InvalidOperationException("The main menu already exists; edit it instead of overwriting it.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = Ink;
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var root = Rect("GameFlowUI", canvas.transform);
        Stretch(root);
        var flow = root.gameObject.AddComponent<GameFlowController>();
        flow.isMainMenu = true;
        flow.canvas = canvas;
        flow.font = Font;
        flow.template = AssetDatabase.LoadAssetAtPath<WorldMapTemplate>("Assets/Data/Maps/CenteredThreeSinWhitebox.asset");
        Background(root);
        var main = Rect("MainMenuPage", root);
        Stretch(main);
        flow.mainPage = main.gameObject;
        var band = Image("MenuBand", main, new Color32(25, 29, 36, 235));
        band.rectTransform.anchorMin = Vector2.zero;
        band.rectTransform.anchorMax = new Vector2(0.44f, 1);
        band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;
        var content = Rect("MenuContent", main);
        Box(content, new Vector2(0, 0.5f), new Vector2(125, 0), new Vector2(620, 760), new Vector2(0, 0.5f));
        Label("GameTitle", content, "其实七宗罪", new Vector2(0, -20), new Vector2(620, 112), 64, Color.white);
        MenuButton("Btn_NewGame", content, "新游戏", -230, flow.BeginNewGame);
        flow.continueButton = MenuButton("Btn_Continue", content, "继续", -330, null);
        flow.continueButton.interactable = false;
        flow.continueButton.GetComponentInChildren<TMP_Text>().color = new Color32(145, 151, 162, 255);
        Label("ContinueStatus", content, "暂无可用存档", new Vector2(18, -404), new Vector2(420, 40), 18, new Color32(181, 185, 193, 255));
        MenuButton("Btn_Settings", content, "设置", -460, flow.OpenSettings);
        MenuButton("Btn_Quit", content, "退出", -560, flow.AskQuit);

        var weaponPage = Image("WeaponSelectionPage", root, new Color32(25, 29, 36, 240));
        Stretch(weaponPage.rectTransform);
        flow.weaponPage = weaponPage.gameObject;
        var choices = Rect("WeaponContent", weaponPage.transform);
        Box(choices, Vector2.one * 0.5f, Vector2.zero, new Vector2(1040, 640), Vector2.one * 0.5f);
        Label("WeaponTitle", choices, "初始武器", new Vector2(0, -10), new Vector2(900, 80), 40, Color.white);
        flow.weaponOptions = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            string id = NewRunFactory.StartingWeapons[i];
            var button = Button("Choose_" + id, choices, "", new Vector2(i * 264, -140), new Vector2(248, 240), null);
            UnityEventTools.AddStringPersistentListener(button.onClick, flow.SelectWeapon, id);
            flow.weaponOptions[i] = button.GetComponent<Image>();
            var icon = Image("WeaponIcon", button.transform, Color.white);
            icon.sprite = WeaponSprite(id);
            Box(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(116, 116), new Vector2(0.5f, 1));
            Label("WeaponLabel", button.transform, NewRunFactory.WeaponName(id), new Vector2(16, -160), new Vector2(216, 52), 30, Ink).alignment = TextAlignmentOptions.Center;
        }
        flow.selectedWeaponText = Label("SelectedWeapon", choices, "", new Vector2(0, -418), new Vector2(600, 52), 24, Color.white);
        Button("Btn_WeaponBack", choices, "返回", new Vector2(0, -520), new Vector2(200, 64), flow.BackToTitle);
        flow.startButton = Button("Btn_StartRun", choices, "进入村庄", new Vector2(760, -520), new Vector2(280, 64), flow.StartSelectedGame, true);
        flow.startButton.interactable = false;
        flow.errorText = Label("NewGameError", choices, "", new Vector2(0, -592), new Vector2(1040, 48), 18, new Color32(245, 155, 160, 255));
        SharedOverlays(flow, root, new[] { content, choices });
        flow.weaponPage.SetActive(false);
        EditorSceneManager.SaveScene(scene, MenuPath);
    }

    static void AddMapFlow()
    {
        var view = Object.FindObjectOfType<WorldMapSceneView>();
        var controller = view.GetComponent<WorldMapController>();
        for (int i = controller.onVillageEntered.GetPersistentEventCount() - 1; i >= 0; i--)
            if (controller.onVillageEntered.GetPersistentTarget(i) is GameFlowController)
                UnityEventTools.RemovePersistentListener(controller.onVillageEntered, i);
        var old = view.canvas.transform.Find("GameFlowUI");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = Rect("GameFlowUI", view.canvas.transform);
        Stretch(root);
        root.SetAsLastSibling();
        var flow = root.gameObject.AddComponent<GameFlowController>();
        flow.canvas = view.canvas;
        flow.font = view.font;
        flow.template = controller.template;
        flow.worldMapGroup = controller.GetComponent<CanvasGroup>();
        flow.playerPanel = view.canvas.GetComponentInChildren<MapPlayerPanelView>(true);
        flow.playerPanel.manageEscape = false;
        var village = Rect("VillageScreen", root);
        Stretch(village);
        flow.villagePage = village.gameObject;
        Background(village);
        var band = Image("VillageBand", village, new Color32(25, 29, 36, 235));
        band.rectTransform.anchorMin = Vector2.zero;
        band.rectTransform.anchorMax = new Vector2(0.44f, 1);
        band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;
        var content = Rect("VillageContent", village);
        Box(content, new Vector2(0, 0.5f), new Vector2(125, 0), new Vector2(620, 680), new Vector2(0, 0.5f));
        Label("VillageTitle", content, "村庄", new Vector2(0, 0), new Vector2(600, 100), 56, Color.white);
        flow.villageWeaponText = Label("VillageWeapon", content, "当前武器：剑", new Vector2(18, -140), new Vector2(550, 48), 24, new Color32(209, 214, 224, 255));
        MenuButton("Btn_Depart", content, "出发", -260, flow.ShowMap);
        MenuButton("Btn_VillageInventory", content, "背包", -360, flow.OpenInventory);
        MenuButton("Btn_VillageReturnTitle", content, "返回主菜单", -460, flow.AskReturnToTitle);
        var menu = Button("Btn_GameMenu", root, "菜单", Vector2.zero, new Vector2(100, 48), flow.OpenPause);
        Box((RectTransform)menu.transform, Vector2.one, new Vector2(-32, -30), new Vector2(100, 48), Vector2.one);
        SharedOverlays(flow, root, new[] { content });
        UnityEventTools.AddPersistentListener(controller.onVillageEntered, flow.ShowVillage);
        flow.villagePage.SetActive(false);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void SharedOverlays(GameFlowController flow, RectTransform root, RectTransform[] initialPanels)
    {
        var fitted = new List<RectTransform>(initialPanels);
        var settings = Modal("SettingsOverlay", root, new Vector2(660, 370), out var settingsWindow);
        flow.settingsOverlay = settings;
        fitted.Add(settingsWindow);
        Label("SettingsTitle", settingsWindow, "设置", new Vector2(36, -24), new Vector2(500, 60), 30, Ink);
        CloseButton(settingsWindow, "Btn_CloseSettings", flow.CloseSettings);
        Label("VolumeLabel", settingsWindow, "主音量", new Vector2(36, -115), new Vector2(140, 44), 22, Ink);
        var sliderRoot = Image("MasterVolume", settingsWindow, new Color(1, 1, 1, 0.01f));
        sliderRoot.raycastTarget = true;
        Box(sliderRoot.rectTransform, new Vector2(0, 1), new Vector2(180, -118), new Vector2(300, 36), new Vector2(0, 1));
        var track = Image("Track", sliderRoot.transform, new Color32(211, 215, 223, 255));
        Box(track.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(284, 10), Vector2.one * 0.5f);
        var fillArea = Rect("FillArea", sliderRoot.transform);
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(8, 13);
        fillArea.offsetMax = new Vector2(-8, -13);
        var fill = Image("Fill", fillArea, Red);
        Stretch(fill.rectTransform);
        var handleArea = Rect("HandleArea", sliderRoot.transform);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(8, 0);
        handleArea.offsetMax = new Vector2(-8, 0);
        var handle = Image("Handle", handleArea, Ink);
        Box(handle.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(16, 28), Vector2.one * 0.5f);
        handle.raycastTarget = true;
        flow.volumeSlider = sliderRoot.gameObject.AddComponent<Slider>();
        flow.volumeSlider.fillRect = fill.rectTransform;
        flow.volumeSlider.handleRect = handle.rectTransform;
        flow.volumeSlider.targetGraphic = handle;
        flow.volumeSlider.value = 1;
        UnityEventTools.AddPersistentListener(flow.volumeSlider.onValueChanged, flow.SetVolume);
        flow.volumeText = Label("VolumeValue", settingsWindow, "100%", new Vector2(510, -115), new Vector2(110, 44), 22, Ink);
        var toggleRoot = Image("Fullscreen", settingsWindow, new Color(1, 1, 1, 0.01f));
        toggleRoot.raycastTarget = true;
        Box(toggleRoot.rectTransform, new Vector2(0, 1), new Vector2(36, -212), new Vector2(400, 44), new Vector2(0, 1));
        var box = Image("Box", toggleRoot.transform, new Color32(211, 215, 223, 255));
        Box(box.rectTransform, new Vector2(0, 1), new Vector2(0, -6), new Vector2(32, 32), new Vector2(0, 1));
        var check = Image("Checkmark", box.transform, Red);
        Box(check.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(20, 20), Vector2.one * 0.5f);
        Label("FullscreenLabel", toggleRoot.transform, "全屏", new Vector2(52, 0), new Vector2(300, 44), 22, Ink);
        flow.fullscreenToggle = toggleRoot.gameObject.AddComponent<Toggle>();
        flow.fullscreenToggle.targetGraphic = box;
        flow.fullscreenToggle.graphic = check;
        UnityEventTools.AddPersistentListener(flow.fullscreenToggle.onValueChanged, flow.SetFullscreen);
        Button("Btn_SettingsDone", settingsWindow, "完成", new Vector2(434, -286), new Vector2(190, 54), flow.CloseSettings, true);

        var pause = Modal("PauseOverlay", root, new Vector2(520, 520), out var pauseWindow);
        flow.pauseOverlay = pause;
        fitted.Add(pauseWindow);
        Label("PauseTitle", pauseWindow, "菜单", new Vector2(36, -24), new Vector2(400, 60), 30, Ink);
        Button("Btn_Resume", pauseWindow, "继续游戏", new Vector2(36, -118), new Vector2(448, 64), flow.ClosePause, true);
        Button("Btn_PauseSettings", pauseWindow, "设置", new Vector2(36, -208), new Vector2(448, 64), flow.OpenSettings);
        Button("Btn_ReturnTitle", pauseWindow, "返回主菜单", new Vector2(36, -298), new Vector2(448, 64), flow.AskReturnToTitle);
        Button("Btn_PauseQuit", pauseWindow, "退出游戏", new Vector2(36, -388), new Vector2(448, 64), flow.AskQuit);

        var confirmation = Modal("ConfirmOverlay", root, new Vector2(680, 280), out var confirmWindow);
        flow.confirmOverlay = confirmation;
        fitted.Add(confirmWindow);
        flow.confirmationText = Label("ConfirmationText", confirmWindow, "退出游戏？", new Vector2(36, -30), new Vector2(608, 100), 24, Ink);
        Button("Btn_ConfirmCancel", confirmWindow, "取消", new Vector2(36, -174), new Vector2(190, 64), flow.CancelConfirmation);
        Button("Btn_ConfirmAccept", confirmWindow, "确认", new Vector2(454, -174), new Vector2(190, 64), flow.AcceptConfirmation, true);
        var loading = Image("LoadingOverlay", root, new Color32(25, 29, 36, 255));
        Stretch(loading.rectTransform);
        loading.raycastTarget = true;
        var loadingLabel = Label("LoadingText", loading.transform, "正在加载", Vector2.zero, new Vector2(800, 90), 30, Color.white);
        Box(loadingLabel.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(800, 90), Vector2.one * 0.5f);
        loadingLabel.alignment = TextAlignmentOptions.Center;
        flow.loadingOverlay = loading.gameObject;
        flow.fittedPanels = fitted.ToArray();
        settings.SetActive(false);
        pause.SetActive(false);
        confirmation.SetActive(false);
        loading.gameObject.SetActive(false);
    }

    static GameObject Modal(string name, Transform parent, Vector2 size, out RectTransform window)
    {
        var backdrop = Image(name, parent, new Color32(13, 16, 22, 180));
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        var panel = Image("Window", backdrop.transform, Paper);
        panel.raycastTarget = true;
        Box(panel.rectTransform, Vector2.one * 0.5f, Vector2.zero, size, Vector2.one * 0.5f);
        window = panel.rectTransform;
        return backdrop.gameObject;
    }

    static void Background(Transform parent)
    {
        var background = Image("ScreenBackground", parent, Color.white);
        background.raycastTarget = true;
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/UI/Map.png");
        Stretch(background.rectTransform);
        var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1672f / 941f;
    }

    static Button MenuButton(string name, Transform parent, string value, float y, UnityAction action)
    {
        var button = Button(name, parent, value, new Vector2(0, y), new Vector2(500, 70), action);
        button.GetComponent<Image>().color = new Color(1, 1, 1, 0.07f);
        var label = button.GetComponentInChildren<TMP_Text>();
        label.color = Color.white;
        label.fontSize = 30;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.margin = new Vector4(18, 0, 0, 0);
        var colors = button.colors;
        colors.disabledColor = new Color32(110, 114, 123, 255);
        button.colors = colors;
        return button;
    }

    static void CloseButton(Transform parent, string name, UnityAction action)
    {
        var button = Button(name, parent, "×", Vector2.zero, new Vector2(44, 44), action);
        Box((RectTransform)button.transform, Vector2.one, new Vector2(-24, -24), new Vector2(44, 44), Vector2.one);
    }

    static Button Button(string name, Transform parent, string label, Vector2 position, Vector2 size, UnityAction action, bool primary = false)
    {
        var image = Image(name, parent, primary ? Red : new Color32(237, 239, 243, 255));
        image.raycastTarget = true;
        Box(image.rectTransform, new Vector2(0, 1), position, size, new Vector2(0, 1));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.fadeDuration = 0;
        button.colors = colors;
        if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
        var text = Label("Label", image.transform, label, Vector2.zero, size, 24, primary ? Color.white : Ink);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);
        return button;
    }

    static TMP_Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.richText = false;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        Box(text.rectTransform, new Vector2(0, 1), position, size, new Vector2(0, 1));
        return text;
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
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static Sprite WeaponSprite(string id)
    {
        string path = "Assets/Sprite/UI/Weapon_" + id + "_Placeholder.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                Color32 color = new Color32(0, 0, 0, 0);
                if (id == "sword" || id == "dual_swords")
                {
                    int offset = id == "dual_swords" ? 5 : 0;
                    if (x >= 14 - offset && x <= 17 - offset && y >= 12 && y < 29) color = new Color32(110, 123, 138, 255);
                    if (x >= 10 - offset && x <= 21 - offset && y >= 10 && y <= 12) color = new Color32(151, 51, 64, 255);
                    if (x >= 14 - offset && x <= 17 - offset && y >= 3 && y < 10) color = new Color32(56, 64, 74, 255);
                    if (id == "dual_swords")
                    {
                        if (x >= 21 && x <= 24 && y >= 12 && y < 29) color = new Color32(110, 123, 138, 255);
                        if (x >= 18 && x <= 27 && y >= 10 && y <= 12) color = new Color32(151, 51, 64, 255);
                        if (x >= 21 && x <= 24 && y >= 3 && y < 10) color = new Color32(56, 64, 74, 255);
                    }
                }
                else
                {
                    if (x >= 14 && x <= 17 && y >= 3 && y <= 27) color = new Color32(56, 64, 74, 255);
                    if (id == "axe" && x >= 6 && x <= 16 && y >= 17 && y <= 27) color = new Color32(110, 123, 138, 255);
                    if (id == "hammer" && x >= 6 && x <= 25 && y >= 21 && y <= 28) color = new Color32(110, 123, 138, 255);
                    if (x >= 13 && x <= 18 && y >= 10 && y <= 12) color = new Color32(151, 51, 64, 255);
                }
                texture.SetPixel(x, y, color);
            }
        texture.Apply();
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
