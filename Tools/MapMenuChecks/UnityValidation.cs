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
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MapMenuValidation
{
    const string Pending = "MapMenuValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static bool finished;
    static string Output { get { var args = Environment.GetCommandLineArgs(); return args[Array.IndexOf(args, "-menuResult") + 1]; } }
    [Serializable] sealed class Result { public bool passed; public string[] checks; public string error; }
    static MapMenuValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        Application.logMessageReceived += Logged;
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated native validation only.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMap.unity");
        SetGameSize();
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void SetGameSize()
    {
        var assembly = typeof(Editor).Assembly; var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var method = sizesType.GetMethod("GetGroup");
        var group = method.Invoke(sizes, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var constructor = sizeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).First(c => c.GetParameters().Length == 4);
        var size = constructor.Invoke(new[] { Enum.ToObject(constructor.GetParameters()[0].ParameterType, 1), (object)1600, 900, "Fast travel validation" });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(view, (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1);
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            new GameObject("FastTravelHarness").AddComponent<BattleSimulatorRound>().StartCoroutine(Guard(Exercise()));
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
        var controller = UnityEngine.Object.FindObjectOfType<WorldMapController>();
        var view = controller.GetComponent<WorldMapSceneView>();
        var flow = UnityEngine.Object.FindObjectOfType<GameFlowController>();
        double deadline = Time.realtimeSinceStartupAsDouble + 20;
        while (view.IsRevealing && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(!view.IsRevealing, "initial map reveal completes");
        Check(view.noticeText == null && view.bossCountText == null && view.sinText == null,
            "map no longer binds stage, boss count or sin debug text");
        Check(!view.canvas.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "Txt_Stage" || t.name == "Txt_BossCount"),
            "obsolete stage and boss count objects are removed from the production hierarchy");
        Check(flow.transform.Find("Btn_GameMenu") == null, "top-right menu button is removed");
        var controls = view.viewport.transform.Find("MapNavigation");
        Check(controls.Find("Overview") == null && controls.Find("Hint") == null,
            "overview button and drag/zoom hint are removed");
        var focus = controls.Find("Focus").GetComponent<Button>();
        var inventory = controls.Find("Inventory").GetComponent<Button>();
        Check(controls.GetComponentsInChildren<Button>().Length == 2 && focus.GetComponentInChildren<TMP_Text>().text == "定位"
            && inventory.GetComponentInChildren<TMP_Text>().text == "背包",
            "map navigation contains inventory followed by the existing recenter button");
        Check(focus.GetComponentInChildren<InventoryInkGraphic>() != null, "recenter uses the ink border style");
        CheckRaycast(inventory); Click(inventory); yield return null;
        Check(flow.playerPanel.IsInventoryOpen, "new inventory button opens the existing equipment panel");
        flow.HandleBack(); yield return new WaitForSecondsRealtime(.6f);
        Check(!flow.playerPanel.IsInventoryOpen && !flow.pauseOverlay.activeSelf, "Escape closes inventory and returns to the map");
        Click(focus); yield return new WaitForSecondsRealtime(.5f);
        Vector2 centered = view.mapContent.anchoredPosition; Vector3 scale = view.mapContent.localScale;
        var pointer = new PointerEventData(EventSystem.current) { pointerId = 2, position = new Vector2(800, 450) };
        view.viewport.OnBeginDrag(pointer); pointer.position += new Vector2(180, 110);
        view.viewport.OnDrag(pointer); view.viewport.OnEndDrag(pointer);
        yield return null;
        Check(Vector2.Distance(view.mapContent.anchoredPosition, centered) > 10, "map still supports drag navigation");
        CheckRaycast(focus); Click(focus); yield return new WaitForSecondsRealtime(.5f);
        Check(Vector2.Distance(view.mapContent.anchoredPosition, centered) < 1 && Vector3.Distance(view.mapContent.localScale, scale) < .01f,
            "renamed recenter button restores the same player position and zoom");
        Capture("map-clean.png", view);
        string state = JsonUtility.ToJson(GameRoot.I.state);
        flow.HandleBack(); yield return null;
        Check(flow.pauseOverlay.activeSelf, "Escape router opens the pause menu without a visible menu button");
        var pause = flow.pauseOverlay.transform.Find("Window");
        Check(pause.GetComponent<Image>().sprite == flow.menuPaper && pause.Find("InkFrame") != null,
            "pause menu uses production parchment and hand-drawn ink frame");
        foreach (var button in pause.GetComponentsInChildren<Button>())
        {
            Check(button.GetComponentInChildren<InventoryInkGraphic>() != null, button.name + " uses ink button artwork");
            CheckRaycast(button);
        }
        Capture("pause-menu.png", view); Capture("pause-menu-4x3.png", view, 1200, 900);
        flow.HandleBack();
        Check(!flow.pauseOverlay.activeSelf, "Escape router closes the pause menu");
        flow.HandleBack(); Click(pause.Find("Btn_PauseSettings").GetComponent<Button>());
        yield return null;
        Check(flow.settingsOverlay.activeSelf && !flow.pauseOverlay.activeSelf, "settings opens from pause");
        flow.volumeSlider.value = .37f;
        Check(Mathf.Abs(AudioListener.volume - .37f) < .01f && flow.volumeText.text == "37%", "styled volume slider retains its callback");
        Check(flow.fullscreenToggle.GetComponentInChildren<InventoryInkGraphic>() != null, "fullscreen toggle uses ink styling");
        CheckRaycast(flow.settingsOverlay.transform.Find("Window/Btn_SettingsDone").GetComponent<Button>());
        Capture("settings.png", view);
        flow.HandleBack();
        Check(flow.pauseOverlay.activeSelf && !flow.settingsOverlay.activeSelf, "Escape from settings returns to pause");
        Click(pause.Find("Btn_ReturnTitle").GetComponent<Button>());
        yield return null;
        Check(flow.confirmOverlay.activeSelf && flow.confirmationText.text.Contains("\u4e3b\u83dc\u5355"), "return to title retains its confirmation");
        CheckRaycast(flow.confirmOverlay.transform.Find("Window/Btn_ConfirmCancel").GetComponent<Button>());
        CheckRaycast(flow.confirmOverlay.transform.Find("Window/Btn_ConfirmAccept").GetComponent<Button>());
        Capture("confirmation.png", view);
        flow.HandleBack(); Check(!flow.confirmOverlay.activeSelf && flow.pauseOverlay.activeSelf, "Escape cancels confirmation without returning to title");
        Click(pause.Find("Btn_PauseQuit").GetComponent<Button>());
        Check(flow.confirmOverlay.activeSelf && flow.confirmationText.text.Contains("\u9000\u51fa"), "quit button retains its distinct confirmation");
        Click(flow.confirmOverlay.transform.Find("Window/Btn_ConfirmCancel").GetComponent<Button>());
        Click(pause.Find("Btn_Resume").GetComponent<Button>());
        Check(!flow.pauseOverlay.activeSelf && !flow.confirmOverlay.activeSelf, "resume returns to the clean map");
        Check(JsonUtility.ToJson(GameRoot.I.state) == state, "menu navigation preserves run state");
    }
    static void CheckFraming(WorldMapSceneView view, MapTravelPlan plan, MapFastTravelUI ui)
    {
        Canvas.ForceUpdateCanvases();
        Rect panel = ScreenBounds((RectTransform)ui.transform.Find("TravelCard"));
        foreach (string id in new[] { plan.OriginId, plan.DestinationId })
        {
            Rect node = ScreenBounds(view.NodeTransform(id));
            Check(node.xMin >= 0 && node.xMax <= Screen.width && node.yMax <= Screen.height
                && node.yMin > panel.yMax + 10, "wide travel camera keeps " + id + " fully visible above the confirmation card");
        }
    }
    static Rect ScreenBounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
    static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    static void CheckRaycast(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = ScreenBounds((RectTransform)button.transform).center }, hits);
        Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "confirmation " + button.name + " button is reachable through the actual raycast stack");
    }
    static void Capture(string name, WorldMapSceneView view, int width = 1600, int height = 900)
    {
        var camera = Camera.main; var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray(); var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var render = new RenderTexture(width, height, 24); render.Create();
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            camera.targetTexture = render; Canvas.ForceUpdateCanvases();
            view.viewport.SendMessage("Update");
            typeof(WorldMapViewport).GetField("startedAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view.viewport, Time.unscaledTime - 10);
            view.viewport.SendMessage("Update");
            if (view.GetComponent<WorldMapController>().FastTravelUI != null && view.GetComponent<WorldMapController>().FastTravelUI.IsOpen)
                view.GetComponent<WorldMapController>().FastTravelUI.SendMessage("LateUpdate");
            foreach (var label in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = render;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Output), name), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; }
            Canvas.ForceUpdateCanvases(); UnityEngine.Object.Destroy(pixels); render.Release(); UnityEngine.Object.Destroy(render);
        }
    }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks.Add(name); }
    static void Logged(string message, string trace, LogType type)
    {
        if (!finished && SessionState.GetBool(Pending, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            Finish(new Exception(message + "\n" + trace));
    }
    static void Finish(Exception error)
    {
        if (finished) return; finished = true; SessionState.SetBool(Pending, false);
        File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = error == null, checks = checks.ToArray(), error = error?.ToString() }, true));
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
