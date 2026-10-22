using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ItemBatchChecks
{
    const string Flag = "ItemChecksRunning";
    static ItemBatchChecks() { EditorApplication.playModeStateChanged += OnPlayMode; }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CombatScene.unity");
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        new GameObject("Item Native Checks").AddComponent<ItemPlayModeChecks>();
    }
}
