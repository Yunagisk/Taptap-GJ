using TMPro;
using UnityEngine;

// TMP generates its own mesh rather than using BaseMeshEffect. Mask its freshly
// generated vertex colours before upload, including fallback font submeshes.
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class MapHudInkRevealText : MonoBehaviour
{
    TMP_Text label;
    MapHudInkRevealGroup owner;

    public void Initialize(MapHudInkRevealGroup group) { owner = group; Invalidate(); }

    void OnEnable()
    {
        label = GetComponent<TMP_Text>();
        label.OnPreRenderText += Mask;
        Invalidate();
    }

    public void Invalidate()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        label.havePropertiesChanged = true;
        label.SetVerticesDirty();
    }

    void Mask(TMP_TextInfo info)
    {
        if (owner == null || !owner.isActiveAndEnabled || owner.State.progress >= 1) return;
        MapHudInkRevealTextMesh.Apply(info, MapUITransform.RelativeMatrix(label.transform, owner.transform), owner.State);
    }

    void OnDisable()
    {
        if (label == null) return;
        label.OnPreRenderText -= Mask;
        Invalidate();
    }
}

public static class MapHudInkRevealTextMesh
{
    public static void Apply(TMP_TextInfo info, Matrix4x4 toWing, MapHudInkRevealState state)
    {
        if (state.progress >= 1) return;
        for (int material = 0; material < info.materialCount; material++)
        {
            var mesh = info.meshInfo[material];
            for (int vertex = 0; vertex < mesh.vertexCount; vertex++)
            {
                float opacity = state.Opacity(toWing.MultiplyPoint3x4(mesh.vertices[vertex]));
                var color = mesh.colors32[vertex];
                color.a = (byte)Mathf.RoundToInt(color.a * opacity);
                mesh.colors32[vertex] = color;
            }
        }
    }

}
