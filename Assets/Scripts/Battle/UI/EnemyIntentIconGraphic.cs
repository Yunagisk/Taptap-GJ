using UnityEngine;
using UnityEngine.UI;

// Four reusable, texture-free damage silhouettes plus a shield for guard actions.
public sealed class EnemyIntentIconGraphic : MaskableGraphic
{
    public EnemyDamageType DamageType { get; private set; }
    public bool IsGuard { get; private set; }
    VertexHelper mesh;
    Vector2 center;
    float radius;

    public void SetIntent(EnemyAttackData action)
    {
        var type = action != null ? action.IntentDamageType : EnemyDamageType.Rend;
        bool guard = action != null && action.Kind == EnemyActionKind.Guard;
        if (DamageType == type && IsGuard == guard) return;
        DamageType = type; IsGuard = guard; SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear(); mesh = helper;
        center = rectTransform.rect.center;
        radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .46f;
        Color tint = IsGuard ? new Color32(184, 174, 145, 255)
            : DamageType == EnemyDamageType.Bite ? new Color32(232, 211, 169, 255)
            : DamageType == EnemyDamageType.Toxin ? new Color32(171, 193, 102, 255)
            : DamageType == EnemyDamageType.Hex ? new Color32(184, 160, 207, 255)
            : new Color32(217, 149, 116, 255);
        if (IsGuard)
        {
            Path(tint, new Vector2(-.77f, .78f), new Vector2(.77f, .78f), new Vector2(.64f, -.3f),
                new Vector2(0, -.95f), new Vector2(-.64f, -.3f), new Vector2(-.77f, .78f));
            Line(new Vector2(0, .58f), new Vector2(0, -.65f), .17f, tint);
            return;
        }
        switch (DamageType)
        {
            case EnemyDamageType.Bite:
                Line(new Vector2(-.88f, .77f), new Vector2(.88f, .77f), .16f, tint);
                Polygon(tint, new Vector2(-.85f, .72f), new Vector2(-.27f, .72f), new Vector2(-.52f, -.2f));
                Polygon(tint, new Vector2(.27f, .72f), new Vector2(.85f, .72f), new Vector2(.52f, -.2f));
                Line(new Vector2(-.76f, -.78f), new Vector2(.76f, -.78f), .16f, tint);
                Polygon(tint, new Vector2(-.7f, -.73f), new Vector2(-.18f, -.73f), new Vector2(-.4f, -.18f));
                Polygon(tint, new Vector2(.18f, -.73f), new Vector2(.7f, -.73f), new Vector2(.4f, -.18f));
                break;
            case EnemyDamageType.Toxin:
                Polygon(tint, new Vector2(0, .97f), new Vector2(.44f, .37f), new Vector2(.72f, -.2f),
                    new Vector2(.57f, -.7f), new Vector2(0, -.96f), new Vector2(-.57f, -.7f),
                    new Vector2(-.72f, -.2f), new Vector2(-.44f, .37f));
                Line(new Vector2(-.31f, -.08f), new Vector2(-.35f, -.4f), .14f, new Color32(44, 54, 27, 255));
                break;
            case EnemyDamageType.Hex:
                Path(tint, new Vector2(-.98f, 0), new Vector2(-.48f, .48f), new Vector2(.48f, .48f),
                    new Vector2(.98f, 0), new Vector2(.48f, -.48f), new Vector2(-.48f, -.48f), new Vector2(-.98f, 0));
                Polygon(tint, new Vector2(0, .34f), new Vector2(.24f, 0), new Vector2(0, -.34f), new Vector2(-.24f, 0));
                Line(new Vector2(0, .65f), new Vector2(0, .98f), .15f, tint);
                Line(new Vector2(0, -.65f), new Vector2(0, -.98f), .15f, tint);
                break;
            default:
                for (int i = 0; i < 3; i++)
                {
                    float x = -.8f + i * .54f;
                    Polygon(tint, new Vector2(x, -.83f), new Vector2(x + .21f, -.25f),
                        new Vector2(x + .58f, .91f), new Vector2(x + .13f, .46f));
                }
                break;
        }
    }

    Vector2 Point(Vector2 value) => center + value * radius;
    void Add(Vector2 value, Color tint)
    { var vertex = UIVertex.simpleVert; vertex.position = Point(value); vertex.color = tint * color; mesh.AddVert(vertex); }
    void Polygon(Color tint, params Vector2[] points)
    {
        int start = mesh.currentVertCount;
        foreach (var point in points) Add(point, tint);
        for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(start, start + i, start + i + 1);
    }
    void Path(Color tint, params Vector2[] points)
    { for (int i = 1; i < points.Length; i++) Line(points[i - 1], points[i], .14f, tint); }
    void Line(Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * width * .5f;
        Polygon(tint, a - normal, a + normal, b + normal, b - normal);
    }
}
