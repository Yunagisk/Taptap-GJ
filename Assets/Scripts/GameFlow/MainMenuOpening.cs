using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>The title screen emerges from black using the original, aligned artwork layers.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuOpening : MonoBehaviour
{
    public RawImage background;
    public RawImage cross;
    public RawImage title;
    public CanvasGroup menu;
    public CanvasGroup skipHint;
    public GameObject skipSurface;
    public Button firstButton;

    [Header("Opening timing (unscaled seconds)")]
    [Min(0)] public float blackHold = .2f;
    [Min(.01f)] public float crossFade = 1f;
    [Min(0)] public float crossHold;
    [Min(.01f)] public float backgroundFade = .85f;
    [Min(0)] public float titleDelay;
    [Min(.01f)] public float titleFade = .5f;
    [Min(.01f)] public float menuFade = .25f;

    [Header("Cross entrance")]
    [Min(0)] public float crossRiseDistance = 160f;
    [Tooltip("Higher values keep the rising cross faint until it approaches its resting position.")]
    [Min(1)] public float crossOpacityPower = 4f;

    public bool IsPlaying { get; private set; }
    public float Elapsed { get; private set; }
    public float BackgroundStartsAt => blackHold + crossFade + crossHold;
    public float TitleStartsAt => BackgroundStartsAt + backgroundFade + titleDelay;
    public float MenuStartsAt => TitleStartsAt + titleFade;
    public float Duration => MenuStartsAt + menuFade;
    public Vector2 CrossRestPosition { get; private set; }

    static bool presentedThisSession;
    static readonly int Reveal = Shader.PropertyToID("_Reveal");
    Material backgroundInstance;
    Material originalBackgroundMaterial;
    Material crossInstance;
    Material originalCrossMaterial;
    GameFlowController flow;
    int unlockAfterFrame = -1;
    bool waitingForFirstFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { presentedThisSession = false; }

    void Awake()
    {
        flow = GetComponent<GameFlowController>();
        originalBackgroundMaterial = background.material;
        backgroundInstance = new Material(originalBackgroundMaterial) { name = "Main menu reveal (runtime)" };
        background.material = backgroundInstance;
        originalCrossMaterial = cross.material;
        crossInstance = new Material(originalCrossMaterial) { name = "Cross rise and fade (runtime)" };
        cross.material = crossInstance;
        CrossRestPosition = cross.rectTransform.anchoredPosition;
        if (presentedThisSession) SkipOpening();
        else ReplayOpening();
    }

    void Update()
    {
        if (IsPlaying)
        {
            // Always render a black frame before advancing, including a freshly loaded scene.
            if (waitingForFirstFrame)
            {
                waitingForFirstFrame = false;
                ApplyTime(0);
                return;
            }
            // Ignore the input that launched the scene, then let keyboard, mouse and touch skip.
            if (Elapsed > .25f && (Input.anyKeyDown || TouchBegan()))
            {
                SkipOpening();
                return;
            }
            // Font/scene loading can take a long frame. Keep that delay from revealing the
            // entire cross in one update before the player has seen the actual fade.
            Elapsed += Mathf.Min(Time.unscaledDeltaTime,.1f);
            ApplyTime(Elapsed);
            if (Elapsed >= Duration) SkipOpening();
        }
        else if (unlockAfterFrame >= 0 && Time.frameCount > unlockAfterFrame)
        {
            unlockAfterFrame = -1;
            menu.interactable = menu.blocksRaycasts = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        else if (!IsPlaying && menu.interactable && firstButton != null && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == null && !flow.settingsOverlay.activeSelf
            && !flow.confirmOverlay.activeSelf && !flow.loadingOverlay.activeSelf
            && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)
                || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)))
        {
            EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
        }
    }

    static bool TouchBegan()
    {
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        return false;
    }

    [ContextMenu("Replay opening (Play Mode)")]
    public void ReplayOpening()
    {
        if (!Application.isPlaying) return;
        presentedThisSession = true;
        IsPlaying = true;
        waitingForFirstFrame = true;
        unlockAfterFrame = -1;
        Elapsed = 0;
        menu.interactable = menu.blocksRaycasts = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        skipSurface.SetActive(true);
        ApplyTime(0);
    }

    public void SkipOpening()
    {
        if (!Application.isPlaying) return;
        presentedThisSession = true;
        IsPlaying = false;
        waitingForFirstFrame = false;
        Elapsed = Duration;
        ApplyTime(Duration);
        skipSurface.SetActive(false);
        // A skip key/click must never also activate New Game in the same frame.
        menu.interactable = menu.blocksRaycasts = false;
        unlockAfterFrame = Time.frameCount + 1;
    }

    void ApplyTime(float time)
    {
        float crossAmount = Fade(time, blackHold, crossFade);
        float backgroundAmount = Fade(time, BackgroundStartsAt, backgroundFade);
        backgroundInstance.SetFloat(Reveal, backgroundAmount);
        // White artwork on black looks solid well before linear alpha reaches one.
        // Keep opacity behind the motion, with both still completing at the same endpoint.
        crossInstance.SetFloat(Reveal, Mathf.Pow(crossAmount, Mathf.Max(1, crossOpacityPower)));
        cross.rectTransform.anchoredPosition = CrossRestPosition + Vector2.down * (crossRiseDistance * (1-crossAmount));
        background.enabled = backgroundAmount > 0;
        cross.enabled = crossAmount > 0 && backgroundAmount < 1;
        cross.color = Color.white;
        title.color = new Color(1, 1, 1, Fade(time, TitleStartsAt, titleFade));
        menu.alpha = Fade(time, MenuStartsAt, menuFade);
        skipHint.alpha = Fade(time, .8f, .6f) * (1 - menu.alpha);
    }

    static float Fade(float time, float start, float duration)
    {
        return Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - start) / Mathf.Max(.01f, duration)));
    }

    void OnDestroy()
    {
        if (backgroundInstance != null)
        {
            if (background != null) background.material = originalBackgroundMaterial;
            Destroy(backgroundInstance);
        }
        if (crossInstance != null)
        {
            if (cross != null)
            {
                cross.material = originalCrossMaterial;
                cross.rectTransform.anchoredPosition = CrossRestPosition;
            }
            Destroy(crossInstance);
        }
    }
}
