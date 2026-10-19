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

    public Sprite Portrait => animations != null ? animations.Portrait : null;
    public bool CanPlayAttack => isActiveAndEnabled && animatedRenderer != null
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
        animatedRenderer.sharedMaterial = appearance.sharedMaterial;
        animatedRenderer.color = appearance.color;
        animatedRenderer.sortingLayerID = appearance.sortingLayerID;
        animatedRenderer.sortingOrder = appearance.sortingOrder;
        animatedRenderer.flipX = appearance.flipX;
        animatedRenderer.flipY = appearance.flipY;
        appearance.enabled = false;
        if (animations.useDeformedIdle)
        {
            var idleRoot = new GameObject("StableIdle");
            idleRoot.transform.SetParent(visual, false);
            stableIdle = idleRoot.AddComponent<EnemyIdleSpriteMesh>();
            stableIdle.Initialize(animations.Portrait, animatedRenderer);
        }
        ownsVisual = true;
        defeated = false;
        ReturnToIdle();
    }

    void Update()
    {
        if (animatedRenderer == null || motion == Motion.Attack) return;
        clock += Time.deltaTime;
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
        if (visual != null) visual.localPosition = visualOffset;
        if (animatedRenderer != null) animatedRenderer.sprite = animations.Portrait;
        if (stableIdle != null) { stableIdle.Apply(0f, animations.idleCycleSeconds); stableIdle.Show(true); }
    }

    [ContextMenu("Play Mode/Preview Hurt")]
    public void PlayHurt()
    {
        if (!isActiveAndEnabled || animatedRenderer == null || !animations.HasHurt) return;
        ++playbackVersion;
        motion = Motion.Hurt;
        if (stableIdle != null) stableIdle.Show(false);
        clock = 0f;
        visual.localPosition = visualOffset;
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
        if (animatedRenderer == null || animations == null || !animations.HasHurt) return;
        ++playbackVersion;
        defeated = true;
        if (stableIdle != null) stableIdle.Show(false);
        visual.localPosition = visualOffset;
        visual.SetParent(null, true);
        visual.name = "DefeatedEnemyVisual";
        animatedRenderer.sprite = animations.hurtFrames[0];
        var ending = visual.gameObject.AddComponent<EnemyDefeatSpritePlayback>();
        ending.Initialize(animatedRenderer, animations.hurtFrames, animations.framesPerSecond);
        visual = null;
        animatedRenderer = null;
    }

    Vector3 Approach(Combatant target)
    {
        if (target == null) return Vector3.zero;
        var collider = target.GetComponent<Collider2D>();
        Bounds bounds = collider != null ? collider.bounds : new Bounds(target.transform.position, Vector3.one);
        Vector3 contact = bounds.center;
        contact.x += Mathf.Sign(transform.position.x - contact.x) * bounds.extents.x;
        contact.z = visual.position.z;
        Vector3 beak = visual.TransformPoint(animations.attackContactPoint);
        return appearance.transform.InverseTransformVector(contact - beak);
    }

    void Pose(EnemyAttackAnimationClock.Sample sample, EnemyAttackAnimationClock timing, Vector3 approach)
    {
        animatedRenderer.sprite = animations.attackFrames[sample.Frame];
        float contact = timing.ContactTime;
        float release = contact * .6f;
        float amount = sample.Time <= contact
            ? Mathf.InverseLerp(release, contact, sample.Time)
            : 1f - Mathf.InverseLerp(contact, timing.Duration, sample.Time);
        amount = Mathf.SmoothStep(0f, 1f, amount);
        visual.localPosition = visualOffset + approach * amount;
    }

    public IEnumerator PlayAttack(Combatant target, Func<bool> attackerAlive, Func<bool> targetAlive, Action onHit)
    {
        if (!CanPlayAttack) yield break;
        int version = ++playbackVersion;
        var timing = new EnemyAttackAnimationClock(animations.attackFrames.Length,
            animations.framesPerSecond, animations.attackContactFrame);
        visual.localPosition = visualOffset;
        Vector3 approach = Approach(target);
        motion = Motion.Attack;
        if (stableIdle != null) stableIdle.Show(false);
        float elapsed = 0f;
        try
        {
            while (this != null && isActiveAndEnabled && version == playbackVersion
                && attackerAlive() && targetAlive())
            {
                var sample = timing.At(elapsed);
                Pose(sample, timing, approach);
                if (sample.DeliverHit) onHit?.Invoke();
                if (sample.Complete) break;
                yield return null;
                elapsed += Time.deltaTime;
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

// The logical enemy may die immediately; its detached sprite finishes the short hit reaction.
internal sealed class EnemyDefeatSpritePlayback : MonoBehaviour
{
    SpriteRenderer appearance;
    Sprite[] frames;
    int fps;
    float elapsed;
    public void Initialize(SpriteRenderer renderer, Sprite[] sequence, int framesPerSecond)
    { appearance = renderer; frames = sequence; fps = Mathf.Max(1, framesPerSecond); }
    void Update()
    {
        if (appearance == null || frames == null) { Destroy(gameObject); return; }
        elapsed += Time.deltaTime;
        int index = Mathf.FloorToInt(elapsed * fps);
        if (index >= frames.Length) { Destroy(gameObject); return; }
        appearance.sprite = frames[index];
    }
}
