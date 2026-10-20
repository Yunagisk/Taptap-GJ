using System;
using System.Collections;
using UnityEngine;

// Only the appearance moves. Target colliders and the battle actor keep their positions.
[DisallowMultipleComponent]
public sealed class EnemySpriteAnimation : MonoBehaviour
{
    public EnemySpriteAnimationSet animations;
    public SpriteRenderer appearance;
    public Vector3 visualScale = Vector3.one;
    public Vector3 visualOffset;

    enum Motion { Idle, Attack, Hurt }
    Motion motion;
    Transform visual;
    SpriteRenderer animatedRenderer;
    EnemyIdleSpriteMesh stableIdle;
    Animator legacyAnimator;
    bool sourceEnabled, animatorEnabled, defeated, ownsVisual;
    int playbackVersion;
    float clock;
    Vector3 hurtStartPosition;
    Vector3 idleStartPosition;

    public Sprite Portrait => animations != null ? animations.Portrait : null;
    public Sprite HudPortrait => animations != null && animations.portraitIcon != null ? animations.portraitIcon : Portrait;
    public bool CanPlayAttack => !defeated && isActiveAndEnabled && animatedRenderer != null
        && animations != null && animations.HasAttack;
    public string CurrentMotion => motion.ToString();
    public Sprite CurrentSprite => animatedRenderer != null ? animatedRenderer.sprite : null;

    void OnEnable()
    {
        if (!Application.isPlaying || animations == null || animations.Portrait == null) return;
        if (appearance == null) appearance = GetComponent<SpriteRenderer>();
        if (appearance == null) return;
        sourceEnabled = appearance.enabled;
        legacyAnimator = appearance.GetComponent<Animator>();
        if (legacyAnimator != null) { animatorEnabled = legacyAnimator.enabled; legacyAnimator.enabled = false; }
        visual = new GameObject("EnemyAnimationVisual").transform;
        visual.SetParent(appearance.transform, false);
        visual.localScale = visualScale;
        visual.localPosition = visualOffset;
        animatedRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
        animatedRenderer.sharedMaterial = animations.artworkMaterial != null ? animations.artworkMaterial : appearance.sharedMaterial;
        animatedRenderer.color = animations.preserveArtworkColors
            ? new Color(1f, 1f, 1f, appearance.color.a) : appearance.color;
        animatedRenderer.sortingLayerID = appearance.sortingLayerID;
        animatedRenderer.sortingOrder = appearance.sortingOrder;
        animatedRenderer.flipX = appearance.flipX;
        animatedRenderer.flipY = appearance.flipY;
        appearance.enabled = false;
        if (animations.useDeformedIdle || animations.UsesArtworkMotion)
        {
            var idleRoot = new GameObject("StableIdle");
            idleRoot.transform.SetParent(visual, false);
            stableIdle = idleRoot.AddComponent<EnemyIdleSpriteMesh>();
            stableIdle.Initialize(animations.Portrait, animatedRenderer, animations);
        }
        ownsVisual = true;
        defeated = false;
        ReturnToIdle();
    }

    void Update()
    {
        if (animatedRenderer == null || motion == Motion.Attack) return;
        clock += Time.deltaTime;
        if (motion == Motion.Idle)
            visual.localPosition = Vector3.Lerp(idleStartPosition, visualOffset, Mathf.SmoothStep(0, 1, clock / .14f));
        else if (motion == Motion.Hurt)
            visual.localPosition = Vector3.Lerp(hurtStartPosition, visualOffset,
                Mathf.SmoothStep(0, 1, clock / (animations.HurtFrameCount / (float)animations.framesPerSecond)));
        if (animations.UsesArtworkMotion && stableIdle != null)
        {
            float duration = animations.HurtFrameCount / (float)animations.framesPerSecond;
            if (motion == Motion.Hurt && clock >= duration) { ReturnToIdle(); return; }
            stableIdle.ApplyArtwork(animations, motion == Motion.Hurt ? EnemyArtworkPose.Hurt : EnemyArtworkPose.Idle,
                clock, duration);
            return;
        }
        if (motion == Motion.Idle && stableIdle != null)
        {
            stableIdle.Apply(clock, animations.idleCycleSeconds);
            return;
        }
        var frames = motion == Motion.Hurt ? animations.hurtFrames : animations.idleFrames;
        if (frames == null || frames.Length == 0) return;
        int frame = Mathf.FloorToInt(clock * Mathf.Max(1, animations.framesPerSecond));
        if (motion == Motion.Hurt && frame >= frames.Length) { ReturnToIdle(); return; }
        animatedRenderer.sprite = frames[motion == Motion.Idle ? frame % frames.Length : frame];
    }

    void ReturnToIdle()
    {
        motion = Motion.Idle;
        clock = 0f;
        if (visual != null) idleStartPosition = visual.localPosition;
        if (animatedRenderer != null) animatedRenderer.sprite = animations.Portrait;
        if (stableIdle != null)
        {
            if (animations.UsesArtworkMotion) { stableIdle.BeginPose(); stableIdle.ApplyArtwork(animations, EnemyArtworkPose.Idle, 0, 1); }
            else stableIdle.Apply(0f, animations.idleCycleSeconds);
            stableIdle.Show(true);
        }
    }

    [ContextMenu("Play Mode/Preview Hurt")]
    public void PlayHurt()
    {
        if (defeated || !isActiveAndEnabled || animatedRenderer == null || !animations.HasHurt) return;
        ++playbackVersion;
        motion = Motion.Hurt;
        hurtStartPosition = visual.localPosition;
        if (animations.UsesArtworkMotion && stableIdle != null)
        {
            clock = 0; stableIdle.BeginPose(); stableIdle.Show(true);
            stableIdle.ApplyArtwork(animations, EnemyArtworkPose.Hurt, 0, animations.HurtFrameCount / (float)animations.framesPerSecond);
            return;
        }
        if (stableIdle != null) stableIdle.Show(false);
        clock = 0f;
        animatedRenderer.sprite = animations.hurtFrames[0];
    }

    [ContextMenu("Play Mode/Preview Attack")]
    void PreviewAttack()
    {
        if (Application.isPlaying && CanPlayAttack)
            StartCoroutine(PlayAttack(null, () => true, () => true, null));
    }

    public void PlayDefeat()
    {
        if (defeated || animatedRenderer == null || animations == null) return;
        if (animations.hurtAndAshOnDeath && animations.HasHurt)
        {
            ++playbackVersion;
            defeated = true;
            visual.SetParent(null, true);
            visual.name = "DefeatedEnemyVisual";
            visual.gameObject.AddComponent<EnemyHurtAshPlayback>().Initialize(animatedRenderer, stableIdle, animations);
            visual = null;
            animatedRenderer = null;
            return;
        }
        bool hasDeath = animations.HasDeath;
        var frames = animations.UsesArtworkMotion ? new Sprite[Mathf.Max(1, animations.artworkDeathFrames)]
            : hasDeath ? animations.deathFrames : animations.hurtFrames;
        if (frames == null || frames.Length == 0) return;
        ++playbackVersion;
        defeated = true;
        Vector3[] visibleVertices = (motion == Motion.Idle || animations.UsesArtworkMotion) && stableIdle != null
            ? stableIdle.GetComponent<MeshFilter>().sharedMesh.vertices : null;
        float footY = (animations.artworkBounds.y * animations.Portrait.rect.height - animations.Portrait.pivot.y)
            / animations.Portrait.pixelsPerUnit;
        float groundY = appearance.transform.TransformPoint(visualOffset + new Vector3(0, footY * visualScale.y, 0)).y;
        if (stableIdle != null) stableIdle.Show(false);
        // Preserve the visible position if a counterattack kills the crow mid-lunge.
        visual.SetParent(null, true);
        visual.name = "DefeatedEnemyVisual";
        var ending = visual.gameObject.AddComponent<EnemyDefeatSpritePlayback>();
        ending.Initialize(animatedRenderer, frames, animations.framesPerSecond,
            hasDeath ? animations.deathHoldSeconds : 0f, hasDeath ? animations.deathFadeSeconds : 0f,
            hasDeath ? animations.deathAshMaterial : null, hasDeath ? animations.deathAshSeconds : 0f,
            hasDeath && (animations.useDeformedDeath || animations.UsesArtworkMotion), visibleVertices, groundY,
            animations.artworkBounds, animations.artworkProfile);
        visual = null;
        animatedRenderer = null;
    }

    [ContextMenu("Play Mode/Preview Death")]
    void PreviewDeath()
    {
        if (!Application.isPlaying || defeated || animatedRenderer == null || !animations.HasDeath) return;
        var enemy = GetComponent<EnemyCombatant>();
        if (enemy != null && !enemy.IsDead) enemy.TakeDamage(enemy.CurrentHP);
        else PlayDefeat();
    }

    Vector3 Approach(Combatant target)
    {
        float gap = target != null ? target.transform.position.x - transform.position.x
            : (appearance.flipX ? 10f : -10f);
        float distance = Mathf.Min(Mathf.Max(0f, animations.attackLungeDistance), Mathf.Abs(gap) * .25f);
        // Convert from world space so differently scaled prefabs all take the same small step.
        return appearance.transform.InverseTransformVector(Vector3.right * (Mathf.Sign(gap) * distance));
    }

    void Pose(EnemyAttackAnimationClock.Sample sample, EnemyAttackAnimationClock timing, Vector3 approach, Vector3 start)
    {
        if (animations.UsesArtworkMotion)
            stableIdle.ApplyArtwork(animations, EnemyArtworkPose.Attack, sample.Time, timing.Duration);
        else animatedRenderer.sprite = animations.attackFrames[sample.Frame];
        float amount = EnemyAttackMotion.Advance(sample.Time / timing.Duration, timing.ContactTime / timing.Duration);
        visual.localPosition = visualOffset + approach * amount
            + (start - visualOffset) * (1f - Mathf.SmoothStep(0, 1, sample.Time / .14f));
    }

    public IEnumerator PlayAttack(Combatant target, Func<bool> attackerAlive, Func<bool> targetAlive, Action onHit)
    {
        if (!CanPlayAttack) yield break;
        int version = ++playbackVersion;
        var timing = new EnemyAttackAnimationClock(animations.AttackFrameCount,
            animations.framesPerSecond, animations.attackContactFrame);
        Vector3 start = visual.localPosition;
        Vector3 approach = Approach(target);
        motion = Motion.Attack;
        if (stableIdle != null)
        {
            if (animations.UsesArtworkMotion) stableIdle.BeginPose();
            stableIdle.Show(animations.UsesArtworkMotion);
        }
        float elapsed = 0f;
        try
        {
            while (this != null && isActiveAndEnabled && version == playbackVersion
                && attackerAlive() && targetAlive())
            {
                var sample = timing.At(elapsed);
                Pose(sample, timing, approach, start);
                // A delayed frame must not skip straight from the contact pose to idle.
                elapsed = sample.Time;
                if (sample.DeliverHit) onHit?.Invoke();
                if (sample.Complete) break;
                yield return null;
                elapsed += Mathf.Min(Time.deltaTime, .05f);
            }
        }
        finally
        {
            if (this != null && version == playbackVersion && !defeated) ReturnToIdle();
        }
    }

    void OnDisable()
    {
        if (!ownsVisual) return;
        ownsVisual = false;
        ++playbackVersion;
        if (visual != null) Destroy(visual.gameObject);
        visual = null;
        animatedRenderer = null;
        stableIdle = null;
        if (!defeated)
        {
            if (appearance != null) appearance.enabled = sourceEnabled;
            if (legacyAnimator != null) legacyAnimator.enabled = animatorEnabled;
        }
    }
}

// Logical death and rewards are immediate; the detached visual finishes its visual sequence.
internal sealed class EnemyDefeatSpritePlayback : MonoBehaviour
{
    internal static bool AnyPlayingIn(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var ending in FindObjectsOfType<EnemyDefeatSpritePlayback>())
            if (ending.isActiveAndEnabled && ending.gameObject.scene == scene) return true;
        foreach (var ending in FindObjectsOfType<EnemyHurtAshPlayback>())
            if (ending.isActiveAndEnabled && ending.gameObject.scene == scene) return true;
        return false;
    }

    internal static void ClearIn(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var ending in FindObjectsOfType<EnemyDefeatSpritePlayback>())
            if (ending.gameObject.scene == scene) { ending.gameObject.SetActive(false); Destroy(ending.gameObject); }
        foreach (var ending in FindObjectsOfType<EnemyHurtAshPlayback>())
            if (ending.gameObject.scene == scene) { ending.gameObject.SetActive(false); Destroy(ending.gameObject); }
    }

    SpriteRenderer appearance;
    Sprite[] frames;
    int fps;
    float elapsed, hold, fade;
    Material ashMaterial;
    float ashSeconds;
    EnemyBlackAshDissolve ash;
    EnemyDeathSpriteMesh stableDeath;
    Color originalColor;
    public void Initialize(SpriteRenderer renderer, Sprite[] sequence, int framesPerSecond,
        float holdSeconds = 0f, float fadeSeconds = 0f, Material blackAshMaterial = null, float blackAshSeconds = 0f,
        bool deformDeath = false, Vector3[] visibleVertices = null, float groundY = 0f, Vector4 paintedBounds = default,
        EnemyArtworkProfile artworkProfile = EnemyArtworkProfile.Crow)
    {
        appearance = renderer; frames = sequence; fps = Mathf.Max(1, framesPerSecond);
        hold = Mathf.Max(0f, holdSeconds); fade = Mathf.Max(0f, fadeSeconds);
        ashMaterial = blackAshMaterial; ashSeconds = Mathf.Max(0f, blackAshSeconds);
        elapsed = 0f;
        if (appearance == null || frames == null || frames.Length == 0) { Destroy(gameObject); return; }
        originalColor = appearance.color;
        if (deformDeath)
        {
            var root = new GameObject("StableDeath"); root.transform.SetParent(appearance.transform, false);
            stableDeath = root.AddComponent<EnemyDeathSpriteMesh>();
            stableDeath.Initialize(appearance, visibleVertices, groundY, paintedBounds, artworkProfile);
        }
        else appearance.sprite = frames[0];
    }
    void Update()
    {
        if (appearance == null || frames == null || frames.Length == 0) { Destroy(gameObject); return; }
        elapsed += Time.deltaTime;
        int index = Mathf.FloorToInt(elapsed * fps);
        float duration = frames.Length / (float)fps;
        if (stableDeath != null) stableDeath.Apply(elapsed / duration);
        else appearance.sprite = frames[Mathf.Min(index, frames.Length - 1)];
        float fadeStart = frames.Length / (float)fps + hold;
        if (elapsed < fadeStart) return;
        if (ashMaterial != null && ashSeconds > 0f)
        {
            if (elapsed >= fadeStart + ashSeconds) { Destroy(gameObject); return; }
            if (ash == null)
            {
                ash = gameObject.AddComponent<EnemyBlackAshDissolve>();
                ash.Initialize(appearance, ashMaterial, stableDeath);
            }
            ash.SetProgress((elapsed - fadeStart) / ashSeconds);
            return;
        }
        if (fade <= 0f || elapsed >= fadeStart + fade) { Destroy(gameObject); return; }
        Color color = originalColor;
        color.a *= 1f - (elapsed - fadeStart) / fade;
        appearance.color = color;
        if (stableDeath != null)
        {
            var properties = new MaterialPropertyBlock(); stableDeath.Appearance.GetPropertyBlock(properties);
            properties.SetColor("_Color", color); stableDeath.Appearance.SetPropertyBlock(properties);
        }
    }
}
