using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Generates only the independent preview scene and its native UI prefab.
public static class WildernessPreviewBuilder
{
    public const string ScenePath = "Assets/Scenes/WildernessPreview.unity";
    public const string PrefabPath = "Assets/Prefabs/UI/WildernessEventPanel.prefab";
    public const string CatalogPath = "Assets/Data/Wilderness/WildernessEvents.json";
    public const string TemplatePath = "Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.asset";
    static readonly Color Ink = new Color32(56, 45, 38, 255);
    static readonly Color Muted = new Color32(105, 83, 63, 255);
    static TMP_FontAsset font;
    static Sprite paper;

    [MenuItem("Tools/Wilderness/Build Preview")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building the wilderness preview.");
        var existing = SceneManager.GetSceneByPath(ScenePath);
        if (existing.IsValid() && existing.isDirty)
            throw new InvalidOperationException("Save the existing wilderness preview scene before rebuilding it.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = default(Scene);
        Scene previousActive = SceneManager.GetActiveScene();
        try
        {
            RequireSafeUntitledScenes();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Huiwen.asset");
            paper = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/UI/TavernTemp/TavernPaperFrame-v1.png");
            var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);
            var template = AssetDatabase.LoadAssetAtPath<WorldMapTemplate>(TemplatePath);
            Require(font != null && paper != null && catalog != null && template != null,
                "Wilderness preview needs Huiwen, tavern paper, its catalog, and the existing resource template.");
            WildernessEventRules.ValidateCatalog(JsonUtility.FromJson<WildernessEventCatalog>(catalog.text));
            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder("Assets/Scenes");
            bool replaceOwnScene = existing.IsValid() && SceneManager.sceneCount == 1;
            if (existing.IsValid() && !replaceOwnScene) EditorSceneManager.CloseScene(existing, true);
            bool replaceBlank = replaceOwnScene || SceneManager.sceneCount == 0
                || SceneManager.sceneCount == 1 && IsEmptyUntitled(SceneManager.GetSceneAt(0));
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceBlank ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root = Rect("WildernessEventPanel", null);
            Stretch(root);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var shade = root.gameObject.AddComponent<Image>();
            shade.color = new Color32(24, 22, 20, 255);
            root.gameObject.SetActive(false);
            var ui = root.gameObject.AddComponent<WildernessPreviewUI>();
            ui.enabled = false;
            ui.catalogAsset = catalog;
            ui.resourceTemplate = template;
            ui.stage = 1;
            ui.initialSeed = 73105;
            BuildPanel(root, ui);
            ui.enabled = true;
            root.gameObject.SetActive(true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Require(prefab != null, "Could not save WildernessEventPanel prefab.");
            Object.DestroyImmediate(root.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Require(instance != null, "Could not instantiate WildernessEventPanel prefab.");
            instance.name = "WildernessEventPanel";
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save WildernessPreview scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("Wilderness preview built: native UI prefab and independent scene; existing scenes and Build Settings unchanged.");
        }
        finally
        {
            // A restore failure must not replace the original build error in the editor log.
            try
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                // Restore saved paths only when it cannot discard another scene's unsaved work.
                if (setup.Length > 0 && setup.All(item => !string.IsNullOrEmpty(item.path))
                    && !Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(item => item.isDirty))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                else
                {
                    var active = setup.FirstOrDefault(item => item.isActive);
                    if (active != null && !string.IsNullOrEmpty(active.path))
                    {
                        var restored = SceneManager.GetSceneByPath(active.path);
                        if (restored.IsValid() && restored.isLoaded) SceneManager.SetActiveScene(restored);
                    }
                }
            }
            catch (Exception restoreError)
            {
                Debug.LogWarning("Wilderness preview could not restore the previous scene setup: " + restoreError.Message);
            }
        }
    }
    static bool IsEmptyUntitled(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && string.IsNullOrEmpty(scene.path)
            && scene.rootCount == 0 && !scene.isDirty;
    }

    static void RequireSafeUntitledScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var loaded = SceneManager.GetSceneAt(i);
            if (!loaded.isLoaded || !string.IsNullOrEmpty(loaded.path)) continue;
            if (SceneManager.sceneCount == 1 && IsEmptyUntitled(loaded)) continue;
            throw new InvalidOperationException("请先保存未命名场景，再生成野外预览；生成工具会保留你的场景内容。");
        }
    }
    static void BuildPanel(RectTransform root, WildernessPreviewUI ui)
    {
        var window = Rect("Window", root);
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(1840, 1020);
        window.anchoredPosition = Vector2.zero;
        Paper(window.gameObject, new Color32(222, 210, 188, 255), 6);
        Text("Title", window, new Vector2(40, 18), new Vector2(1720, 65), "野外 · 七处问号", 44, Ink);
        Text("Subtitle", window, new Vector2(42, 84), new Vector2(1720, 38),
            "沿相邻道路探索，事件只能选择一次；攻击、受击与道具效果暂以文字呈现。", 26, Muted);
        ui.resourcesText = Text("Resources", window, new Vector2(40, 132), new Vector2(1760, 43),
            "第 1 天 早晨    生命 65/100    罪恶 40/100    罪骸 0", 30, Ink);
        Text("Rules", window, new Vector2(42, 183), new Vector2(1750, 36),
            "独立关卡预览 · 每次移动耗时 1 · 村外每单位时间罪恶 +3 · 存读仅作用于此处", 24, Muted);
        Text("LocationsTitle", window, new Vector2(40, 237), new Vector2(340, 40), "探索地点", 32, Ink);
        ui.currentNodeText = Text("CurrentLocation", window, new Vector2(420, 237), new Vector2(900, 40), "当前位置：村庄入口", 27, Muted);
        for (int i = 0; i < 8; i++)
        {
            TMP_Text label;
            ui.nodeButtons[i] = Button("Location" + i, window, new Vector2(40, 288 + i * 57), new Vector2(340, 52),
                i == 0 ? "● 村庄入口" : "? 问号地点 " + i, 25, out label);
            ui.nodeLabels[i] = label;
        }
        ui.eventTitle = Text("EventTitle", window, new Vector2(420, 285), new Vector2(900, 48), "村庄入口", 37, Ink);
        ui.eventDescription = Text("Description", window, new Vector2(422, 344), new Vector2(895, 110),
            "从左侧选择相邻的问号地点，开始这次探索。", 27, Ink);
        ui.eventDescription.enableAutoSizing = true;
        ui.eventDescription.fontSizeMin = 22;
        ui.eventDescription.fontSizeMax = 27;
        ui.eventMeta = Text("EventMeta", window, new Vector2(422, 461), new Vector2(895, 47),
            "7 个问号 · 10 个模板 · 每关不重复", 23, Muted);
        for (int i = 0; i < 2; i++)
        {
            TMP_Text label;
            ui.choiceButtons[i] = Button("Choice" + i, window, new Vector2(420, 518 + i * 115), new Vector2(900, 105),
                "选择", 26, out label);
            label.enableAutoSizing = true;
            label.fontSizeMin = 22;
            label.fontSizeMax = 26;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(22, 10, 22, 10);
            ui.choiceLabels[i] = label;
            ui.choiceButtons[i].gameObject.SetActive(false);
        }
        TMP_Text unused;
        ui.leaveButton = Button("Leave", window, new Vector2(420, 755), new Vector2(260, 52), "离开 / 收起", 26, out unused);
        ui.victoryButton = Button("EncounterVictory", window, new Vector2(700, 755), new Vector2(300, 52), "遭遇：胜利", 26, out unused);
        ui.defeatButton = Button("EncounterDefeat", window, new Vector2(1020, 755), new Vector2(300, 52), "遭遇：失败", 26, out unused);
        ui.victoryButton.gameObject.SetActive(false);
        ui.defeatButton.gameObject.SetActive(false);
        Text("RoadTitle", window, new Vector2(1360, 237), new Vector2(430, 40), "道路与断桥", 32, Ink);
        ui.roadsText = Text("Roads", window, new Vector2(1362, 288), new Vector2(420, 519),
            "已开放道路\n村庄 — 地点 1\n村庄 — 地点 2\n\n断桥横路\n待探索", 25, Ink);
        ui.roadsText.enableAutoSizing = true;
        ui.roadsText.fontSizeMin = 22;
        ui.roadsText.fontSizeMax = 25;
        Text("HistoryTitle", window, new Vector2(42, 818), new Vector2(1720, 35), "最近的结算", 28, Ink);
        ui.historyText = Text("History", window, new Vector2(42, 856), new Vector2(1740, 73), "尚无事件结算记录。", 23, Muted);
        ui.historyText.enableAutoSizing = true;
        ui.historyText.fontSizeMin = 19;
        ui.historyText.fontSizeMax = 23;
        ui.messageText = Text("Message", window, new Vector2(42, 934), new Vector2(1740, 32), "选择相邻地点开始探索。", 23, Ink);
        Text("SeedLabel", window, new Vector2(42, 973), new Vector2(75, 36), "种子", 25, Muted);
        ui.seedInput = Input(window, new Vector2(118, 971), new Vector2(220, 40));
        ui.seedInput.text = ui.initialSeed.ToString();
        ui.regenerateButton = Button("Regenerate", window, new Vector2(362, 970), new Vector2(340, 42), "按种子重新生成", 25, out unused);
        ui.saveButton = Button("Save", window, new Vector2(725, 970), new Vector2(340, 42), "保存预览进度", 25, out unused);
        ui.loadButton = Button("Load", window, new Vector2(1088, 970), new Vector2(340, 42), "读取预览进度", 25, out unused);
        Text("SaveHint", window, new Vector2(1453, 973), new Vector2(340, 36), "与正式游戏存档分开", 23, Muted);
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        result.gameObject.layer = 5;
        if (parent != null) result.SetParent(parent, false);
        return result;
    }

    static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static Image Paper(GameObject target, Color color, float multiplier)
    {
        var image = target.AddComponent<Image>();
        image.sprite = paper;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier;
        image.color = color;
        return image;
    }

    static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color)
    {
        var rect = Rect(name, parent);
        Place(rect, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.text = value;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, out TMP_Text label)
    {
        var rect = Rect(name, parent);
        Place(rect, position, size);
        var image = Paper(rect.gameObject, new Color32(238, 225, 198, 255), 12);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 236, 192, 255);
        colors.selectedColor = new Color32(255, 236, 192, 255);
        colors.pressedColor = new Color32(185, 164, 124, 255);
        colors.disabledColor = new Color32(164, 160, 148, 255);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        label = Text("Label", rect, Vector2.zero, size, value, fontSize, Ink);
        Stretch((RectTransform)label.transform);
        label.alignment = TextAlignmentOptions.Center;
        label.margin = new Vector4(12, 4, 12, 4);
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize - 3;
        label.fontSizeMax = fontSize;
        return button;
    }

    static TMP_InputField Input(Transform parent, Vector2 position, Vector2 size)
    {
        var rect = Rect("SeedInput", parent);
        Place(rect, position, size);
        var image = Paper(rect.gameObject, new Color32(244, 232, 210, 255), 12);
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 11;
        var viewport = Rect("TextArea", rect);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(12, 3);
        viewport.offsetMax = new Vector2(-12, -3);
        viewport.gameObject.AddComponent<RectMask2D>();
        var label = Text("Text", viewport, Vector2.zero, size, "73105", 26, Ink);
        Stretch((RectTransform)label.transform);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        input.textViewport = viewport;
        input.textComponent = label;
        return input;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}




