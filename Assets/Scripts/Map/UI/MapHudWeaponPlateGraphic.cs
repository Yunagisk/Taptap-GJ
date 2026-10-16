using UnityEngine;
using UnityEngine.UI;

// A circular paper fill beneath the distressed raster rim. Weapon Images keep
// their original sprites and render above it without a clipping mask.
public sealed class MapHudWeaponPlateGraphic : MaskableGraphic
{
    const int Segments = 64;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect bounds = rectTransform.rect;
        AddVertex(bounds.center);
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            AddVertex(bounds.center + new Vector2(
                Mathf.Cos(angle) * bounds.width * .5f,
                Mathf.Sin(angle) * bounds.height * .5f));
        }
        for (int i = 0; i < Segments; i++)
            mesh.AddTriangle(0, i + 1, (i + 1) % Segments + 1);

        void AddVertex(Vector2 position)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position; vertex.color = color; vertex.uv0 = Vector2.zero;
            mesh.AddVert(vertex);
        }
    }
}
