using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TavernBatchChecks
{
    const string Pending = "TavernChecks.Pending";
    static TavernBatchChecks()
    {
        EditorApplication.playModeStateChanged += change => {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            var runner = new GameObject("Tavern checks");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<TavernPlayChecks>();
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated tavern checks only.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
}
