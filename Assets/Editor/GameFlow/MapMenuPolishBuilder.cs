using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MapMenuPolishBuilder
{
    [MenuItem("Tools/Game Flow/Apply Map Menu Ink Style")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string mapPath = "Assets/Prefabs/MapPrefabs/WorldMapPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(mapPath);
        try
        {
            var view = root.GetComponent<WorldMapSceneView>();
            view.sinText = view.bossCountText = view.noticeText = null;
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)
                .Where(t => t.name == "Txt_Stage" || t.name == "Txt_Sin" || t.name == "Txt_BossCount").ToArray())
                Object.DestroyImmediate(label.gameObject);
            var viewport = root.GetComponentInChildren<WorldMapViewport>(true);
            var controls = viewport.transform.Find("MapNavigation");
            foreach (string name in new[] { "Overview", "Hint" })
            {
                var child = controls.Find(name);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
            var focus = controls.Find("Focus").GetComponent<Button>();
            focus.GetComponentInChildren<TMP_Text>(true).text = "定位";
            GameFlowMenuStyle.StyleButton(focus);
            ((RectTransform)controls).sizeDelta = new Vector2(136, 72);
            var serialized = new SerializedObject(viewport);
            serialized.FindProperty("overviewButton").objectReferenceValue = null;
            serialized.FindProperty("overviewLabel").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, mapPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        const string hudPath = "Assets/Prefabs/MapPrefabs/WorldMapHUD.prefab";
        root = PrefabUtility.LoadPrefabContents(hudPath);
        try
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true).Where(t => t.name == "Txt_BossCount").ToArray())
                Object.DestroyImmediate(label.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, hudPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var scene = EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath);
        var map = Object.FindObjectOfType<WorldMapSceneView>();
        map.sinText = map.bossCountText = map.noticeText = null;
        PrefabUtility.RecordPrefabInstancePropertyModifications(map);
        var flow = Object.FindObjectOfType<GameFlowController>();
        var menu = flow.transform.Find("Btn_GameMenu");
        if (menu != null) Object.DestroyImmediate(menu.gameObject);
        flow.menuPaper = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/UI/InventoryParchment2K/InventoryPaper.png");
        GameFlowMenuStyle.Apply(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
}
