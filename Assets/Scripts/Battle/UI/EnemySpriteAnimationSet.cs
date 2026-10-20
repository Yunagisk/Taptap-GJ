using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Enemy Sprite Animation Set")]
public sealed class EnemySpriteAnimationSet : ScriptableObject
{
    [Min(1)] public int framesPerSecond = 30;
    public Sprite[] idleFrames;
    public Material artworkMaterial;
    public bool preserveArtworkColors;
    public Sprite portraitIcon;
    public bool useDeformedIdle;
    public EnemyArtworkProfile artworkProfile;
    [Min(1)] public int artworkAttackFrames = 21;
    [Min(1)] public int artworkHurtFrames = 12;
    [Min(1)] public int artworkDeathFrames = 27;
    // Point on the original silhouette: mouth for rat, reaching fingertips for wraith.
    public Vector2 artworkContact = new Vector2(.04f, .4f);
    [Min(.1f)] public float idleCycleSeconds = 3.2f;
    public Sprite[] attackFrames;
    public Sprite[] hurtFrames;
    public Sprite[] deathFrames;
    public bool useDeformedDeath;
    public bool hurtAndAshOnDeath;
    // Actual painted bounds in bottom-up normalized sprite coordinates, excluding transparent padding.
    public Vector4 artworkBounds = new Vector4(0f, 0f, 1f, 1f);
    [Min(0f)] public float deathHoldSeconds = .15f;
    [Min(0f)] public float deathFadeSeconds = .25f;
    public Material deathAshMaterial;
    [Min(0f)] public float deathAshSeconds = 1f;
    [Min(0)] public int attackContactFrame = 9;
    // World-space travel of the visual only; attacks do not need physical contact.
    [Min(0f)] public float attackLungeDistance = .12f;
    // Contact point of the beak in the attack sprite's local coordinates.
    public Vector2 attackContactPoint = new Vector2(-3.1f, -2.87f);

    public Sprite Portrait => idleFrames != null && idleFrames.Length > 0 ? idleFrames[0] : null;
    public bool UsesArtworkMotion => artworkProfile != EnemyArtworkProfile.Crow;
    public int AttackFrameCount => UsesArtworkMotion ? Mathf.Max(1, artworkAttackFrames) : attackFrames != null ? attackFrames.Length : 0;
    public int HurtFrameCount => UsesArtworkMotion ? Mathf.Max(1, artworkHurtFrames) : hurtFrames != null ? hurtFrames.Length : 0;
    public bool HasAttack => AttackFrameCount > 0;
    public bool HasHurt => HurtFrameCount > 0;
    public bool HasDeath => (hurtAndAshOnDeath && HasHurt) || UsesArtworkMotion || (deathFrames != null && deathFrames.Length > 0);
}
