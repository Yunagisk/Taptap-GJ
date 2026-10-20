using UnityEngine;
using UnityEngine.UI;

// Native UI geometry keeps the cut-corner frames sharp at every resolution.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DialoguePanelGraphic : MaskableGraphic
{
    public Color borderColor = new Color32(167, 139, 89, 255);
    public float borderWidth = 1.6f;
    public float cornerCut = 15f;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0 || r.height <= 0) return;
        float inset = Mathf.Min(borderWidth, Mathf.Min(r.width, r.height) * .25f);
        var outer = Corners(r, cornerCut);
        var inner = Corners(new Rect(r.x + inset, r.y + inset,
            r.width - inset * 2, r.height - inset * 2), Mathf.Max(0, cornerCut - inset * .6f));
        for (int i = 0; i < 8; i++) Add(outer[i], borderColor);
        for (int i = 0; i < 8; i++) Add(inner[i], color);
        for (int i = 0; i < 8; i++)
        {
            int j = (i + 1) % 8;
            mesh.AddTriangle(i, j, i + 8);
            mesh.AddTriangle(j, j + 8, i + 8);
        }
        for (int i = 1; i < 7; i++) mesh.AddTriangle(8, 8 + i, 9 + i);

        void Add(Vector2 point, Color tint)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = point;
            vertex.color = tint;
            mesh.AddVert(vertex);
        }
    }

    static Vector2[] Corners(Rect r, float cut)
    {
        float c = Mathf.Min(cut, Mathf.Min(r.width, r.height) * .25f);
        return new[] {
            new Vector2(r.xMin + c, r.yMin), new Vector2(r.xMax - c, r.yMin),
            new Vector2(r.xMax, r.yMin + c), new Vector2(r.xMax, r.yMax - c),
            new Vector2(r.xMax - c, r.yMax), new Vector2(r.xMin + c, r.yMax),
            new Vector2(r.xMin, r.yMax - c), new Vector2(r.xMin, r.yMin + c)
        };
    }
}
