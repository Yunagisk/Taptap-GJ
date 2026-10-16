using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class MapHudInkRevealGroup : MonoBehaviour
{
    public RectTransform origin;
    [Tooltip("Spread away from the portrait for the crescent; flow left to right for the status bars.")]
    public bool radial;
    public int noiseSeed = 1729;

    readonly List<MapHudInkRevealEffect> images = new List<MapHudInkRevealEffect>();
    readonly List<MapHudInkRevealText> texts = new List<MapHudInkRevealText>();
    bool initialized;
    public MapHudInkRevealState State { get; private set; }

    public void SetReveal(float progress)
    {
        if (!initialized) Initialize();
        var rect = (RectTransform)transform;
        Rect originBounds = origin != null ? MapUITransform.Bounds(origin, rect) : default;
        var state = new MapHudInkRevealState
        {
            bounds = rect.rect,
            origin = origin != null ? (Vector2)MapUITransform.RelativeMatrix(origin, rect).MultiplyPoint3x4(origin.rect.center)
                : rect.rect.center,
            radial = radial,
            innerRadius = radial ? Mathf.Min(originBounds.width, originBounds.height) * 0.5f : 0,
            progress = Mathf.Clamp01(progress),
            seed = noiseSeed
        };
        var previous = State;
        if (previous.bounds == state.bounds && previous.origin == state.origin && previous.radial == state.radial
            && previous.innerRadius == state.innerRadius
            && previous.progress == state.progress && previous.seed == state.seed) return;
        State = state;
        foreach (var effect in images) if (effect != null) effect.Invalidate();
        foreach (var effect in texts) if (effect != null) effect.Invalidate();
    }

    void Initialize()
    {
        initialized = true;
        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic is TMP_SubMeshUI) continue; // The parent TMP event handles every font material.
            if (graphic is TMP_Text)
            {
                var effect = graphic.GetComponent<MapHudInkRevealText>();
                if (effect == null) effect = graphic.gameObject.AddComponent<MapHudInkRevealText>();
                effect.Initialize(this);
                texts.Add(effect);
            }
            else
            {
                var effect = graphic.GetComponent<MapHudInkRevealEffect>();
                if (effect == null) effect = graphic.gameObject.AddComponent<MapHudInkRevealEffect>();
                effect.Initialize(this);
                images.Add(effect);
            }
        }
    }
}
