using UnityEditor;
using UnityEngine;

public static class RpgDialoguePrefabBuilder
{
    [MenuItem("Tools/Game Flow/Build RPG Dialogue Layout")]
    public static void Build()
    {
        const string path = "Assets/Prefabs/UI/DialoguePanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ui = root.GetComponent<DialogueUI>();
            if (ui == null) throw new System.InvalidOperationException("DialoguePanel requires DialogueUI.");
            var view = RpgDialogueLayout.Ensure(ui);
            ui.visibleRoot.SetActive(true);
            view.Present(new DialogueNode { speaker = "说话人" });
            view.SetPage(1, 1, false);
            view.SetChoicesVisible(false);
            ui.visibleRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("RPG dialogue layout saved; existing dialogue references were preserved.");
    }
}
