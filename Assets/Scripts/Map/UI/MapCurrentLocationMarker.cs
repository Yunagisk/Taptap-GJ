using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(MapMarkerRevealEffect))]
public sealed class MapCurrentLocationMarker : MonoBehaviour
{
    public Image image;
    public MapMarkerRevealEffect revealEffect;
    [Min(0.1f)] public float revealDuration = 0.9f;
    [Tooltip("Marker size relative to the badge. A 128px encounter uses an approximately 171px marker.")]
    [Min(1)] public float nodeSizeScale = 4f / 3f;
    [Tooltip("Lift the cross above encounter badges, in map UI pixels. The village keeps its original cross height.")]
    [Min(0)] public float encounterCrossOffset = 22;
    [Tooltip("Vertical breathing amplitude of the cross in map UI pixels. The surrounding arcs stay fixed.")]
    [Min(0)] public float crossFloatAmplitude = 4;
    [Tooltip("Seconds for one complete up-and-down breathing cycle.")]
    [Min(0.1f)] public float crossFloatPeriod = 3.2f;
    [Tooltip("The village artwork is wider than its click target. Frame the artwork with an ellipse.")]
    public Vector2 villageSize = new Vector2(560, 360);
    public Vector2 villageOffset = new Vector2(-7, 18);

    RectTransform rect;
    RectTransform target;
    bool atVillage;
    bool animating;
    float revealStartedAt;
    int noiseSeed;

    public bool IsAnimating => animating && isActiveAndEnabled && image != null && image.enabled;

    void Awake()
    {
        rect = (RectTransform)transform;
        if (image == null) image = GetComponent<Image>();
        if (revealEffect == null) revealEffect = GetComponent<MapMarkerRevealEffect>();
        image.raycastTarget = false;
    }

    public void Refresh(WorldMapSceneView view, MapProgression map, bool replay = false)
    {
        var next = view.NodeTransform(map.State.currentNodeId);
        bool changed = target != next;
        target = next;
        atVillage = map.State.currentNodeId == map.Layout.villageNodeId;
        noiseSeed = MapRoadGeometry.Seed(map.State.currentNodeId, "location");
        UpdatePose();
        if (target != null && (changed || replay)) PlayReveal();
    }

    // A separate overlay stays intact when the encounter badge cracks or turns
    // grey. Sharing MapRoot also makes it follow dragging and zooming exactly.
    void LateUpdate()
    {
        UpdatePose();
        if (!animating || revealEffect == null) return;
        float phase = Mathf.Clamp01((Time.unscaledTime - revealStartedAt) / Mathf.Max(0.1f, revealDuration));
        revealEffect.SetReveal(phase, noiseSeed);
        if (phase >= 1) animating = false;
    }

    void PlayReveal()
    {
        if (revealEffect == null) return;
        revealStartedAt = Time.unscaledTime;
        animating = true;
        revealEffect.SetReveal(0, noiseSeed);
    }

    void OnEnable() { if (target != null) PlayReveal(); }

    void OnDisable()
    {
        animating = false;
        if (revealEffect != null)
        {
            revealEffect.SetCrossOffset(atVillage ? 0 : encounterCrossOffset);
            revealEffect.SetReveal(1, noiseSeed);
        }
    }

    void UpdatePose()
    {
        if (image == null) return;
        bool visible = target != null && target.gameObject.activeInHierarchy;
        if (image.enabled != visible) image.enabled = visible;
        if (!visible) return;
        if (rect == null) rect = (RectTransform)transform;
        Rect bounds = MapUITransform.Bounds(target, rect.parent);
        Vector2 position = bounds.center + (atVillage ? villageOffset : Vector2.zero);
        Vector2 size = atVillage ? villageSize : bounds.size * Mathf.Max(1, nodeSizeScale);
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        if (revealEffect != null)
        {
            // Start at rest after the ink reveal. Unscaled time keeps the
            // breathing smooth when map time is paused, without blocking travel.
            float elapsed = Mathf.Max(0, Time.unscaledTime - revealStartedAt - Mathf.Max(0.1f, revealDuration));
            float period = Mathf.Max(0.1f, crossFloatPeriod);
            float breathing = Mathf.Sin(Mathf.Repeat(elapsed, period) / period * Mathf.PI * 2)
                * Mathf.Max(0, crossFloatAmplitude);
            revealEffect.SetCrossOffset((atVillage ? 0 : encounterCrossOffset) + breathing);
        }
    }
}
