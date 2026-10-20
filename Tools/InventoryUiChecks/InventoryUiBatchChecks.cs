using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
static class InventoryUiBatchChecks
{
    const string Flag = "InventoryCategoryChecks.Running";
    static InventoryUiBatchChecks() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new System.InvalidOperationException("Use the isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        new GameObject("Inventory Category Native Checks").AddComponent<InventoryUiPlayChecks>();
    }
}
