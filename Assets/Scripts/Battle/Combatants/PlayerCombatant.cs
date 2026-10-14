using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombatant : Combatant
{

    // ================= 初始化 =================

    PlayerState runPlayer;

    void Awake()
    {
        OnHpChanged += SyncRunHealth;
        OnDeterminationChanged += SyncRunDetermination;
    }

    void OnDestroy()
    {
        OnHpChanged -= SyncRunHealth;
        OnDeterminationChanged -= SyncRunDetermination;
    }

    public override void Initialize()
    {
        var root = GameRoot.I;
        bool sharedRun = root != null && root.state != null && root.state.map != null
            && !string.IsNullOrEmpty(root.state.map.templateId);
        runPlayer = sharedRun ? root.state.player : null;
        maxDetermination = runPlayer != null ? Mathf.Max(0, runPlayer.maxResolve) : StandaloneMaxDetermination;
        currentDetermination = runPlayer != null
            ? Mathf.Clamp(runPlayer.resolve, 0, maxDetermination) : 0;
        IsDefending = false;

        if (runPlayer != null)
            RestoreHealth(runPlayer.hp, runPlayer.maxHp);
        else
            base.Initialize();

        Statuses.ClearOrdinary();
        NotifyMarks();
        OnDeterminationChanged?.Invoke(currentDetermination);
        OnDefenseChanged?.Invoke(this, IsDefending);
    }

    void SyncRunHealth(Combatant combatant)
    {
        SyncRunState();
    }

    void SyncRunDetermination(int determination)
    {
        SyncRunState();
    }

    void SyncRunState()
    {
        // 重开和复活可能已更新全局状态，销毁旧单位时不要覆盖它。
        if (runPlayer == null || GameRoot.I == null || GameRoot.I.state == null
            || GameRoot.I.state.player != runPlayer) return;
        runPlayer.hp = Mathf.Clamp(currentHp, 0, maxHp);
        runPlayer.maxHp = maxHp;
        runPlayer.resolve = Mathf.Clamp(currentDetermination, 0, maxDetermination);
        runPlayer.maxResolve = maxDetermination;
    }

    protected override void Die()
    {
        SyncRunState();
        base.Die();
    }

    // ================= 武器 =================
    [Header("当前武器")]
    [SerializeField] private WeaponData currentWeapon;

    public WeaponData CurrentWeapon => currentWeapon;

    public int AttackCount
    {
        get
        {
            int count = currentWeapon != null ? currentWeapon.AttackCount : 0;
            if (count <= 0)
                return 0;
            return count + StatusRules.ExtraBasicSegments(Statuses);
        }
    }

    public IReadOnlyList<DamageComponent> DamageComponents =>
        currentWeapon != null
            ? currentWeapon.DamageComponents
            : System.Array.Empty<DamageComponent>();

    public event Action<WeaponData> OnWeaponChanged;

    public void EquipWeapon(WeaponData weapon)
    {
        if (weapon == currentWeapon)
            return;

        currentWeapon = weapon;

        if (currentWeapon != null)
            Debug.Log($"玩家装备武器：{currentWeapon.WeaponName}");

        OnWeaponChanged?.Invoke(currentWeapon);
    }

    // ================= 防御系统 =================

    [Header("防御属性")]
    [SerializeField, Range(0f, 1f)]
    private float defenseRate = 0.5f;

    public bool IsDefending { get; private set; }

    public event Action<Combatant, bool> OnDefenseChanged;


    public void StartDefense()
    {
        if (IsDead || IsDefending)
            return;

        IsDefending = true;

        Debug.Log($"{gameObject.name}进入防御状态");

        OnDefenseChanged?.Invoke(this, IsDefending);
    }


    public void EndDefense()
    {
        if (!IsDefending)
            return;

        IsDefending = false;

        Debug.Log($"{gameObject.name}结束防御状态");

        OnDefenseChanged?.Invoke(this, IsDefending);
    }

    // ================= 决心系统 =================
    [Header("决心值")]
    private const int StandaloneMaxDetermination = 4;
    private int maxDetermination = StandaloneMaxDetermination;
    [SerializeField] private int currentDetermination = 0;

    public int CurrentDetermination => currentDetermination;
    public int MaxDetermination => maxDetermination;

    public event Action<int> OnDeterminationChanged;

    // 疲惫把本次收益变成 0。坚定只在额外 1 点真的进池后减层。
    public int AddDetermination(int amount = 1)
    {
        bool exhausted = Statuses.Has(StatusId.Exhaust);
        ResolveGrant grant = StatusRules.GrantResolve(
            currentDetermination,
            maxDetermination,
            amount,
            exhausted,
            Statuses.Stacks(StatusId.Steadfast));

        if (exhausted && amount > 0)
            Debug.Log($"{name} 疲惫，决心不增加");

        if (grant.ConsumeSteadfast)
            Statuses.Reduce(StatusId.Steadfast, 1);

        if (grant.Granted <= 0)
            return 0;

        currentDetermination += grant.Granted;
        OnDeterminationChanged?.Invoke(currentDetermination);
        return grant.Granted;
    }

    //消耗决心
    public bool SpendDetermination(int amount)
    {
        if (amount < 0 || currentDetermination < amount)
            return false;

        currentDetermination -= amount;

        if (amount > 0)
        {
            OnDeterminationChanged?.Invoke(currentDetermination);
        }

        return true;
    }

    // ================= 受伤逻辑 =================

    public override int TakeDamage(int damage)
    {
        int finalDamage = Mathf.Max(0, damage);

        if (IsDefending)
        {
            finalDamage = Mathf.FloorToInt(
                finalDamage * defenseRate
            );
        }

        return base.TakeDamage(finalDamage);
    }


}