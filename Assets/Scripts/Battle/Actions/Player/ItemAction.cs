using System.Collections;

public sealed class ItemAction : IBattleAction
{
    readonly GameState state;
    public string ItemId { get; }
    public ItemResult Result { get; private set; }

    public ItemAction(GameState state, string id)
    { this.state = state; ItemId = ItemTable.CanonicalId(id); }

    public static ItemResult Validate(GameState state, string id, PlayerCombatant user,
        EnemyCombatant target = null, bool selectingTarget = false)
    {
        return ItemRules.Validate(state, id, true, user != null ? new BattleUser(user) : null,
            target != null ? new WeaknessTarget(target) : null, selectingTarget);
    }

    public IEnumerator Execute(Combatant user, Combatant target)
    {
        Result = ItemRules.TryUse(state, ItemId, true, user != null ? new BattleUser(user) : null,
            target is EnemyCombatant enemy ? new WeaknessTarget(enemy) : null);
        yield break;
    }

    sealed class BattleUser : IItemUser
    {
        readonly Combatant body;
        public BattleUser(Combatant body) { this.body = body; }
        public int CurrentHp => body != null ? body.CurrentHP : 0;
        public int MaxHp => body != null ? body.MaxHP : 0;
        public StatusSet Statuses => body != null ? body.Statuses : null;
        public int Heal(int amount) => body != null ? body.HealRaw(amount) : 0;
    }

    sealed class WeaknessTarget : IItemWeaknessTarget
    {
        readonly EnemyCombatant body;
        readonly DamageResistance resistance;
        public WeaknessTarget(EnemyCombatant body)
        { this.body = body; resistance = body.GetComponent<DamageResistance>(); }
        public bool IsAlive => body != null && !body.IsDead;
        public int UnknownWeaknessCount => resistance != null ? resistance.UnknownWeaknessCount : 0;
        public int RevealWeaknesses() => IsAlive && resistance != null ? resistance.RevealWeaknesses() : 0;
    }
}
