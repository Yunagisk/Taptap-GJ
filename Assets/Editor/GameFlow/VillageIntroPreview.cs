using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class VillageIntroPreview
{
    const string Pending = "VillageIntroPreview.Pending";
    static double startAfter;
    static VillageIntroPreview() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Game Flow/Preview Full Guide")]
    public static void OpenFullGuide() => Open();
    [MenuItem("Tools/Game Flow/Preview Full Guide", true)]
    static bool CanOpenFullGuide() => CanOpen();

    [MenuItem("Tools/Game Flow/Preview Village Arrival")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/" + GameFlowController.MenuScene + ".unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/Game Flow/Preview Village Arrival", true)]
    static bool CanOpen() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
        {
            SessionState.SetBool(Pending, false);
            startAfter = EditorApplication.timeSinceStartup + .25;
            EditorApplication.update += StartArrival;
        }
        if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= StartArrival;
    }

    static void StartArrival()
    {
        if (EditorApplication.timeSinceStartup < startAfter || GameRoot.I == null) return;
        var flow = Object.FindObjectOfType<GameFlowController>();
        if (flow == null || !flow.isMainMenu) return;
        EditorApplication.update -= StartArrival;
        flow.BeginNewGame();
    }
}
