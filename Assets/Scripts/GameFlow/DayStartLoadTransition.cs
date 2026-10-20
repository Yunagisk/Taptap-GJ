using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A persistent black veil covers both restoring the run and the destination's first frame.
public sealed class DayStartLoadTransition : MonoBehaviour
{
    public static bool IsBusy { get; private set; }
    CanvasGroup veil;

    public static bool TryBegin(WorldMapTemplate template, Action<string> failed)
    {
        if (IsBusy || GameRoot.I == null || template == null) return false;
        try
        {
            var layout = template.Load();
            if (!GameRoot.I.SaveSlot.TryLoad(layout, out var morning, out var error))
            { failed?.Invoke(error); return false; }
            string scene = morning.map.currentNodeId == layout.villageNodeId
                && string.IsNullOrEmpty(morning.map.pendingRequestId)
                ? GameFlowController.VillageScene : GameFlowController.MapScene;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            { failed?.Invoke("读档所需的场景未加入构建列表。"); return false; }
            var host = new GameObject("DayStartLoadTransition", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(GraphicRaycaster));
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var black = new GameObject("Blackout", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            black.transform.SetParent(host.transform, false);
            black.rectTransform.anchorMin = Vector2.zero;
            black.rectTransform.anchorMax = Vector2.one;
            black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero;
            black.color = Color.black;
            black.raycastTarget = true;
            var transition = host.AddComponent<DayStartLoadTransition>();
            transition.veil = host.GetComponent<CanvasGroup>();
            transition.veil.alpha = 0;
            transition.veil.blocksRaycasts = true;
            IsBusy = true;
            DontDestroyOnLoad(host);
            transition.StartCoroutine(transition.Load(morning, template, scene, failed));
            return true;
        }
        catch (Exception error) { failed?.Invoke("读档失败：" + error.Message); return false; }
    }

    IEnumerator Load(GameState morning, WorldMapTemplate template, string scene, Action<string> failed)
    {
        yield return Fade(0, 1);
        yield return null; // Present a completely black frame before replacing any state.
        var root = GameRoot.I;
        var previous = root.state;
        AsyncOperation operation = null;
        try
        {
            root.InstallMorning(morning, template);
            Time.timeScale = 1;
            operation = SceneManager.LoadSceneAsync(scene);
        }
        catch (Exception error)
        {
            root.state = previous;
            failed?.Invoke("读档失败：" + error.Message);
        }
        if (operation != null)
        {
            while (!operation.isDone) yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(.25f);
        }
        yield return Fade(1, 0);
        Destroy(gameObject);
    }

    IEnumerator Fade(float from, float to)
    {
        float elapsed = 0;
        while (elapsed < .35f)
        {
            elapsed += Time.unscaledDeltaTime;
            veil.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .35f)));
            yield return null;
        }
        veil.alpha = to;
    }

    void OnDestroy() { IsBusy = false; }
}
