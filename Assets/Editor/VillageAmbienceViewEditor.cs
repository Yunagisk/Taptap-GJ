using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VillageAmbienceView))]
public sealed class VillageAmbienceViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var view = (VillageAmbienceView)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("运行 Village 场景后，可预览三个时段。预览只改变画面，不消耗游戏时间。", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("早晨")) view.Preview(VillageAmbienceView.Period.Morning);
            if (GUILayout.Button("下午")) view.Preview(VillageAmbienceView.Period.Afternoon);
            if (GUILayout.Button("夜间")) view.Preview(VillageAmbienceView.Period.Night);
            if (GUILayout.Button("跟随游戏")) view.Preview(VillageAmbienceView.Period.GameTime);
        }
    }
}
