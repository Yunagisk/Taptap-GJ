using System.Collections.Generic;
using UnityEngine;

// Small animated marks around the painted silhouette. The actor's sprite,
// renderer tint and material are never modified by a status effect.
[DisallowMultipleComponent, DefaultExecutionOrder(450)]
public sealed class PlayerStatusEffects : MonoBehaviour
{
    PlayerCombatant player;
    PlayerAttackAnimation actorAnimation;
    MeshRenderer particles;
    Mesh mesh;
    Material material;
    readonly float[] strength = new float[9];
    readonly bool[] active = new bool[9];
    readonly double[] appliedAt = new double[9];
    readonly List<Vector3> vertices = new List<Vector3>(6000);
    readonly List<Color> colors = new List<Color>(6000);
    readonly List<int> triangles = new List<int>(12000);
    float width, height, time;
    public bool HasVisibleEffects => particles != null && particles.enabled;

    void OnEnable()
    {
        player = GetComponent<PlayerCombatant>();
        actorAnimation = GetComponent<PlayerAttackAnimation>();
        if (player == null) return;
        player.OnMarksChanged += Changed;
        Changed(player);
    }
    void Changed(Combatant actor)
    {
        for (int i = 0; i < active.Length; i++)
        {
            bool present = player.Statuses.Has((StatusId)i) && !player.IsDead;
            if (present && !active[i]) appliedAt[i] = Time.realtimeSinceStartupAsDouble;
            active[i] = present;
        }
    }
    void LateUpdate()
    {
        if (player == null || player.IsDead) { if (particles != null) particles.enabled = false; return; }
        bool visible = false;
        for (int i = 0; i < strength.Length; i++)
        {
            strength[i] = Mathf.MoveTowards(strength[i], active[i] ? 1 : 0, Time.unscaledDeltaTime / .18f);
            visible |= strength[i] > .001f;
        }
        if (!visible) { if (particles != null) particles.enabled = false; return; }
        SpriteRenderer body = null;
        Bounds local = default;
        if (actorAnimation == null || !actorAnimation.TryGetBodyVisual(out body, out local))
        {
            body = GetComponentInChildren<SpriteRenderer>();
            if (body == null || body.sprite == null) return;
            local = body.sprite.bounds;
        }
        var bounds = new Bounds(body.transform.TransformPoint(local.min), Vector3.zero);
        for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
            bounds.Encapsulate(body.transform.TransformPoint(new Vector3(x == 0 ? local.min.x : local.max.x,
                y == 0 ? local.min.y : local.max.y, local.center.z)));
        EnsureMesh();
        particles.transform.position = new Vector3(bounds.center.x, bounds.min.y, body.transform.position.z - .12f);
        particles.transform.rotation = Quaternion.identity;
        var scale = transform.lossyScale;
        particles.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
        particles.sortingLayerID = body.sortingLayerID;
        particles.sortingOrder = body.sortingOrder + 12;
        width = bounds.size.x; height = bounds.size.y; time = Time.unscaledTime;
        vertices.Clear(); colors.Clear(); triangles.Clear();
        for (int i = 0; i < strength.Length; i++)
            if (strength[i] > .001f) Draw((StatusId)i, strength[i]);
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds(); particles.enabled = vertices.Count > 0;
    }
    void EnsureMesh()
    {
        if (particles != null) return;
        var root = new GameObject("PlayerStatusParticles");
        root.layer = gameObject.layer; root.transform.SetParent(transform, false);
        particles = root.AddComponent<MeshRenderer>();
        material = new Material(Shader.Find("Sprites/Default")) { name = "Status Motes", hideFlags = HideFlags.DontSave };
        material.mainTexture = Texture2D.whiteTexture;
        particles.sharedMaterial = material;
        mesh = new Mesh { name = "Player Status Motes" }; mesh.MarkDynamic();
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
    }
    void Draw(StatusId id, float opacity)
    {
        Color tint = StatusAppearance.ColorOf(id); tint.a = opacity;
        switch (id)
        {
            case StatusId.Poison:
                Disc(P(-.24f, .025f), new Vector2(width * .38f, height * .035f), Alpha(tint, .16f));
                Disc(P(.25f, .03f), new Vector2(width * .4f, height * .026f), Alpha(tint, .13f));
                Ellipse(P(0, .02f), new Vector2(width * .61f, height * .038f), Alpha(tint, .32f), time * .5f, 4.6f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 previous = default;
                    for (int s = 0; s <= 24; s++)
                    {
                        float u = s / 24f;
                        Vector2 p = P(side * (.5f + .07f * Mathf.Sin(u * 8 - time * 1.7f + side)), .04f + .62f * u);
                        if (s > 0) Line(previous, p, height * .0022f, Alpha(tint, Mathf.Sin(u * Mathf.PI) * .3f));
                        previous = p;
                    }
                }
                for (int i = 0; i < 10; i++)
                {
                    float p = Mathf.Repeat(time * .17f + i * .137f, 1);
                    float fade = Mathf.Sin(p * Mathf.PI);
                    Vector2 position = P((i % 2 == 0 ? -1 : 1) * (.46f + .10f * Mathf.Sin(time + i * 2)), .06f + p * .7f);
                    float radius = height * (.008f + i % 3 * .003f);
                    Disc(position, Vector2.one * radius * 2.5f, Alpha(tint, fade * .1f));
                    Ellipse(position, Vector2.one * radius, Alpha(tint, fade * .8f));
                    Disc(position + Vector2.one * radius * .24f, Vector2.one * radius * .22f, Alpha(tint, fade));
                }
                float age = (float)(Time.realtimeSinceStartupAsDouble - appliedAt[(int)id]);
                if (age < .65f) Ellipse(P(0, .025f), new Vector2(width * (.4f + age * .55f), height * .05f), Alpha(tint, (1 - age / .65f) * .7f));
                break;
            case StatusId.Ignite:
                for (int i = 0; i < 9; i++)
                {
                    float p = Mathf.Repeat(time * .45f + i * .149f, 1);
                    Vector2 origin = P((i % 2 == 0 ? -1 : 1) * (.34f + .06f * Mathf.Sin(i * 3)), .015f);
                    float flame = height * (.08f + .03f * Mathf.Sin(time * 7 + i));
                    Triangle(origin + Vector2.left * flame * .2f, origin + Vector2.up * flame + Vector2.right * flame * Mathf.Sin(time * 5 + i) * .2f,
                        origin + Vector2.right * flame * .2f, Alpha(tint, .54f));
                    Vector2 ember = origin + new Vector2(Mathf.Sin(i + time * 2) * width * .06f, height * (.04f + p * .45f));
                    Disc(ember, Vector2.one * height * .007f, Alpha(tint, Mathf.Sin(p * Mathf.PI) * .9f));
                }
                break;
            case StatusId.Confuse:
                Ellipse(P(0, 1.045f), new Vector2(width * .32f, height * .027f), Alpha(tint, .35f));
                for (int i = 0; i < 3; i++)
                {
                    float a = time * 1.8f + i * Mathf.PI * 2 / 3;
                    Vector2 p = P(Mathf.Cos(a) * .32f, 1.045f + Mathf.Sin(a) * .026f);
                    Diamond(p, height * .014f, tint);
                }
                break;
            case StatusId.Sunder:
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 a = P(side * .52f, .53f), b = P(side * .63f, .48f), c = P(side * .57f, .36f);
                    Line(a, b, height * .004f, tint); Line(b, c, height * .004f, tint);
                    float p = Mathf.Repeat(time * .45f + (side + 1) * .25f, 1);
                    Diamond(P(side * (.58f + p * .08f), .35f - p * .24f), height * .014f, Alpha(tint, Mathf.Sin(p * Mathf.PI)));
                }
                break;
            case StatusId.Weak:
            case StatusId.Exhaust:
                for (int i = 0; i < 5; i++)
                {
                    float p = Mathf.Repeat(time * .24f + i * .213f, 1);
                    Vector2 top = P((i % 2 == 0 ? -1 : 1) * (.51f + i % 3 * .06f), .74f - p * .48f);
                    Color faded = Alpha(tint, Mathf.Sin(p * Mathf.PI) * .65f);
                    Line(top, top + Vector2.down * height * .055f, height * .003f, faded);
                    Vector2 end = top + Vector2.down * height * .055f;
                    Line(end, end + new Vector2(-height * .014f, height * .015f), height * .003f, faded);
                    Line(end, end + new Vector2(height * .014f, height * .015f), height * .003f, faded);
                }
                break;
            case StatusId.Regen:
                for (int i = 0; i < 6; i++)
                {
                    float p = Mathf.Repeat(time * .24f + i * .17f, 1);
                    Vector2 position = P((i % 2 == 0 ? -1 : 1) * (.43f + .08f * Mathf.Sin(i)), .04f + p * .55f);
                    Color faded = Alpha(tint, Mathf.Sin(p * Mathf.PI) * .8f);
                    float r = height * .012f;
                    Line(position - Vector2.right * r, position + Vector2.right * r, r * .45f, faded);
                    Line(position - Vector2.up * r, position + Vector2.up * r, r * .45f, faded);
                }
                break;
            case StatusId.Haste:
                for (int i = 0; i < 4; i++)
                {
                    float p = Mathf.Repeat(time * .7f + i * .27f, 1);
                    Vector2 a = P(-.72f + p * .3f, .06f + i * .06f);
                    Line(a, a + Vector2.right * width * .23f, height * .003f, Alpha(tint, Mathf.Sin(p * Mathf.PI) * .7f));
                }
                break;
            case StatusId.Steadfast:
                Ellipse(P(0, .025f), new Vector2(width * .48f, height * .028f), Alpha(tint, .48f), -time * .22f, 5.4f);
                for (int i = 0; i < 4; i++)
                    Diamond(P(Mathf.Cos(time * .6f + i * Mathf.PI * .5f) * .5f, .1f + i % 2 * .06f), height * .01f, Alpha(tint, .75f));
                break;
        }
    }
    Vector2 P(float x, float y) => new Vector2(x * width, y * height);
    static Color Alpha(Color tint, float alpha) { tint.a *= alpha; return tint; }
    void Vertex(Vector2 point, Color tint) { vertices.Add(point); colors.Add(tint); }
    void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int start = vertices.Count; Vertex(a, tint); Vertex(b, tint); Vertex(c, tint);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
    }
    void Line(Vector2 a, Vector2 b, float stroke, Color tint)
    {
        Vector2 n = new Vector2(a.y - b.y, b.x - a.x).normalized * stroke * .5f;
        Triangle(a - n, a + n, b + n, tint); Triangle(a - n, b + n, b - n, tint);
    }
    void Disc(Vector2 center, Vector2 radius, Color tint)
    {
        int start = vertices.Count; Vertex(center, tint);
        for (int i = 0; i <= 16; i++)
        {
            float a = i * Mathf.PI * 2 / 16;
            Vertex(center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y), Alpha(tint, 0));
            if (i > 0) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
        }
    }
    void Ellipse(Vector2 center, Vector2 radius, Color tint, float start = 0, float span = Mathf.PI * 2)
    {
        for (int i = 0; i < 36; i++)
        {
            float a = start + span * i / 36, b = start + span * (i + 1) / 36;
            Line(center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y),
                center + new Vector2(Mathf.Cos(b) * radius.x, Mathf.Sin(b) * radius.y), height * .002f, tint);
        }
    }
    void Diamond(Vector2 p, float radius, Color tint)
    {
        Triangle(p + Vector2.up * radius, p + Vector2.right * radius * .55f, p - Vector2.up * radius, tint);
        Triangle(p - Vector2.up * radius, p - Vector2.right * radius * .55f, p + Vector2.up * radius, tint);
    }
    void OnDisable()
    {
        if (!ReferenceEquals(player, null)) player.OnMarksChanged -= Changed;
        if (particles != null) particles.enabled = false;
    }
    void OnDestroy()
    {
        if (particles != null) Destroy(particles.gameObject);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
