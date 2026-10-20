using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DialogueUiBatchChecks
{
    const string Flag = "DialogueUiChecksRunning";
    static DialogueUiBatchChecks() { EditorApplication.playModeStateChanged += OnPlayMode; }

    public static void Run()
    {
        RpgDialoguePrefabBuilder.Build();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        var runner = new GameObject("Dialogue checks");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<DialogueUiPlayChecks>();
    }
}
