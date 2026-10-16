using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MapTopologyBuilder
{
    [MenuItem("Tools/Map Whitebox/Apply Map Layout")]
    public static void UpdateCurrentScene()
    {
        if (EditorApplication.isPlaying) return;
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MapSceneBuilder.ScenePath)
        {
            Debug.LogWarning("Open the copied map scene before applying the map layout.");
            return;
        }
        if (!EditorUtility.DisplayDialog("Apply Map Layout", "Update node positions and roads from the map template? Background and player UI are preserved.", "Apply", "Cancel")) return;
        Apply();
        if (stage == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void Apply()
    {
        var view = FindView();
        if (view == null) throw new InvalidOperationException("The native map scene is required.");
        if (PrefabStageUtility.GetCurrentPrefabStage() == null
            && WorldMapUiPrefabBuilder.Connected(view.gameObject, WorldMapUiPrefabBuilder.MapPrefabPath))
        {
            var root = PrefabUtility.LoadPrefabContents(WorldMapUiPrefabBuilder.MapPrefabPath);
            try
            {
                Apply(root.GetComponent<WorldMapSceneView>());
                PrefabUtility.SaveAsPrefabAsset(root, WorldMapUiPrefabBuilder.MapPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return;
        }
        Apply(view);
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
    }

    static void Apply(WorldMapSceneView view)
    {
        var layout = view.GetComponent<WorldMapController>().template.Load();
        if (view.visualLayout == null) view.visualLayout = view.GetComponent<WorldMapController>().template.visualLayout;
        MapLayoutValidator.Validate(layout);
        var nodes = view.mapContent.GetComponentsInChildren<MapSceneNode>(true).ToDictionary(node => node.nodeId);
        if (nodes.Count != layout.nodes.Count || layout.nodes.Any(node => !nodes.ContainsKey(node.id)))
            throw new InvalidOperationException("Scene nodes must match the template before updating roads.");
        var lineRoot = view.mapContent.Find("LineRoot");
        if (lineRoot == null) throw new InvalidOperationException("The scene needs a LineRoot.");
        if (lineRoot.GetComponent<MapRoadLayoutGroup>() == null) lineRoot.gameObject.AddComponent<MapRoadLayoutGroup>();
        var existing = view.mapContent.GetComponentsInChildren<MapSceneRoad>(true).ToDictionary(road => Key(road.fromNodeId, road.toNodeId));
        var required = new HashSet<string>();
        if (view.visualLayout != null) view.visualLayout.Validate(layout);
        foreach (var definition in layout.nodes)
        {
            var rect = (RectTransform)nodes[definition.id].transform;
            rect.anchoredPosition = view.visualLayout != null
                ? view.visualLayout.PositionFor(definition) : new Vector2(definition.x, definition.y);
            if (PrefabUtility.IsPartOfPrefabInstance(rect)) PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }
        WorldMapViewport.Configure(view);
        foreach (var definition in layout.nodes)
        {
            foreach (string neighbor in definition.neighbors)
            {
                if (string.CompareOrdinal(definition.id, neighbor) >= 0) continue;
                string key = Key(definition.id, neighbor);
                required.Add(key);
                if (!existing.TryGetValue(key, out var road))
                {
                    var obj = new GameObject("Road_" + definition.id + "_" + neighbor, typeof(RectTransform), typeof(CanvasRenderer), typeof(MapSceneRoad));
                    obj.layer = 5;
                    obj.transform.SetParent(lineRoot, false);
                    var rect = (RectTransform)obj.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                    road = obj.GetComponent<MapSceneRoad>();
                    road.color = new Color32(35, 34, 29, 245);
                }
                road.fromNodeId = definition.id;
                road.toNodeId = neighbor;
                road.from = (RectTransform)nodes[definition.id].transform;
                road.to = (RectTransform)nodes[neighbor].transform;
                road.village = (RectTransform)nodes[layout.villageNodeId].transform;
                road.SendMessage("LateUpdate");
            }
        }
        foreach (var pair in existing)
            if (!required.Contains(pair.Key)) Object.DestroyImmediate(pair.Value.gameObject);
        EditorUtility.SetDirty(view);
        Debug.Log("Applied map layout: " + nodes.Count + " nodes, " + required.Count + " roads.");
    }

    [MenuItem("Tools/Map Whitebox/Save Visual Layout")]
    public static void SaveVisualLayout()
    {
        if (EditorApplication.isPlaying) return;
        var view = FindView();
        if (view == null || view.visualLayout == null)
            throw new InvalidOperationException("Assign the map's visual layout asset before saving node positions.");
        Undo.RecordObject(view.visualLayout, "Save world map visual layout");
        view.visualLayout.nodes = view.mapContent.GetComponentsInChildren<MapSceneNode>(true)
            .OrderBy(node => node.nodeId, StringComparer.Ordinal)
            .Select(node => new WorldMapVisualLayout.NodePosition
            {
                nodeId = node.nodeId,
                position = ((RectTransform)node.transform).anchoredPosition
            }).ToList();
        view.visualLayout.mapSize = view.mapContent.rect.size;
        view.visualLayout.Validate(view.GetComponent<WorldMapController>().template.Load());
        EditorUtility.SetDirty(view.visualLayout);
        AssetDatabase.SaveAssets();
        Debug.Log("Saved world map visual layout: " + view.visualLayout.nodes.Count + " nodes.", view.visualLayout);
    }

    static WorldMapSceneView FindView()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        return stage != null ? stage.prefabContentsRoot.GetComponentInChildren<WorldMapSceneView>(true)
            : Object.FindObjectOfType<WorldMapSceneView>();
    }

    static string Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
}
