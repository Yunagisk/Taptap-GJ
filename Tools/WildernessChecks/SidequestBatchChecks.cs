#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SidequestBatchChecks
{
    const string Pending = "SidequestChecks.Pending";
    static SidequestBatchChecks() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated checks only.");
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var method = sizesType.GetMethod("GetGroup");
        var group = method.Invoke(sizes,new[]{Enum.Parse(method.GetParameters()[0].ParameterType,"Standalone")});
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var constructor = sizeType.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
            .First(value=>value.GetParameters().Length==4);
        var size=constructor.Invoke(new[]{Enum.ToObject(constructor.GetParameters()[0].ParameterType,1),(object)1920,1080,"Sidequests"});
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
            .SetValue(view,(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
        SessionState.SetBool(Pending,false);
        var runner=new GameObject("SidequestChecks"); UnityEngine.Object.DontDestroyOnLoad(runner);
        runner.AddComponent<SidequestPlayChecks>();
    }
}
#endif
