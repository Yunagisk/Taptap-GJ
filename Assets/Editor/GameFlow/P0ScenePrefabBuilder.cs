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

// Run explicitly in the editor or in an isolated project with -executeMethod P0ScenePrefabBuilder.Build.
public static class P0ScenePrefabBuilder
{
    const string Folder = "Assets/Prefabs/UI";
    const string EquipmentPath = Folder + "/EquipmentPanel.prefab";
    const string OwnedRowPath = Folder + "/EquipmentOwnedRow.prefab";
    const string ChoicePath = Folder + "/DialogueChoiceButton.prefab";
    const string DialoguePath = Folder + "/DialoguePanel.prefab";
    const string StoryPath = Folder + "/VillageStoryPanel.prefab";
    const string IntroPath = Folder + "/IntroCanvas.prefab";
    const string BattleWeaponPath = Folder + "/BattleWeaponPanel.prefab";
    static readonly string[] ScenePaths = {
        "Assets/Scenes/MainMap.unity", "Assets/Scenes/Village.unity",
        "Assets/Scenes/CombatScene.unity", "Assets/Scenes/Intro.unity"
    };
    static readonly Color Paper = new Color32(248, 246, 238, 255);
    static readonly Color Ink = new Color32(38, 40, 43, 255);
    static readonly Color ButtonColor = new Color32(233, 230, 220, 255);
    static Scene preview;
    static TMP_FontAsset font;
    static WeaponLibrary library;

    [MenuItem("Tools/Game Flow/Build P0 Story And Equipment Prefabs")]
    public static void Build()
    {
        RequireCleanEditor();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            LoadAssets();
            EnsureFolder();
            preview = EditorSceneManager.NewPreviewScene();
            CreateReusablePrefabs();
            EditorSceneManager.ClosePreviewScene(preview);
            preview = default(Scene);
            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (path.EndsWith("/MainMap.unity")) ApplyMap(scene);
                else if (path.EndsWith("/Village.unity")) ApplyVillage(scene);
                else if (path.EndsWith("/CombatScene.unity")) ApplyCombat(scene);
                else ApplyIntro(scene);
                ValidateScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save " + path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("P0 prefabs and scene references built successfully.");
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            preview = default(Scene);
            RestoreSavedSceneSetup(setup);
        }
    }

    public static void Apply() { Build(); }

    [MenuItem("Tools/Game Flow/Validate P0 Story And Equipment Prefabs")]
    public static void Validate()
    {
        RequireCleanEditor();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in new[] { EquipmentPath, OwnedRowPath, ChoicePath, DialoguePath, StoryPath, IntroPath, BattleWeaponPath })
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null, "Missing prefab " + path);
            foreach (string path in ScenePaths)
                ValidateScene(EditorSceneManager.OpenScene(path, OpenSceneMode.Single));
            Debug.Log("P0 validation passed: four scenes, connected prefabs, required fields and no missing scripts/references.");
        }
        finally { RestoreSavedSceneSetup(setup); }
    }

    static void RestoreSavedSceneSetup(SceneSetup[] setup)
    {
        // Unity batch mode can start with an unnamed clean scene which cannot be restored.
        if (setup == null || setup.Length == 0 || setup.Any(entry => string.IsNullOrEmpty(entry.path))
            || !setup.Any(entry => entry.isActive && entry.isLoaded)) return;
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    static void RequireCleanEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building P0 scenes.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty)
                throw new InvalidOperationException("Save or discard your own unsaved scene changes first: " + scene.path);
        }
    }

    static void LoadAssets()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Huiwen.asset");
        library = AssetDatabase.LoadAssetAtPath<WeaponLibrary>("Assets/Data/Weapons/WeaponLibrary.asset");
        Require(font != null, "Huiwen font asset is missing.");
        Require(library != null, "WeaponLibrary.asset is missing.");
        foreach (string name in new[] { "VillageWelcome", "VillageCommission", "VillageAfterTutorial", "VillageTurnIn", "VillageAfterTurnIn" })
            Require(Dialogue(name) != null, "Missing story dialogue " + name);
    }

    static DialogueData Dialogue(string name) =>
        AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Data/DialogueData/" + name + ".asset");

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
    }

    static void CreateReusablePrefabs()
    {
        var row = MakeButton("EquipmentOwnedRow", null, "武器", new Vector2(440, 54), 22);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
        SavePrefab(row.gameObject, OwnedRowPath);
        Object.DestroyImmediate(row.gameObject);

        var choice = MakeButton("DialogueChoiceButton", null, "选择", new Vector2(820, 62), 22);
        choice.gameObject.AddComponent<LayoutElement>().preferredHeight = 62;
        SavePrefab(choice.gameObject, ChoicePath);
        Object.DestroyImmediate(choice.gameObject);

        BuildEquipmentPrefab();
        BuildDialoguePrefab();
        BuildStoryPrefab();
    }

    static void BuildEquipmentPrefab()
    {
        var root = Rect("EquipmentPanel", null);
        Stretch(root);
        root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);
        var window = Rect("Window", root);
        Box(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 640), new Vector2(0.5f, 0.5f));
        window.gameObject.AddComponent<Image>().color = Paper;
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var toggler = root.gameObject.AddComponent<PanelToggler>();
        toggler.target = group;
        var view = root.gameObject.AddComponent<EquipmentPanelView>();
        view.window = window;
        view.font = font;
        view.weaponLibrary = library;
        view.visibility = group;
        view.panelToggler = toggler;
        view.manageEscape = false;
        Label("Title", window, "背包与装备", new Vector2(24, -20), new Vector2(800, 42), 30);
        view.closeButton = ButtonAt("Close", window, "关闭", new Vector2(870, -20), new Vector2(66, 42), 20);
        Click(view.closeButton, view.Close);
        view.healthText = Label("Health", window, "生命", new Vector2(24, -76), new Vector2(900, 34), 22);
        view.statusText = Label("Resources", window, "决心 / 提灯 / 罪骸", new Vector2(24, -114), new Vector2(900, 64), 22);
        view.sinText = Label("Sin", window, "罪恶值", new Vector2(24, -182), new Vector2(900, 32), 22);
        Label("OwnedTitle", window, "已拥有武器", new Vector2(24, -220), new Vector2(440, 36), 24);
        Label("SlotsTitle", window, "四个装备槽", new Vector2(500, -220), new Vector2(436, 36), 24);
        view.ownedWeaponsScroll = Scroll("OwnedWeapons", window, new Vector2(24, -262), new Vector2(440, 254),
            out var rows);
        view.ownedRowsParent = rows;
        view.rowButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OwnedRowPath).GetComponent<Button>();
        view.slotButtons = new Button[EquipmentRules.SlotCount];
        view.slotLabels = new TMP_Text[EquipmentRules.SlotCount];
        for (int i = 0; i < EquipmentRules.SlotCount; i++)
        {
            view.slotButtons[i] = ButtonAt("Slot_" + (i + 1), window, "槽 " + (i + 1),
                new Vector2(500 + (i % 2) * 222, -262 - (i / 2) * 78), new Vector2(210, 70), 21);
            view.slotLabels[i] = view.slotButtons[i].GetComponentInChildren<TMP_Text>();
        }
        view.selectionText = Label("Selection", window, "选择槽位与已拥有武器", new Vector2(500, -426), new Vector2(436, 50), 20);
        view.equipButton = ButtonAt("Equip", window, "装备", new Vector2(500, -482), new Vector2(136, 44), 21);
        view.unequipButton = ButtonAt("Unequip", window, "卸下", new Vector2(648, -482), new Vector2(136, 44), 21);
        view.selectWeaponButton = ButtonAt("SetCurrent", window, "设为当前", new Vector2(796, -482), new Vector2(140, 44), 21);
        view.questText = Label("CurrentGoal", window, "当前目标", new Vector2(24, -536), new Vector2(912, 40), 21);
        view.itemsText = Label("Items", window, "暂无道具", new Vector2(24, -582), new Vector2(454, 40), 20);
        view.messageText = Label("Message", window, "", new Vector2(500, -582), new Vector2(436, 40), 20);
        SavePrefab(root.gameObject, EquipmentPath);
        Object.DestroyImmediate(root.gameObject);
    }

    static void BuildDialoguePrefab()
    {
        var root = Rect("DialoguePanel", null);
        Stretch(root);
        var manager = root.gameObject.AddComponent<DialogueManager>();
        var ui = root.gameObject.AddComponent<DialogueUI>();
        manager.ui = ui;
        ui.manager = manager;
        var overlay = Rect("VisibleRoot", root);
        Stretch(overlay);
        overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);
        ui.visibleRoot = overlay.gameObject;
        var window = Rect("Window", overlay);
        Box(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 520), new Vector2(0.5f, 0.5f));
        window.gameObject.AddComponent<Image>().color = Paper;
        ui.avatarImage = Rect("Avatar", window).gameObject.AddComponent<Image>();
        Box(ui.avatarImage.rectTransform, new Vector2(0, 1), new Vector2(24, -64), new Vector2(88, 88), new Vector2(0, 1));
        ui.avatarImage.preserveAspect = true;
        ui.speakerText = Label("Speaker", window, "说话人", new Vector2(140, -28), new Vector2(820, 42), 28);
        ui.contentText = Label("Content", window, "", new Vector2(140, -82), new Vector2(820, 204), 24);
        ui.contentText.alignment = TextAlignmentOptions.TopLeft;
        var choices = Rect("Choices", window);
        Box(choices, new Vector2(0, 1), new Vector2(140, -302), new Vector2(820, 152), new Vector2(0, 1));
        var layout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ui.choiceParent = choices;
        ui.choicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePath).GetComponent<Button>();
        ui.continueButton = ButtonAt("Continue", window, "下一段", new Vector2(828, -466), new Vector2(140, 38), 21);
        overlay.gameObject.SetActive(false);
        SavePrefab(root.gameObject, DialoguePath);
        Object.DestroyImmediate(root.gameObject);
    }

    static void BuildStoryPrefab()
    {
        var root = Rect("VillageStoryPanel", null);
        Stretch(root);
        var view = root.gameObject.AddComponent<VillageStoryPanelUI>();
        var hud = Rect("Objective", root);
        Box(hud, new Vector2(0, 1), new Vector2(32, -104), new Vector2(630, 176), new Vector2(0, 1));
        var image = hud.gameObject.AddComponent<Image>();
        image.color = Paper;
        image.raycastTarget = false;
        view.questText = Label("Goal", hud, "当前目标", new Vector2(16, -12), new Vector2(598, 64), 23);
        view.talkButton = ButtonAt("Talk", hud, "与村民交谈", new Vector2(16, -86), new Vector2(248, 44), 22);
        view.talkLabel = view.talkButton.GetComponentInChildren<TMP_Text>();
        view.messageText = Label("Message", hud, "", new Vector2(276, -86), new Vector2(332, 60), 18);
        var dialogue = InstantiatePrefab(DialoguePath, root);
        view.dialogueManager = dialogue.GetComponent<DialogueManager>();
        view.welcomeDialogue = Dialogue("VillageWelcome");
        view.commissionDialogue = Dialogue("VillageCommission");
        view.afterTutorialDialogue = Dialogue("VillageAfterTutorial");
        view.turnInDialogue = Dialogue("VillageTurnIn");
        view.afterTurnInDialogue = Dialogue("VillageAfterTurnIn");
        SavePrefab(root.gameObject, StoryPath);
        Object.DestroyImmediate(root.gameObject);
    }

    static void ApplyMap(Scene scene)
    {
        var player = Components<MapPlayerPanelView>(scene).FirstOrDefault();
        Require(player != null && player.canvas != null, "MainMap player HUD is missing.");
        GameObject old = player.equipmentPanel != null ? player.equipmentPanel.gameObject
            : player.inventoryPanel != null ? player.inventoryPanel.gameObject
            : Find(scene, "InventoryPanel");
        Require(old != null, "MainMap InventoryPanel is missing.");
        var replacement = InstantiatePrefab(EquipmentPath, old.transform.parent);
        replacement.name = "InventoryPanel";
        replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
        var equipment = replacement.GetComponent<EquipmentPanelView>();
        var mapping = RootMapping(old, replacement);
        Add(mapping, player.inventoryWindow, equipment.window);
        Add(mapping, player.inventoryPanel, equipment.panelToggler);
        Add(mapping, player.inventoryHealthText, equipment.healthText);
        Add(mapping, player.resourcesText, equipment.statusText);
        Add(mapping, player.sinValueText, equipment.sinText);
        Add(mapping, player.itemsText, equipment.itemsText);
        RemapReferences(scene, mapping);
        player.SetEquipmentPanel(equipment);
        player.inventoryWindow = equipment.window;
        player.inventoryPanel = equipment.panelToggler;
        player.inventoryHealthText = equipment.healthText;
        player.resourcesText = equipment.statusText;
        player.sinValueText = equipment.sinText;
        player.itemsText = equipment.itemsText;
        player.itemsScroll = equipment.ownedWeaponsScroll;
        player.inventoryHealthFill = null;
        player.weaponNames = equipment.slotLabels;
        player.weaponStatuses = new TMP_Text[0];
        player.weaponSlots = equipment.slotButtons.Select(button => button.GetComponent<Image>()).ToArray();
        player.sinStatusText = null;
        player.virtueStatusText = null;
        Click(equipment.closeButton, player.CloseInventory);
        foreach (var view in Components<WorldMapSceneView>(scene)) view.sinText = equipment.sinText;
        Object.DestroyImmediate(old);
        equipment.Close();
        EditorUtility.SetDirty(player);
        PrefabUtility.RecordPrefabInstancePropertyModifications(equipment);
        PrefabUtility.RecordPrefabInstancePropertyModifications(equipment.visibility);
        PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
        PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
    }

    static void ApplyVillage(Scene scene)
    {
        var screen = Components<VillageScreen>(scene).FirstOrDefault();
        Require(screen != null && screen.panelBag != null, "VillageScreen/Panel_Bag is missing.");
        var old = screen.panelBag;
        var replacement = InstantiatePrefab(EquipmentPath, old.transform.parent);
        replacement.name = "Panel_Bag";
        replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
        var equipment = replacement.GetComponent<EquipmentPanelView>();
        var mapping = RootMapping(old, replacement);
        Add(mapping, screen.focusSlot, equipment.slotButtons[0].gameObject);
        RemapReferences(scene, mapping);
        screen.panelBag = replacement;
        screen.focusSlot = equipment.slotButtons[0].gameObject;
        Click(equipment.closeButton, screen.ClosePanels);
        Object.DestroyImmediate(old);
        replacement.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
        PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
        // Keep the migrated tavern entry when rebuilding unrelated P0 UI.
        var tavernStory = screen.panelEvent != null
            ? screen.panelEvent.GetComponent<VillageStoryPanelUI>() : null;
        if (tavernStory != null)
        {
            tavernStory.template = screen.template;
            foreach (var previous in Components<VillageStoryPanelUI>(scene).ToArray())
                if (previous != tavernStory) Object.DestroyImmediate(previous.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tavernStory);
            EditorUtility.SetDirty(screen);
            return;
        }
        foreach (var previous in Components<VillageStoryPanelUI>(scene).ToArray())
            Object.DestroyImmediate(previous.gameObject);
        var canvas = screen.hpText != null ? screen.hpText.GetComponentInParent<Canvas>()
            : Components<Canvas>(scene).FirstOrDefault();
        Require(canvas != null, "Village Canvas is missing.");
        var story = InstantiatePrefab(StoryPath, canvas.transform).GetComponent<VillageStoryPanelUI>();
        story.template = screen.template;
        story.transform.SetAsLastSibling();
        PrefabUtility.RecordPrefabInstancePropertyModifications(story);
        EditorUtility.SetDirty(screen);
    }

    static void ApplyCombat(Scene scene)
    {
        var loadout = Components<PlayerWeaponLoadout>(scene).FirstOrDefault();
        var flow = Components<BattleFlowController>(scene).FirstOrDefault();
        Require(loadout != null && flow != null, "CombatScene player loadout or battle flow is missing.");
        var serialized = new SerializedObject(loadout);
        var libraryField = serialized.FindProperty("weaponLibrary");
        Require(libraryField != null, "PlayerWeaponLoadout.weaponLibrary is missing.");
        libraryField.objectReferenceValue = library;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach (var controller in Components<BattleWeaponController>(scene))
        {
            serialized = new SerializedObject(controller);
            var flowField = serialized.FindProperty("battleFlow");
            Require(flowField != null, "BattleWeaponController.battleFlow is missing.");
            flowField.objectReferenceValue = flow;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        ApplyBattleWeaponPanel(scene, loadout, flow);
    }

    static void ApplyBattleWeaponPanel(Scene scene, PlayerWeaponLoadout loadout, BattleFlowController flow)
    {
        var controller = Components<BattleWeaponController>(scene).FirstOrDefault();
        Require(controller != null, "BattleWeaponController is missing.");
        var existing = Components<BattleLoadoutView>(scene).FirstOrDefault();
        Button[] buttons;
        GameObject old;
        if (existing != null)
        {
            buttons = existing.slotButtons;
            old = existing.gameObject;
        }
        else
        {
            buttons = new Button[EquipmentRules.SlotCount];
            foreach (var button in Components<Button>(scene))
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentMethodName(i) != "SwitchWeapon") continue;
                    var serialized = new SerializedObject(button);
                    var slotProperty = serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls.Array.data["
                        + i + "].m_Arguments.m_IntArgument");
                    Require(slotProperty != null, "Cannot read battle weapon slot.");
                    int slot = slotProperty.intValue;
                    Require(slot >= 0 && slot < buttons.Length && buttons[slot] == null,
                        "Battle weapon button slots must be unique indexes 0 to 3.");
                    buttons[slot] = button;
                }
            Require(buttons.All(button => button != null), "Four SwitchWeapon buttons are required.");
            var ancestor = buttons[0].transform;
            while (ancestor != null && buttons.Any(button => !button.transform.IsChildOf(ancestor)))
                ancestor = ancestor.parent;
            Require(ancestor != null && ancestor is RectTransform && ancestor.GetComponent<Canvas>() == null,
                "Battle weapon buttons must share their own UI panel, not the scene Canvas.");
            old = ancestor.gameObject;
            existing = old.AddComponent<BattleLoadoutView>();
        }
        Require(buttons != null && buttons.Length == EquipmentRules.SlotCount && buttons.All(button => button != null),
            "Battle loadout requires four valid buttons.");
        existing.slotButtons = buttons;
        existing.slotLabels = buttons.Select(button => button.GetComponentInChildren<TMP_Text>(true)).ToArray();
        Require(existing.slotLabels.All(label => label != null), "Battle weapon button labels are missing.");
        existing.loadout = loadout;
        existing.controller = controller;
        existing.flow = flow;
        existing.font = font;
        for (int i = 0; i < buttons.Length; i++)
        {
            while (buttons[i].onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(buttons[i].onClick, 0);
            var label = existing.slotLabels[i];
            label.font = font;
            label.enableAutoSizing = true;
            label.fontSizeMax = Mathf.Clamp(label.fontSize, 14, 32);
            label.fontSizeMin = 14;
            var weapons = loadout.EquippedWeapons;
            var weapon = weapons != null && i < weapons.Length ? weapons[i] : null;
            label.text = weapon != null ? weapon.WeaponName : "空槽";
            EditorUtility.SetDirty(buttons[i]);
            EditorUtility.SetDirty(label);
        }
        var originalReferences = CaptureHierarchyReferences(old);
        SavePrefab(old, BattleWeaponPath);
        var replacement = InstantiatePrefab(BattleWeaponPath, old.transform.parent);
        replacement.name = old.name;
        replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
        CopyRect((RectTransform)old.transform, (RectTransform)replacement.transform);
        var mapping = HierarchyMapping(old, replacement);
        RestoreHierarchyReferences(originalReferences, mapping);
        RemapReferences(scene, mapping);
        var view = replacement.GetComponent<BattleLoadoutView>();
        view.loadout = loadout;
        view.controller = controller;
        view.flow = flow;
        view.font = font;
        PrefabUtility.RecordPrefabInstancePropertyModifications(view);
        PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
        PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
        Object.DestroyImmediate(old);
    }

    static void ApplyIntro(Scene scene)
    {
        var manager = Components<DialogueManager>(scene).FirstOrDefault();
        Require(manager != null && manager.ui != null, "Intro dialogue manager/UI is missing.");
        var oldCanvas = Components<Canvas>(scene).FirstOrDefault();
        Require(oldCanvas != null, "Intro Canvas is missing.");
        GameObject old = oldCanvas.gameObject;
        // Existing Intro logic objects and authored data remain outside this reusable UI prefab.
        string connected = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old);
        if (connected != IntroPath)
        {
            var originalReferences = CaptureHierarchyReferences(old);
            SavePrefab(old, IntroPath);
            var replacement = InstantiatePrefab(IntroPath, old.transform.parent);
            replacement.name = old.name;
            replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
            CopyRect((RectTransform)old.transform, (RectTransform)replacement.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
            var mapping = HierarchyMapping(old, replacement);
            RestoreHierarchyReferences(originalReferences, mapping);
            RemapReferences(scene, mapping);
            Object.DestroyImmediate(old);
            old = replacement;
        }
        manager.ui.manager = manager;
        manager.ui.visibleRoot = old;
        EditorUtility.SetDirty(manager.ui);
        EditorUtility.SetDirty(manager);
    }

    static void ValidateScene(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                    "Missing script: " + scene.path + "/" + transform.name);
        foreach (var component in Components<Component>(scene))
        {
            if (component == null) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    throw new InvalidOperationException("Unresolved reference: " + scene.path + "/"
                        + component.name + "." + property.propertyPath);
        }
        if (scene.path.EndsWith("/MainMap.unity"))
        {
            var player = Components<MapPlayerPanelView>(scene).First();
            ValidateEquipment(player.equipmentPanel);
            foreach (var view in Components<WorldMapSceneView>(scene)) Require(view.sinText != null, "Missing map sin text.");
        }
        else if (scene.path.EndsWith("/Village.unity"))
        {
            var screen = Components<VillageScreen>(scene).First();
            ValidateEquipment(screen.panelBag.GetComponent<EquipmentPanelView>());
            var story = Components<VillageStoryPanelUI>(scene).Single();
            string storyPrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(story.gameObject);
            Require(storyPrefab == StoryPath
                || (storyPrefab == Folder + "/TavernPanel.prefab" && screen.panelEvent == story.gameObject),
                "Village story UI must be a connected story or tavern prefab.");
            Require(story.questText != null && story.talkButton != null && story.template != null, "Story HUD references are missing.");
            Require(story.welcomeDialogue != null && story.commissionDialogue != null && story.afterTutorialDialogue != null
                && story.turnInDialogue != null && story.afterTurnInDialogue != null, "Story dialogues are missing.");
            ValidateDialogue(story.dialogueManager);
        }
        else if (scene.path.EndsWith("/CombatScene.unity"))
        {
            ValidateBattleLoadout(Components<BattleLoadoutView>(scene).Single());
            var loadout = Components<PlayerWeaponLoadout>(scene).First();
            Require(new SerializedObject(loadout).FindProperty("weaponLibrary").objectReferenceValue != null, "Combat weapon library is missing.");
            foreach (var controller in Components<BattleWeaponController>(scene))
                Require(new SerializedObject(controller).FindProperty("battleFlow").objectReferenceValue != null, "Battle weapon flow reference is missing.");
        }
        else if (scene.path.EndsWith("/Intro.unity"))
        {
            var manager = Components<DialogueManager>(scene).First();
            ValidateDialogue(manager);
            Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(manager.ui.visibleRoot) == IntroPath,
                "Intro Canvas must be a connected prefab.");
            Require(Components<DialogueTester>(scene).First().introData != null, "Intro data is missing.");
        }
    }

    static void ValidateEquipment(EquipmentPanelView view)
    {
        Require(view != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject) == EquipmentPath,
            "Equipment window must be a connected EquipmentPanel prefab.");
        Require(view.slotButtons != null && view.slotButtons.Length == EquipmentRules.SlotCount
            && view.slotButtons.All(button => button != null), "Four equipment slots are required.");
        Require(view.window != null && view.visibility != null && view.panelToggler != null && view.rowButtonPrefab != null
            && view.ownedRowsParent != null && view.ownedWeaponsScroll != null && view.weaponLibrary != null
            && view.healthText != null && view.statusText != null && view.sinText != null && view.itemsText != null
            && view.questText != null && view.equipButton != null && view.unequipButton != null
            && view.selectWeaponButton != null && view.closeButton != null, "Equipment panel references are missing.");
    }

    static void ValidateDialogue(DialogueManager manager)
    {
        Require(manager != null && manager.ui != null && manager.ui.manager == manager && manager.ui.visibleRoot != null,
            "Dialogue manager/UI references are missing.");
        var ui = manager.ui;
        Require(ui.speakerText != null && ui.contentText != null && ui.avatarImage != null
            && ui.continueButton != null && ui.choiceParent != null && ui.choicePrefab != null,
            "Dialogue UI fields are missing.");
    }

    static IEnumerable<T> Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    static GameObject Find(Scene scene, string name) =>
        Components<Transform>(scene).FirstOrDefault(transform => transform.name == name)?.gameObject;

    static Dictionary<Object, Object> RootMapping(GameObject old, GameObject replacement)
    {
        var map = new Dictionary<Object, Object>();
        Add(map, old, replacement);
        Add(map, old.transform, replacement.transform);
        return map;
    }

    static void ValidateBattleLoadout(BattleLoadoutView view)
    {
        Require(view != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject) == BattleWeaponPath,
            "Battle weapon UI must be a connected BattleWeaponPanel prefab.");
        Require(view.slotButtons != null && view.slotButtons.Length == EquipmentRules.SlotCount
            && view.slotButtons.All(button => button != null) && view.slotLabels != null
            && view.slotLabels.Length == EquipmentRules.SlotCount && view.slotLabels.All(label => label != null),
            "Battle weapon UI requires four buttons and labels.");
        Require(view.loadout != null && view.controller != null && view.flow != null && view.font != null,
            "Battle weapon UI references are missing.");
        foreach (var button in view.slotButtons)
            Require(button.onClick.GetPersistentEventCount() == 0, "Battle weapon buttons must bind only once through their view.");
    }

    static Dictionary<MonoBehaviour, Dictionary<string, Object>> CaptureHierarchyReferences(GameObject root)
    {
        var snapshot = new Dictionary<MonoBehaviour, Dictionary<string, Object>>();
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            var fields = new Dictionary<string, Object>();
            var property = new SerializedObject(component).GetIterator();
            while (property.Next(true))
            {
                if (!IsUserObjectReference(property)) continue;
                var reference = property.objectReferenceValue;
                // Asset references survive prefab serialization; scene references require instance overrides.
                if (reference != null && !EditorUtility.IsPersistent(reference))
                    fields[property.propertyPath] = reference;
            }
            if (fields.Count > 0) snapshot[component] = fields;
        }
        return snapshot;
    }

    static void RestoreHierarchyReferences(Dictionary<MonoBehaviour, Dictionary<string, Object>> snapshot,
        Dictionary<Object, Object> mapping)
    {
        foreach (var entry in snapshot)
        {
            Require(mapping.TryGetValue(entry.Key, out Object target) && target is MonoBehaviour,
                "Cannot map copied UI component " + entry.Key.name);
            var component = (MonoBehaviour)target;
            var serialized = new SerializedObject(component);
            foreach (var field in entry.Value)
            {
                var property = serialized.FindProperty(field.Key);
                Require(property != null && property.propertyType == SerializedPropertyType.ObjectReference,
                    "Cannot restore copied UI field " + field.Key);
                property.objectReferenceValue = mapping.TryGetValue(field.Value, out Object replacement) ? replacement : field.Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    static bool IsUserObjectReference(SerializedProperty property)
    {
        if (property.propertyType != SerializedPropertyType.ObjectReference) return false;
        string path = property.propertyPath;
        return path != "m_GameObject" && path != "m_Script" && path != "m_CorrespondingSourceObject"
            && path != "m_PrefabInstance" && path != "m_PrefabAsset";
    }

    static Dictionary<Object, Object> HierarchyMapping(GameObject old, GameObject replacement)
    {
        var map = RootMapping(old, replacement);
        var oldTransforms = old.GetComponentsInChildren<Transform>(true);
        var newTransforms = replacement.GetComponentsInChildren<Transform>(true);
        Require(oldTransforms.Length == newTransforms.Length, "Prefab hierarchy changed while copying Intro UI.");
        for (int i = 0; i < oldTransforms.Length; i++)
        {
            Add(map, oldTransforms[i].gameObject, newTransforms[i].gameObject);
            var oldComponents = oldTransforms[i].GetComponents<Component>();
            var newComponents = newTransforms[i].GetComponents<Component>();
            Require(oldComponents.Length == newComponents.Length, "Prefab component count changed while copying Intro UI.");
            for (int j = 0; j < oldComponents.Length; j++) Add(map, oldComponents[j], newComponents[j]);
        }
        return map;
    }

    static void Add(Dictionary<Object, Object> map, Object old, Object replacement)
    {
        if (old != null && replacement != null) map[old] = replacement;
    }

    static void RemapReferences(Scene scene, Dictionary<Object, Object> mapping)
    {
        var replacedRoots = mapping.Keys.OfType<GameObject>().ToArray();
        foreach (var component in Components<MonoBehaviour>(scene).ToArray())
        {
            if (component == null || replacedRoots.Any(root => component.transform == root.transform
                || component.transform.IsChildOf(root.transform))) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_GameObject") continue;
                Object reference = property.objectReferenceValue;
                if (reference == null || !mapping.TryGetValue(reference, out Object replacement)) continue;
                property.objectReferenceValue = replacement;
                changed = true;
            }
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
    }

    static void CopyRect(RectTransform old, RectTransform replacement)
    {
        replacement.anchorMin = old.anchorMin;
        replacement.anchorMax = old.anchorMax;
        replacement.pivot = old.pivot;
        replacement.anchoredPosition = old.anchoredPosition;
        replacement.sizeDelta = old.sizeDelta;
        replacement.localScale = old.localScale;
        replacement.localRotation = old.localRotation;
    }

    static GameObject InstantiatePrefab(string path, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(prefab != null, "Cannot load prefab " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        if (parent == null && preview.IsValid()) SceneManager.MoveGameObjectToScene(instance, preview);
        return instance;
    }

    static void SavePrefab(GameObject root, string path)
    {
        var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
        Require(asset != null, "Cannot create prefab " + path);
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        if (parent != null) rect.SetParent(parent, false);
        else if (preview.IsValid()) SceneManager.MoveGameObjectToScene(rect.gameObject, preview);
        return rect;
    }

    static TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.color = Ink;
        label.text = text;
        label.richText = false;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        Box(label.rectTransform, new Vector2(0, 1), position, size, new Vector2(0, 1));
        return label;
    }

    static Button MakeButton(string name, Transform parent, string text, Vector2 size, float fontSize)
    {
        var rect = Rect(name, parent);
        rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonColor;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = Label("Label", rect, text, Vector2.zero, size, fontSize);
        Stretch(label.rectTransform);
        label.margin = new Vector4(8, 2, 8, 2);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    static Button ButtonAt(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var button = MakeButton(name, parent, text, size, fontSize);
        Box((RectTransform)button.transform, new Vector2(0, 1), position, size, new Vector2(0, 1));
        return button;
    }

    static ScrollRect Scroll(string name, Transform parent, Vector2 position, Vector2 size, out RectTransform content)
    {
        var root = Rect(name, parent);
        Box(root, new Vector2(0, 1), position, size, new Vector2(0, 1));
        root.gameObject.AddComponent<Image>().color = new Color32(224, 221, 211, 255);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        var viewport = Rect("Viewport", root);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        return scroll;
    }

    static void Click(Button button, UnityAction action)
    {
        while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
        if (PrefabUtility.IsPartOfPrefabInstance(button)) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
    }

    static void Box(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
