using System;
using System.Collections.Generic;
using System.Text;

// 同名状态合并成一组。再次施加会刷新整组的施加轮，当轮免扣。
public sealed class StatusSet
{
    sealed class StatusInstance
    {
        public int Stacks;
        public int AppliedRound;
    }

    readonly Dictionary<StatusId, StatusInstance> map = new Dictionary<StatusId, StatusInstance>();

    public event Action Changed;

    public int Stacks(StatusId id)
    {
        StatusInstance inst;
        return map.TryGetValue(id, out inst) ? inst.Stacks : 0;
    }

    public bool Has(StatusId id)
    {
        return Stacks(id) > 0;
    }

    public int AppliedRound(StatusId id)
    {
        StatusInstance inst;
        return map.TryGetValue(id, out inst) ? inst.AppliedRound : 0;
    }

    public void Apply(StatusId id, int amount, int round)
    {
        if (amount <= 0)
            return;

        StatusInstance inst;
        if (!map.TryGetValue(id, out inst))
        {
            inst = new StatusInstance();
            map[id] = inst;
        }

        int cap = StatusRules.CapOf(id);
        int stacked = inst.Stacks + amount;
        inst.Stacks = stacked > cap ? cap : stacked;
        inst.AppliedRound = round;
        Changed?.Invoke();
    }

    public void Reduce(StatusId id, int amount)
    {
        if (amount <= 0)
            return;

        StatusInstance inst;
        if (!map.TryGetValue(id, out inst))
            return;

        inst.Stacks -= amount;
        if (inst.Stacks <= 0)
            map.Remove(id);
        Changed?.Invoke();
    }

    // 净化只解除普通负面，不碰增益、诅咒和破防。
    public bool Purge(StatusId id)
    {
        if (!StatusRules.IsDebuff(id))
            return false;
        if (!map.Remove(id))
            return false;
        Changed?.Invoke();
        return true;
    }

    public int DebuffCount
    {
        get
        {
            int count = 0;
            foreach (var id in map.Keys) if (StatusRules.IsDebuff(id)) count++;
            return count;
        }
    }

    public int PurgeAllDebuffs()
    {
        int count = 0;
        foreach (StatusId id in Enum.GetValues(typeof(StatusId)))
            if (StatusRules.IsDebuff(id) && map.Remove(id)) count++;
        if (count > 0) Changed?.Invoke();
        return count;
    }

    public void WriteSnapshot(List<StatusSnapshot> result)
    {
        if (result == null) return;
        result.Clear();
        foreach (var pair in map) result.Add(new StatusSnapshot(pair.Key, pair.Value.Stacks));
    }

    public void RestoreSnapshot(IReadOnlyList<StatusSnapshot> source)
    {
        map.Clear();
        if (source != null)
            foreach (var entry in source)
            {
                if (entry.stacks <= 0 || !Enum.IsDefined(typeof(StatusId), entry.id)) continue;
                map[entry.id] = new StatusInstance
                { Stacks = Math.Min(entry.stacks, StatusRules.CapOf(entry.id)), AppliedRound = 0 };
            }
        Changed?.Invoke();
    }

    public void ClearOrdinary()
    {
        if (map.Count == 0)
            return;
        map.Clear();
        Changed?.Invoke();
    }

    // 施加当轮不减。只减持续时间型：加速、衰弱、破甲、疲惫、混乱、点燃。
    public void TickDuration(int round)
    {
        if (map.Count == 0)
            return;

        StatusId[] keys = new StatusId[map.Count];
        map.Keys.CopyTo(keys, 0);
        bool changed = false;
        for (int i = 0; i < keys.Length; i++)
        {
            StatusId id = keys[i];
            if (StatusRules.ClockOf(id) != StatusClock.DurationOnRoundEnd)
                continue;

            StatusInstance inst = map[id];
            if (inst.AppliedRound >= round)
                continue;

            inst.Stacks -= 1;
            changed = true;
            if (inst.Stacks <= 0)
                map.Remove(id);
        }

        if (changed)
            Changed?.Invoke();
    }

    public string Format()
    {
        StringBuilder builder = new StringBuilder();
        foreach (StatusId id in Enum.GetValues(typeof(StatusId)))
        {
            int stacks = Stacks(id);
            if (stacks <= 0)
                continue;
            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(StatusRules.DisplayName(id));
            builder.Append(stacks);
        }
        return builder.ToString();
    }
}
