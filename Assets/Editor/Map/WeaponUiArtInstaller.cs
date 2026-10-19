using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Explicit prefab authoring only; importing this script never changes a scene.
public static class WeaponUiArtInstaller
{
    public const string ArtFolder = "Assets/Sprite/UI/WeaponIconsInk";
    const string InventoryPrefab = "Assets/Prefabs/UI/EquipmentPanel.prefab";
    const string HudPrefab = "Assets/Prefabs/MapPrefabs/WorldMapHUD.prefab";
    const string InventoryMaterial = ArtFolder + "/InventoryWeaponInk.mat";
    const string HudMaterial = ArtFolder + "/HudWeaponInk.mat";
    static readonly string[] Ids = { "sword", "greatsword", "spear", "dual_swords", "axe", "sickle", "hammer", "flail", "torch", "poleaxe", "flame_tongue", "heaven_fire" };
    static readonly string[] Names = { "Sword", "Greatsword", "Spear", "DualSwords", "Axe", "Sickle", "Hammer", "Flail", "Torch", "Poleaxe", "FlameTongue", "HeavenFire" };

    [MenuItem("Tools/Map UI/Install Weapon Ink Icons")]
    public static void Install()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before editing UI prefabs.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var sprites = new Sprite[Ids.Length];
        var hudSprites = new Sprite[Ids.Length];
        for (int i = 0; i < Ids.Length; i++)
        {
            sprites[i] = ImportSprite(ArtFolder + "/Weapon" + Names[i] + ".png");
            hudSprites[i] = ImportSprite(ArtFolder + "/Hud/Weapon" + Names[i] + ".png");
        }
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/SharedUI/WeaponInkIcon.shader");
        Require(shader != null && !ShaderUtil.ShaderHasError(shader), "Weapon icon shader did not compile.");
        var inventoryMaterial = Material(InventoryMaterial, shader, .65f, 1.08f, .8f);
        var hudMaterial = Material(HudMaterial, shader, .08f, 1.38f, .62f);
        EditPrefab(HudPrefab, root =>
        {
            var hud = root.GetComponentInChildren<MapPlayerInkHud>(true);
            Require(hud != null, "HUD has no MapPlayerInkHud.");
            hud.weaponIcons = Ids.Select((id, i) => new MapPlayerInkHud.WeaponIcon { weaponId = id, sprite = hudSprites[i] }).ToArray();
            foreach (var image in hud.weaponImages) Configure(image, hudMaterial);
            EditorUtility.SetDirty(hud);
        });
        EditPrefab(InventoryPrefab, root =>
        {
            var inventory = root.GetComponentInChildren<InventoryLayoutView>(true);
            Require(inventory != null && inventory.weaponTemplate != null, "Inventory weapon template missing.");
            inventory.weaponArt = Ids.Select((id, i) => new InventoryLayoutView.WeaponArt { id = id, sprite = sprites[i] }).ToArray();
            Configure(inventory.weaponTemplate.icon, inventoryMaterial);
            EditorUtility.SetDirty(inventory);
        });
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("Weapon UI: installed all twelve icons into the inventory and HUD prefabs.");
    }

    static Sprite ImportSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Require(importer != null, "Missing " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.spritePivot = new Vector2(.5f, .5f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Require(sprite != null, "Sprite failed to import: " + path);
        return sprite;
    }

    static Material Material(string path, Shader shader, float saturation, float contrast, float accent)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Saturation", saturation);
        material.SetFloat("_Contrast", contrast);
        material.SetFloat("_WarmAccent", accent);
        EditorUtility.SetDirty(material);
        return material;
    }
    static void Configure(Image image, Material material)
    {
        Require(image != null, "Weapon Image is missing.");
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.material = material;
        image.raycastTarget = false;
        EditorUtility.SetDirty(image);
    }
    static void EditPrefab(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); Require(PrefabUtility.SaveAsPrefabAsset(root, path) != null, "Cannot save " + path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Map UI/Validate Weapon Ink Icons")]
    public static void Validate()
    {
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefab).GetComponentInChildren<MapPlayerInkHud>(true);
        var inventory = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPrefab).GetComponentInChildren<InventoryLayoutView>(true);
        Require(hud.weaponImages.Length == 4 && hud.weaponIcons.Length == 12, "HUD weapon bindings are incomplete.");
        Require(inventory.weaponArt.Length == 12, "Inventory weapon bindings are incomplete.");
        Require(hud.weaponIcons.Select(x => x.weaponId).Distinct().Count() == 12, "Duplicate HUD weapon id.");
        Require(inventory.weaponArt.Select(x => x.id).Distinct().Count() == 12, "Duplicate inventory weapon id.");
        foreach (var offer in WeaponTable.All)
        {
            var hudIcon = hud.weaponIcons.Single(x => x.weaponId == offer.id).sprite;
            var inventoryIcon = inventory.weaponArt.Single(x => x.id == offer.id).sprite;
            Require(hudIcon != null && inventoryIcon != null && hudIcon.name == inventoryIcon.name, "Missing or inconsistent icon for " + offer.id);
            Require(AssetDatabase.GetAssetPath(hudIcon).StartsWith(ArtFolder + "/", StringComparison.Ordinal), "Unexpected icon source.");
            Require(AssetDatabase.GetAssetPath(hudIcon).Contains("/Hud/"), "HUD framing variant missing.");
        }
        Require(hud.weaponReveal != null && hud.statusReveal != null && Mathf.Approximately(hud.animationDuration, .7f), "HUD reveal wiring changed.");
        foreach (var image in hud.weaponImages)
            Require(image.preserveAspect && AssetDatabase.GetAssetPath(image.material) == HudMaterial, "HUD material/aspect missing.");
        Require(inventory.weaponTemplate.icon.preserveAspect && AssetDatabase.GetAssetPath(inventory.weaponTemplate.icon.material) == InventoryMaterial, "Inventory material/aspect missing.");
        var shader = Shader.Find("UI/Weapon Ink Icon");
        Require(shader != null && !ShaderUtil.ShaderHasError(shader), "Icon shader has compilation errors.");
        var root = Directory.GetParent(Application.dataPath).FullName;
        var directory = Path.Combine(root, "output/imagegen/weapon-ui-ink");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "unity-validation.json"),
            "{\"passed\":true,\"hudIcons\":12,\"inventoryIcons\":12,\"shaderCompiled\":true,\"aspectPreserved\":true,\"revealReferencesPreserved\":true}");
    }

    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
