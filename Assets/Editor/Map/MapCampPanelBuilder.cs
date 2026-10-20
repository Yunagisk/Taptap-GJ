using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit prefab authoring only; importing this script never changes a scene.
public static class MapCampPanelBuilder
{
    public const string PrefabPath = "Assets/Prefabs/UI/MapCampPanel.prefab";
    const string MapPrefabPath = "Assets/Prefabs/MapPrefabs/WorldMapPanel.prefab";
    static readonly Color Ink = new Color32(66, 49, 35, 255);
    static readonly Color Muted = new Color32(117, 91, 65, 255);

    [MenuItem("Tools/Map Whitebox/Build Camp Panel")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build camp UI outside Play mode.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Huiwen.asset");
        var paper = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/UI/TavernTemp/TavernPaperFrame-v1.png");
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/UI/MapArt2K/CampNode.png");
        if (font == null || paper == null || icon == null) throw new InvalidOperationException("Camp UI artwork or font is missing.");
        var root = new GameObject("MapCampPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.SetActive(false);
        try
        {
            Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color32(20, 16, 13, 190);
            var ui = root.AddComponent<MapCampUI>();
            var window = Image("Window", root.transform, Color.white);
            window.sprite = paper;
            window.type = UnityEngine.UI.Image.Type.Sliced;
            window.pixelsPerUnitMultiplier = 6;
            Box(window.rectTransform, Vector2.zero, new Vector2(760, 580));
            ui.window = window.rectTransform;

            Label("Title", window.transform, font, "火堆", 38, Ink, new Vector2(0, 217), new Vector2(620, 54));
            var rule = Image("Rule", window.transform, new Color32(143, 112, 77, 110));
            Box(rule.rectTransform, new Vector2(0, 177), new Vector2(610, 1));
            rule.raycastTarget = false;
            var fire = Image("CampEmblem", window.transform, new Color32(245, 225, 183, 255));
            fire.sprite = icon;
            fire.preserveAspect = true;
            fire.raycastTarget = false;
            Box(fire.rectTransform, new Vector2(0, 94), new Vector2(142, 142));
            ui.description = Label("Description", window.transform, font,
                "火堆噼啪作响，微光驱散了周遭的寒意。\n你卸下行囊，终于能在这里歇息片刻。",
                26, Ink, new Vector2(0, -31), new Vector2(640, 94));
            ui.description.lineSpacing = 10;
            ui.recoveryText = Label("Recovery", window.transform, font, "恢复 25 点生命  ·  休息耗时 1",
                22, Muted, new Vector2(0, -111), new Vector2(620, 38));
            var buttonImage = Image("Rest", window.transform, new Color32(192, 159, 111, 255));
            Box(buttonImage.rectTransform, new Vector2(0, -191), new Vector2(244, 62));
            var outline = buttonImage.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(102, 77, 45, 210);
            outline.effectDistance = new Vector2(1, -1);
            ui.restButton = buttonImage.gameObject.AddComponent<Button>();
            ui.restButton.targetGraphic = buttonImage;
            ui.restButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = ui.restButton.colors;
            colors.highlightedColor = new Color32(255, 235, 188, 255);
            colors.selectedColor = new Color32(255, 235, 188, 255);
            colors.pressedColor = new Color32(188, 149, 90, 255);
            ui.restButton.colors = colors;
            var restLabel = Label("Label", buttonImage.transform, font, "休息", 30, Ink, Vector2.zero, new Vector2(244, 62));
            Stretch(restLabel.rectTransform);
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Could not save camp UI prefab.");
            var mapRoot = PrefabUtility.LoadPrefabContents(MapPrefabPath);
            try
            {
                var controller = mapRoot.GetComponent<WorldMapController>();
                if (controller == null) throw new InvalidOperationException("WorldMapPanel controller is missing.");
                controller.campUIPrefab = prefab.GetComponent<MapCampUI>();
                PrefabUtility.SaveAsPrefabAsset(mapRoot, MapPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(mapRoot); }
            AssetDatabase.SaveAssets();
            Debug.Log("Camp panel authored and connected to WorldMapPanel.prefab.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    static Image Image(string name, Transform parent, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        var image = root.GetComponent<Image>(); image.color = color;
        return image;
    }

    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string content,
        float size, Color color, Vector2 position, Vector2 bounds)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        var text = root.GetComponent<TextMeshProUGUI>();
        text.font = font; text.text = content; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.enableWordWrapping = true; text.raycastTarget = false;
        Box(text.rectTransform, position, bounds);
        return text;
    }

    static void Box(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
