using System.Collections.Generic;
using UnityEngine;

// The same palette is used by battle effects, health bars and status markers.
public static class StatusAppearance
{
    static readonly StatusId[] Priority = { StatusId.Poison, StatusId.Ignite, StatusId.Sunder,
        StatusId.Confuse, StatusId.Exhaust, StatusId.Weak, StatusId.Regen, StatusId.Haste, StatusId.Steadfast };

    public static Color ColorOf(StatusId id)
    {
        switch (id)
        {
            case StatusId.Poison: return new Color32(137, 186, 71, 255);
            case StatusId.Ignite: return new Color32(228, 132, 62, 255);
            case StatusId.Sunder: return new Color32(201, 114, 96, 255);
            case StatusId.Confuse: return new Color32(183, 147, 216, 255);
            case StatusId.Exhaust: return new Color32(140, 165, 186, 255);
            case StatusId.Weak: return new Color32(175, 153, 178, 255);
            case StatusId.Regen: return new Color32(106, 194, 164, 255);
            case StatusId.Haste: return new Color32(125, 197, 215, 255);
            default: return new Color32(225, 192, 109, 255);
        }
    }
    public static StatusId? Primary(StatusSet statuses)
    {
        if (statuses != null) foreach (var id in Priority) if (statuses.Has(id)) return id;
        return null;
    }
    public static Color HealthTint(StatusSet statuses, Color normal)
    {
        var primary = Primary(statuses);
        return primary.HasValue ? BarColor(primary.Value) : normal;
    }
    public static Color HealthTint(IReadOnlyList<StatusSnapshot> statuses, Color normal)
    {
        if (statuses != null) foreach (var id in Priority)
            foreach (var entry in statuses) if (entry.id == id && entry.stacks > 0) return BarColor(id);
        return normal;
    }
    static Color BarColor(StatusId id)
    {
        Color tint = ColorOf(id) * .65f;
        tint.a = 1;
        return tint;
    }
}
