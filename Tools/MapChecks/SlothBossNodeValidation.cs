using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class SlothBossNodeValidation
{
    const string Pending = "SlothBossNodeValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static readonly List<CaptureFrame> frames = new List<CaptureFrame>();
    static bool finishing;
    [Serializable] class CaptureFrame { public string file; public float time; }
    [Serializable] class Result { public bool passed; public string[] checks; public CaptureFrame[] frames; public string error; }
    static string Output
    {
        get { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-bossNodeResult");
            return i < 0 ? Path.Combine(Application.dataPath, "../sloth-boss-node-validation.json") : args[i + 1]; }
    }
    static SlothBossNodeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            new GameObject("SlothBossNodeCheckHarness").AddComponent<EnemySpriteAnimation>().StartCoroutine(Guard(Exercise()));
        };
        Application.logMessageReceived += (message, stack, type) =>
        { if (SessionState.GetBool(Pending, false) && (type == LogType.Error || type == LogType.Exception)) Finish(false, message + "\n" + stack); };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated Unity project.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMap.unity");
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }
    static IEnumerator Guard(IEnumerator work)
    {
        while (true)
        {
            bool more; try { more = work.MoveNext(); } catch (Exception e) { Finish(false, e.ToString()); yield break; }
            if (!more) break; yield return work.Current;
        }
        Finish(true, null);
    }
    static IEnumerator Exercise()
    {
        yield return null;
        var view = UnityEngine.Object.FindObjectOfType<WorldMapSceneView>(true);
        Check(view != null, "MainMap contains the real map view");
        if (!view.gameObject.activeInHierarchy)
        {
            for (var t = view.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
            yield return null;
        }
        var controller = view.GetComponent<WorldMapController>(); var map = controller.Progression;
        var nodes = view.mapContent.GetComponentsInChildren<MapSceneNode>(true);
        var boss = nodes.Single(n => n.nodeId == "r1_boss"); var normal = nodes.Single(n => n.nodeId == "r1_a");
        var bossRect = (RectTransform)boss.transform; var normalRect = (RectTransform)normal.transform;
        Check(AssetDatabase.GetAssetPath(boss.button.image.sprite) == "Assets/Sprite/UI/MapArt2K/SlothBossNode.png",
            "actual MainMap prefab uses the final shrine artwork");
        Check(Mathf.Abs(bossRect.rect.width * bossRect.rect.height / (normalRect.rect.width * normalRect.rect.height) - 3) < .001f,
            "boss node occupies exactly three times the ordinary node rectangle area");
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(boss.button.image.sprite));
        Check(importer.textureType == TextureImporterType.Sprite && importer.alphaIsTransparency && !importer.mipmapEnabled,
            "node imports as a transparent, non-mipmapped UI sprite");
        Check(view.IsRevealing && boss.GetComponent<MapNodeRevealEffect>() != null,
            "initially spawned Sloth starts the same MapNodeRevealEffect as ordinary nodes");
        int time = map.World.time, sin = map.World.sin; string state = JsonUtility.ToJson(map.State);
        float started = Time.unscaledTime, maxScale = 0; bool sawCloseup = false, sawNodeReveal = false, stayedBlocked = true;
        Vector2 ordinaryPose = Vector2.zero; bool capturedOrdinary = false;
        int frame = 0;
        while (view.IsRevealing && Time.unscaledTime - started < 8)
        {
            maxScale = Mathf.Max(maxScale, view.mapContent.localScale.x);
            sawCloseup |= view.viewport.IsBossCloseup;
            sawNodeReveal |= boss.GetComponent<MapNodeRevealEffect>().enabled;
            stayedBlocked &= !normal.button.interactable;
            if (view.viewport.IsBossCloseup && !capturedOrdinary)
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                ordinaryPose = (Vector2)typeof(WorldMapViewport).GetField("beforeBossCenter", flags).GetValue(view.viewport);
                capturedOrdinary = true;
                var oldPosition = view.mapContent.anchoredPosition;
                view.viewport.OnScroll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width / 2, Screen.height / 2), scrollDelta = new Vector2(0, 10) });
                Check(oldPosition == view.mapContent.anchoredPosition, "scroll input cannot interrupt the introduction camera");
            }
            Capture("introduction/" + (frame++).ToString("000") + ".png", Time.unscaledTime - started);
            yield return null;
        }
        Check(!view.IsRevealing && sawCloseup && sawNodeReveal && stayedBlocked && maxScale > 1.5f,
            "camera approaches, shows the ordinary ink reveal, holds, returns and unlocks the map");
        Check(!view.viewport.IsBossCloseup && normal.button.interactable, "navigation and ordinary-node interaction are restored");
        var privateFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        var center = (Vector2)typeof(WorldMapViewport).GetField("center", privateFlags).GetValue(view.viewport);
        Check(capturedOrdinary && Vector2.Distance(center, ordinaryPose) < .01f, "camera returns to its previous exploration center");
        Check(map.World.time == time && map.World.sin == sin && JsonUtility.ToJson(map.State) == state,
            "the cinematic never changes map progress, time, sin or boss availability");
        view.Refresh(); yield return null;
        Check(!view.IsRevealing, "repeated Refresh does not replay the introduction");
        view.gameObject.SetActive(false); view.gameObject.SetActive(true); view.Refresh(); yield return null;
        Check(!view.IsRevealing, "temporarily closing the map does not replay the introduction");
        normal.PrepareReveal();
        Check(normal.GetComponent<MapNodeRevealEffect>().GetType() == boss.GetComponent<MapNodeRevealEffect>().GetType(),
            "boss and ordinary nodes use the exact same reveal component");
        normal.CompleteReveal();

        // Repeat with a fresh presentation memory to verify cancellation during a shot.
        GameRoot.I.mapBossIntroductions = new MapBossIntroductionState();
        view.Refresh(); yield return null; yield return null;
        Check(view.IsRevealing && view.viewport.IsBossCloseup, "a new presentation state can introduce Sloth");
        view.gameObject.SetActive(false);
        Check(!view.IsRevealing && !view.viewport.IsBossCloseup, "closing the map during a close-up cancels camera and reveal cleanly");
        view.gameObject.SetActive(true); yield return null;

        // A newly spawned boss can be disconnected from any newly revealed road.
        GameRoot.I.mapBossIntroductions = new MapBossIntroductionState();
        map.State.revealedNodes.Remove(boss.nodeId); view.Refresh(); yield return null;
        Check(!boss.gameObject.activeSelf, "an unrevealed boss stays hidden");
        map.State.revealedNodes.Add(boss.nodeId); view.Refresh(); yield return null; yield return null;
        Check(view.IsRevealing && view.viewport.IsBossCloseup && boss.gameObject.activeSelf,
            "a boss appearing without a new incoming road still receives its reveal and close-up");
        float deadline = Time.unscaledTime + 6;
        while (view.IsRevealing && Time.unscaledTime < deadline) yield return null;
        Check(!view.IsRevealing && normal.button.interactable, "a disconnected boss introduction also completes and restores control");
        var bounds = MapUITransform.Bounds(bossRect, view.mapContent);
        Check(view.viewport.BeginBossCloseup(bounds), "can frame the authored node for the final preview");
        yield return new WaitForSecondsRealtime(.65f);
        Capture("closeup.png", 0);
        view.viewport.EndBossCloseup(true);
        var explorationCenter = (Vector2)typeof(WorldMapViewport).GetField("center", privateFlags).GetValue(view.viewport);
        Check(view.viewport.BeginTravelPreview(new Rect(-1700, -400, 1900, 1300)), "travel preview can start after the boss shot");
        yield return new WaitForSecondsRealtime(.12f);
        var previousPose = view.mapContent.anchoredPosition;
        Check(view.viewport.BeginBossCloseup(bounds) && previousPose == view.mapContent.anchoredPosition,
            "a boss shot takes over a travel preview without teleporting the camera");
        yield return new WaitForSecondsRealtime(.65f);
        view.viewport.EndBossCloseup();
        yield return new WaitForSecondsRealtime(.65f);
        center = (Vector2)typeof(WorldMapViewport).GetField("center", privateFlags).GetValue(view.viewport);
        Check(!view.viewport.IsTravelPreview && !view.viewport.IsBossCloseup && Vector2.Distance(center, explorationCenter) < .01f,
            "camera handoff returns to exploration rather than the temporary travel-preview pose");
    }
    static void Capture(string file, float time)
    {
        var camera = Camera.main;
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray();
        var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var distances = canvases.Select(c => c.planeDistance).ToArray();
        var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
        var target = new RenderTexture(1600, 900, 24); target.Create();
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            foreach (var canvas in canvases)
            { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            string path = Path.Combine(Path.GetDirectoryName(Output), file); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = previousTarget;
            for (int i = 0; i < canvases.Length; i++)
            { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
            UnityEngine.Object.Destroy(image); target.Release(); UnityEngine.Object.Destroy(target);
            Canvas.ForceUpdateCanvases();
        }
        if (file.StartsWith("introduction/")) frames.Add(new CaptureFrame { file = file, time = time });
    }
    static void Check(bool pass, string text)
    { if (!pass) throw new InvalidOperationException(text); checks.Add(text); Debug.Log("SLOTH BOSS NODE CHECK: " + text); }
    static void Finish(bool pass, string error)
    {
        if (finishing) return; finishing = true; SessionState.SetBool(Pending, false); Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = pass, checks = checks.ToArray(), frames = frames.ToArray(), error = error }, true));
        EditorApplication.Exit(pass ? 0 : 1);
    }
}
