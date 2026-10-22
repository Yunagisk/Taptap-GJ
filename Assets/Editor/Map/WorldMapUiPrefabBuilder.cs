using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit authoring commands only: importing scripts never rewrites an open scene.
public static class WorldMapUiPrefabBuilder
{
    public const string MapPrefabPath = "Assets/Prefabs/MapPrefabs/WorldMapPanel.prefab";
    public const string HudPrefabPath = "Assets/Prefabs/MapPrefabs/WorldMapHUD.prefab";
    public const string LocationMarkerPrefabPath = "Assets/Prefabs/MapPrefabs/CurrentLocationMarker.prefab";
    public const string EntryTransitionPrefabPath = "Assets/Prefabs/MapPrefabs/MapSceneTransition.prefab";

    sealed class ReferenceSnapshot
    {
        public Component owner;
        public readonly Dictionary<string, Object> fields = new Dictionary<string, Object>();
    }

    [MenuItem("Tools/Map Whitebox/Create UI Prefabs")]
    public static void Create()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before converting map UI.");
        var scene = SceneManager.GetActiveScene();
        Require(scene.path == MapSceneBuilder.ScenePath, "Open MainMap before converting map UI.");
        ConvertCurrentScene();
        Require(EditorSceneManager.SaveScene(scene), "Cannot save MainMap.");
        AssetDatabase.SaveAssets();
        Debug.Log("Map UI prefabs connected. Edit WorldMapPanel.prefab and WorldMapHUD.prefab for UI changes.");
    }

    public static void ConvertCurrentScene()
    {
        var scene = SceneManager.GetActiveScene();
        var view = Components<WorldMapSceneView>(scene).Single();
        var hud = Components<MapPlayerPanelView>(scene).Single();
        if (!Connected(view.gameObject, MapPrefabPath))
        {
            WorldMapViewport.Configure(view);
            EnsureLocationMarker(view);
            EnsureEntryTransition(view);
            ConvertRoot(scene, view.gameObject, MapPrefabPath);
        }
        if (!Connected(hud.gameObject, HudPrefabPath)) ConvertRoot(scene, hud.gameObject, HudPrefabPath);
        ValidateScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static void EnsureLocationMarker(WorldMapSceneView view)
    {
        if (view.currentLocationMarker != null) return;
        view.currentLocationMarker = view.mapContent.GetComponentInChildren<MapCurrentLocationMarker>(true);
        if (view.currentLocationMarker == null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(LocationMarkerPrefabPath);
            Require(asset != null, "Missing " + LocationMarkerPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, view.mapContent);
            view.currentLocationMarker = instance.GetComponent<MapCurrentLocationMarker>();
            instance.transform.SetAsLastSibling();
        }
        EditorUtility.SetDirty(view);
        if (PrefabUtility.IsPartOfPrefabInstance(view)) PrefabUtility.RecordPrefabInstancePropertyModifications(view);
    }

    public static void EnsureEntryTransition(WorldMapSceneView view)
    {
        var controller = view.GetComponent<WorldMapController>();
        if (controller.entryTransitionPrefab != null) return;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(EntryTransitionPrefabPath);
        Require(asset != null, "Missing " + EntryTransitionPrefabPath);
        controller.entryTransitionPrefab = asset.GetComponent<MapSceneTransition>();
        EditorUtility.SetDirty(controller);
        if (PrefabUtility.IsPartOfPrefabInstance(controller)) PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
    }

    // Rebuild a root only once. Repeating conversion keeps scene wiring and
    // existing prefab overrides; later UI edits belong in Prefab Mode.
    static void ConvertRoot(Scene scene, GameObject old, string path)
    {
        Require(!PrefabUtility.IsPartOfPrefabInstance(old), "Convert an unpacked UI root: " + old.name);
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(path) == null,
            "An unrelated prefab already exists at " + path + "; connect it explicitly instead of overwriting it.");
        var snapshots = CaptureReferences(scene, old);
        var asset = PrefabUtility.SaveAsPrefabAsset(old, path);
        Require(asset != null, "Cannot create " + path);
        var replacement = (GameObject)PrefabUtility.InstantiatePrefab(asset, old.transform.parent);
        Require(replacement != null, "Cannot instantiate " + path);
        replacement.name = old.name;
        replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
        CopyRect((RectTransform)old.transform, (RectTransform)replacement.transform);
        replacement.SetActive(old.activeSelf);
        RestoreReferences(snapshots, MapHierarchy(old, replacement));
        PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
        PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
        Object.DestroyImmediate(old);
    }

    static List<ReferenceSnapshot> CaptureReferences(Scene scene, GameObject root)
    {
        var objects = new HashSet<Object>();
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            objects.Add(transform.gameObject);
            foreach (var component in transform.GetComponents<Component>()) objects.Add(component);
        }
        var result = new List<ReferenceSnapshot>();
        foreach (var component in Components<Component>(scene))
        {
            if (component == null || component is Transform) continue;
            var snapshot = new ReferenceSnapshot { owner = component };
            var property = new SerializedObject(component).GetIterator();
            while (property.Next(true))
            {
                if (!UserReference(property)) continue;
                var reference = property.objectReferenceValue;
                if (reference != null && (objects.Contains(reference)
                    || objects.Contains(component) && !EditorUtility.IsPersistent(reference)))
                    snapshot.fields[property.propertyPath] = reference;
            }
            if (snapshot.fields.Count > 0) result.Add(snapshot);
        }
        return result;
    }

    static Dictionary<Object, Object> MapHierarchy(GameObject old, GameObject replacement)
    {
        var before = old.GetComponentsInChildren<Transform>(true);
        var after = replacement.GetComponentsInChildren<Transform>(true);
        Require(before.Length == after.Length, "Copied UI hierarchy does not match.");
        var result = new Dictionary<Object, Object>();
        for (int i = 0; i < before.Length; i++)
        {
            Require(before[i].name == after[i].name, "Copied UI child order does not match.");
            result[before[i].gameObject] = after[i].gameObject;
            var source = before[i].GetComponents<Component>();
            var target = after[i].GetComponents<Component>();
            Require(source.Length == target.Length, "Copied UI component count does not match.");
            for (int j = 0; j < source.Length; j++)
            {
                Require(source[j] != null && target[j] != null && source[j].GetType() == target[j].GetType(),
                    "Copied UI has a missing or mismatched component.");
                result[source[j]] = target[j];
            }
        }
        return result;
    }

    static void RestoreReferences(List<ReferenceSnapshot> snapshots, Dictionary<Object, Object> mapping)
    {
        foreach (var snapshot in snapshots)
        {
            var owner = mapping.TryGetValue(snapshot.owner, out var copied) ? (Component)copied : snapshot.owner;
            var serialized = new SerializedObject(owner);
            foreach (var field in snapshot.fields)
            {
                var property = serialized.FindProperty(field.Key);
                Require(property != null && UserReference(property), "Cannot restore UI reference " + field.Key);
                property.objectReferenceValue = mapping.TryGetValue(field.Value, out var target) ? target : field.Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
            if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }
    }

    static bool UserReference(SerializedProperty property) =>
        property.propertyType == SerializedPropertyType.ObjectReference
        && property.propertyPath != "m_GameObject" && property.propertyPath != "m_Script"
        && property.propertyPath != "m_CorrespondingSourceObject" && property.propertyPath != "m_PrefabInstance"
        && property.propertyPath != "m_PrefabAsset";

    [MenuItem("Tools/Map Whitebox/Validate UI Prefabs")]
    public static void Validate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before validating map UI.");
        ValidateScene(SceneManager.GetActiveScene());
        Debug.Log("Map UI validation passed: prefab instances, nodes, roads, navigation, HUD and scene wiring.");
    }

    static void ValidateScene(Scene scene)
    {
        Require(scene.path == MapSceneBuilder.ScenePath, "Open MainMap before validating map UI.");
        var view = Components<WorldMapSceneView>(scene).Single();
        var hud = Components<MapPlayerPanelView>(scene).Single();
        Require(Connected(view.gameObject, MapPrefabPath), "Map UI must be a connected WorldMapPanel prefab.");
        Require(Connected(hud.gameObject, HudPrefabPath), "HUD must be a connected WorldMapHUD prefab.");
        var map = view.GetComponent<WorldMapController>();
        Require(view.canvas != null && view.canvas == view.GetComponentInParent<Canvas>(), "Map Canvas binding is missing.");
        Require(map.template != null && view.visualLayout != null, "Map template or visual layout is missing.");
        Require(map.entryTransitionPrefab != null && map.entryTransitionPrefab.overlay != null
            && Mathf.Approximately(map.entryTransitionPrefab.fadeDuration, 0.5f), "Map entry fade prefab is missing.");
        var layout = map.template.Load();
        var nodes = view.mapContent.GetComponentsInChildren<MapSceneNode>(true);
        var roads = view.mapContent.GetComponentsInChildren<MapSceneRoad>(true);
        Require(nodes.Length == layout.nodes.Count && nodes.Select(node => node.nodeId).Distinct().Count() == nodes.Length,
            "Map nodes must match the template.");
        Require(roads.Length == layout.nodes.Sum(node => node.neighbors.Count) / 2
            && roads.All(road => road.from != null && road.to != null && road.village != null), "Map road endpoints are missing.");
        Require(view.viewport != null && view.viewport.content == view.mapContent, "Map viewport binding is missing.");
        Require(view.currentLocationMarker != null && view.currentLocationMarker.image != null
            && view.currentLocationMarker.revealEffect != null
            && view.currentLocationMarker.image.sprite != null && !view.currentLocationMarker.image.raycastTarget
            && view.currentLocationMarker.transform.parent == view.mapContent
            && Connected(view.currentLocationMarker.gameObject, LocationMarkerPrefabPath),
            "Current location marker must be a nested, non-interactive prefab in MapRoot.");
        var navigation = view.viewport.transform.Find("MapNavigation");
        Require(navigation != null && navigation.GetComponentsInChildren<Button>(true).Length == 2
            && navigation.Find("Focus") != null && navigation.Find("Inventory") != null, "Prefab navigation controls are missing.");
        Require(view.hpText == hud.hudHealthText && view.timeText != null && view.bossCountText != null
            && view.sinText != null && view.completeButton != null, "Map HUD or completion bindings are missing.");
        Require(hud.canvas == view.canvas && hud.mapController == map && hud.equipmentPanel != null
            && hud.inventoryPanel != null && hud.hudHealthFill != null, "Player HUD or equipment binding is missing.");
        var flow = Components<GameFlowController>(scene).Single();
        Require(flow.worldMapGroup == view.GetComponent<CanvasGroup>() && flow.playerPanel == hud,
            "Game flow must reference the replacement map and HUD.");
        Require(Enumerable.Range(0, map.onVillageEntered.GetPersistentEventCount()).Any(i =>
            map.onVillageEntered.GetPersistentTarget(i) == flow
            && map.onVillageEntered.GetPersistentMethodName(i) == nameof(GameFlowController.ShowVillage)),
            "Village entry callback is missing.");
        Require(hud.equipmentPanel.closeButton.onClick.GetPersistentEventCount() > 0
            && hud.equipmentPanel.closeButton.onClick.GetPersistentTarget(0) == hud,
            "Equipment close button must reference the replacement HUD.");
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                    "Missing script on " + transform.name);
    }

    public static bool Connected(GameObject root, string path) =>
        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == path;

    static IEnumerable<T> Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition3D = source.anchoredPosition3D;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
