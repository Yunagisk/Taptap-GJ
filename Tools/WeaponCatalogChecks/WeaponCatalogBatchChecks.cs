using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WeaponCatalogBatchChecks
{
    const string Pending = "WeaponCatalogChecks.Pending";
    static WeaponCatalogBatchChecks()
    {
        EditorApplication.playModeStateChanged += change => {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            var host = new GameObject("Weapon catalogue checks");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<WeaponCatalogPlayChecks>();
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new System.InvalidOperationException("Isolated checks only.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
}
