using UnityEngine;

// The lethal flinch and ash share one clock. No collapse, floor landing or return to idle.
public sealed class EnemyHurtAshPlayback : MonoBehaviour
{
    SpriteRenderer source;
    EnemyIdleSpriteMesh hurtMesh;
    EnemySpriteAnimationSet animations;
    EnemyBlackAshDissolve ash;
    float elapsed, hurtSeconds, lifetime;
    Color color;
    public float Elapsed => elapsed;
    public float HurtSeconds => hurtSeconds;
    public bool IsPlayingHurt => elapsed < hurtSeconds;

    public void Initialize(SpriteRenderer renderer, EnemyIdleSpriteMesh visibleMesh, EnemySpriteAnimationSet set)
    {
        source = renderer; animations = set; color = source.color;
        hurtSeconds = set.HurtFrameCount / (float)Mathf.Max(1, set.framesPerSecond);
        lifetime = Mathf.Max(hurtSeconds, set.deathAshMaterial != null ? set.deathAshSeconds : set.deathFadeSeconds);
        if (set.UsesArtworkMotion && visibleMesh != null)
        {
            hurtMesh = visibleMesh;
            hurtMesh.BeginPose(); hurtMesh.Show(true);
        }
        else if (visibleMesh != null) visibleMesh.Show(false);
        ApplyHurt();
        if (set.deathAshMaterial != null && set.deathAshSeconds > 0f)
        {
            ash = gameObject.AddComponent<EnemyBlackAshDissolve>();
            ash.Initialize(source, set.deathAshMaterial, hurtMesh);
            ash.SetProgress(0);
        }
    }

    void ApplyHurt()
    {
        if (hurtMesh != null)
            hurtMesh.ApplyArtwork(animations, EnemyArtworkPose.Hurt, Mathf.Min(elapsed, hurtSeconds), hurtSeconds);
        else
        {
            int frame = Mathf.Min(animations.hurtFrames.Length - 1, Mathf.FloorToInt(elapsed * Mathf.Max(1, animations.framesPerSecond)));
            source.sprite = animations.hurtFrames[frame];
            if (ash != null) ash.SetBodyDrawing(source.sprite);
        }
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        ApplyHurt();
        if (ash != null) ash.SetProgress(elapsed / Mathf.Max(.001f, animations.deathAshSeconds));
        else
        {
            Color faded = color; faded.a *= 1 - Mathf.Clamp01(elapsed / Mathf.Max(.001f, lifetime));
            source.color = faded;
            if (hurtMesh != null)
            {
                var block = new MaterialPropertyBlock(); hurtMesh.Appearance.GetPropertyBlock(block);
                block.SetColor("_Color", faded); hurtMesh.Appearance.SetPropertyBlock(block);
            }
        }
        if (elapsed >= lifetime) Destroy(gameObject);
    }
}
