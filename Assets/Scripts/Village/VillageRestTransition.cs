using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Native map artwork and anchors, shown above a full-screen input-blocking black veil.
public sealed class VillageRestTransition : MonoBehaviour
{
    public Image blackout;
    public CanvasGroup dialVisibility;
    public MapTimeDialGraphic dial;
    public TMP_Text periodLabel;
    public float fadeSeconds = .45f;
    public float dialFadeSeconds = .22f;
    public float holdSeconds = .6f;
    public bool IsRunning { get; private set; }
    public bool HasSettled { get; private set; }
    public string LastMessage { get; private set; }
    VillageScreen owner;

    public bool Begin(VillageScreen screen, GameState state, MapLayout layout)
    {
        if (IsRunning || screen == null || layout == null || !VillageInnRules.CanRest(state)) return false;
        owner = screen; IsRunning = true; HasSettled = false; LastMessage = "";
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        var overlay = GetComponent<Canvas>();
        var parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        if (overlay != null && parentCanvas != null)
        {
            overlay.renderMode = parentCanvas.rootCanvas.renderMode;
            overlay.worldCamera = parentCanvas.rootCanvas.worldCamera;
            overlay.overrideSorting = true;
            overlay.sortingOrder = Mathf.Max(200, parentCanvas.rootCanvas.sortingOrder + 1);
        }
        blackout.canvasRenderer.cullTransparentMesh = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        StartCoroutine(Stay(state, layout, state.world.time));
        Canvas.ForceUpdateCanvases();
        return true;
    }

    IEnumerator Stay(GameState state, MapLayout layout, int from)
    {
        blackout.color = Color.clear;
        blackout.raycastTarget = true;
        dialVisibility.alpha = 0;
        dialVisibility.blocksRaycasts = false;
        dial.SetRotation((float)MapTimeDialMotion.Wrap(MapTimeDialMotion.Rotation(from)));
        periodLabel.text = MapTimeDialMotion.FormatLabel(from);
        yield return Fade(0, 1, fadeSeconds, alpha => blackout.color = new Color(0, 0, 0, alpha));
        owner.HidePanelsForRest();
        yield return Fade(0, 1, dialFadeSeconds, alpha => dialVisibility.alpha = alpha);

        bool ok = false;
        string message = "歇息中断，请重试。";
        try
        {
            if (GameRoot.I != null && ReferenceEquals(GameRoot.I.state, state))
                ok = VillageInnRules.TryRest(state, layout, from, out message);
        }
        catch (Exception error) { Debug.LogException(error, this); }
        HasSettled = ok; LastMessage = message;
        owner.RefreshStatus();
        if (ok)
        {
            double angle = MapTimeDialMotion.Rotation(from);
            double step = MapTimeDialMotion.Wrap(MapTimeDialMotion.Rotation(state.world.time) - angle);
            if (step < .000001) step = 360;
            yield return Fade(0, 1, (float)MapTimeDialMotion.Duration,
                progress => dial.SetRotation((float)MapTimeDialMotion.Wrap(angle + step * progress)));
            periodLabel.text = MapTimeDialMotion.FormatLabel(state.world.time);
            MapTimeDialView.RememberPresentedTime(state.world);
        }
        yield return new WaitForSecondsRealtime(Mathf.Max(0, holdSeconds));
        // Let the existing village background finish changing while it is still covered.
        var ambience = FindObjectOfType<VillageAmbienceView>();
        float deadline = Time.unscaledTime + 3;
        while (ambience != null && ambience.IsTransitioning && Time.unscaledTime < deadline) yield return null;
        yield return Fade(1, 0, dialFadeSeconds, alpha => dialVisibility.alpha = alpha);
        yield return Fade(1, 0, fadeSeconds, alpha => blackout.color = new Color(0, 0, 0, alpha));
        IsRunning = false;
        owner.CompleteInnPresentation(LastMessage);
        gameObject.SetActive(false);
    }

    static IEnumerator Fade(float from, float to, float seconds, Action<float> apply)
    {
        float elapsed = 0;
        apply(from);
        while (elapsed < Mathf.Max(0, seconds))
        {
            elapsed += Time.unscaledDeltaTime;
            apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / seconds))));
            yield return null;
        }
        apply(to);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (blackout != null) blackout.color = Color.clear;
        if (dialVisibility != null) dialVisibility.alpha = 0;
        IsRunning = false;
    }
}
