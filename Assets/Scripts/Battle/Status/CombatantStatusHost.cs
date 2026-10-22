using UnityEngine;

public sealed class CombatantStatusHost : IStatusBody
{
    readonly Combatant body;

    public CombatantStatusHost(Combatant body)
    {
        this.body = body;
    }

    public bool IsDead => body == null || body.IsDead;
    public int MaxHp => body.MaxHP;
    public StatusSet Statuses => body.Statuses;
    public BreakGauge Break => (body as EnemyCombatant)?.Break;

    public TypeRelation FireRelation
    {
        get
        {
            if (body == null)
                return TypeRelation.Normal;

            DamageResistance resistance = body.GetComponent<DamageResistance>();
            if (resistance == null)
                return TypeRelation.Normal;

            return Map(resistance.GetDamageReaction(PlayerDamageType.Fire));
        }
    }

    public int LoseRaw(int amount)
    {
        return body.LoseRaw(amount);
    }

    public int HealRaw(int amount)
    {
        return body.HealRaw(amount);
    }

    public static TypeRelation Map(DamageReaction reaction)
    {
        switch (reaction)
        {
            case DamageReaction.Weak: return TypeRelation.Weak;
            case DamageReaction.Resistant: return TypeRelation.Resistant;
            case DamageReaction.Immune: return TypeRelation.Immune;
            default: return TypeRelation.Normal;
        }
    }
}
