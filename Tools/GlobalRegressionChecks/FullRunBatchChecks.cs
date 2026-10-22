using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FullRunBatchChecks
{
    const string Pending = "FullRunChecks.Pending";
    static FullRunBatchChecks() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated checks only.");
        var content = Resources.Load<VillageIntroContent>(VillageIntroController.ContentResource);
        if (content == null || content.dialoguePrefab == null || content.streetDialogue == null
            || content.arrivalBackground == null || content.streetBackground == null)
            throw new InvalidOperationException("Introduction resources do not deserialize.");
        foreach (var data in new[] { content.arrivalDialogue, content.streetDialogue, content.mayorApproachDialogue,
            content.blacksmithDialogue, content.outsideDialogue, content.encounterDialogue, content.returnDialogue,
            content.reportDialogue, content.switchTutorial, content.lanternTutorial, content.resolveTutorial,
            content.breakTutorial, content.itemTutorial }) ValidateDialogue(data);
        if (content.tutorialEnemy == null || content.tutorialEnemy.actions.Length == 0
            || content.blacksmithBackground == null || content.outsideBackground == null)
            throw new InvalidOperationException("Guide gameplay or background resources are missing.");
        if (content.slothShrineBackground == null || content.slothShrineBackground == content.outsideBackground)
            throw new InvalidOperationException("Sloth's shrine must have its own installed background.");
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var method = sizesType.GetMethod("GetGroup");
        var group = method.Invoke(sizes, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var constructor = sizeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .First(value => value.GetParameters().Length == 4);
        var size = constructor.Invoke(new[] { Enum.ToObject(constructor.GetParameters()[0].ParameterType, 1),
            (object)1920, 1080, "Village introduction" });
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(view, (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    static void ValidateDialogue(DialogueData data)
    {
        if (data == null || data.startNode == null) throw new InvalidOperationException("Missing guide dialogue.");
        var nodes = new[] { data.startNode }.Concat(data.nodes).ToDictionary(node => node.id);
        var visited = new HashSet<string>();
        var pending = new Stack<string>(); pending.Push(data.startNode.id);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!visited.Add(id)) continue;
            if (!nodes.TryGetValue(id, out var node)) throw new InvalidOperationException(data.name + " has missing target " + id);
            if (string.IsNullOrWhiteSpace(node.content)) throw new InvalidOperationException(data.name + " has empty node " + id);
            if (!string.IsNullOrEmpty(node.nextNodeID)) pending.Push(node.nextNodeID);
            foreach (var choice in node.choices) if (!string.IsNullOrEmpty(choice.targetNodeID)) pending.Push(choice.targetNodeID);
        }
        if (visited.Count != nodes.Count) throw new InvalidOperationException(data.name + " has unreachable guide lines.");
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        var runner = new GameObject("VillageIntroChecks");
        UnityEngine.Object.DontDestroyOnLoad(runner);
        runner.AddComponent<FullRunPlayChecks>();
    }
}
