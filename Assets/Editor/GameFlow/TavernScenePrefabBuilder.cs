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

/// <summary>Builds an editable list/detail tavern UI prefab with temporary character art.</summary>
public static class TavernScenePrefabBuilder
{
    const string ScenePath = "Assets/Scenes/Village.unity";
    const string PrefabPath = "Assets/Prefabs/UI/TavernPanel.prefab";

    sealed class ReferenceSnapshot
    {
        public Component owner;
        public readonly Dictionary<string, Object> fields = new Dictionary<string, Object>();
    }

    const string ArtFolder = "Assets/Sprite/UI/TavernTemp";
    static readonly Color Ink = new Color32(58, 47, 39, 255);
    static readonly Color MutedInk = new Color32(111, 92, 72, 255);
    static TMP_FontAsset font;
    static Sprite paperFrame;

    [MenuItem("Tools/Game Flow/Build Tavern Prefab")]
    public static void Build()
    {
        RequireCleanEditor();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Huiwen.asset");
            Require(font != null, "Tavern needs the existing Huiwen font.");
            var chief = ImportSprite(ArtFolder + "/TavernChief-v1.png", false);
            var villager = ImportSprite(ArtFolder + "/TavernVillager-v1.png", false);
            var traveler = ImportSprite(ArtFolder + "/TavernTraveler-v1.png", false);
            paperFrame = ImportSprite(ArtFolder + "/TavernPaperFrame-v1.png", true);
            var chatter = EnsureDialogue("TavernChatter", "tavern_chatter", "村民", villager,
                "村外近来不太平。出门之前，先把背包里的武器整理妥当。",
                "如果受了伤，就回旅馆歇一歇。养足精神再出发。");
            var rumor = EnsureDialogue("TavernRumor", "tavern_rumor", "旅人", traveler,
                "我在这里歇歇脚。近来村外不太平，路上可要当心。",
                "出门前检查好装备；受伤了，也别忘了村里的旅馆。");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var screen = Components<VillageScreen>(scene).Single();
            var story = Components<VillageStoryPanelUI>(scene).Single();
            Require(screen.panelRoot != null && screen.panelEvent != null, "Village event panels are missing.");
            Require(story.talkButton != null && story.questText != null && story.dialogueManager != null,
                "Village story references are missing.");
            var root = story.gameObject;
            var oldWindow = root == screen.panelEvent ? story.eventListRoot : screen.panelEvent;
            Require(oldWindow != null && oldWindow != root, "Tavern needs a separate event-list window.");
            if (PrefabUtility.IsAnyPrefabInstanceRoot(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = "TavernPanel";
            root.transform.SetParent(screen.panelRoot.transform, false);
            Stretch((RectTransform)root.transform);
            root.transform.SetAsLastSibling();
            // The legacy separate card may still be outside the event window.
            if (!story.questText.transform.IsChildOf(oldWindow.transform))
            {
                var oldCard = story.questText.transform.parent.gameObject;
                Require(oldCard != root && oldCard != story.dialogueManager.gameObject,
                    "The original story card cannot be replaced safely.");
                oldCard.transform.SetParent(oldWindow.transform, false);
            }
            var oldQuest = story.questText;
            var oldTalk = story.talkButton;
            var oldLabel = story.talkLabel;
            var oldMessage = story.messageText;
            var browser = root.GetComponent<TavernEventBrowserUI>();
            if (browser == null) browser = root.AddComponent<TavernEventBrowserUI>();
            BuildBrowser(root, screen, story, browser, chief, villager, traveler, chatter, rumor);
            var window = story.eventListRoot;
            var replacements = new Dictionary<Object, Object>();
            AddReplacement(replacements, oldWindow, window);
            AddReplacement(replacements, oldQuest.gameObject, story.questText.gameObject);
            AddReplacement(replacements, oldTalk.gameObject, story.talkButton.gameObject);
            if (oldLabel != null) AddReplacement(replacements, oldLabel.gameObject, story.talkLabel.gameObject);
            if (oldMessage != null) AddReplacement(replacements, oldMessage.gameObject, story.messageText.gameObject);
            RemapReplacedWindow(scene, oldWindow, replacements);
            Object.DestroyImmediate(oldWindow);
            window.name = "Window";
            story.template = screen.template;
            story.externalInteraction = true;
            foreach (var data in new[] { story.welcomeDialogue, story.commissionDialogue, story.afterTutorialDialogue,
                story.turnInDialogue, story.afterTurnInDialogue })
                FillMissingPortraits(data, chief, villager);
            var dialogueRoot = story.dialogueManager.gameObject;
            Require(dialogueRoot.transform.parent == root.transform,
                "Dialogue must remain a direct full-screen tavern child.");
            Stretch((RectTransform)dialogueRoot.transform);
            dialogueRoot.transform.SetAsLastSibling();
            var dialogueUI = story.dialogueManager.ui;
            Require(dialogueUI != null && dialogueUI.visibleRoot != null, "Tavern dialogue UI is missing.");
            dialogueUI.visibleRoot.SetActive(false);
            var shade = root.GetComponent<Image>();
            if (shade == null) shade = root.AddComponent<Image>();
            shade.color = new Color(0.045f, 0.035f, 0.025f, 0.73f);
            shade.raycastTarget = true;
            EnsureSorting(root);
            root.SetActive(false);
            EnsureFolder();
            var references = CaptureReferences(scene, root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Require(prefab != null, "Cannot save tavern prefab.");
            CopySerializedSorting(root.GetComponent<Canvas>(), prefab.GetComponent<Canvas>());
            Require(PrefabUtility.SavePrefabAsset(prefab) != null, "Cannot save native tavern sorting.");
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform.parent);
            Require(replacement != null, "Cannot instantiate tavern prefab.");
            replacement.name = root.name;
            replacement.transform.SetSiblingIndex(root.transform.GetSiblingIndex());
            RestoreReferences(references, MapHierarchy(root, replacement));
            Object.DestroyImmediate(root);
            var newStory = replacement.GetComponent<VillageStoryPanelUI>();
            screen.panelEvent = replacement;
            screen.focusOption = newStory.talkButton.gameObject;
            Bind(screen.focusTavern.GetComponent<Button>(), screen.OpenTavern);
            Bind(Child<Button>(replacement, "Btn_EventClose"), screen.ClosePanels);
            EnsureSorting(replacement);
            replacement.SetActive(false);
            EditorUtility.SetDirty(screen);
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(newStory);
            if (PrefabUtility.IsPartOfPrefabInstance(screen))
                PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
            ValidateScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Cannot save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Tavern list/detail prefab built successfully; only Village.unity was updated.");
        }
        finally { RestoreSavedSceneSetup(setup); }
    }

    static void BuildBrowser(GameObject root, VillageScreen screen, VillageStoryPanelUI story,
        TavernEventBrowserUI browser, Sprite chief, Sprite villager, Sprite traveler,
        DialogueData chatter, DialogueData rumor)
    {
        var window = Rect("WindowReplacement", root.transform);
        Center(window, new Vector2(1120, 860));
        var background = Paper("Paper", window, Vector2.zero, new Vector2(1120, 860));
        Stretch(background.rectTransform);
        background.color = Color.white;
        Label("Title", window, "酒馆", new Vector2(42, -24), new Vector2(340, 56), 40);
        Label("Subtitle", window, "歇脚 · 交谈 · 村中诸事", new Vector2(44, -79), new Vector2(740, 28), 21, MutedInk);
        var close = ButtonAt("Btn_EventClose", window, "关闭", new Vector2(978, -37), new Vector2(98, 44), 23);
        close.GetComponent<Image>().color = new Color32(238, 224, 196, 255);
        browser.resourcesText = Label("Resources", window, "第 1 天 早晨    生命 100/100    罪恶 40/100    罪骸 0",
            new Vector2(46, -115), new Vector2(1026, 27), 20, MutedInk);
        Rule("HeaderRule", window, new Vector2(42, -153), new Vector2(1034, 2));
        Label("ListCaption", window, "人物与事件 · 可滚动", new Vector2(48, -166), new Vector2(324, 30), 23, MutedInk);

        browser.story = story;
        browser.villageScreen = screen;
        browser.chiefPortrait = chief;
        browser.villagerPortrait = villager;
        browser.travelerPortrait = traveler;
        browser.villageChatter = chatter;
        browser.tavernRumor = rumor;
        browser.eventButtons = new Button[3];
        browser.eventPortraits = new Image[3];
        browser.eventTitles = new TMP_Text[3];
        browser.eventSummaries = new TMP_Text[3];
        browser.selectionFrames = new Image[3];

        var scrollRect = Rect("EventScroll", window);
        Place(scrollRect, new Vector2(42, -203), new Vector2(348, 426));
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34;
        var viewport = Rect("Viewport", scrollRect);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1, 1, 1, 0.01f);
        viewportImage.raycastTarget = true;
        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(2, 6, 2, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        browser.eventListContent = content;

        var titles = new[] { "村长委托", "村民闲谈", "酒馆见闻" };
        var summaries = new[] { "交谈，了解村里的委托。", "听听村民的叮嘱。", "与歇脚的旅人聊一聊。" };
        var portraits = new[] { villager, villager, traveler };
        for (int i = 0; i < 3; i++)
        {
            var row = EventRow("Event_" + i, content, portraits[i], titles[i], summaries[i]);
            browser.eventButtons[i] = row;
            browser.eventPortraits[i] = Child<Image>(row.gameObject, "Portrait");
            browser.eventTitles[i] = Child<TMP_Text>(row.gameObject, "EventTitle");
            browser.eventSummaries[i] = Child<TMP_Text>(row.gameObject, "EventSummary");
            browser.selectionFrames[i] = Child<Image>(row.gameObject, "SelectionFrame");
            browser.selectionFrames[i].enabled = i == 0;
        }
        var caption = Label("TimedCaption", content, "限时事件 · 随时间出现", Vector2.zero, new Vector2(336, 34), 21, MutedInk);
        LayoutHeight(caption.gameObject, 34);
        browser.timedEventEmptyText = Label("TimedEmpty", content, "此刻没有待处理的限时事件。\n推进时间后可能出现新的事情。",
            Vector2.zero, new Vector2(336, 96), 18, MutedInk);
        browser.timedEventEmptyText.alignment = TextAlignmentOptions.TopLeft;
        LayoutHeight(browser.timedEventEmptyText.gameObject, 96);
        browser.timedEventTemplate = EventRow("TimedEventTemplate", content, villager, "限时事件", "限时 · 至第 4 天早晨前");
        browser.timedEventTemplate.gameObject.SetActive(false);
        browser.waitButton = ButtonAt("WaitOneUnit", window, "等候 1 单位时间", new Vector2(46, -643), new Vector2(338, 44), 22);
        browser.waitButton.GetComponent<Image>().color = new Color32(236, 223, 198, 255);

        var detail = Rect("Detail", window);
        Place(detail, new Vector2(414, -174), new Vector2(664, 514));
        var detailPaper = Paper("DetailPaper", detail, Vector2.zero, new Vector2(664, 514));
        detailPaper.color = new Color32(250, 243, 225, 255);
        browser.detailTitle = Label("DetailTitle", detail, "村长委托", new Vector2(28, -21), new Vector2(607, 44), 32);
        browser.detailMetaText = Label("DetailMeta", detail, "交谈不消耗时间", new Vector2(29, -71), new Vector2(605, 26), 16, MutedInk);
        Rule("DetailRule", detail, new Vector2(29, -105), new Vector2(605, 1));
        browser.detailPortrait = Portrait("DetailPortrait", detail, villager, new Vector2(20, -122), new Vector2(184, 134));
        browser.detailDescription = Label("DetailDescription", detail,
            "村民和村长聚在酒馆里。\n\n先与他们交谈，了解村里的情况与委托。",
            new Vector2(228, -122), new Vector2(404, 134), 20);
        browser.detailDescription.alignment = TextAlignmentOptions.TopLeft;
        browser.actionButton = ButtonAt("Talk", detail, "开始交谈", new Vector2(270, -426), new Vector2(358, 56), 25);
        browser.actionButton.GetComponent<Image>().color = new Color32(226, 202, 157, 255);
        browser.actionLabel = browser.actionButton.GetComponentInChildren<TMP_Text>();
        browser.choiceButtons = new Button[3];
        browser.choiceLabels = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            var choice = ButtonAt("TimedChoice_" + i, detail, "确认选项\n1 单位时间 · 罪恶 -15",
                new Vector2(26, -268 - i * 78), new Vector2(612, 72), 18);
            choice.GetComponent<Image>().color = new Color32(235, 218, 184, 255);
            browser.choiceButtons[i] = choice;
            browser.choiceLabels[i] = choice.GetComponentInChildren<TMP_Text>();
            browser.choiceLabels[i].margin = new Vector4(12, 3, 12, 3);
            choice.gameObject.SetActive(false);
        }
        story.talkButton = browser.actionButton;
        story.talkLabel = browser.actionLabel;
        story.messageText = Label("Message", window, "", new Vector2(46, -700), new Vector2(1025, 68), 17, MutedInk);
        story.messageText.alignment = TextAlignmentOptions.TopLeft;
        story.messageText.enableAutoSizing = true;
        story.messageText.fontSizeMin = 14;
        story.messageText.fontSizeMax = 17;
        Rule("FooterRule", window, new Vector2(42, -778), new Vector2(1034, 2));
        story.questText = Label("CurrentGoal", window, "当前目标\n与酒馆里的村民交谈。",
            new Vector2(46, -790), new Vector2(710, 48), 19);
        story.questText.alignment = TextAlignmentOptions.TopLeft;
        browser.rewardHistoryText = Label("RewardHistory", window, "",
            new Vector2(782, -790), new Vector2(294, 48), 16, MutedInk);
        browser.rewardHistoryText.alignment = TextAlignmentOptions.TopLeft;
        story.eventListRoot = window.gameObject;
        story.externalInteraction = true;
    }

    static Button EventRow(string name, Transform parent, Sprite portrait, string title, string summary)
    {
        var row = ButtonAt(name, parent, "", Vector2.zero, new Vector2(336, 84), 22);
        row.GetComponent<Image>().pixelsPerUnitMultiplier = 12;
        Object.DestroyImmediate(row.GetComponentInChildren<TMP_Text>().gameObject);
        LayoutHeight(row.gameObject, 84);
        var frame = Paper("SelectionFrame", row.transform, Vector2.zero, new Vector2(336, 84));
        Stretch(frame.rectTransform);
        frame.color = new Color32(168, 110, 56, 255);
        frame.fillCenter = false;
        frame.pixelsPerUnitMultiplier = 12;
        frame.enabled = false;
        Portrait("Portrait", row.transform, portrait, new Vector2(8, -6), new Vector2(64, 72));
        Label("EventTitle", row.transform, title, new Vector2(84, -12), new Vector2(234, 26), 21);
        var text = Label("EventSummary", row.transform, summary, new Vector2(85, -40), new Vector2(229, 36), 16, MutedInk);
        text.alignment = TextAlignmentOptions.TopLeft;
        return row;
    }

    static void LayoutHeight(GameObject item, float height)
    {
        var element = item.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = height;
        element.flexibleHeight = 0;
    }

    static Sprite ImportSprite(string path, bool sliced)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Require(importer != null, "Missing temporary tavern art: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.spriteBorder = sliced ? new Vector4(144, 144, 144, 144) : Vector4.zero;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Require(sprite != null, "Cannot load temporary tavern sprite: " + path);
        return sprite;
    }

    static DialogueData EnsureDialogue(string name, string id, string speaker, Sprite portrait,
        string firstLine, string secondLine)
    {
        EnsureAssetFolder("Assets/Data/DialogueData");
        string path = "Assets/Data/DialogueData/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
        if (existing != null) return existing;
        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.name = name;
        data.dialogueId = id;
        data.completionEffect = StoryDialogueEffect.None;
        data.startNode = new DialogueNode { id = "node0", speaker = speaker, content = firstLine,
            avatar = portrait, choices = new List<DialogueChoice>(), nextNodeID = "node1" };
        data.nodes = new List<DialogueNode> {
            new DialogueNode { id = "node1", speaker = speaker, content = secondLine,
                avatar = portrait, choices = new List<DialogueChoice>(), nextNodeID = "" }
        };
        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    static void FillMissingPortraits(DialogueData data, Sprite chief, Sprite villager)
    {
        Require(data != null, "Existing village story dialogue is missing.");
        bool changed = false;
        var nodes = new List<DialogueNode>();
        if (data.startNode != null) nodes.Add(data.startNode);
        if (data.nodes != null) nodes.AddRange(data.nodes);
        foreach (var node in nodes)
        {
            if (node == null || node.avatar != null) continue;
            node.avatar = !string.IsNullOrEmpty(node.speaker) && node.speaker.Contains("村长") ? chief : villager;
            changed = true;
        }
        if (changed) EditorUtility.SetDirty(data);
    }

    static void EnsureAssetFolder(string path)
    {
        string current = "Assets";
        foreach (string segment in path.Split('/').Skip(1))
        {
            string next = current + "/" + segment;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
            current = next;
        }
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(parent, false);
        return rect;
    }

    static Image Paper(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent);
        Place(rect, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = paperFrame;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 6;
        image.raycastTarget = false;
        return image;
    }

    static Image Portrait(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent);
        Place(rect, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        CharacterPortraitStyle.Apply(image, sprite);
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text Label(string name, Transform parent, string text, Vector2 position,
        Vector2 size, float fontSize, Color? color = null)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        Place(label.rectTransform, position, size);
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color ?? Ink;
        label.richText = false;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    static Button ButtonAt(string name, Transform parent, string text, Vector2 position,
        Vector2 size, float fontSize)
    {
        var image = Paper(name, parent, position, size);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color32(250, 233, 203, 255);
        colors.pressedColor = new Color32(222, 195, 153, 255);
        colors.selectedColor = new Color32(250, 233, 203, 255);
        colors.disabledColor = new Color(1, 1, 1, 0.48f);
        button.colors = colors;
        var label = Label("Label", button.transform, text, Vector2.zero, size, fontSize);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    static void Rule(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent);
        Place(rect, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color32(164, 137, 102, 110);
        image.raycastTarget = false;
    }

    static void AddReplacement(Dictionary<Object, Object> mapping, GameObject old, GameObject replacement)
    {
        if (old == null || replacement == null) return;
        mapping[old] = replacement;
        foreach (var component in old.GetComponents<Component>())
        {
            var counterpart = replacement.GetComponent(component.GetType());
            if (counterpart != null) mapping[component] = counterpart;
        }
    }

    static void RemapReplacedWindow(Scene scene, GameObject old, Dictionary<Object, Object> mapping)
    {
        var obsolete = HierarchyObjects(old);
        foreach (var component in Components<Component>(scene))
        {
            if (component == null || component is Transform || obsolete.Contains(component)) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (!IsUserReference(property) || property.objectReferenceValue == null
                    || !obsolete.Contains(property.objectReferenceValue)) continue;
                Require(mapping.TryGetValue(property.objectReferenceValue, out Object replacement),
                    "Cannot remap replaced tavern control: " + component.name + "." + property.propertyPath);
                property.objectReferenceValue = replacement;
                changed = true;
            }
            if (!changed) continue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    [MenuItem("Tools/Game Flow/Validate Tavern Prefab")]
    public static void Validate()
    {
        RequireCleanEditor();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            ValidateScene(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single));
            Debug.Log("Tavern prefab and village references are valid.");
        }
        finally { RestoreSavedSceneSetup(setup); }
    }

    static void RequireCleanEditor()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before editing the tavern prefab.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            Require(!scene.isDirty, "Save or discard your unsaved scene changes first: " + scene.path);
        }
    }

    static void RestoreSavedSceneSetup(SceneSetup[] setup)
    {
        if (setup == null || setup.Length == 0 || setup.Any(item => string.IsNullOrEmpty(item.path))
            || !setup.Any(item => item.isActive && item.isLoaded)) return;
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = Vector3.zero;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition3D = new Vector3(position.x, position.y, 0);
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.anchoredPosition3D = Vector3.zero;
    }

    static void EnsureSorting(GameObject panel)
    {
        var parentCanvas = panel.transform.parent != null ? panel.transform.parent.GetComponentInParent<Canvas>() : null;
        Require(parentCanvas != null, "Tavern needs a parent Canvas.");
        var rootCanvas = parentCanvas.rootCanvas;
        var inactive = new List<GameObject>();
        for (var current = panel.transform; current != null; current = current.parent)
            if (!current.gameObject.activeSelf) inactive.Add(current.gameObject);
        Canvas canvas = null;
        try
        {
            for (int i = inactive.Count - 1; i >= 0; i--) inactive[i].SetActive(true);
            canvas = panel.GetComponent<Canvas>();
            if (canvas == null) canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = rootCanvas.renderMode;
            canvas.overrideSorting = true;
            canvas.sortingLayerID = rootCanvas.sortingLayerID;
            canvas.sortingOrder = rootCanvas.sortingOrder + 2;
            if (panel.GetComponent<GraphicRaycaster>() == null) panel.AddComponent<GraphicRaycaster>();
        }
        finally { foreach (var item in inactive) item.SetActive(false); }
        WriteSerializedSorting(canvas, (int)rootCanvas.renderMode, rootCanvas.sortingLayerID, rootCanvas.sortingOrder + 2);
        if (PrefabUtility.IsPartOfPrefabInstance(canvas)) PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
    }

    static void WriteSerializedSorting(Canvas canvas, int mode, int layer, int order)
    {
        Require(canvas != null, "Tavern Canvas is missing.");
        var serialized = new SerializedObject(canvas);
        serialized.FindProperty("m_RenderMode").intValue = mode;
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
        Require(result != null, "Missing tavern UI child " + name);
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
                Require(component != null, "Missing script in tavern: " + transform.name);
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
        Require(oldTransforms.Length == newTransforms.Length, "Copied tavern hierarchy does not match.");
        var mapping = new Dictionary<Object, Object>();
        for (int i = 0; i < oldTransforms.Length; i++)
        {
            Require(oldTransforms[i].name == newTransforms[i].name, "Copied tavern child order does not match.");
            mapping[oldTransforms[i].gameObject] = newTransforms[i].gameObject;
            var oldComponents = oldTransforms[i].GetComponents<Component>();
            var newComponents = newTransforms[i].GetComponents<Component>();
            Require(oldComponents.Length == newComponents.Length, "Copied tavern component count does not match.");
            for (int j = 0; j < oldComponents.Length; j++)
            {
                Require(oldComponents[j] != null && newComponents[j] != null
                    && oldComponents[j].GetType() == newComponents[j].GetType(), "Copied tavern component types do not match.");
                mapping[oldComponents[j]] = newComponents[j];
            }
        }
        return mapping;
    }

    static void RestoreReferences(List<ReferenceSnapshot> snapshots, Dictionary<Object, Object> mapping)
    {
        foreach (var snapshot in snapshots)
        {
            var owner = mapping.TryGetValue(snapshot.owner, out Object copiedOwner) ? (Component)copiedOwner : snapshot.owner;
            var serialized = new SerializedObject(owner);
            foreach (var field in snapshot.fields)
            {
                var property = serialized.FindProperty(field.Key);
                Require(property != null && IsUserReference(property), "Cannot restore tavern reference " + field.Key);
                property.objectReferenceValue = mapping.TryGetValue(field.Value, out Object copiedReference) ? copiedReference : field.Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
            if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }
    }

    static void Bind(Button button, UnityAction action)
    {
        Require(button != null, "Cannot bind a missing tavern button.");
        while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
        if (PrefabUtility.IsPartOfPrefabInstance(button)) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
    }

    static void ValidateScene(Scene scene)
    {
        var screen = Components<VillageScreen>(scene).Single();
        var story = Components<VillageStoryPanelUI>(scene).Single();
        Require(screen.panelEvent == story.gameObject && story.transform.parent == screen.panelRoot.transform,
            "Story must belong to the tavern modal under panelRoot.");
        Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(screen.panelEvent) == PrefabPath,
            "Tavern must be a connected TavernPanel prefab.");
        Require(story.eventListRoot != null && story.eventListRoot.transform.parent == story.transform,
            "Tavern event list window is missing.");
        Require(story.talkButton != null && screen.focusOption == story.talkButton.gameObject,
            "Tavern focus must point at the story action.");
        Require(story.questText != null && story.questText.transform.IsChildOf(story.eventListRoot.transform)
            && story.talkButton.transform.IsChildOf(story.eventListRoot.transform), "Story card must be inside the event list.");
        Require(story.dialogueManager != null && story.dialogueManager.transform.parent == story.transform
            && story.dialogueManager.ui != null && story.dialogueManager.ui.visibleRoot != null,
            "Tavern dialogue references are missing.");
        Require(story.welcomeDialogue != null && story.commissionDialogue != null && story.afterTutorialDialogue != null
            && story.turnInDialogue != null && story.afterTurnInDialogue != null && story.template == screen.template,
            "Existing village dialogue or map assets were not retained.");
        var canvas = screen.panelEvent.GetComponent<Canvas>();
        var parentCanvas = screen.panelEvent.transform.parent.GetComponentInParent<Canvas>();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(canvas != null && parentCanvas != null && HasSerializedOverride(canvas)
            && canvas.sortingOrder > parentCanvas.rootCanvas.sortingOrder
            && screen.panelEvent.GetComponent<GraphicRaycaster>() != null,
            "Tavern must receive clicks above the village canvas.");
        Require(asset != null && HasSerializedOverride(asset.GetComponent<Canvas>())
            && asset.GetComponent<GraphicRaycaster>() != null, "Tavern sorting components must belong to its prefab.");
        Require(screen.panelEvent.GetComponent<Image>() != null
            && screen.panelEvent.GetComponent<Image>().raycastTarget, "Tavern needs a full-screen modal shade.");
        ValidateButton(screen.focusTavern.GetComponent<Button>(), screen, nameof(VillageScreen.OpenTavern));
        ValidateBrowser(screen, story);
        ValidateButton(Child<Button>(screen.panelEvent, "Btn_EventClose"), screen, nameof(VillageScreen.ClosePanels));
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

    static void ValidateBrowser(VillageScreen screen, VillageStoryPanelUI story)
    {
        var browser = story.GetComponent<TavernEventBrowserUI>();
        Require(browser != null && browser.story == story && story.externalInteraction,
            "Tavern must use its list/detail browser as the single interaction owner.");
        Require(browser.eventButtons != null && browser.eventButtons.Length == 3
            && browser.eventPortraits != null && browser.eventPortraits.Length == 3
            && browser.eventTitles != null && browser.eventTitles.Length == 3
            && browser.eventSummaries != null && browser.eventSummaries.Length == 3
            && browser.selectionFrames != null && browser.selectionFrames.Length == 3,
            "Tavern requires exactly three permanent conversation rows.");
        var expectedFrame = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/TavernPaperFrame-v1.png");
        Require(expectedFrame != null && Child<Image>(story.eventListRoot, "Paper").sprite == expectedFrame
            && Child<Image>(story.eventListRoot, "Paper").type == Image.Type.Sliced,
            "The tavern window must use its real sliced temporary paper frame.");
        Require(browser.chiefPortrait != null && browser.villagerPortrait != null
            && browser.travelerPortrait != null, "All three temporary character portraits are required.");
        for (int i = 0; i < 3; i++)
        {
            Require(browser.eventButtons[i] != null && browser.eventPortraits[i] != null
                && browser.eventTitles[i] != null && browser.eventSummaries[i] != null
                && browser.selectionFrames[i] != null && browser.eventPortraits[i].sprite != null,
                "Incomplete tavern event row " + i);
            Require(browser.eventButtons[i].transform.IsChildOf(story.eventListRoot.transform)
                && browser.eventPortraits[i].transform.IsChildOf(browser.eventButtons[i].transform)
                && browser.selectionFrames[i].transform.IsChildOf(browser.eventButtons[i].transform),
                "Tavern event-row hierarchy is invalid.");
            Require(browser.eventButtons[i].onClick.GetPersistentEventCount() == 0,
                "Event rows must have one runtime listener and no persistent callback.");
        }
        Require(browser.detailTitle != null && browser.detailDescription != null
            && browser.detailPortrait != null && browser.detailPortrait.sprite != null
            && browser.actionLabel != null && browser.actionButton == story.talkButton
            && browser.actionLabel == story.talkLabel
            && browser.actionButton.onClick.GetPersistentEventCount() == 0,
            "The right-side detail/action controls are incomplete or have duplicate callbacks.");
        Require(browser.villageScreen == screen && browser.resourcesText != null && browser.detailMetaText != null
            && browser.rewardHistoryText != null && browser.eventListContent != null && browser.timedEventTemplate != null
            && !browser.timedEventTemplate.gameObject.activeSelf && browser.timedEventEmptyText != null && browser.waitButton != null,
            "Tavern timed-event list, template, status or wait controls are incomplete.");
        Require(browser.eventListContent.GetComponentInParent<ScrollRect>(true) != null
            && browser.eventListContent.GetComponent<VerticalLayoutGroup>() != null,
            "The event list must scroll when several events coexist.");
        Require(browser.choiceButtons != null && browser.choiceButtons.Length == 3
            && browser.choiceLabels != null && browser.choiceLabels.Length == 3,
            "Tavern requires three option slots for the conditional event.");
        foreach (var button in browser.choiceButtons)
            Require(button != null && button.onClick.GetPersistentEventCount() == 0,
                "Timed-event choices must have one runtime callback only.");
        Require(browser.waitButton.onClick.GetPersistentEventCount() == 0
            && browser.timedEventTemplate.onClick.GetPersistentEventCount() == 0,
            "Wait and timed-event rows must not have persistent callbacks.");
        Require(browser.villageChatter != null && browser.tavernRumor != null
            && browser.villageChatter.completionEffect == StoryDialogueEffect.None
            && browser.tavernRumor.completionEffect == StoryDialogueEffect.None,
            "Tavern side conversations must not progress the main story.");
        Require(screen.panelEvent.GetComponentsInChildren<Button>(true).Count(button => button.name == "Btn_EventClose") == 1,
            "Tavern must have one close button.");
        Require(!screen.panelEvent.GetComponentsInChildren<Transform>(true).Any(child =>
                child.name == "Btn_EventOption_A" || child.name == "Btn_EventOption_B"),
            "The old placeholder event buttons must be removed.");
    }

    static void ValidateButton(Button button, VillageScreen screen, string method)
    {
        Require(button != null && button.onClick.GetPersistentEventCount() == 1
            && button.onClick.GetPersistentTarget(0) == screen && button.onClick.GetPersistentMethodName(0) == method,
            "Tavern button must invoke VillageScreen." + method + " exactly once.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
