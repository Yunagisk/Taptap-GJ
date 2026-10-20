using System;
using UnityEngine;

// Matte heavy-weapon effects. Fixed buffers are reused throughout battle;
// contact is captured before damage so a lethal target can disappear safely.
public sealed class PlayerHeavyEffects : MonoBehaviour
{
    const int Capacity = 1024;
    float TrailLifetime => torch ? .12f : .095f;
    float ImpactLifetime => torch ? .28f : .21f;
    struct BladeSample { public Vector3 heel, tip; public float time; }
    readonly BladeSample[] samples = new BladeSample[12];
    readonly Vector3[] vertices = new Vector3[Capacity];
    readonly Color32[] colors = new Color32[Capacity];
    readonly int[] triangles = new int[Capacity / 4 * 6];
    static readonly Color Ivory = new Color(.91f, .90f, .84f, 1f);
    static readonly Color Ink = new Color(.20f, .19f, .18f, 1f);
    static readonly Color Ember = new Color(.65f, .34f, .24f, 1f);
    static readonly Color FireCore = new Color(.92f, .72f, .43f, 1f);
    Color TrailTint => torch ? Ember : blunt ? new Color(.48f, .42f, .34f, 1f) : Ivory;
    Color HitTint => torch ? FireCore : blunt ? new Color(.67f, .60f, .47f, 1f) : Ivory;
    Transform blade;
    Mesh mesh;
    MeshRenderer meshRenderer;
    int sampleCount, nextVertex;
    float poseTime, trailStart, trailEnd, impactStarted = -100f, impactRadius;
    Vector3 impactPoint, impactDirection;
    bool attacking, torch;
    float lastTrailSample = -1f;

    PlayerHeavyAnimationSet.Weapon mount;
    bool blunt, hooked, axe, pole;
    public void Configure(PlayerHeavyAnimationSet.Weapon weapon)
    {
        if (mount == weapon) return;
        Cancel(); mount = weapon;
        torch = weapon != null && weapon.id == "heaven_fire";
        blunt = weapon != null && (weapon.id == "hammer" || torch);
        hooked = weapon != null && weapon.id == "sickle";
        axe = weapon != null && weapon.id == "axe";
        pole = weapon != null && (weapon.id == "spear" || weapon.id == "poleaxe");
    }

    public void Initialize(SpriteRenderer weapon, SpriteRenderer appearance)
    {
        blade = weapon.transform;
        var obj = new GameObject("HeavyInkEffects");
        obj.transform.SetParent(transform, false);
        meshRenderer = obj.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = appearance.sharedMaterial;
        meshRenderer.sortingLayerID = appearance.sortingLayerID;
        meshRenderer.sortingOrder = appearance.sortingOrder + 3;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", Texture2D.whiteTexture);
        properties.SetColor("_Color", Color.white);
        properties.SetColor("_RendererColor", Color.white);
        properties.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
        meshRenderer.SetPropertyBlock(properties);
        meshRenderer.enabled = false;
        mesh = new Mesh { name = "HeavyInkEffects" };
        mesh.MarkDynamic();
        for (int q = 0; q < Capacity / 4; q++)
        {
            int v = q * 4, t = q * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 128f);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
    }

    public void BeginAttack(float release, float hit)
    {
        attacking = true; poseTime = 0f; sampleCount = 0; lastTrailSample = -1f;
        trailStart = release; trailEnd = hit + .035f;
    }

    public void SetPoseTime(float seconds) => poseTime = seconds;
    public void EndAttack() { attacking = false; sampleCount = 0; }

    public void PlayImpact(Combatant target)
    {
        var targetSprite = target.GetComponentInChildren<SpriteRenderer>();
        Bounds bounds = targetSprite != null ? targetSprite.bounds
            : new Bounds(target.transform.position, Vector3.one);
        Vector3 direction = bounds.center - blade.position;
        direction.z = 0f;
        direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector3.right;
        impactPoint = transform.InverseTransformPoint(bounds.center - direction * bounds.extents.x * .2f);
        impactDirection = transform.InverseTransformVector(direction).normalized;
        impactRadius = transform.InverseTransformVector(Vector3.right * Mathf.Clamp(bounds.extents.y * .5f, .3f, .65f)).magnitude;
        impactStarted = Time.time;
    }

    void LateUpdate()
    {
        if (mesh == null || blade == null || mount == null) return;
        while (sampleCount > 0 && Time.time - samples[0].time >= TrailLifetime)
        {
            for (int i = 1; i < sampleCount; i++) samples[i - 1] = samples[i];
            sampleCount--;
        }
        if (attacking && poseTime >= trailStart && poseTime <= trailEnd && poseTime != lastTrailSample)
        {
            lastTrailSample = poseTime;
            if (sampleCount == samples.Length)
            {
                for (int i = 1; i < sampleCount; i++) samples[i - 1] = samples[i];
                sampleCount--;
            }
            samples[sampleCount++] = new BladeSample {
                heel = transform.InverseTransformPoint(blade.TransformPoint(mount.trailHeel)),
                tip = transform.InverseTransformPoint(blade.TransformPoint(mount.trailTip)),
                time = Time.time
            };
        }
        float impactAge = Time.time - impactStarted;
        if (sampleCount < 2 && (impactAge < 0f || impactAge >= ImpactLifetime))
        {
            meshRenderer.enabled = false;
            return;
        }
        Array.Clear(colors, 0, colors.Length);
        nextVertex = 0;
        DrawTrail();
        if (impactAge >= 0f && impactAge < ImpactLifetime) DrawImpact(impactAge / ImpactLifetime);
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        meshRenderer.enabled = nextVertex > 0;
    }

    void DrawTrail()
    {
        for (int i = 1; i < sampleCount; i++)
        {
            BladeSample a = samples[i - 1], b = samples[i];
            float aa = Mathf.Clamp01(1f - (Time.time - a.time) / TrailLifetime);
            float ab = Mathf.Clamp01(1f - (Time.time - b.time) / TrailLifetime);
            if (pole)
            {
                // A thrust translates along the shaft: a blade ribbon would
                // collapse to zero area. Leave a narrow tapered wake at its tip.
                Stroke(a.tip, b.tip, .085f, ab * .48f, Ivory);
                continue;
            }
            Vector3 innerA = Vector3.Lerp(a.heel, a.tip, .18f), innerB = Vector3.Lerp(b.heel, b.tip, .18f);
            Vector3 middleA = Vector3.Lerp(a.heel, a.tip, .82f), middleB = Vector3.Lerp(b.heel, b.tip, .82f);
            Quad(innerA, middleA, middleB, innerB, 0f, aa * .20f, ab * .20f, 0f, TrailTint);
            Quad(middleA, a.tip, b.tip, middleB, aa * .20f, aa * .50f, ab * .50f, ab * .20f, TrailTint);
        }
        if (sampleCount == 0 || !attacking || poseTime > trailEnd) return;
        BladeSample current = samples[sampleCount - 1];
        Vector3 along = (current.tip - current.heel).normalized;
        Vector3 across = new Vector3(-along.y, along.x);
        for (int i = 0; i < 3; i++)
        {
            Vector3 start = current.tip - along * (1.1f + i * .33f) + across * ((i - 1) * .17f);
            Stroke(start, current.tip + across * ((i - 1) * .24f) + along * (i == 1 ? .42f : .15f),
                .045f - i * .007f, .72f - i * .14f, HitTint);
        }
    }

    void DrawImpact(float t)
    {
        float grow = 1f - (1f - t) * (1f - t);
        float fade = Mathf.Clamp01((1f - t) / .7f);
        Vector3 side = new Vector3(-impactDirection.y, impactDirection.x);
        int rays = pole ? 5 : axe ? 6 : 8;
        for (int i = 0; i < rays; i++)
        {
            float angle = i * Mathf.PI * 2f / rays + (i % 2 == 0 ? .08f : -.13f);
            Vector3 ray = impactDirection * Mathf.Cos(angle) + side * Mathf.Sin(angle);
            float reach = impactRadius * (i % 2 == 0 ? .75f : 1f) * (.5f + .5f * grow);
            Stroke(impactPoint + ray * impactRadius * .07f, impactPoint + ray * reach,
                impactRadius * .065f * (1f - .5f * t), fade * .92f, HitTint);
            // Offset ink fragments give the hit a drawn edge instead of a neon halo.
            Vector3 mark = impactPoint + ray * impactRadius * (.48f + .6f * grow);
            Stroke(mark, mark + ray * impactRadius * .16f, impactRadius * .022f, fade * .72f, torch ? Ember : Ink);
        }
        Stroke(impactPoint - impactDirection * impactRadius * .5f,
            impactPoint + impactDirection * impactRadius * (1.2f + .2f * grow),
            impactRadius * .08f * (1f - t), fade, HitTint);
        if (blunt) DrawHeavyLanding(t, fade);
        if (hooked) DrawHookArc(t, fade);
        if (torch)
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = (i * .7f - 1.4f);
                Vector3 ray = impactDirection * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                Vector3 spark = impactPoint + ray * impactRadius * (.5f + t * (1.2f + i * .15f));
                spark.y += impactRadius * t * .45f;
                Stroke(spark, spark + ray * impactRadius * .16f, impactRadius * .035f * (1f - t), fade, FireCore);
            }
        }
    }

    void DrawHeavyLanding(float t, float fade)
    {
        Vector3 center = new Vector3(-3.25f, .02f, 0f);
        float radius = .25f + t * 1.5f;
        Color dust = torch ? Ember : new Color(.35f, .29f, .23f, 1f);
        for (int i = 0; i < 14; i++)
        {
            float a = i * Mathf.PI * 2f / 14f, b = (i + .72f) * Mathf.PI * 2f / 14f;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * .18f);
            Vector3 q = center + new Vector3(Mathf.Cos(b) * radius, Mathf.Sin(b) * radius * .18f);
            Stroke(p, q, .055f * (1f-t), fade * .65f, dust);
        }
        for (int i = 0; i < 5; i++)
        {
            Vector3 p = impactPoint + new Vector3((i-2) * impactRadius * .35f * (1f+t), t * (1f-t) * (1f+i*.1f));
            Stroke(p, p + new Vector3(.04f, .09f), .09f * (1f-t), fade * .8f, dust);
        }
    }

    void DrawHookArc(float t, float fade)
    {
        float radius = impactRadius * (.6f+t*.5f);
        for(int i=0;i<9;i++)
        {
            float a = -.9f + i*.22f, b = a+.17f;
            Vector3 p = impactPoint + new Vector3(Mathf.Cos(a),Mathf.Sin(a))*radius;
            Vector3 q = impactPoint + new Vector3(Mathf.Cos(b),Mathf.Sin(b))*radius;
            Stroke(p,q,.055f*(1f-t),fade*.75f,Ivory);
        }
    }

    void Stroke(Vector3 start, Vector3 end, float width, float alpha, Color color)
    {
        Vector3 direction = (end - start).normalized;
        Vector3 normal = new Vector3(-direction.y, direction.x) * width;
        Vector3 tipNormal = normal * .08f;
        Quad(start - normal * .38f, end - tipNormal, end + tipNormal, start + normal * .38f,
            alpha * .7f, alpha, alpha, alpha * .7f, color);
        Quad(start - normal, end - tipNormal * 2f, end - tipNormal, start - normal * .38f,
            0f, 0f, alpha, alpha * .7f, color);
        Quad(start + normal * .38f, end + tipNormal, end + tipNormal * 2f, start + normal,
            alpha * .7f, alpha, 0f, 0f, color);
    }

    void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float aa, float ab, float ac, float ad, Color tint)
    {
        if (nextVertex + 4 > Capacity) return;
        Vertex(a, aa, tint); Vertex(b, ab, tint); Vertex(c, ac, tint); Vertex(d, ad, tint);
    }

    void Vertex(Vector3 position, float alpha, Color tint)
    {
        vertices[nextVertex] = position;
        tint.a = alpha;
        colors[nextVertex++] = tint;
    }

    public void Cancel()
    {
        attacking = false;
        sampleCount = 0;
        impactStarted = -100f;
        if (meshRenderer != null) meshRenderer.enabled = false;
    }

    void OnDisable() => Cancel();
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
