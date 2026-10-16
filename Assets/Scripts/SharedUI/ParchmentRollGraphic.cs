using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// Both the textured curl and its soft contact shadow use the same curved edge.
// No graphic is dirtied from inside a Canvas mesh rebuild.
public sealed class ParchmentRollGraphic : MaskableGraphic
{
    public Sprite paper;
    public bool shadow;
    [SerializeField, Range(0, 1)] float phase;
    public override Texture mainTexture => !shadow && paper != null ? paper.texture : base.mainTexture;

    public void SetPhase(float value)
    {
        value = Mathf.Clamp01(value);
        if (phase == value) return;
        phase = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        Vector4 uv = paper != null && !shadow ? DataUtility.GetOuterUV(paper) : new Vector4(0, 0, 1, 1);
        const int rows = 64;
        int columns = shadow ? 24 : 4;
        for (int row = 0; row <= rows; row++)
        {
            float y = (float)row / rows;
            float bend = ParchmentMotion.Bend(y, phase) * rect.width;
            for (int column = 0; column <= columns; column++)
            {
                float x = (float)column / columns;
                var vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(rect.xMin + x * rect.width + bend, rect.yMin + y * rect.height);
                vertex.uv0 = new Vector2(Mathf.Lerp(uv.x, uv.z, x), Mathf.Lerp(uv.y, uv.w, y));
                Color tint = color;
                if (shadow)
                {
                    float feather = Mathf.Sin(x * Mathf.PI);
                    float ends = Mathf.Clamp01(Mathf.Min(y, 1 - y) * 30);
                    tint.a *= feather * feather * ends;
                }
                vertex.color = tint;
                mesh.AddVert(vertex);
                if (row == 0 || column == 0) continue;
                int d = row * (columns + 1) + column, a = d - columns - 2;
                mesh.AddTriangle(a, a + 1, d - 1);
                mesh.AddTriangle(a + 1, d, d - 1);
            }
        }
    }
}
