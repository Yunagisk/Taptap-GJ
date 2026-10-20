using UnityEngine;
using UnityEngine.UI;

// Small, texture-free ink signs for services and items without a painted asset.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class VillageServiceIcon : MaskableGraphic
{
    public enum Symbol { Bed, Heart, Check, Close, Left, Right, Swords, Bottle, Bandage, Bag, Mug }
    public Symbol symbol;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var center = rectTransform.rect.center;
        float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .43f;
        void Vertex(Vector2 p) { var v = UIVertex.simpleVert; v.position = center + p * scale; v.color = color; mesh.AddVert(v); }
        void Line(Vector2 a, Vector2 b)
        {
            Vector2 n = new Vector2(a.y - b.y, b.x - a.x).normalized * .055f;
            int first = mesh.currentVertCount;
            Vertex(a - n); Vertex(a + n); Vertex(b + n); Vertex(b - n);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }
        void Path(params Vector2[] points) { for (int i = 1; i < points.Length; i++) Line(points[i - 1], points[i]); }
        switch (symbol)
        {
            case Symbol.Bed:
                Path(new Vector2(-1,-.8f),new Vector2(-1,.5f),new Vector2(-.7f,.5f),new Vector2(-.7f,-.2f),new Vector2(1,-.2f),new Vector2(1,-.8f));
                Path(new Vector2(-.65f,-.15f),new Vector2(-.65f,.35f),new Vector2(.85f,.35f),new Vector2(1,.1f),new Vector2(1,-.2f));
                Line(new Vector2(-.25f,.35f),new Vector2(-.25f,-.2f)); Line(new Vector2(-1,-.55f),new Vector2(1,-.55f)); break;
            case Symbol.Heart:
                Path(new Vector2(0,-.9f),new Vector2(-.85f,.05f),new Vector2(-.9f,.5f),new Vector2(-.6f,.85f),new Vector2(-.25f,.85f),new Vector2(0,.5f),new Vector2(.25f,.85f),new Vector2(.6f,.85f),new Vector2(.9f,.5f),new Vector2(.85f,.05f),new Vector2(0,-.9f)); break;
            case Symbol.Check: Path(new Vector2(-.9f,0),new Vector2(-.25f,-.6f),new Vector2(.9f,.8f)); break;
            case Symbol.Close: Line(new Vector2(-.7f,-.7f),new Vector2(.7f,.7f)); Line(new Vector2(-.7f,.7f),new Vector2(.7f,-.7f)); break;
            case Symbol.Left: Path(new Vector2(.4f,.8f),new Vector2(-.4f,0),new Vector2(.4f,-.8f)); break;
            case Symbol.Right: Path(new Vector2(-.4f,.8f),new Vector2(.4f,0),new Vector2(-.4f,-.8f)); break;
            case Symbol.Swords:
                Path(new Vector2(-.9f,-.9f),new Vector2(.7f,.7f),new Vector2(.85f,.9f),new Vector2(.9f,.6f),new Vector2(-.65f,-.85f));
                Line(new Vector2(-.9f,-.2f),new Vector2(-.3f,-.85f));
                Path(new Vector2(.9f,-.9f),new Vector2(-.7f,.7f),new Vector2(-.85f,.9f),new Vector2(-.9f,.6f),new Vector2(.65f,-.85f));
                Line(new Vector2(.9f,-.2f),new Vector2(.3f,-.85f)); break;
            case Symbol.Bottle:
                Path(new Vector2(-.3f,.95f),new Vector2(.3f,.95f),new Vector2(.3f,.35f),new Vector2(.75f,-.35f),new Vector2(.7f,-.8f),new Vector2(-.7f,-.8f),new Vector2(-.75f,-.35f),new Vector2(-.3f,.35f),new Vector2(-.3f,.95f));
                Line(new Vector2(-.55f,-.25f),new Vector2(.55f,-.25f)); Line(new Vector2(-.4f,.65f),new Vector2(.4f,.65f)); break;
            case Symbol.Bandage:
                Path(new Vector2(-.95f,-.55f),new Vector2(.55f,.95f),new Vector2(.95f,.55f),new Vector2(-.55f,-.95f),new Vector2(-.95f,-.55f));
                Path(new Vector2(-.4f,0),new Vector2(0,.4f),new Vector2(.4f,0),new Vector2(0,-.4f),new Vector2(-.4f,0)); break;
            case Symbol.Bag:
                Path(new Vector2(-.8f,.4f),new Vector2(.8f,.4f),new Vector2(.65f,-.85f),new Vector2(-.65f,-.85f),new Vector2(-.8f,.4f));
                Path(new Vector2(-.35f,.35f),new Vector2(-.35f,.85f),new Vector2(.35f,.85f),new Vector2(.35f,.35f)); break;
            case Symbol.Mug:
                Path(new Vector2(-.7f,.65f),new Vector2(.45f,.65f),new Vector2(.35f,-.85f),
                    new Vector2(-.6f,-.85f),new Vector2(-.7f,.65f));
                Path(new Vector2(.45f,.45f),new Vector2(.95f,.45f),new Vector2(.95f,-.35f),new Vector2(.4f,-.35f));
                Line(new Vector2(-.65f,.35f),new Vector2(.4f,.35f));
                Line(new Vector2(-.35f,.05f),new Vector2(-.3f,-.55f));
                Line(new Vector2(.05f,.05f),new Vector2(.05f,-.55f)); break;
        }
    }
}
