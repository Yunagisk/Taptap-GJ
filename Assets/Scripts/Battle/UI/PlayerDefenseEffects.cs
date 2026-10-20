using UnityEngine;

// A single shield flash on entering defense. Geometry is reused between turns.
[DisallowMultipleComponent]
public sealed class PlayerDefenseEffects : MonoBehaviour
{
    [SerializeField, Min(.1f)] float duration = .6f;
    [SerializeField] Color shieldColor = new Color(.61f, .79f, .86f, 1f);
    [SerializeField] Color edgeColor = new Color(.88f, .96f, 1f, 1f);

    const int Rows = 40;
    const int Capacity = 512;
    // Clockwise, with a bowed crown, broad shoulders and a tapered point.
    static readonly Vector2[] Outline = {
        new Vector2(0f, .5f), new Vector2(.25f, .47f), new Vector2(.46f, .38f),
        new Vector2(.45f, .08f), new Vector2(.38f, -.16f), new Vector2(.23f, -.36f),
        new Vector2(0f, -.52f), new Vector2(-.23f, -.36f), new Vector2(-.38f, -.16f),
        new Vector2(-.45f, .08f), new Vector2(-.46f, .38f), new Vector2(-.25f, .47f)
    };

    PlayerCombatant player;
    Mesh mesh;
    MeshRenderer shield;
    Vector3[] vertices;
    Color[] colors;
    int nextVertex;
    Vector3 restingPosition, restingScale;
    float age;
    bool playing;

    void OnEnable()
    {
        player = GetComponent<PlayerCombatant>();
        if (player == null) return;
        player.OnDefenseChanged += DefenseChanged;
        player.OnDied += Died;
        if (player.IsDefending && !player.IsDead) Play();
    }

    void OnDisable()
    {
        if (player != null)
        {
            player.OnDefenseChanged -= DefenseChanged;
            player.OnDied -= Died;
        }
        Cancel();
    }

    void OnDestroy()
    {
        if (shield != null) Destroy(shield.gameObject);
        if (mesh != null) Destroy(mesh);
    }

    void DefenseChanged(Combatant actor, bool defending)
    {
        if (defending) Play();
        else Cancel();
    }

    void Died(Combatant actor) => Cancel();

    void Play()
    {
        if (player == null || player.IsDead) return;
        var animation = player.GetComponent<PlayerAttackAnimation>();
        SpriteRenderer body = null;
        Bounds bounds = default;
        bool registered = animation != null && animation.TryGetBodyVisual(out body, out bounds);
        if (!registered) body = player.GetComponentInChildren<SpriteRenderer>();
        if (body == null || body.sprite == null) return;
        EnsureMesh(body);

        // The animation supplies painted bounds, excluding its transparent
        // canvas and weapon. Static fallback art uses the sprite's tight mesh.
        if (!registered)
        {
            var points = body.sprite.vertices;
            bounds = body.sprite.bounds;
            if (points.Length > 0)
            {
                bounds = new Bounds(points[0], Vector3.zero);
                for (int i = 1; i < points.Length; i++) bounds.Encapsulate(points[i]);
            }
        }
        float height = Mathf.Max(.1f, bounds.size.y * .88f);
        float width = height * .76f;
        shield.transform.SetParent(body.transform, false);
        restingPosition = bounds.center + new Vector3(bounds.size.x * .12f, bounds.size.y * .015f, -.02f);
        restingScale = new Vector3(width, height, 1f);
        shield.sortingLayerID = body.sortingLayerID;
        shield.sortingOrder = body.sortingOrder + 20;
        age = 0f;
        playing = true;
        Draw();
    }

    void EnsureMesh(SpriteRenderer body)
    {
        if (shield != null) return;
        var root = new GameObject("DefenseShieldFlash");
        root.layer = body.gameObject.layer;
        root.transform.SetParent(transform, false);
        shield = root.AddComponent<MeshRenderer>();
        shield.sharedMaterial = body.sharedMaterial;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", Texture2D.whiteTexture);
        properties.SetColor("_Color", Color.white);
        properties.SetColor("_RendererColor", Color.white);
        properties.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
        shield.SetPropertyBlock(properties);
        vertices = new Vector3[Capacity];
        colors = new Color[Capacity];
        var triangles = new int[Capacity / 4 * 6];
        for (int q = 0; q < Capacity / 4; q++)
        {
            int v = q * 4, t = q * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        mesh = new Mesh { name = "DefenseShieldFlash" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(new Vector3(0f, -.01f, 0f), new Vector3(1f, 1.1f, .02f));
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        shield.enabled = false;
    }

    void LateUpdate()
    {
        if (!playing) return;
        age += Time.deltaTime;
        if (player == null || player.IsDead || !player.IsDefending || shield == null || age >= duration)
        { Cancel(); return; }
        Draw();
    }

    void Draw()
    {
        float t = Mathf.Clamp01(age / duration);
        float appear = Mathf.SmoothStep(0f, 1f, t / .12f);
        float fade = 1f - Mathf.SmoothStep(.40f, 1f, t);
        float opacity = appear * fade;
        float scale = Mathf.Lerp(.86f, 1f, Mathf.SmoothStep(0f, 1f, t / .25f)) + t * .035f;
        shield.transform.localPosition = restingPosition + Vector3.up * (restingScale.y * t * .035f);
        shield.transform.localScale = new Vector3(restingScale.x * scale, restingScale.y * scale, 1f);
        nextVertex = 0;

        // The highlight travels down the surface once, clipped to the shield.
        float sweep = Mathf.Lerp(.68f, -.7f, Mathf.Clamp01((t - .10f) / .65f));
        for (int row = 0; row < Rows; row++)
        {
            float y0 = Mathf.Lerp(-.52f, .5f, row / (float)Rows);
            float y1 = Mathf.Lerp(-.52f, .5f, (row + 1f) / Rows);
            float w0 = HalfWidth(y0), w1 = HalfWidth(y1);
            Color c0 = SurfaceColor(y0, sweep, opacity), c1 = SurfaceColor(y1, sweep, opacity);
            Quad(new Vector2(-w0, y0), new Vector2(-w1, y1), new Vector2(w1, y1), new Vector2(w0, y0), c0, c1, c1, c0);
        }
        Ring(1.065f, 1f, shieldColor, opacity * .16f);
        Ring(1f, .975f, edgeColor, opacity * .92f);
        Ring(.915f, .905f, shieldColor, opacity * .42f);

        // A restrained crest keeps the silhouette legible over dark clothing.
        Color crest = edgeColor; crest.a *= opacity * .62f;
        Quad(new Vector2(0, .15f), new Vector2(-.06f, .04f), new Vector2(0, -.1f), new Vector2(.06f, .04f), crest, crest, crest, crest);
        Color glint = edgeColor; glint.a *= opacity * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .7f));
        Quad(new Vector2(-.28f, .31f), new Vector2(-.275f, .38f), new Vector2(-.27f, .31f), new Vector2(-.275f, .24f), glint, glint, glint, glint);
        Quad(new Vector2(-.33f, .31f), new Vector2(-.275f, .316f), new Vector2(-.22f, .31f), new Vector2(-.275f, .304f), glint, glint, glint, glint);

        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        shield.enabled = true;
    }

    Color SurfaceColor(float y, float sweep, float opacity)
    {
        float band = Mathf.Clamp01(1f - Mathf.Abs(y - sweep) / .10f);
        Color color = Color.Lerp(shieldColor, edgeColor, band);
        color.a *= opacity * (.075f + band * band * .32f);
        return color;
    }

    static float HalfWidth(float y)
    {
        // The first half of Outline runs from crown to point.
        for (int i = 0; i < Outline.Length / 2; i++)
        {
            Vector2 a = Outline[i], b = Outline[i + 1];
            if (y >= b.y) return Mathf.Lerp(a.x, b.x, Mathf.InverseLerp(a.y, b.y, y));
        }
        return 0f;
    }

    void Ring(float outer, float inner, Color color, float alpha)
    {
        color.a *= alpha;
        for (int i = 0; i < Outline.Length; i++)
        {
            Vector2 a = Outline[i], b = Outline[(i + 1) % Outline.Length];
            Quad(a * outer, b * outer, b * inner, a * inner, color, color, color, color);
        }
    }

    void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
    {
        vertices[nextVertex] = a; colors[nextVertex++] = ca;
        vertices[nextVertex] = b; colors[nextVertex++] = cb;
        vertices[nextVertex] = c; colors[nextVertex++] = cc;
        vertices[nextVertex] = d; colors[nextVertex++] = cd;
    }

    void Cancel()
    {
        playing = false;
        if (shield != null) shield.enabled = false;
    }
}
