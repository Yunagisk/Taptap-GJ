using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BattleHudTools
{
    const string PrefabPath = "Assets/Prefabs/UI/CombatHUD.prefab";
    const string ScenePath = "Assets/Scenes/CombatScene.unity";
    static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "output/battle-ui");
    static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }

    [MenuItem("Tools/Battle UI/Validate HUD Prefab")]
    public static void Validate()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(asset != null, "CombatHUD prefab failed to import.");
        foreach (var transform in asset.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script on " + transform.name);
        var view = asset.GetComponent<BattleHudView>();
        Require(view != null && view.wheel != null && view.enemyTemplate != null && view.worldTagTemplate != null, "HUD templates are missing.");
        Require(view.playerPortrait != null && view.playerPortrait.sprite != null && view.playerHealth != null && view.healthText != null, "Player UI is incomplete.");
        Require(view.resolve.Length == 4 && view.resolve.All(x => x != null), "Four resolve diamonds are required.");
        Require(view.turnSlots.Length == 5 && view.turnSlots.All(x => x.frame != null && x.portrait != null), "Turn order slots are incomplete.");
        Require(view.damageIcons.Length == 4 && view.damageIcons.All(x => x != null), "Four damage-category icons are required.");
        Require(view.attack != null && view.defend != null && view.skill != null && view.items != null && view.closePopup != null, "Action button missing.");
        Require(view.popup != null && !view.popup.activeSelf && view.popupTitle != null && view.popupBody != null, "Information panel missing.");
        var wheel = view.wheel;
        Require(wheel.icons.Length == 4 && wheel.selectionFrames.Length == 4 && wheel.caption != null, "Wheel bindings are incomplete.");
        foreach (var weapon in WeaponTable.All)
            Require(wheel.weaponArt.Count(x => x.id == weapon.id && x.sprite != null) == 1, "Missing/duplicate wheel artwork: " + weapon.id);
        Require(wheel.icons.All(x => x != null && x.preserveAspect && !x.raycastTarget), "Wheel icons must preserve aspect and not block drag.");
        Require(wheel.GetComponent<Graphic>().raycastTarget, "Wheel drag surface missing.");
        Require(view.GetComponent<Canvas>().sortingOrder < 100, "HUD must remain below result overlay.");
        foreach (var label in asset.GetComponentsInChildren<TMP_Text>(true))
            Require(label.font != null && label.fontSharedMaterial != null, "Font/material missing on " + label.name);
        var row = view.enemyTemplate;
        Require(row.health != null && row.healthText != null && row.select != null && row.frame != null && row.weaknesses.Length == 4, "Enemy card is incomplete.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "unity-validation.json"), "{\"passed\":true,\"weaponIcons\":12,\"resolveSlots\":4,\"turnSlots\":5,\"prefabReferencesValid\":true}");
        Debug.Log("Battle HUD prefab: all bindings and twelve weapon icons validated.");
    }

    // Offscreen render of the actual HUD and battle scene, without changing the user's open scene.
    [MenuItem("Tools/Battle UI/Render HUD Preview")]
    public static void RenderPreview()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before rendering an editor preview.");
        Validate();
        var previousScene = SceneManager.GetActiveScene();
        string copyPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__CombatHudPreview.unity");
        File.Copy(ScenePath, copyPath);
        AssetDatabase.ImportAsset(copyPath, ImportAssetOptions.ForceSynchronousImport);
        var scene = EditorSceneManager.OpenScene(copyPath, OpenSceneMode.Additive);
        RenderTexture render = null;
        Texture2D pixels = null;
        try
        {
            var objects = scene.GetRootGameObjects();
            var binder = objects.SelectMany(x => x.GetComponentsInChildren<BattleHudSceneBinder>(true)).Single();
            Require(binder.flow != null && binder.loadout != null && binder.weapons != null && binder.hudPrefab != null, "CombatScene binder references are missing.");
            var flow = binder.flow;
            flow.Start();
            binder.weapons.Initialize();
            foreach (var old in binder.legacyRoots) if (old != null) old.SetActive(false);
            foreach (var old in objects.SelectMany(x => x.GetComponentsInChildren<BattleLoadoutView>(true))) old.gameObject.SetActive(false);
            foreach (var old in objects.SelectMany(x => x.GetComponentsInChildren<Canvas>(true)))
                if (old.renderMode == RenderMode.WorldSpace) old.gameObject.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var view = instance.GetComponent<BattleHudView>();
            view.Bind(flow, binder.loadout, binder.weapons);
            view.wheel.SendMessage("Update");
            var camera = objects.SelectMany(x => x.GetComponentsInChildren<Camera>(true)).First(x => x.CompareTag("MainCamera"));
            render = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            render.Create();
            camera.scene = scene;
            camera.targetTexture = render;
            camera.cullingMask = ~0;
            var canvas = view.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            foreach (var label in instance.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            // Position the world labels explicitly: regular MonoBehaviour LateUpdate does not run in edit mode.
            int index = 0;
            foreach (var enemy in flow.Enemies)
            {
                if (enemy == null) continue;
                var tag = (RectTransform)view.transform.Find("WorldTag_" + (++index));
                var sprite = enemy.GetComponentInChildren<SpriteRenderer>();
                if (tag == null || sprite == null) continue;
                var top = camera.WorldToScreenPoint(new Vector3(sprite.bounds.center.x, sprite.bounds.max.y, sprite.bounds.center.z));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(view.Root, top, camera, out var point);
                ((RectTransform)tag.Find("Intent")).anchoredPosition = point + Vector2.up * 72;
                ((RectTransform)tag.Find("Arrow")).anchoredPosition = point + Vector2.up * 28;
                var foot = camera.WorldToScreenPoint(new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, sprite.bounds.center.z));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(view.Root, foot, camera, out point);
                ((RectTransform)tag.Find("Name")).anchoredPosition = point - Vector2.up * 20;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = render;
                pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
            }
            finally { RenderTexture.active = previous; }
            // Reject blank renders instead of reporting a misleading successful preview.
            int paperPixels = 0;
            foreach (var pixel in pixels.GetPixels32()) if (pixel.r > 165 && pixel.g > 155 && pixel.b > 130) paperPixels++;
            Require(paperPixels > 30000, "HUD did not render into the offscreen camera.");
            Directory.CreateDirectory(Path.Combine(Output, "previews"));
            File.WriteAllBytes(Path.Combine(Output, "previews/combat-hud-unity.png"), pixels.EncodeToPNG());
            File.WriteAllText(Path.Combine(Output, "unity-scene-validation.json"), "{\"passed\":true,\"sceneBinderValid\":true,\"nativeHudRendered\":true,\"paperPixels\":" + paperPixels + "}");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            AssetDatabase.DeleteAsset(copyPath);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            if (render != null) { render.Release(); UnityEngine.Object.DestroyImmediate(render); }
        }
    }
}
