using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Creates only the native village-entry modal and its MainMap references.</summary>
public static class VillageEntryConfirmationBuilder
{
    public const string ScenePath = "Assets/Scenes/MainMap.unity";
    public const string PrefabPath = "Assets/Prefabs/UI/VillageEntryConfirmation.prefab";
    const string PaperPath = "Assets/Sprite/UI/TavernTemp/TavernPaperFrame-v1.png";
    static readonly Color Ink = new Color32(58, 47, 39, 255);
    static readonly Color MutedInk = new Color32(110, 92, 74, 255);

    [MenuItem("Tools/Map Whitebox/Build Village Entry Confirmation")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build village entry confirmation outside Play Mode.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Scene previousActive = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForBuild = !scene.IsValid() || !scene.isLoaded;
        if (!openedForBuild && scene.isDirty)
            throw new InvalidOperationException("Save MainMap before rebuilding its village entry UI.");
        GameObject temporaryRoot = null;
        try
        {
            // Additive loading preserves every other open scene, including unsaved edits.
            if (openedForBuild) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var map = Components<WorldMapController>(scene).Single();
            var view = Components<WorldMapSceneView>(scene).Single();
            var flow = Components<GameFlowController>(scene).Single(item => !item.isMainMenu);
            if (view.canvas == null)
            {
                view.canvas = view.GetComponentInParent<Canvas>(true);
                EditorUtility.SetDirty(view);
                if (PrefabUtility.IsPartOfPrefabInstance(view))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            }
            Require(view.canvas != null, "MainMap must have its existing world map Canvas.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Huiwen.asset");
            var paper = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
            Require(font != null && paper != null, "Existing Huiwen font and tavern paper art are required.");

            temporaryRoot = MakePrefab(font, paper);
            SceneManager.MoveGameObjectToScene(temporaryRoot, scene);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, PrefabPath);
            Require(prefab != null, "Could not save the village entry confirmation Prefab.");
            Object.DestroyImmediate(temporaryRoot);
            temporaryRoot = null;

            // Only replace this feature's own old instance; all existing map UI is preserved.
            foreach (var old in Components<VillageEntryConfirmationUI>(scene).ToArray())
                Object.DestroyImmediate(old.gameObject);
            var instanceRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instanceRoot.transform.SetParent(view.canvas.transform, false);
            instanceRoot.transform.SetAsLastSibling();
            Stretch((RectTransform)instanceRoot.transform);
            var instance = instanceRoot.GetComponent<VillageEntryConfirmationUI>();
            instance.controller = map;
            instance.flow = flow;
            map.villageEntryConfirmation = instance;
            flow.villageEntryConfirmation = instance;
            instanceRoot.SetActive(false);
            EditorUtility.SetDirty(instance);
            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(flow);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            if (PrefabUtility.IsPartOfPrefabInstance(map))
                PrefabUtility.RecordPrefabInstancePropertyModifications(map);
            if (PrefabUtility.IsPartOfPrefabInstance(flow))
                PrefabUtility.RecordPrefabInstancePropertyModifications(flow);
            Validate(scene, instance, map, flow, view.canvas, prefab);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save MainMap.");
            AssetDatabase.SaveAssets();
            Debug.Log("Village entry confirmation built: " + ScenePath + "; " + PrefabPath);
        }
        finally
        {
            if (temporaryRoot != null) Object.DestroyImmediate(temporaryRoot);
            if (openedForBuild && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
            // Closing just the scene opened here restores the captured scene setup without
            // reloading any other scene, so an unsaved Village or Combat scene is untouched.
            if (previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);
            else if (setup.Length == 0 && SceneManager.sceneCount == 0)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static GameObject MakePrefab(TMP_FontAsset font, Sprite paper)
    {
        var root = new GameObject("VillageEntryConfirmation", typeof(RectTransform));
        Stretch((RectTransform)root.transform);
        var backdrop = root.AddComponent<Image>();
        backdrop.color = new Color32(22, 20, 18, 178);
        backdrop.raycastTarget = true;
        var ui = root.AddComponent<VillageEntryConfirmationUI>();
        var window = Image("Window", root.transform, Color.white);
        window.sprite = paper;
        window.type = UnityEngine.UI.Image.Type.Sliced;
        window.pixelsPerUnitMultiplier = 6;
        window.raycastTarget = true;
        Box(window.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(640, 320), Vector2.one * 0.5f);
        ui.window = window.rectTransform;

        var title = Label("Title", window.transform, font, "进入村庄", 34, Ink);
        Box(title.rectTransform, new Vector2(0, 1), new Vector2(48, -34), new Vector2(544, 50), new Vector2(0, 1));
        var separator = Image("Separator", window.transform, new Color32(146, 126, 100, 135));
        Box(separator.rectTransform, new Vector2(0, 1), new Vector2(48, -97), new Vector2(544, 1), new Vector2(0, 1));

        ui.messageText = Label("Message", window.transform, font,
            "是否进入村庄？", 28, MutedInk);
        ui.messageText.lineSpacing = 12;
        Box(ui.messageText.rectTransform, new Vector2(0, 1), new Vector2(48, -113),
            new Vector2(544, 91), new Vector2(0, 1));
        ui.cancelButton = MakeButton("Cancel", window.transform, font, "取消",
            new Vector2(48, 35), new Color32(225, 214, 191, 255));
        ui.confirmButton = MakeButton("Confirm", window.transform, font, "进入村庄",
            new Vector2(344, 35), new Color32(193, 168, 127, 255));
        LinkNavigation(ui.cancelButton, ui.confirmButton);
        LinkNavigation(ui.confirmButton, ui.cancelButton);
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = 5;
        root.SetActive(false);
        return root;
    }

    static void Validate(Scene scene, VillageEntryConfirmationUI ui, WorldMapController map,
        GameFlowController flow, Canvas canvas, GameObject prefab)
    {
        Require(Components<VillageEntryConfirmationUI>(scene).Count() == 1, "MainMap must have exactly one village entry modal.");
        Require(!ui.gameObject.activeSelf && ui.transform.parent == canvas.transform, "The modal must start hidden under the map Canvas.");
        Require(map.villageEntryConfirmation == ui && flow.villageEntryConfirmation == ui
            && ui.controller == map && ui.flow == flow, "Village entry references are incomplete.");
        Require(PrefabUtility.GetCorrespondingObjectFromSource(ui.gameObject) == prefab,
            "The modal must be a native Prefab instance.");
        var assetUI = prefab.GetComponent<VillageEntryConfirmationUI>();
        Require(assetUI.controller == null && assetUI.flow == null, "Prefab must not store scene controller references.");
        Require(ui.confirmButton != null && ui.cancelButton != null && ui.window != null
            && ui.messageText != null, "The modal UI is incomplete.");
        foreach (var button in new[] { ui.confirmButton, ui.cancelButton })
            Require(button.onClick.GetPersistentEventCount() == 0, "Buttons must use runtime listeners only.");
        foreach (var behaviour in ui.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Require(behaviour != null, "The modal contains a missing script.");
            var serialized = new SerializedObject(behaviour);
            var property = serialized.GetIterator();
            while (property.NextVisible(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                    Require(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                        "The modal has a missing reference: " + property.propertyPath);
        }
    }

    static T[] Components<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }

    static Button MakeButton(string name, Transform parent, TMP_FontAsset font, string text,
        Vector2 position, Color background)
    {
        var image = Image(name, parent, background);
        Box(image.rectTransform, Vector2.zero, position, new Vector2(248, 57), Vector2.zero);
        image.raycastTarget = true;
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(103, 82, 56, 180);
        outline.effectDistance = new Vector2(1, -1);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color32(255, 244, 220, 255);
        colors.pressedColor = new Color32(190, 166, 128, 255);
        colors.selectedColor = new Color32(251, 238, 207, 255);
        button.colors = colors;
        var label = Label("Label", image.transform, font, text, 27, Ink);
        label.alignment = TextAlignmentOptions.Center;
        Stretch(label.rectTransform);
        return button;
    }

    static void LinkNavigation(Button button, Button other)
    {
        var nav = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnLeft = other, selectOnRight = other, selectOnUp = other, selectOnDown = other };
        button.navigation = nav;
    }

    static Image Image(string name, Transform parent, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        var image = root.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text,
        float size, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        var label = root.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Truncate;
        return label;
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

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
