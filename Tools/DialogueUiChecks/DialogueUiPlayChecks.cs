using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class DialogueUiPlayChecks : MonoBehaviour
{
    int checks, errors;
    readonly string output = Path.GetFullPath("..");
    Camera cameraView;
    Canvas canvas;
    DialogueManager manager;
    DialogueUI ui;
    RpgDialogueLayout layout;
    int completions;

    void Awake() { Application.logMessageReceived += Track; }
    void OnDestroy() { Application.logMessageReceived -= Track; }
    void Track(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    void Check(bool ok, string message)
    { checks++; if (!ok) throw new Exception("FAILED: " + message); }

    IEnumerator Start()
    {
        var run = Checks();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(output, "failure.txt"), e.ToString());
                Debug.LogException(e);
                UnityEditor.EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        File.WriteAllText(Path.Combine(output, "result.json"),
            "{\"passed\":true,\"checks\":" + checks + ",\"runtimeErrors\":" + errors + "}");
        UnityEditor.EditorApplication.Exit(0);
    }

    IEnumerator Checks()
    {
        cameraView = new GameObject("Camera").AddComponent<Camera>();
        cameraView.clearFlags = CameraClearFlags.SolidColor;
        cameraView.backgroundColor = new Color(.12f, .11f, .09f);
        cameraView.transform.position = new Vector3(0, 0, -10);
        cameraView.tag = "MainCamera";
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var backdrop = new GameObject("Village", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        backdrop.transform.SetParent(canvas.transform, false);
        backdrop.rectTransform.anchorMin = Vector2.zero; backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
        backdrop.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/UI/VillageAmbience/VillageAfternoon.jpg");
        backdrop.preserveAspect = true; backdrop.raycastTarget = false;
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DialoguePanel.prefab");
        var instance = Instantiate(prefab, canvas.transform);
        manager = instance.GetComponent<DialogueManager>(); ui = manager.ui;
        layout = ui.GetComponent<RpgDialogueLayout>();
        manager.DialogueCompleted += _ => completions++;
        var portrait = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/UI/TavernTemp/TavernChief-v1.png");
        yield return null;

        var data = Data(new DialogueNode { id = "start", speaker = "哈罗德", avatar = portrait,
            content = "范斯大人的佩剑我已经交给了村里的铁匠，请他按猎魔人的惯例重新锻打过。您现在就去取。他叫加斯帕，铁匠铺在村子西边，门口有一块挂着的蹄铁。",
            choices = new List<DialogueChoice> {
                new DialogueChoice { choiceText = "我这就去。", targetNodeID = "reply" },
                new DialogueChoice { choiceText = "先告诉我怪物是什么样子。", targetNodeID = "reply" },
                new DialogueChoice { choiceText = "如果我没回来呢？", targetNodeID = "reply" }
            } }, new DialogueNode { id = "reply", speaker = "猎魔人", content = "我这就去。" });
        manager.StartDialogue(data); yield return null;
        Check(layout != null && layout.stage != null && layout.stage.GetComponentInParent<Canvas>() != null, "saved prefab renders under a Canvas");
        Check(ui.avatarImage.enabled && layout.nameplate.gameObject.activeSelf && ui.speakerText.text == "哈罗德", "speaker name and portrait are displayed");
        Check(ui.PageCount == 1 && ui.CanChoose && !ui.continueButton.gameObject.activeSelf, "short dialogue presents choices without a continue button");
        Check(Buttons().Length == 3, "all three document choices are displayed");
        Check(EventSystem.current.currentSelectedGameObject == Buttons()[0].gameObject, "first choice receives keyboard focus");
        Check(ui.contentText.rectTransform.anchoredPosition.x > 400, "text leaves room for the portrait");
        var hitPoint = RectTransformUtility.WorldToScreenPoint(null, Buttons()[1].transform.position);
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = hitPoint }, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == Buttons()[1], "choice frame receives native pointer input");
        Capture("dialogue-choices-1920.png", 1920, 1080);
        Capture("dialogue-choices-1280.png", 1280, 720);
        Capture("dialogue-choices-4x3.png", 1024, 768);
        Capture("dialogue-choices-ultrawide.png", 2560, 1080);
        var move = new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down, moveVector = Vector2.down };
        ExecuteEvents.Execute(Buttons()[0].gameObject, move, ExecuteEvents.moveHandler);
        Check(EventSystem.current.currentSelectedGameObject == Buttons()[1].gameObject, "arrow navigation selects the next response");
        ExecuteEvents.Execute(Buttons()[1].gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        yield return null;
        Check(manager.IsPlaying && ui.speakerText.text == "猎魔人" && !ui.CanChoose, "submit follows a choice target exactly once");
        Check(Buttons().Length == 0 && !ui.avatarImage.enabled, "switching speaker removes old choices and portraits");
        manager.Next(); yield return null;
        Check(!manager.IsPlaying && !ui.visibleRoot.activeSelf && completions == 1, "terminal line completes the conversation once");

        string paragraph = "这是一个虫鸣喧闹的凉爽夏夜。如果你还在波西米亚，在同样的季节和天气下应当正享受着酒馆里的美味啤酒，配合着潺潺溪流边动听的圣徒故事。";
        var longNode = new DialogueNode { id = "long", speaker = "旁白", lineKind = DialogueLineKind.Narration,
            avatar = portrait, content = string.Concat(Enumerable.Repeat(paragraph, 12)),
            choices = new List<DialogueChoice> { null,
                new DialogueChoice { choiceText = "埃尔南多？", targetNodeID = "reply" } } };
        manager.StartDialogue(Data(longNode, data.nodes[0])); yield return null;
        Check(ui.PageCount > 2 && ui.CurrentPage == 1, "long prose uses multiple readable pages");
        Check(!ui.avatarImage.enabled && !layout.nameplate.gameObject.activeSelf, "narration hides its portrait and nameplate");
        Check(ui.contentText.rectTransform.anchoredPosition.x < 100, "narration expands into the portrait space");
        Check(!ui.CanChoose && Buttons().Length == 0 && ui.continueButton.gameObject.activeSelf, "choices wait until the final text page");
        manager.Choose(1);
        Check(ui.CurrentPage == 1 && ui.speakerText.text == "旁白", "manager rejects a choice before its text is read");
        Capture("dialogue-narration.png", 1920, 1080);
        ui.Advance(); ui.Advance();
        Check(ui.CurrentPage == 2, "duplicate submits in one frame cannot skip a page");
        yield return null;
        while (ui.CurrentPage < ui.PageCount) { manager.Next(); yield return null; }
        Check(ui.CanChoose && Buttons().Length == 1, "final page enables non-null choices");
        Buttons()[0].onClick.Invoke(); yield return null;
        Check(ui.speakerText.text == "猎魔人", "filtered choice list preserves its original target index");
        manager.CancelDialogue(); yield return null;
        Check(completions == 1, "cancelling an unfinished conversation never fires completion");

        var many = new DialogueNode { id = "many", speaker = "声音低沉的壮汉", avatar = portrait,
            content = "圣马丁的斗篷在哪里？", choices = new List<DialogueChoice>() };
        for (int i = 0; i < 8; i++) many.choices.Add(new DialogueChoice {
            choiceText = i == 7 ? "这是一条较长的回答，用来确认选项会自动换行，并且用键盘选择最后一项时仍然能够完整阅读和确认。" : "回答 " + (i + 1), targetNodeID = "reply" });
        manager.StartDialogue(Data(many, data.nodes[0])); yield return null;
        Check(layout.choiceScroll.content.rect.height > layout.choiceScroll.viewport.rect.height, "many responses use a scrollable list");
        EventSystem.current.SetSelectedGameObject(Buttons()[7].gameObject);
        yield return null;
        Check(layout.choiceScroll.verticalNormalizedPosition < .1f, "keyboard focus reveals the last option");
        Check(Buttons()[7].GetComponent<LayoutElement>().preferredHeight > 76, "long response grows to fit its wrapped text");
        Capture("dialogue-long-options.png", 1920, 1080);
        manager.CancelDialogue(); yield return null;
        manager.StartDialogue(Data(new DialogueNode { id = "tip", lineKind = DialogueLineKind.System,
            avatar = portrait, content = "举起提灯，可照见敌人全部弱点。提灯一场战斗只能用两次，回村休整才能添油。" }));
        yield return null;
        Check(!ui.avatarImage.enabled && ui.speakerText.text == "系统提示" && layout.nameplate.gameObject.activeSelf, "system tips have a name and no portrait");
        Capture("dialogue-system-tip.png", 1920, 1080);
        manager.CancelDialogue();
        yield return null;
        Check(errors == 0, "new native UI has no runtime errors");

        // Open the real legacy Intro scene: its UI controller is outside the Canvas.
        var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/Intro.unity", new LoadSceneParameters(LoadSceneMode.Single));
        while (!operation.isDone) yield return null;
        yield return null; yield return null;
        manager = FindObjectOfType<DialogueManager>(); ui = manager.ui;
        layout = ui.GetComponent<RpgDialogueLayout>();
        Check(manager.IsPlaying && layout != null && layout.stage.GetComponentInParent<Canvas>() != null, "actual legacy Intro uses the new layout automatically");
        Check(ui.contentText.text.Length > 0 && ui.visibleRoot.activeInHierarchy, "legacy Intro retains its serialized dialogue content");
        int oldPage = ui.CurrentPage;
        if (ui.PageCount > 1)
        {
            var surface = ui.visibleRoot.GetComponent<DialogueAdvanceSurface>();
            ExecuteEvents.Execute(surface.gameObject, new PointerEventData(EventSystem.current) {
                button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            Check(ui.CurrentPage == oldPage + 1, "legacy background clicks reach the external dialogue controller");
        }
        Check(errors == 0, "legacy scene compatibility produces no runtime errors");
    }

    Button[] Buttons() => ui.choiceParent.GetComponentsInChildren<Button>(false);
    static DialogueData Data(DialogueNode start, params DialogueNode[] nodes)
    {
        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.startNode = start; data.nodes = new List<DialogueNode>(nodes);
        return data;
    }

    void Capture(string name, int width, int height)
    {
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        var oldDistance = canvas.planeDistance;
        var previous = RenderTexture.active; var oldTarget = cameraView.targetTexture;
        var target = new RenderTexture(width, height, 24); target.Create();
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraView; canvas.planeDistance = 1;
            cameraView.targetTexture = target;
            Canvas.ForceUpdateCanvases(); layout.RefreshLayout(); Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; layout.window.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(cameraView, corner);
                Check(point.x >= 0 && point.x <= width && point.y >= 0 && point.y <= height, "dialogue band fits " + width + "x" + height);
            }
            cameraView.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; cameraView.targetTexture = oldTarget;
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            Destroy(image); target.Release(); Destroy(target);
            Canvas.ForceUpdateCanvases(); layout.RefreshLayout();
        }
    }
}
