using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// The screen overlay is authored as a prefab and survives the old scene so
// loading and the destination's first UI frame stay behind an opaque screen.
public sealed class MapSceneTransition : MonoBehaviour
{
    public CanvasGroup overlay;
    [Min(0.1f)] public float fadeDuration = 0.5f;

    static MapSceneTransition current;
    AsyncOperation loading;
    Action finished;
    public static bool IsBusy => current != null;

    public static bool Begin(MapSceneTransition prefab, WorldMapController source, Func<AsyncOperation> enter, Action onFinished)
    {
        if (IsBusy) return false;
        if (prefab == null)
        {
            Debug.LogError("Assign the map scene transition prefab.", source);
            return false;
        }
        current = Instantiate(prefab);
        DontDestroyOnLoad(current.gameObject);
        current.finished = onFinished;
        current.StartCoroutine(current.Run(source, enter));
        return true;
    }

    // Entry callbacks that load a scene use this wrapper so the overlay can
    // hold black until that asynchronous load actually finishes.
    public static AsyncOperation LoadSceneAsync(string scene)
    {
        var operation = SceneManager.LoadSceneAsync(scene);
        if (current != null) current.loading = operation;
        return operation;
    }

    IEnumerator Run(WorldMapController source, Func<AsyncOperation> enter)
    {
        overlay.blocksRaycasts = true;
        overlay.interactable = false;
        try
        {
            yield return MapSceneTransitionSequence.Run(fadeDuration,
                () => source != null && source.isActiveAndEnabled,
                () =>
                {
                    var view = source != null ? source.GetComponent<WorldMapSceneView>() : null;
                    return view != null && view.currentLocationMarker != null && view.currentLocationMarker.IsAnimating;
                },
                () => Time.unscaledTime,
                alpha => overlay.alpha = alpha,
                () =>
                {
                    try
                    {
                        var operation = enter();
                        if (operation != null) loading = operation;
                    }
                    catch (Exception error) { Debug.LogException(error); }
                },
                () => loading != null && !loading.isDone);
        }
        finally
        {
            Release();
            Destroy(gameObject);
        }
    }

    void Release()
    {
        if (overlay != null) overlay.blocksRaycasts = false;
        if (current == this) current = null;
        var callback = finished;
        finished = null;
        callback?.Invoke();
    }

    void OnDestroy() => Release();
}

// Clock and loading state are injected so the exact coroutine order can be
// checked without opening Unity or loading a scene on the user's desktop.
public static class MapSceneTransitionSequence
{
    public static IEnumerator Run(float duration, Func<bool> sourceAlive, Func<bool> markerAnimating,
        Func<float> clock, Action<float> opacity, Action enter, Func<bool> loading)
    {
        opacity(0);
        // A pending battle can resume in the controller's Start, before the
        // map view's Start has initialized and begun its marker animation.
        yield return null;
        while (sourceAlive() && markerAnimating()) yield return null;
        if (!sourceAlive()) yield break;
        float half = Mathf.Max(0.05f, duration * 0.5f);
        yield return Fade(0, 1, half, clock, opacity);
        // Render a fully black frame before any entry callback can swap scenes.
        yield return null;
        if (sourceAlive())
        {
            enter();
            while (loading()) yield return null;
        }
        // Let the destination run Start and construct its UI before revealing it.
        yield return null;
        yield return Fade(1, 0, half, clock, opacity);
    }

    static IEnumerator Fade(float from, float to, float duration, Func<float> clock, Action<float> opacity)
    {
        float start = clock();
        while (clock() - start < duration)
        {
            opacity(Mathf.Lerp(from, to, MapRevealMath.Smooth((clock() - start) / duration)));
            yield return null;
        }
        opacity(to);
    }
}
