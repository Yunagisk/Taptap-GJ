using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MapBossCameraValidation
{
    const string Pending = "MapBossCameraValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static bool finished;
    static int coverageSamples;
    static WorldMapSceneView view;
    static string Output { get { var args = Environment.GetCommandLineArgs(); return args[Array.IndexOf(args, "-bossCameraResult") + 1]; } }
    [Serializable] sealed class Result { public bool passed; public string[] checks; public int coverageSamples; public string error; }

    static MapBossCameraValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                new GameObject("BossCameraHarness").AddComponent<BattleSimulatorRound>().StartCoroutine(Guard(Exercise()));
        };
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (!finished && SessionState.GetBool(Pending, false)
                && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                Finish(new Exception(message + "\n" + trace));
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated validation project only.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMap.unity");
        SetGameSize(1600, 900);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    static IEnumerator Guard(IEnumerator work)
    {
        while (!finished)
        {
            bool more;
            try { more = work.MoveNext(); }
            catch (Exception error) { Finish(error); yield break; }
            if (!more) { Finish(null); yield break; }
            yield return work.Current;
        }
    }
    static IEnumerator Exercise()
    {
        yield return new WaitForSecondsRealtime(4);
        view = UnityEngine.Object.FindObjectOfType<WorldMapSceneView>();
        var controller = view.GetComponent<WorldMapController>();
        var map = controller.Progression;
        Check(Camera.main.backgroundColor == WorldMapViewport.PaperColor, "production scene camera has a paper fallback");
        Check(view.viewport.GetComponent<Image>().color.a == 1, "screen-sized viewport always has an opaque paper backing");
        // Deliberately make camera clearing blue: pixel checks must be protected
        // by real artwork coverage/backing rather than only by the fallback color.
        Camera.main.backgroundColor = new Color(.1f, .3f, .7f, 1);
        foreach (string id in new[] { "r1_boss", "r2_boss", "r3_boss" })
        {
            if (!map.State.spawnedBossNodes.Contains(id)) map.State.spawnedBossNodes.Add(id);
            if (!map.State.revealedNodes.Contains(id)) map.State.revealedNodes.Add(id);
        }
        GameRoot.I.mapBossIntroductions = new MapBossIntroductionState();
        foreach (string id in new[] { "r2_boss", "r3_boss" }) GameRoot.I.mapBossIntroductions.MarkShown(map.State, id);
        string savedState = JsonUtility.ToJson(GameRoot.I.state);
        view.Refresh();
        float deadline = Time.unscaledTime + 15;
        bool sawShot = false;
        while (view.IsRevealing && Time.unscaledTime < deadline)
        {
            sawShot |= view.viewport.IsBossCloseup;
            Coverage(); yield return null;
        }
        Check(sawShot && !view.IsRevealing && !view.viewport.IsBossCloseup, "real Sloth introduction completes without exposing artwork edges on any sampled frame");
        Check(JsonUtility.ToJson(GameRoot.I.state) == savedState, "introduction changes neither map progress nor player resources");

        foreach (var size in new[] { new Vector2(1600, 900), new Vector2(1200, 900), new Vector2(2100, 900) })
        {
            SetGameSize((int)size.x, (int)size.y); yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (string id in new[] { "r1_boss", "r2_boss", "r3_boss" })
            {
                var bounds = MapUITransform.Bounds(view.NodeTransform(id), view.mapContent);
                Check(view.viewport.BeginBossCloseup(bounds), "start edge close-up " + id + " at " + size);
                float start = Time.unscaledTime; bool capturedApproach = false;
                while (Time.unscaledTime - start < WorldMapViewport.BossFocusDuration + .1f)
                {
                    Coverage();
                    if (!capturedApproach && Time.unscaledTime - start > .2f && id == "r1_boss")
                    { Capture((int)size.x + "-approach.png"); capturedApproach = true; }
                    yield return null;
                }
                var node = MapUITransform.Bounds(view.NodeTransform(id), (RectTransform)view.viewport.transform);
                var window = ((RectTransform)view.viewport.transform).rect;
                Check(window.Contains(node.min) && window.Contains(node.max), "entire boss node stays visible " + id + " at " + size);
                if (id == "r1_boss") Capture((int)size.x + "-closeup.png");
                view.viewport.EndBossCloseup();
                start = Time.unscaledTime;
                while (Time.unscaledTime - start < WorldMapViewport.BossFocusDuration + .1f)
                { Coverage(); yield return null; }
                Check(!view.viewport.IsBossCloseup, "return restores exploration " + id + " at " + size);
            }
        }
        var bossBounds = MapUITransform.Bounds(view.NodeTransform("r1_boss"), view.mapContent);
        view.viewport.BeginBossCloseup(bossBounds); yield return null;
        SetGameSize(1200, 900); yield return null; yield return null;
        Coverage(); Capture("resize-during-closeup.png");
        view.viewport.EndBossCloseup(true); Coverage();
        Check(!view.viewport.IsBossCloseup, "immediate cancellation restores a covered exploration frame");

        Check(view.viewport.BeginTravelPreview(new Rect(-1700, -400, 1900, 1300)), "wide travel preview starts");
        yield return new WaitForSecondsRealtime(.65f);
        Capture("travel-paper-margin.png");
        Vector2 oldPose = view.mapContent.anchoredPosition;
        Check(view.viewport.BeginBossCloseup(bossBounds) && oldPose == view.mapContent.anchoredPosition,
            "travel-to-boss handoff preserves its starting pose");
        yield return new WaitForSecondsRealtime(.15f); Capture("travel-to-boss.png");
        yield return new WaitForSecondsRealtime(.5f); Coverage();
        view.viewport.EndBossCloseup(); yield return new WaitForSecondsRealtime(.7f); Coverage();
        Check(!view.viewport.IsTravelPreview && !view.viewport.IsBossCloseup && view.viewport.GetComponent<Image>().color.a == 1,
            "handoff and return retain paper backing and restore normal navigation");
        Check(coverageSamples > 100, "camera bounds sampled throughout approach, hold and return");
    }
    static void Coverage()
    {
        Canvas.ForceUpdateCanvases();
        view.viewport.SendMessage("Update");
        var painted = MapUITransform.Bounds(view.originalBackground.rectTransform, (RectTransform)view.viewport.transform);
        var window = ((RectTransform)view.viewport.transform).rect;
        if (painted.xMin > window.xMin + 1 || painted.xMax < window.xMax - 1
            || painted.yMin > window.yMin + 1 || painted.yMax < window.yMax - 1)
            throw new Exception("Camera left the painted map: artwork=" + painted + "; viewport=" + window);
        coverageSamples++;
    }
    static void Capture(string filename)
    {
        var camera = Camera.main;
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray(); var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        int width = Screen.width, height = Screen.height;
        var target = new RenderTexture(width, height, 24); target.Create();
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            camera.targetTexture = target; Canvas.ForceUpdateCanvases();
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) text.ForceMeshUpdate();
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Output), filename), pixels.EncodeToPNG());
            var colors = pixels.GetPixels32();
            // Detect the bright blue clearing color, not isolated dark purple
            // pixels that are part of the painted node's original outline.
            var blue = Enumerable.Range(0, colors.Length).Where(i => colors[i].b > 130 && colors[i].g > 40
                && colors[i].b > colors[i].r + 45 && colors[i].b > colors[i].g + 45).ToArray();
            Check(blue.Length == 0, filename + " contains no camera-blue pixels; count=" + blue.Length
                + (blue.Length == 0 ? "" : "; first=" + blue[0] % width + "," + blue[0] / width + " color=" + colors[blue[0]]));
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; }
            Canvas.ForceUpdateCanvases(); UnityEngine.Object.Destroy(pixels); target.Release(); UnityEngine.Object.Destroy(target);
        }
    }
    static void SetGameSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly; var type = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
        var method = type.GetMethod("GetGroup");
        var group = method.Invoke(sizes, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var ctor = sizeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).First(c => c.GetParameters().Length == 4);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { ctor.Invoke(new[] { Enum.ToObject(ctor.GetParameters()[0].ParameterType, 1), (object)width, height, "Boss camera check" }) });
        var game = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        game.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(game, (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1);
    }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks.Add(name); }
    static void Finish(Exception error)
    {
        if (finished) return; finished = true; SessionState.SetBool(Pending, false);
        File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = error == null, checks = checks.ToArray(), coverageSamples = coverageSamples, error = error?.ToString() }, true));
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
