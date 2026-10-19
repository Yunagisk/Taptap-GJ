using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Enemy Sprite Animation Set")]
public sealed class EnemySpriteAnimationSet : ScriptableObject
{
    [Min(1)] public int framesPerSecond = 30;
    public Sprite[] idleFrames;
    public bool useDeformedIdle;
    [Min(.1f)] public float idleCycleSeconds = 3.2f;
    public Sprite[] attackFrames;
    public Sprite[] hurtFrames;
    [Min(0)] public int attackContactFrame = 9;
    // Contact point of the beak in the attack sprite's local coordinates.
    public Vector2 attackContactPoint = new Vector2(-3.1f, -2.87f);

    public Sprite Portrait => idleFrames != null && idleFrames.Length > 0 ? idleFrames[0] : null;
    public bool HasAttack => attackFrames != null && attackFrames.Length > 0;
    public bool HasHurt => hurtFrames != null && hurtFrames.Length > 0;
}
