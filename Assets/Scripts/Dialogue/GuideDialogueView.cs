using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A shared narrative layer for village, map and live battle teaching.
public sealed class GuideDialogueView : MonoBehaviour
{
    public DialogueManager Manager { get; private set; }
    public Sprite Background => background.sprite;
    public bool IsOpen => surface != null && surface.activeSelf;
    public bool IsPaused { get; private set; }
    GameObject surface, pause;
    Image background;
    AspectRatioFitter backgroundFitter;
    Action<DialogueData> finished;
    DialogueData active;
    public static GuideDialogueView Create(Transform parent, VillageIntroContent content)
    {
        var root = new GameObject("GuidePresentation"); root.transform.SetParent(parent, false);
        var view = root.AddComponent<GuideDialogueView>();
        var canvas = GuideUI.Canvas(root.transform, "GuideCanvas", 150); view.surface = canvas.gameObject;
        view.background = GuideUI.Image("Background", canvas.transform, Color.white);
        GuideUI.Stretch(view.background.rectTransform); view.background.preserveAspect = true;
        view.backgroundFitter = view.background.gameObject.AddComponent<AspectRatioFitter>();
        view.backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        view.backgroundFitter.enabled = false;
        var dialogue = Instantiate(content.dialoguePrefab, canvas.transform, false);
        GuideUI.Stretch((RectTransform)dialogue.transform);
        view.Manager = dialogue.GetComponent<DialogueManager>(); view.Manager.DialogueCompleted += view.Completed;
        var pause = GuideUI.Image("Pause", canvas.transform, new Color(0, 0, 0, .7f));
        GuideUI.Stretch(pause.rectTransform); view.pause = pause.gameObject;
        GuideUI.Button("Resume", pause.transform, "继续", Vector2.zero, new Vector2(280, 70), content.font, view.TogglePause);
        view.pause.SetActive(false); view.surface.SetActive(false); return view;
    }
    public void Play(DialogueData data, Sprite sprite, Action<DialogueData> onComplete, string startNodeId = null)
    {
        if (data == null || Manager.IsPlaying) return;
        active = data; finished = onComplete; IsPaused = false; pause.SetActive(false);
        Manager.gameObject.SetActive(true); background.sprite = sprite;
        // Fill the viewport without stretching the artwork or revealing the map at its edges.
        // A sprite-less battle tutorial keeps its original full-screen translucent overlay.
        backgroundFitter.enabled = sprite != null;
        if (sprite != null)
            backgroundFitter.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        else
            GuideUI.Stretch(background.rectTransform);
        background.color = sprite != null ? Color.white : new Color(0, 0, 0, .12f);
        surface.SetActive(true); Manager.StartDialogue(data, startNodeId);
    }
    void Completed(DialogueData data)
    {
        if (data != active) return;
        var callback = finished; active = null; finished = null;
        surface.SetActive(false); IsPaused = false; callback?.Invoke(data);
    }
    void Update() { if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) TogglePause(); }
    public void TogglePause()
    {
        if (!IsOpen) return;
        IsPaused = !IsPaused; Manager.gameObject.SetActive(!IsPaused); pause.SetActive(IsPaused);
        if (EventSystem.current == null) return;
        var choices = Manager.ui.choiceParent.GetComponentsInChildren<Button>(false);
        var target = IsPaused ? pause.GetComponentInChildren<Button>()
            : Manager.ui.CanChoose && choices.Length > 0 ? choices[0] : Manager.ui.continueButton;
        if (target != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
    }
    void OnDestroy() { if (Manager != null) Manager.DialogueCompleted -= Completed; }
}

public static class GuideUI
{
    public static Canvas Canvas(Transform parent, string name, int order)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(parent, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true; canvas.sortingOrder = order;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f; return canvas;
    }
    public static Image Image(string name, Transform parent, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image)); root.transform.SetParent(parent, false);
        var image = root.GetComponent<Image>(); image.color = color; return image;
    }
    public static TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size,
        TMP_FontAsset font, int fontSize = 26)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); root.transform.SetParent(parent, false);
        var label = root.GetComponent<TextMeshProUGUI>(); label.font = SharedUIFont.Get(font);
        label.text = text; label.fontSize = fontSize; label.color = new Color32(38, 36, 32, 255);
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        Center(label.rectTransform, position, size); return label;
    }
    public static Button Button(string name, Transform parent, string text, Vector2 position, Vector2 size,
        TMP_FontAsset font, UnityEngine.Events.UnityAction action)
    {
        var image = Image(name, parent, new Color32(245, 243, 235, 250)); Center(image.rectTransform, position, size);
        var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = new Color32(45, 42, 36, 220);
        outline.effectDistance = new Vector2(1, -1);
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        Label("Label", image.transform, text, Vector2.zero, size - new Vector2(12, 8), font, 24); return button;
    }
    public static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    public static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}
