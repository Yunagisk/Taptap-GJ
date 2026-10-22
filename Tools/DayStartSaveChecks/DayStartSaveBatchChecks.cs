using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DayStartSaveBatchChecks
{
    const string Pending = "DayStartSaveChecks.Pending";
    static DayStartSaveBatchChecks() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated checks only.");
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var get = sizesType.GetMethod("GetGroup");
        var group = get.Invoke(sizes, new[] { Enum.Parse(get.GetParameters()[0].ParameterType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var constructor = sizeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .First(value => value.GetParameters().Length == 4);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] {
            constructor.Invoke(new[] { Enum.ToObject(constructor.GetParameters()[0].ParameterType, 1),
                (object)1920, 1080, "Day start save checks" }) });
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(view, (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        var host = new GameObject("DayStartSaveChecks");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<DayStartSavePlayChecks>();
    }
}
