using UnityEngine;

public enum SmallMonsterKind { Rat, Crow, Wraith }

[CreateAssetMenu(menuName = "Battle/Small Monster")]
public sealed class SmallMonsterData : ScriptableObject
{
    public SmallMonsterKind kind;
    public string displayName;
    [Min(1)] public int maxHp = 18;
    [Min(1)] public int breakThreshold = 2;
    [Min(0)] public int lowHealthGuardWeight;
    public EnemyAttackData[] actions;
    public DamageResistance.DamageRule[] resistances;
}
