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
public static class MapFastTravelValidation
{
    const string Pending = "MapFastTravelValidation.Pending";
    static readonly List<string> checks = new List<string>();
    static bool finished;
    static string Output { get { var args = Environment.GetCommandLineArgs(); return args[Array.IndexOf(args, "-travelResult") + 1]; } }
    [Serializable] sealed class Result { public bool passed; public string[] checks; public string error; }
    static MapFastTravelValidation()
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
        yield return new WaitForSecondsRealtime(5);
        var controller = UnityEngine.Object.FindObjectOfType<WorldMapController>();
        var view = controller.GetComponent<WorldMapSceneView>();
        var map = controller.Progression;
        double deadline = Time.realtimeSinceStartupAsDouble + 15;
        while (view.IsRevealing && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Check(!view.IsRevealing && view.viewport != null, "real map finishes its initial discovery before travel");
        foreach (var node in map.Layout.nodes)
        {
            if (!map.State.visitedNodes.Contains(node.id)) map.State.visitedNodes.Add(node.id);
            if (!map.State.clearedNodes.Contains(node.id)) map.State.clearedNodes.Add(node.id);
            if (!map.State.revealedNodes.Contains(node.id)) map.State.revealedNodes.Add(node.id);
        }
        foreach (var region in map.Layout.regions)
            if (!map.State.spawnedBossNodes.Contains(region.bossNodeId)) map.State.spawnedBossNodes.Add(region.bossNodeId);
        map.State.currentNodeId = "r1_camp"; map.World.time = 2;
        view.Refresh();
        yield return new WaitForSecondsRealtime(.6f);
        var destination = view.NodeTransform("r3_camp").GetComponent<MapSceneNode>();
        Check(destination.button.interactable && !map.CanMove(destination.nodeId), "distant visited node is clickable without pretending it is adjacent");
        int encounters = 0, villages = 0;
        controller.NodeEntered += _ => encounters++; controller.onVillageEntered.AddListener(() => villages++);
        int time = map.World.time;
        string before = JsonUtility.ToJson(GameRoot.I.state);
        Vector2 oldPosition = view.mapContent.anchoredPosition; Vector3 oldScale = view.mapContent.localScale;
        Click(destination.button);
        Check(controller.IsFastTravelConfirmationOpen && controller.FastTravelUI.IsOpen,
            "clicking the native distant node opens the travel confirmation");
        var plan = controller.PendingTravel;
        Check(plan.TimeCost >= 4 && JsonUtility.ToJson(GameRoot.I.state) == before,
            "native route preview has a real multi-edge quote and spends no resources");
        Check(controller.FastTravelUI.transform.Find("TravelCard/Arrival").GetComponent<TMP_Text>().text.Contains(WorldResourceRules.TimeLabel(plan.ArrivalTime)),
            "confirmation reports the exact arrival day and period");
        yield return new WaitForSecondsRealtime(.65f);
        CheckFraming(view, plan, controller.FastTravelUI);
        CheckRaycast(controller.FastTravelUI.ConfirmButton);
        CheckRaycast(controller.FastTravelUI.CancelButton);
        Capture("route-preview.png", view);
        Capture("route-preview-4x3.png", view, 1200, 900);
        yield return new WaitForSecondsRealtime(.5f);
        controller.MoveToNode("r2_camp");
        Check(controller.PendingTravel == plan && map.World.time == time, "clicking another map destination cannot overwrite an open confirmation");
        Click(controller.FastTravelUI.CancelButton);
        yield return new WaitForSecondsRealtime(.5f);
        Check(!controller.IsFastTravelConfirmationOpen && JsonUtility.ToJson(GameRoot.I.state) == before,
            "canceling changes neither position nor time nor any run data");
        Check(Vector2.Distance(view.mapContent.anchoredPosition, oldPosition) < 1 && Vector3.Distance(view.mapContent.localScale, oldScale) < .01f,
            "canceling restores the previous camera position and zoom");
        Check(!view.mapContent.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("TravelEndpoint_")),
            "cancellation removes the temporary endpoint markers");
        Click(destination.button); yield return new WaitForSecondsRealtime(.45f);
        plan = controller.PendingTravel;
        Click(controller.FastTravelUI.ConfirmButton); controller.ConfirmFastTravel();
        Check(map.State.currentNodeId == destination.nodeId && map.World.time == plan.ArrivalTime,
            "confirming moves to the selected location and pays the exact quoted cost once");
        Check(!controller.IsFastTravelConfirmationOpen && map.State.pendingRequestId == null && encounters == 0 && villages == 0,
            "fast travel never dispatches intermediate encounters, camps or village scenes");
        yield return new WaitForSecondsRealtime(1.1f);
        Capture("arrived.png", view);
        Check(!view.viewport.IsTravelPreview, "arrival returns camera controls to normal map navigation");
        controller.MoveToNode("r1_camp"); yield return new WaitForSecondsRealtime(.4f);
        Check(controller.PendingTravel != null, "return route can be previewed from the new location");
        time = ++map.World.time; string location = map.State.currentNodeId;
        controller.ConfirmFastTravel();
        Check(!controller.IsFastTravelConfirmationOpen && map.World.time == time && map.State.currentNodeId == location,
            "changing world time while a confirmation is open invalidates it without extra charges");
        yield return new WaitForSecondsRealtime(.45f);
        controller.MoveToNode("r1_camp");
        controller.FastTravelUI.gameObject.SetActive(false);
        Check(!controller.IsFastTravelConfirmationOpen && map.World.time == time,
            "disabling the confirmation releases its pending quote without paying");
        yield return new WaitForSecondsRealtime(.45f);
        controller.MoveToNode("r1_camp"); controller.enabled = false;
        Check(!controller.IsFastTravelConfirmationOpen && !controller.FastTravelUI.IsOpen,
            "disabling the map controller cancels its travel UI");
        controller.MoveToNode("r1_camp");
        Check(!controller.IsFastTravelConfirmationOpen, "disabled controller cannot reopen a travel confirmation");
        controller.enabled = true;
        yield return new WaitForSecondsRealtime(.45f);
        controller.MoveToNode("r1_camp");
        var flow = UnityEngine.Object.FindObjectOfType<GameFlowController>();
        Check(flow != null && controller.IsFastTravelConfirmationOpen, "Escape integration fixture has the active game navigation controller");
        flow.HandleBack();
        Check(!controller.IsFastTravelConfirmationOpen && !flow.pauseOverlay.activeSelf,
            "Escape cancels the travel confirmation without also opening the pause menu");
        yield return new WaitForSecondsRealtime(.45f);
        map.State.visitedNodes.Remove("r1_camp"); view.Refresh();
        Check(!view.NodeTransform("r1_camp").GetComponent<MapSceneNode>().button.interactable,
            "unvisited distant destinations remain unavailable");
        Check(map.World.time == time && encounters == 0, "cancellation and rejection tests never trigger encounters or additional travel time");
        controller.MoveToNode("r2_camp");
        Check(controller.IsFastTravelConfirmationOpen, "scene cleanup starts with an open travel confirmation");
        UnityEngine.Object.Destroy(view.canvas.gameObject);
        yield return null;
        Check(controller == null || !controller.IsFastTravelConfirmationOpen,
            "destroying the map canvas releases the quote without stale view callbacks");
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
