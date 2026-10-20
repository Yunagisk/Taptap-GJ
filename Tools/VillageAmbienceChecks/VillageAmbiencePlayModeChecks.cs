using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class VillageAmbiencePlayModeChecks : MonoBehaviour
{
    int checks;
    int errors;
    readonly string output = Path.GetFullPath("..");
    void Awake() { Application.logMessageReceived += Track; }
    void OnDestroy() { Application.logMessageReceived -= Track; }
    void Track(string message, string stack, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++; }
    void Check(bool ok, string reason)
    { checks++; if (!ok) throw new Exception("FAILED: " + reason); }

    IEnumerator Start()
    {
        var run = Checks();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
                Debug.LogException(error);
                UnityEditor.EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        File.WriteAllText(Path.Combine(output, "result.json"), "{\"passed\":true,\"checks\":" + checks
            + ",\"runtimeErrors\":" + errors + "}");
        UnityEditor.EditorApplication.Exit(0);
    }

    IEnumerator Checks()
    {
        yield return null;
        var view = FindObjectOfType<VillageAmbienceView>();
        var screen = FindObjectOfType<VillageScreen>();
        Check(view != null && screen != null, "actual village scene has background controller");
        Check(view.morningBackground != null && view.afternoonBackground != null && view.nightBackground != null,
            "all three original images are imported and assigned");
        var world = GameRoot.I.state.world;
        var background = view.GetComponent<Image>();
        int gameTime = world.time;
        int initialChildren = view.GetComponentsInChildren<Transform>(true).Length;
        var atmosphere = view.GetComponentInChildren<VillageAtmosphereGraphic>();
        var activity = view.GetComponentInChildren<VillageActivityGraphic>();
        Check(atmosphere != null && activity != null, "smoke, mist and activity meshes are created");
        Check(!atmosphere.raycastTarget && !activity.raycastTarget && !background.raycastTarget,
            "effects never receive pointer input");
        Check(!view.IllustrationSpace.GetComponent<CanvasGroup>().blocksRaycasts, "effect canvas passes pointer input");
        Check(atmosphere.mainTexture.width == 96, "watercolour cloud texture exists");

        for (int period = 0; period < 3; period++)
        {
            world.time = period;
            view.Preview(VillageAmbienceView.Period.GameTime);
            yield return new WaitForSecondsRealtime(1.5f);
            Check(view.CurrentPeriod == period && !view.IsTransitioning && background.sprite == view.BackgroundFor(period),
                "game clock selects and finishes background fade for period " + period);
            Check(world.time == period, "ambience never advances gameplay time");
            Capture(background.GetComponentInParent<Canvas>(), Path.Combine(output, "village-" + period + ".png"));
            yield return null;
        }

        // Effects use unscaled time, including when a menu pauses gameplay.
        Time.timeScale = 0f;
        float animationBefore = view.AnimationTime;
        world.time = 3;
        yield return new WaitForSecondsRealtime(1.5f);
        Check(view.CurrentPeriod == 0 && background.sprite == view.morningBackground && !view.IsTransitioning,
            "next dawn wraps correctly while gameplay is paused");
        Check(view.AnimationTime > animationBefore + 1f, "ambient animation runs while paused");
        Time.timeScale = 1f;

        Canvas.ForceUpdateCanvases();
        var mesh = atmosphere.canvasRenderer.GetMesh();
        Check(mesh.vertexCount > 0, "visible smoke and fog are submitted to the Canvas");
        Vector3 before = mesh.vertices[0];
        yield return new WaitForSecondsRealtime(0.2f);
        Canvas.ForceUpdateCanvases();
        mesh = atmosphere.canvasRenderer.GetMesh();
        Check((mesh.vertices[0] - before).sqrMagnitude > 0.001f, "atmosphere vertices actually animate");
        view.chimneySmoke = view.driftingFog = false;
        yield return null;
        Canvas.ForceUpdateCanvases();
        mesh = atmosphere.canvasRenderer.GetMesh();
        Check(mesh == null || mesh.vertexCount == 0, "smoke and fog switches remove their geometry");
        view.chimneySmoke = view.driftingFog = true;
        view.windowLights = view.fallingLeaves = view.flyingBirds = false;
        yield return null;
        Canvas.ForceUpdateCanvases();
        mesh = activity.canvasRenderer.GetMesh();
        Check(mesh == null || mesh.vertexCount == 0, "activity switches remove their geometry");
        view.windowLights = view.fallingLeaves = view.flyingBirds = true;

        var rect = background.rectTransform;
        Vector2 originalSize = rect.sizeDelta;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1000f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1000f);
        yield return null;
        Vector3 scale = view.IllustrationSpace.localScale;
        Check(Mathf.Abs(scale.x - scale.y) < 0.0001f && Mathf.Abs(scale.x - 1000f / 1920f) < 0.002f,
            "letterboxing preserves effect scale and image registration");
        rect.sizeDelta = originalSize;
        yield return null;

        view.Preview(VillageAmbienceView.Period.Night);
        yield return new WaitForSecondsRealtime(1.5f);
        Check(world.time == 3 && view.CurrentPeriod == 2, "inspector preview does not modify the game clock");
        view.Preview(VillageAmbienceView.Period.GameTime);
        yield return new WaitForSecondsRealtime(1.5f);
        Check(view.CurrentPeriod == 0 && world.time == 3, "returning from preview follows the clock again");
        Check(view.GetComponentsInChildren<Transform>(true).Length == initialChildren,
            "time changes reuse layers instead of leaking objects");

        Check(view.GetComponentsInChildren<AudioSource>(true).Length == 0, "village ambience creates no audio sources");

        var button = screen.focusInn.GetComponent<Button>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            "inn button remains reachable through all atmosphere layers");
        button.onClick.Invoke();
        yield return null;
        Check(screen.panelRoot.activeSelf && screen.panelInn.activeInHierarchy, "existing inn action still opens its panel");
        screen.ClosePanels();
        world.time = gameTime;
        Check(errors == 0, "no runtime errors during scene, clock, rendering and UI checks");
    }

    void Capture(Canvas canvas, string path)
    {
        var camera = Camera.main;
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = new RenderTexture(1920, 1080, 24);
        var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            // A render target can change the camera canvas dimensions synchronously.
            var view = canvas.GetComponentInChildren<VillageAmbienceView>();
            view.RefreshLayout();
            Canvas.ForceUpdateCanvases();
            var backdrop = view.GetComponent<RectTransform>();
            Rect bounds = backdrop.rect;
            float fit = Mathf.Min(bounds.width / 1920f, bounds.height / 1080f);
            Vector3 expected = backdrop.TransformPoint(new Vector3((490f - 960f) * fit, (540f - 348f) * fit));
            Vector3 actual = view.IllustrationSpace.TransformPoint(new Vector3(490f - 960f, 540f - 348f));
            Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(camera, expected),
                RectTransformUtility.WorldToScreenPoint(camera, actual)) < 1f,
                "chimney effects register to the image in the rendered camera canvas");
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            Destroy(image);
            target.Release();
            Destroy(target);
            Canvas.ForceUpdateCanvases();
        }
    }
}
