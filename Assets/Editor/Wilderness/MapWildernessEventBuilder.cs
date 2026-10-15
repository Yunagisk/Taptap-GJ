using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Creates only the MainMap question-event modal and its scene references.</summary>
public static class MapWildernessEventBuilder
{
    public const string ScenePath = "Assets/Scenes/MainMap.unity";
    public const string PrefabPath = "Assets/Prefabs/UI/MapWildernessEventPanel.prefab";
    public const string RepairedRoadPrefabPath = "Assets/Prefabs/UI/WildernessRepairedRoad.prefab";
    public const string CatalogPath = "Assets/Data/Wilderness/WildernessEvents.json";
    static readonly Color Ink = new Color32(58, 47, 39, 255);
    static readonly Color Muted = new Color32(107, 87, 67, 255);

    [MenuItem("Tools/Wilderness/Build Map Event Panel")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building the map event panel.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForBuild = !scene.IsValid() || !scene.isLoaded;
        if (!openedForBuild && scene.isDirty)
            throw new InvalidOperationException("Save MainMap before rebuilding its question-event UI.");
        RequireSafeUntitledScenes();
        Scene previousActive = SceneManager.GetActiveScene();
        GameObject temporaryRoot = null;
        try
        {
            if (openedForBuild)
            {
                bool replaceBlank = SceneManager.sceneCount == 0 || SceneManager.sceneCount == 1
                    && IsEmptyUntitled(SceneManager.GetSceneAt(0));
                scene = EditorSceneManager.OpenScene(ScenePath,
                    replaceBlank ? OpenSceneMode.Single : OpenSceneMode.Additive);
            }
            SceneManager.SetActiveScene(scene);
            var map = Components<WorldMapController>(scene).Single();
            var view = Components<WorldMapSceneView>(scene).Single();
            var flow = Components<GameFlowController>(scene).Single(item => !item.isMainMenu);
            Require(view.canvas != null, "MainMap must have its existing world-map Canvas.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Huiwen.asset");
            var paper = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/UI/TavernTemp/TavernPaperFrame-v1.png");
            var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);
            Require(font != null && paper != null && catalog != null,
                "Huiwen, existing tavern paper and wilderness content are required.");
            EnsureFolder("Assets/Prefabs/UI");
            temporaryRoot = MakeRoadPrefab();
            SceneManager.MoveGameObjectToScene(temporaryRoot, scene);
            var roadPrefab = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, RepairedRoadPrefabPath);
            Require(roadPrefab != null, "Could not save the repaired-road Prefab.");
            Object.DestroyImmediate(temporaryRoot);
            temporaryRoot = null;
            var roadSource = roadPrefab.GetComponent<MapSceneRoad>();
            Require(roadSource != null && roadSource.from == null && roadSource.to == null
                && string.IsNullOrEmpty(roadSource.fromNodeId) && string.IsNullOrEmpty(roadSource.toNodeId),
                "Repaired-road Prefab must not store scene endpoints.");
            view.repairedRoadPrefab = roadSource;
            EditorUtility.SetDirty(view);
            if (PrefabUtility.IsPartOfPrefabInstance(view)) PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            temporaryRoot = MakePrefab(font, paper, catalog);
            SceneManager.MoveGameObjectToScene(temporaryRoot, scene);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, PrefabPath);
            Require(prefab != null, "Could not save the map question-event Prefab.");
            Object.DestroyImmediate(temporaryRoot);
            temporaryRoot = null;

            foreach (var old in Components<MapWildernessEventUI>(scene).ToArray())
                Object.DestroyImmediate(old.gameObject);
            var instanceRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instanceRoot.transform.SetParent(view.canvas.transform, false);
            instanceRoot.transform.SetAsLastSibling();
            Stretch((RectTransform)instanceRoot.transform);
            var instance = instanceRoot.GetComponent<MapWildernessEventUI>();
            instance.controller = map;
            instance.flow = flow;
            map.wildernessEventUI = instance;
            flow.wildernessEventUI = instance;
            instanceRoot.SetActive(false);
            EditorUtility.SetDirty(instance);
            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(flow);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instanceRoot.transform);
            if (PrefabUtility.IsPartOfPrefabInstance(map)) PrefabUtility.RecordPrefabInstancePropertyModifications(map);
            if (PrefabUtility.IsPartOfPrefabInstance(flow)) PrefabUtility.RecordPrefabInstancePropertyModifications(flow);
            Validate(scene, instance, map, flow, view.canvas, prefab);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save MainMap.");
            AssetDatabase.SaveAssets();
            Debug.Log("MainMap wilderness panel built: native Prefab; existing map UI and other scenes preserved.");
        }
        finally
        {
            if (temporaryRoot != null) Object.DestroyImmediate(temporaryRoot);
            if (openedForBuild && scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }

    static GameObject MakeRoadPrefab()
    {
        var root = new GameObject("WildernessRepairedRoad", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.layer = 5;
        Box((RectTransform)root.transform, Vector2.one * 0.5f, Vector2.zero,
            new Vector2(100, 3), Vector2.one * 0.5f);
        var image = root.GetComponent<Image>();
        image.color = new Color32(105, 87, 67, 190);
        image.raycastTarget = false;
        var road = root.AddComponent<MapSceneRoad>();
        road.fromNodeId = "";
        road.toNodeId = "";
        road.from = null;
        road.to = null;
        return root;
    }
    static GameObject MakePrefab(TMP_FontAsset font, Sprite paper, TextAsset catalog)
    {
        var root = new GameObject("MapWildernessEventPanel", typeof(RectTransform));
        root.SetActive(false);
        Stretch((RectTransform)root.transform);
        var shade = root.AddComponent<Image>();
        shade.color = new Color32(22, 20, 18, 190);
        shade.raycastTarget = true;
        var ui = root.AddComponent<MapWildernessEventUI>();
        ui.catalogAsset = catalog;
        var window = Picture("Window", root.transform, Color.white);
        window.sprite = paper;
        window.type = Image.Type.Sliced;
        window.pixelsPerUnitMultiplier = 6;
        window.raycastTarget = true;
        Box(window.rectTransform, Vector2.one * 0.5f, Vector2.zero,
            new Vector2(1100, 760), Vector2.one * 0.5f);
        ui.window = window.rectTransform;
        ui.eventTitle = Label("EventTitle", window.transform, font, "野外事件", 38, Ink,
            new Vector2(48, 28), new Vector2(1004, 58));
        ui.resourcesText = Label("Resources", window.transform, font, "", 25, Muted,
            new Vector2(48, 95), new Vector2(1004, 38));
        var separator = Picture("Separator", window.transform, new Color32(146, 126, 100, 135));
        Place(separator.rectTransform, new Vector2(48, 147), new Vector2(1004, 1));
        ui.eventDescription = Label("Description", window.transform, font, "", 27, Ink,
            new Vector2(48, 167), new Vector2(1004, 115));
        ui.eventMeta = Label("EventStatus", window.transform, font, "", 23, Muted,
            new Vector2(48, 286), new Vector2(1004, 35));
        ui.choiceButtons = new Button[2];
        ui.choiceLabels = new TMP_Text[2];
        for (int i = 0; i < 2; i++)
        {
            ui.choiceButtons[i] = MakeButton("Choice" + (i + 1), window.transform, font, "",
                new Vector2(48, 343 + i * 121), new Vector2(1004, 107),
                new Color32(223, 208, 183, 255), 25);
            ui.choiceLabels[i] = ui.choiceButtons[i].GetComponentInChildren<TMP_Text>(true);
            ui.choiceLabels[i].alignment = TextAlignmentOptions.MidlineLeft;
            ui.choiceLabels[i].margin = new Vector4(20, 10, 20, 10);
            ui.choiceLabels[i].fontSizeMin = 21;
        }
        ui.resultScroll = MakeResults(window.transform, font, out ui.resultText);
        Label("TextEffectsHint", window.transform, font, "攻击、受击与道具效果以文字记录。", 21, Muted,
            new Vector2(48, 608), new Vector2(1004, 32));
        ui.victoryButton = MakeButton("EncounterVictory", window.transform, font, "模拟胜利",
            new Vector2(48, 658), new Vector2(270, 58), new Color32(201, 192, 156, 255), 26);
        ui.defeatButton = MakeButton("EncounterDefeat", window.transform, font, "模拟失败",
            new Vector2(340, 658), new Vector2(270, 58), new Color32(222, 194, 177, 255), 26);
        ui.closeButton = MakeButton("CloseEvent", window.transform, font, "离开此处",
            new Vector2(802, 658), new Vector2(250, 58), new Color32(192, 165, 124, 255), 27);
        ui.closeLabel = ui.closeButton.GetComponentInChildren<TMP_Text>(true);
        ui.victoryButton.gameObject.SetActive(false);
        ui.defeatButton.gameObject.SetActive(false);
        ui.resultScroll.gameObject.SetActive(false);
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        return root;
    }

    static ScrollRect MakeResults(Transform parent, TMP_FontAsset font, out TMP_Text text)
    {
        var root = new GameObject("ResultScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform;
        Place(rect, new Vector2(48, 331), new Vector2(1004, 269));
        root.GetComponent<Image>().color = new Color32(247, 236, 217, 75);
        root.GetComponent<Image>().raycastTarget = true;
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        Stretch((RectTransform)viewport.transform);
        ((RectTransform)viewport.transform).offsetMin = new Vector2(16, 10);
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-16, -10);
        text = Label("ResultText", viewport.transform, font, "", 24, Ink, Vector2.zero, new Vector2(972, 0));
        var content = text.rectTransform;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        text.overflowMode = TextOverflowModes.Overflow;
        text.lineSpacing = 8;
        var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 26;
        return scroll;
    }

    static void Validate(Scene scene, MapWildernessEventUI ui, WorldMapController map,
        GameFlowController flow, Canvas canvas, GameObject prefab)
    {
        Require(Components<MapWildernessEventUI>(scene).Length == 1, "MainMap must have exactly one question-event modal.");
        Require(!ui.gameObject.activeSelf && ui.transform.parent == canvas.transform,
            "The modal must start hidden directly under the existing map Canvas.");
        Require(map.wildernessEventUI == ui && flow.wildernessEventUI == ui && ui.controller == map
            && ui.flow == flow && ui.catalogAsset != null, "Question-event references are incomplete.");
        Require(PrefabUtility.GetCorrespondingObjectFromSource(ui.gameObject) == prefab,
            "Question-event UI must be a native Prefab instance.");
        var assetUI = prefab.GetComponent<MapWildernessEventUI>();
        Require(assetUI.controller == null && assetUI.flow == null, "Prefab must not store scene references.");
        Require(ui.choiceButtons.Length == 2 && ui.choiceLabels.Length == 2 && ui.resultScroll != null
            && ui.resultText != null && ui.closeButton != null && ui.victoryButton != null
            && ui.defeatButton != null, "Question-event UI controls are incomplete.");
        foreach (var button in ui.GetComponentsInChildren<Button>(true))
            Require(button.onClick.GetPersistentEventCount() == 0, "Question-event buttons must use runtime listeners only.");
        foreach (var behaviour in ui.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Require(behaviour != null, "Question-event UI has a missing script.");
            var serialized = new SerializedObject(behaviour);
            var property = serialized.GetIterator();
            while (property.NextVisible(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                    Require(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                        "Question-event UI has a missing reference: " + property.propertyPath);
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
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !string.IsNullOrEmpty(scene.path)) continue;
            if (SceneManager.sceneCount == 1 && IsEmptyUntitled(scene)) continue;
            throw new InvalidOperationException("请先保存未命名场景，再生成问号事件窗口；生成工具会保留其他场景内容。");
        }
    }
    static T[] Components<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
    static Button MakeButton(string name, Transform parent, TMP_FontAsset font, string text,
        Vector2 position, Vector2 size, Color color, float fontSize)
    {
        var image = Picture(name, parent, color);
        image.raycastTarget = true;
        Place(image.rectTransform, position, size);
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(103, 82, 56, 160);
        outline.effectDistance = new Vector2(1, -1);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color32(255, 244, 220, 255);
        colors.pressedColor = new Color32(190, 166, 128, 255);
        colors.selectedColor = new Color32(251, 238, 207, 255);
        button.colors = colors;
        var label = Label("Label", image.transform, font, text, fontSize, Ink, Vector2.zero, size);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.margin = new Vector4(12, 4, 12, 4);
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize - 3;
        label.fontSizeMax = fontSize;
        return button;
    }
    static Image Picture(string name, Transform parent, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        var image = root.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text,
        float fontSize, Color color, Vector2 position, Vector2 size)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        var label = root.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Truncate;
        Place(label.rectTransform, position, size);
        return label;
    }
    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        Box(rect, new Vector2(0, 1), new Vector2(position.x, -position.y), size, new Vector2(0, 1));
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
        rect.pivot = Vector2.one * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
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

