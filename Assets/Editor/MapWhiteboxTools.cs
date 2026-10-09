using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MapWhiteboxTools
{
    [MenuItem("Tools/Map Whitebox/Open Scene")]
    public static void OpenScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(MapSceneBuilder.ScenePath);
    }

    [MenuItem("Tools/Map Whitebox/Validate Template")]
    public static void ValidateTemplate()
    {
        var template = AssetDatabase.LoadAssetAtPath<WorldMapTemplate>("Assets/Data/Maps/CenteredThreeSinWhitebox.asset");
        MapLayoutValidator.Validate(template.Load());
        Debug.Log("Map whitebox valid: 28 nodes, 3 regions; shortest route 5, minimum ordinary battles 3.");
    }
}
