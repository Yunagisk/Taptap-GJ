using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicitly converts only the village inn UI into a connected prefab.</summary>
public static class InnScenePrefabBuilder
{
    const string ScenePath = "Assets/Scenes/Village.unity";
    const string PrefabPath = "Assets/Prefabs/UI/InnPanel.prefab";
    const string BodyText = "确认休息后恢复全部生命。关闭不会回血。";
    const string ConfirmText = "休息回血";

    sealed class ReferenceSnapshot
    {
        public Component owner;
        public readonly Dictionary<string, Object> fields = new Dictionary<string, Object>();
    }

    [MenuItem("Tools/Game Flow/Build Inn Prefab")]
    public static void Build()
    {
        RequireCleanEditor();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var screen = Components<VillageScreen>(scene).Single();
            Require(screen.panelInn != null, "VillageScreen.panelInn is missing.");
            var old = screen.panelInn;
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old) == PrefabPath)
            {
                var existingAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                // Upgrade older InnPanel assets so sorting components belong to the prefab itself.
                if (existingAsset == null || !HasSerializedOverride(existingAsset.GetComponent<Canvas>())
                    || existingAsset.GetComponent<GraphicRaycaster>() == null)
                    PrefabUtility.UnpackPrefabInstance(old, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            }
            EnsureInnSorting(old);
            var confirm = Child<Button>(old, "Btn_InnConfirm");
            var body = Child<TMP_Text>(old, "Txt_InnBody");
            var label = confirm.GetComponentInChildren<TMP_Text>(true);
            Require(label != null, "Inn confirm button label is missing.");

            body.text = BodyText;
            label.text = ConfirmText;
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(label);

            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old) != PrefabPath)
            {
                EnsureFolder();
                var references = CaptureReferences(scene, old);
                var prefab = PrefabUtility.SaveAsPrefabAsset(old, PrefabPath);
                Require(prefab != null, "Cannot create " + PrefabPath);
                // Prefab roots have no parent Canvas, so persist the nested sorting flag explicitly.
                CopySerializedSorting(old.GetComponent<Canvas>(), prefab.GetComponent<Canvas>());
                Require(PrefabUtility.SavePrefabAsset(prefab) != null, "Cannot save inn sorting components.");
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.transform.parent);
                Require(replacement != null, "Cannot instantiate " + PrefabPath);
                replacement.name = old.name;
                replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                CopyRect((RectTransform)old.transform, (RectTransform)replacement.transform);
                replacement.SetActive(old.activeSelf);
                var mapping = MapHierarchy(old, replacement);
                RestoreReferences(references, mapping);
                EnsureInnSorting(replacement);
                screen.panelInn = replacement;
                screen.focusInnConfirm = Child<Button>(replacement, "Btn_InnConfirm").gameObject;
                PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
                PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
                Object.DestroyImmediate(old);
            }
            else
            {
                // Repeating the builder preserves the existing instance and its scene overrides.
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                PrefabUtility.RecordPrefabInstancePropertyModifications(old.GetComponent<Canvas>());
            }

            screen.focusInnConfirm = Child<Button>(screen.panelInn, "Btn_InnConfirm").gameObject;
            Bind(screen.focusInnConfirm.GetComponent<Button>(), screen.ConfirmInn);
            Bind(Child<Button>(screen.panelInn, "Btn_InnClose"), screen.ClosePanels);
            EditorUtility.SetDirty(screen);
            if (PrefabUtility.IsPartOfPrefabInstance(screen))
                PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
            ValidateScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Cannot save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Inn prefab built successfully; only Village.unity was updated.");
        }
        finally { RestoreSavedSceneSetup(setup); }
    }

    [MenuItem("Tools/Game Flow/Validate Inn Prefab")]
    public static void Validate()
    {
        RequireCleanEditor();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            ValidateScene(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single));
            Debug.Log("Inn prefab and village references are valid.");
        }
        finally { RestoreSavedSceneSetup(setup); }
    }

    static void RequireCleanEditor()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before building the inn prefab.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            Require(!scene.isDirty, "Save or discard your unsaved scene changes first: " + scene.path);
        }
    }

    static void RestoreSavedSceneSetup(SceneSetup[] setup)
    {
        // Unity batch mode may start with an unnamed scene which cannot be restored.
        if (setup == null || setup.Length == 0 || setup.Any(item => string.IsNullOrEmpty(item.path))
            || !setup.Any(item => item.isActive && item.isLoaded)) return;
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    static void EnsureInnSorting(GameObject inn)
    {
        var parentCanvas = inn.transform.parent != null
            ? inn.transform.parent.GetComponentInParent<Canvas>() : null;
        Require(parentCanvas != null, "Inn panel needs a parent Canvas.");
        var rootCanvas = parentCanvas.rootCanvas;
        var inactive = new List<GameObject>();
        for (var transform = inn.transform; transform != null; transform = transform.parent)
            if (!transform.gameObject.activeSelf) inactive.Add(transform.gameObject);
        Canvas canvas = null;
        try
        {
            // Register the new nested Canvas under its actual parent before setting native sorting.
            for (int i = inactive.Count - 1; i >= 0; i--) inactive[i].SetActive(true);
            canvas = inn.GetComponent<Canvas>();
            if (canvas == null) canvas = inn.AddComponent<Canvas>();
            canvas.renderMode = rootCanvas.renderMode;
            canvas.overrideSorting = true;
            canvas.sortingLayerID = rootCanvas.sortingLayerID;
            canvas.sortingOrder = rootCanvas.sortingOrder + 1;
            if (inn.GetComponent<GraphicRaycaster>() == null) inn.AddComponent<GraphicRaycaster>();
        }
        finally
        {
            foreach (var item in inactive) item.SetActive(false);
        }
        // Inactive Canvases can temporarily report themselves as roots. Keep serialized values authoritative.
        WriteSerializedSorting(canvas, (int)rootCanvas.renderMode, rootCanvas.sortingLayerID, rootCanvas.sortingOrder + 1);
        if (PrefabUtility.IsPartOfPrefabInstance(canvas))
            PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
    }

    static void WriteSerializedSorting(Canvas canvas, int renderMode, int layer, int order)
    {
        Require(canvas != null, "Inn sorting Canvas is missing.");
        var serialized = new SerializedObject(canvas);
        serialized.FindProperty("m_RenderMode").intValue = renderMode;
        serialized.FindProperty("m_OverrideSorting").boolValue = true;
        serialized.FindProperty("m_SortingLayerID").intValue = layer;
        serialized.FindProperty("m_SortingOrder").intValue = order;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(canvas);
    }

    static void CopySerializedSorting(Canvas source, Canvas target)
    {
        var serialized = new SerializedObject(source);
        WriteSerializedSorting(target, serialized.FindProperty("m_RenderMode").intValue,
            serialized.FindProperty("m_SortingLayerID").intValue, serialized.FindProperty("m_SortingOrder").intValue);
    }

    static bool HasSerializedOverride(Canvas canvas) =>
        canvas != null && new SerializedObject(canvas).FindProperty("m_OverrideSorting").boolValue;

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
    }

    static T Child<T>(GameObject root, string name) where T : Component
    {
        var result = root.GetComponentsInChildren<T>(true).SingleOrDefault(component => component.name == name);
        Require(result != null, "Missing inn UI child " + name);
        return result;
    }

    static IEnumerable<T> Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    static HashSet<Object> HierarchyObjects(GameObject root)
    {
        var objects = new HashSet<Object>();
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            objects.Add(transform.gameObject);
            foreach (var component in transform.GetComponents<Component>())
            {
                Require(component != null, "Missing script in inn panel: " + transform.name);
                objects.Add(component);
            }
        }
        return objects;
    }

    static List<ReferenceSnapshot> CaptureReferences(Scene scene, GameObject old)
    {
        var oldObjects = HierarchyObjects(old);
        var snapshots = new List<ReferenceSnapshot>();
        foreach (var component in Components<Component>(scene))
        {
            if (component == null || component is Transform) continue;
            bool copiedOwner = oldObjects.Contains(component);
            var snapshot = new ReferenceSnapshot { owner = component };
            var property = new SerializedObject(component).GetIterator();
            while (property.Next(true))
            {
                if (!IsUserReference(property)) continue;
                var reference = property.objectReferenceValue;
                if (reference != null && (oldObjects.Contains(reference)
                    || copiedOwner && !EditorUtility.IsPersistent(reference)))
                    snapshot.fields[property.propertyPath] = reference;
            }
            if (snapshot.fields.Count != 0) snapshots.Add(snapshot);
        }
        return snapshots;
    }

    static bool IsUserReference(SerializedProperty property)
    {
        if (property.propertyType != SerializedPropertyType.ObjectReference) return false;
        string path = property.propertyPath;
        return path != "m_GameObject" && path != "m_Script" && path != "m_CorrespondingSourceObject"
            && path != "m_PrefabInstance" && path != "m_PrefabAsset";
    }

    static Dictionary<Object, Object> MapHierarchy(GameObject old, GameObject replacement)
    {
        var oldTransforms = old.GetComponentsInChildren<Transform>(true);
        var newTransforms = replacement.GetComponentsInChildren<Transform>(true);
        Require(oldTransforms.Length == newTransforms.Length, "Copied inn hierarchy does not match.");
        var mapping = new Dictionary<Object, Object>();
        for (int i = 0; i < oldTransforms.Length; i++)
        {
            Require(oldTransforms[i].name == newTransforms[i].name, "Copied inn child order does not match.");
            mapping[oldTransforms[i].gameObject] = newTransforms[i].gameObject;
            var oldComponents = oldTransforms[i].GetComponents<Component>();
            var newComponents = newTransforms[i].GetComponents<Component>();
            Require(oldComponents.Length == newComponents.Length, "Copied inn component count does not match.");
            for (int j = 0; j < oldComponents.Length; j++)
            {
                Require(oldComponents[j] != null && newComponents[j] != null
                    && oldComponents[j].GetType() == newComponents[j].GetType(), "Copied inn component types do not match.");
                mapping[oldComponents[j]] = newComponents[j];
            }
        }
        return mapping;
    }

    static void RestoreReferences(List<ReferenceSnapshot> snapshots, Dictionary<Object, Object> mapping)
    {
        foreach (var snapshot in snapshots)
        {
            var owner = mapping.TryGetValue(snapshot.owner, out Object copiedOwner)
                ? (Component)copiedOwner : snapshot.owner;
            var serialized = new SerializedObject(owner);
            foreach (var field in snapshot.fields)
            {
                var property = serialized.FindProperty(field.Key);
                Require(property != null && IsUserReference(property), "Cannot restore inn reference " + field.Key);
                property.objectReferenceValue = mapping.TryGetValue(field.Value, out Object copiedReference)
                    ? copiedReference : field.Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
            if (PrefabUtility.IsPartOfPrefabInstance(owner))
                PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }
    }

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

    static void Bind(Button button, UnityAction action)
    {
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
        PrefabUtility.RecordPrefabInstancePropertyModifications(button);
    }

    static void ValidateScene(Scene scene)
    {
        var screen = Components<VillageScreen>(scene).Single();
        Require(screen.panelInn != null
            && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(screen.panelInn) == PrefabPath,
            "Inn UI must be a connected InnPanel prefab.");
        var innCanvas = screen.panelInn.GetComponent<Canvas>();
        var parentCanvas = screen.panelInn.transform.parent != null
            ? screen.panelInn.transform.parent.GetComponentInParent<Canvas>() : null;
        var rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
        var innAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(innCanvas != null && rootCanvas != null && HasSerializedOverride(innCanvas)
            && innCanvas.sortingLayerID == rootCanvas.sortingLayerID
            && innCanvas.sortingOrder > rootCanvas.sortingOrder
            && screen.panelInn.GetComponent<GraphicRaycaster>() != null,
            "Inn must render and receive clicks above the parent root Canvas. Inn order/layer="
            + (innCanvas != null ? innCanvas.sortingOrder + "/" + innCanvas.sortingLayerID : "missing")
            + ", root=" + (rootCanvas != null ? rootCanvas.name + " " + rootCanvas.sortingOrder
                + "/" + rootCanvas.sortingLayerID : "missing")
            + ", inn active=" + screen.panelInn.activeInHierarchy);
        Require(innAsset != null && innAsset.GetComponent<Canvas>() != null
            && HasSerializedOverride(innAsset.GetComponent<Canvas>()) && innAsset.GetComponent<GraphicRaycaster>() != null,
            "Inn sorting Canvas and GraphicRaycaster must belong to the prefab asset.");
        var confirm = Child<Button>(screen.panelInn, "Btn_InnConfirm");
        var close = Child<Button>(screen.panelInn, "Btn_InnClose");
        Require(screen.focusInnConfirm == confirm.gameObject, "Inn confirm focus is missing.");
        Require(Child<TMP_Text>(screen.panelInn, "Txt_InnBody").text == BodyText, "Inn description is outdated.");
        Require(confirm.GetComponentInChildren<TMP_Text>(true).text == ConfirmText, "Inn confirm label is outdated.");
        Require(confirm.onClick.GetPersistentEventCount() == 1
            && confirm.onClick.GetPersistentTarget(0) == screen
            && confirm.onClick.GetPersistentMethodName(0) == nameof(VillageScreen.ConfirmInn),
            "Inn confirm button must invoke VillageScreen.ConfirmInn exactly once.");
        Require(close.onClick.GetPersistentEventCount() == 1
            && close.onClick.GetPersistentTarget(0) == screen
            && close.onClick.GetPersistentMethodName(0) == nameof(VillageScreen.ClosePanels),
            "Inn close button must invoke VillageScreen.ClosePanels exactly once.");
        foreach (var transform in Components<Transform>(scene))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                "Missing script in village: " + transform.name);
        foreach (var component in Components<Component>(scene))
        {
            if (component == null) continue;
            var property = new SerializedObject(component).GetIterator();
            while (property.Next(true))
                Require(property.propertyType != SerializedPropertyType.ObjectReference
                    || property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                    "Unresolved village reference: " + component.name + "." + property.propertyPath);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
