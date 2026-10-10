using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MapSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/MainMap.unity";
    public const string NodePrefabPath = "Assets/Prefabs/Map/WorldMapNode.prefab";

    [MenuItem("Tools/Map Whitebox/Create From Original Scene")]
    public static void CreateFromOriginal()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<Object>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath);
            return;
        }
        Build(true);
    }

    public static void Build(bool showOriginalBackground)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(ScenePath) != null)
            throw new System.InvalidOperationException("The copied scene already exists; edit it instead of overwriting it.");
        if (!AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity", ScenePath))
            throw new System.InvalidOperationException("Could not copy SampleScene.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var canvas = Object.FindObjectOfType<Canvas>();
        var allRects = canvas.GetComponentsInChildren<RectTransform>(true);
        RectTransform Find(string name) => allRects.First(rect => rect.name == name);
        var worldPanel = Find("WorldMapPanel");
        var mainPanels = Find("MainPanels");
        var mapRoot = Find("MapRoot");
        var lineRoot = Find("LineRoot");
        var background = Find("Background").GetComponent<Image>();
        var hud = Find("HUDPanel");
        var oldController = worldPanel.GetComponent<MapController>();
        if (oldController != null) Object.DestroyImmediate(oldController);
        Stretch(mainPanels);
        Stretch(worldPanel);
        Stretch(hud);
        Center(mapRoot, Vector2.zero, new Vector2(1920, 1080));

        var referenceNodes = Rect("OriginalNodeLayout", worldPanel);
        Center(referenceNodes, Vector2.zero, new Vector2(1920, 1080));
        foreach (Transform child in mapRoot.Cast<Transform>().ToArray()) child.SetParent(referenceNodes, false);
        referenceNodes.gameObject.SetActive(false);
        lineRoot.SetParent(mapRoot, false);
        Center(lineRoot, Vector2.zero, new Vector2(1920, 1080));

        var baseImage = Image("MapSurface", worldPanel, new Color32(237, 239, 235, 255));
        Stretch(baseImage.rectTransform);
        baseImage.transform.SetAsFirstSibling();
        Stretch(background.rectTransform);
        background.preserveAspect = false;
        background.raycastTarget = false;
        background.enabled = showOriginalBackground;
        background.transform.SetSiblingIndex(1);
        mapRoot.SetAsLastSibling();

        var template = AssetDatabase.LoadAssetAtPath<WorldMapTemplate>("Assets/Data/Maps/CenteredThreeSinWhitebox.asset");
        var layout = template.Load();
        MapLayoutValidator.Validate(layout);
        var controller = worldPanel.gameObject.AddComponent<WorldMapController>();
        controller.template = template;
        var view = worldPanel.gameObject.AddComponent<WorldMapSceneView>();
        view.canvas = canvas;
        view.mapContent = mapRoot;
        view.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/SiYuanHeiTi-Regular/SourceHanSansSC-Regular-2 SDF.asset");
        view.originalBackground = background;
        view.showOriginalBackground = showOriginalBackground;

        var nodePrefab = CreateNodePrefab(view.font);
        var nodeObjects = new Dictionary<string, MapSceneNode>();
        foreach (var definition in layout.nodes)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(nodePrefab, mapRoot);
            obj.name = "Node_" + definition.id;
            var node = obj.GetComponent<MapSceneNode>();
            node.nodeId = definition.id;
            node.label.text = definition.displayName;
            Center((RectTransform)obj.transform, new Vector2(definition.x, definition.y), new Vector2(112, 64));
            nodeObjects.Add(definition.id, node);
        }
        foreach (var definition in layout.nodes)
        {
            foreach (string neighbor in definition.neighbors)
            {
                if (string.CompareOrdinal(definition.id, neighbor) >= 0) continue;
                var image = Image("Road_" + definition.id + "_" + neighbor, lineRoot, new Color32(90, 96, 86, 255));
                var road = image.gameObject.AddComponent<MapSceneRoad>();
                road.fromNodeId = definition.id;
                road.toNodeId = neighbor;
                road.from = (RectTransform)nodeObjects[definition.id].transform;
                road.to = (RectTransform)nodeObjects[neighbor].transform;
                road.SendMessage("LateUpdate");
            }
        }

        var hp = Find("Txt_Hp");
        var time = Find("Txt_Time");
        hp.SetParent(hud, false);
        time.SetParent(hud, false);
        view.hpText = Configure(hp.GetComponent<TMP_Text>(), new Vector2(-820, 480), new Vector2(210, 45));
        view.timeText = Configure(time.GetComponent<TMP_Text>(), new Vector2(-500, 480), new Vector2(310, 45));
        TopLeft(hp, new Vector2(35, -60));
        TopLeft(time, new Vector2(280, -60));
        view.sinText = Text("Txt_Sin", hud, view.font, "罪恶 40 / 100", new Vector2(-100, 480), new Vector2(300, 45));
        view.bossCountText = Text("Txt_BossCount", hud, view.font, "原罪 0 / 3", new Vector2(220, 480), new Vector2(220, 45));
        view.noticeText = Configure(Find("Txt_Stage").GetComponent<TMP_Text>(), new Vector2(0, 420), new Vector2(400, 45));
        view.noticeText.text = "懒惰已出现";
        Center(Find("Btn_Inventory"), new Vector2(825, 480), new Vector2(130, 55));
        TopRight(Find("Btn_Inventory"), new Vector2(-35, -60));

        var inventory = Find("InventoryPanel");
        Stretch(inventory);
        var inventoryGroup = inventory.GetComponent<CanvasGroup>();
        inventoryGroup.alpha = 0;
        inventoryGroup.interactable = inventoryGroup.blocksRaycasts = false;
        Center(Find("Btn_Close"), new Vector2(800, 440), new Vector2(170, 50));
        TopRight(Find("Btn_Close"), new Vector2(-35, -130));
        var inventoryBackground = inventory.Find("Bg") as RectTransform;
        if (inventoryBackground != null) Stretch(inventoryBackground);

        var panel = Image("NodeActionPanel", worldPanel, new Color32(249, 249, 245, 255));
        panel.raycastTarget = true;
        Center(panel.rectTransform, new Vector2(730, -230), new Vector2(350, 270));
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(1, 0.5f);
        panel.rectTransform.anchoredPosition = new Vector2(-220, -230);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(90, 96, 86, 255);
        outline.effectDistance = new Vector2(2, -2);
        view.nodePanel = panel.gameObject;
        view.nodeTitle = Text("NodeTitle", panel.transform, view.font, "节点", new Vector2(0, 90), new Vector2(300, 45));
        view.nodeTitle.fontSize = 24;
        view.nodeDescription = Text("NodeDescription", panel.transform, view.font, "处理中", new Vector2(0, 20), new Vector2(300, 85));
        var action = Image("CompleteNode", panel.transform, new Color32(54, 86, 66, 255));
        Center(action.rectTransform, new Vector2(0, -90), new Vector2(290, 55));
        action.raycastTarget = true;
        view.completeButton = action.gameObject.AddComponent<Button>();
        view.completeButton.targetGraphic = action;
        var actionLabel = Text("Label", action.transform, view.font, "完成节点", Vector2.zero, new Vector2(290, 55));
        actionLabel.alignment = TextAlignmentOptions.Center;
        actionLabel.color = Color.white;
        panel.gameObject.SetActive(false);
        hud.SetAsLastSibling();
        MapPlayerPanelBuilder.Apply();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Created copied map scene: " + ScenePath);
    }

    static GameObject CreateNodePrefab(TMP_FontAsset font)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(NodePrefabPath);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Map")) AssetDatabase.CreateFolder("Assets/Prefabs", "Map");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Btn_Node.prefab");
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(obj.GetComponent<NodeView>());
        obj.name = "WorldMapNode";
        var node = obj.AddComponent<MapSceneNode>();
        node.button = obj.GetComponent<Button>();
        node.label = obj.GetComponentInChildren<TMP_Text>();
        node.outline = obj.AddComponent<Outline>();
        node.outline.effectDistance = new Vector2(2, -2);
        node.label.font = font;
        node.label.fontSize = 20;
        node.label.enableAutoSizing = false;
        node.label.alignment = TextAlignmentOptions.Center;
        node.label.margin = new Vector4(5, 3, 5, 3);
        node.label.raycastTarget = false;
        Stretch(node.label.rectTransform);
        var colors = node.button.colors;
        colors.disabledColor = Color.white;
        node.button.colors = colors;
        var prefab = PrefabUtility.SaveAsPrefabAsset(obj, NodePrefabPath);
        Object.DestroyImmediate(obj);
        return prefab;
    }

    static TMP_Text Configure(TMP_Text text, Vector2 position, Vector2 size)
    {
        Center(text.rectTransform, position, size);
        text.fontSize = 22;
        text.color = new Color32(30, 33, 29, 255);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        return text;
    }

    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value, Vector2 position, Vector2 size)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        return Configure(text, position, size);
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

    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static void TopLeft(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = position;
    }

    static void TopRight(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = position;
    }
}
