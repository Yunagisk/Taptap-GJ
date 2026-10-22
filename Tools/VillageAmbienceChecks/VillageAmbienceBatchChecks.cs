using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class VillageAmbienceBatchChecks
{
    const string Flag = "VillageAmbienceChecksRunning";
    static VillageAmbienceBatchChecks() { EditorApplication.playModeStateChanged += OnPlayMode; }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Village.unity");
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        new GameObject("Village ambience checks").AddComponent<VillageAmbiencePlayModeChecks>();
    }
}
