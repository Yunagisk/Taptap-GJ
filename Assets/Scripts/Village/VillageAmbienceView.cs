using UnityEngine;
using UnityEngine.UI;

// All positions are authored against the 1920 x 1080 illustration, not the screen.
[DisallowMultipleComponent, RequireComponent(typeof(Image))]
public sealed class VillageAmbienceView : MonoBehaviour
{
    public enum Period { GameTime = -1, Morning = 0, Afternoon = 1, Night = 2 }

    [Header("Backgrounds: morning / afternoon / night")]
    public Sprite morningBackground;
    public Sprite afternoonBackground;
    public Sprite nightBackground;
    [Min(0f)] public float crossfadeSeconds = 1.25f;
    public Period previewPeriod = Period.GameTime;

    [Header("Independent effect switches")]
    public bool chimneySmoke = true;
    public bool driftingFog = true;
    public bool windowLights = true;
    public bool fallingLeaves = true;
    public bool flyingBirds = true;
    [Range(0f, 1f)] public float effectStrength = 1f;

    public int CurrentPeriod { get; private set; } = -1;
    public RectTransform IllustrationSpace => illustrationSpace;
    public bool IsTransitioning => fading;
    public float AnimationTime { get; private set; }

    Image background;
    Image incoming;
    RectTransform illustrationSpace;
    VillageAtmosphereGraphic atmosphere;
    VillageActivityGraphic activity;
    Sprite originalBackground;
    float transitionTime;
    float nightWeight;
    float fogWeight;
    bool fading;

    void Awake()
    {
        background = GetComponent<Image>();
        originalBackground = background.sprite;
        background.raycastTarget = false;
        CreateLayers();
        SelectPeriod(ResolvePeriod(), true);
        RefreshLayout();
    }

    void Update()
    {
        int period = ResolvePeriod();
        if (period != CurrentPeriod) SelectPeriod(period, false);
        float dt = Time.unscaledDeltaTime;
        AnimationTime += dt;
        if (fading)
        {
            transitionTime += dt;
            float blend = crossfadeSeconds <= 0f ? 1f : Mathf.Clamp01(transitionTime / crossfadeSeconds);
            incoming.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, blend));
            if (blend >= 1f)
            {
                background.sprite = incoming.sprite;
                incoming.enabled = false;
                fading = false;
            }
        }

        nightWeight = Mathf.MoveTowards(nightWeight, period == 2 ? 1f : 0f, dt / 1.25f);
        fogWeight = Mathf.MoveTowards(fogWeight, FogFor(period), dt * 0.4f);
        atmosphere.Animate(this, nightWeight, fogWeight);
        activity.Animate(this, nightWeight);
    }

    void LateUpdate() => RefreshLayout();
    void OnRectTransformDimensionsChange() => RefreshLayout();

    int ResolvePeriod()
    {
        if (previewPeriod != Period.GameTime) return Mathf.Clamp((int)previewPeriod, 0, 2);
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        return state != null && state.world != null
            ? Mathf.Max(0, state.world.time) % WorldResourceRules.UnitsPerDay : 0;
    }

    public Sprite BackgroundFor(int period)
    {
        Sprite sprite = period == 2 ? nightBackground : period == 1 ? afternoonBackground : morningBackground;
        return sprite != null ? sprite : originalBackground;
    }

    public void Preview(Period period)
    {
        previewPeriod = period;
        if (background != null) SelectPeriod(ResolvePeriod(), false);
    }

    void SelectPeriod(int period, bool instantly)
    {
        CurrentPeriod = period;
        Sprite target = BackgroundFor(period);
        if (instantly || crossfadeSeconds <= 0f)
        {
            background.sprite = target;
            incoming.enabled = false;
            fading = false;
            nightWeight = period == 2 ? 1f : 0f;
            fogWeight = FogFor(period);
        }
        else
        {
            // A second clock change during a fade starts from its dominant image.
            if (fading && incoming.color.a >= 0.5f) background.sprite = incoming.sprite;
            incoming.sprite = target;
            incoming.color = new Color(1f, 1f, 1f, 0f);
            incoming.enabled = target != background.sprite;
            fading = incoming.enabled;
            transitionTime = 0f;
        }
    }

    static float FogFor(int period) => period == 2 ? 1f : period == 0 ? 0.65f : 0.28f;

    void CreateLayers()
    {
        var fade = new GameObject("TimeOfDayCrossfade", typeof(RectTransform), typeof(Image));
        fade.transform.SetParent(transform, false);
        incoming = fade.GetComponent<Image>();
        incoming.raycastTarget = false;
        incoming.preserveAspect = background.preserveAspect;
        Stretch(incoming.rectTransform);

        var art = new GameObject("VillageAmbience_1920x1080", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasGroup), typeof(RectMask2D));
        art.transform.SetParent(transform, false);
        illustrationSpace = art.GetComponent<RectTransform>();
        illustrationSpace.anchorMin = illustrationSpace.anchorMax = illustrationSpace.pivot = Vector2.one * 0.5f;
        illustrationSpace.sizeDelta = new Vector2(1920f, 1080f);
        var group = art.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        atmosphere = AddGraphic<VillageAtmosphereGraphic>("SmokeAndMist");
        activity = AddGraphic<VillageActivityGraphic>("WindowsLeavesAndBirds");
    }

    T AddGraphic<T>(string objectName) where T : Graphic
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(illustrationSpace, false);
        var graphic = go.AddComponent<T>();
        graphic.raycastTarget = false;
        Stretch(graphic.rectTransform);
        return graphic;
    }

    public void RefreshLayout()
    {
        if (illustrationSpace == null || background == null) return;
        Rect r = background.GetPixelAdjustedRect();
        float scaleX = r.width / 1920f, scaleY = r.height / 1080f;
        // Match Image.preserveAspect, including letterboxed and portrait windows.
        if (background.preserveAspect) scaleX = scaleY = Mathf.Min(scaleX, scaleY);
        illustrationSpace.localScale = new Vector3(scaleX, scaleY, 1f);
        illustrationSpace.anchoredPosition = Vector2.zero;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
