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
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MapSceneBuilder.ScenePath)
        {
            Debug.LogWarning("Open the copied map scene before applying the map layout.");
            return;
        }
        if (!EditorUtility.DisplayDialog("Apply Map Layout", "Update node positions and roads from the map template? Background and player UI are preserved.", "Apply", "Cancel")) return;
        Apply();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void Apply()
    {
        var view = Object.FindObjectOfType<WorldMapSceneView>();
        if (view == null) throw new InvalidOperationException("The native map scene is required.");
        var layout = view.GetComponent<WorldMapController>().template.Load();
        MapLayoutValidator.Validate(layout);
        var nodes = view.mapContent.GetComponentsInChildren<MapSceneNode>(true).ToDictionary(node => node.nodeId);
        if (nodes.Count != layout.nodes.Count || layout.nodes.Any(node => !nodes.ContainsKey(node.id)))
            throw new InvalidOperationException("Scene nodes must match the template before updating roads.");
        var lineRoot = view.mapContent.Find("LineRoot");
        if (lineRoot == null) throw new InvalidOperationException("The scene needs a LineRoot.");
        var existing = view.mapContent.GetComponentsInChildren<MapSceneRoad>(true).ToDictionary(road => Key(road.fromNodeId, road.toNodeId));
        var required = new HashSet<string>();
        foreach (var definition in layout.nodes)
            ((RectTransform)nodes[definition.id].transform).anchoredPosition = new Vector2(definition.x, definition.y);
        foreach (var definition in layout.nodes)
        {
            foreach (string neighbor in definition.neighbors)
            {
                if (string.CompareOrdinal(definition.id, neighbor) >= 0) continue;
                string key = Key(definition.id, neighbor);
                required.Add(key);
                if (!existing.TryGetValue(key, out var road))
                {
                    var obj = new GameObject("Road_" + definition.id + "_" + neighbor, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(MapSceneRoad));
                    obj.layer = 5;
                    obj.transform.SetParent(lineRoot, false);
                    var rect = (RectTransform)obj.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                    var image = obj.GetComponent<Image>();
                    image.color = new Color32(90, 96, 86, 255);
                    image.raycastTarget = false;
                    road = obj.GetComponent<MapSceneRoad>();
                }
                road.fromNodeId = definition.id;
                road.toNodeId = neighbor;
                road.from = (RectTransform)nodes[definition.id].transform;
                road.to = (RectTransform)nodes[neighbor].transform;
                road.SendMessage("LateUpdate");
            }
        }
        foreach (var pair in existing)
            if (!required.Contains(pair.Key)) Object.DestroyImmediate(pair.Value.gameObject);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Applied map layout: " + nodes.Count + " nodes, " + required.Count + " roads.");
    }

    static string Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
}
