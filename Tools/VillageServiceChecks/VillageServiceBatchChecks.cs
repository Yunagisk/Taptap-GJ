using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class VillageServiceBatchChecks
{
    const string Flag = "VillageServiceChecks.Pending";
    static VillageServiceBatchChecks() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated village checks only.");
        VillageServicesBuilder.Build();
        ConfigureView();
        EditorSceneManager.OpenScene(VillageServicesBuilder.ScenePath);
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        new GameObject("Village service checks").AddComponent<VillageServicePlayChecks>();
    }

    static void ConfigureView()
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var method = sizesType.GetMethod("GetGroup");
        var group = method.Invoke(sizes, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var ctor = sizeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .First(value => value.GetParameters().Length == 4);
        var size = ctor.Invoke(new[] { Enum.ToObject(ctor.GetParameters()[0].ParameterType, 1), (object)1920, 1080, "Village services" });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] {size});
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(view, (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1);
    }
}
