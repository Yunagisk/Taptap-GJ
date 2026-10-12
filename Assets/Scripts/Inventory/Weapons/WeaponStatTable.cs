using System.Text;

// 武器普攻与技能说明。数字来自 docs/[程序_策划] 数值配置表.xlsx 的「武器普攻」「武器技能」。
// 战斗普攻读这里。技能说明只给商店详情，技能效果仍未进战斗结算。
public sealed class WeaponProfile
{
    public readonly string id;
    public readonly int pierce;
    public readonly int slash;
    public readonly int blunt;
    public readonly int fire;
    public readonly int hits;
    public readonly string skillName;
    public readonly int skillResolve;
    public readonly string skillTarget;
    public readonly string skillDamage;
    public readonly string skillNote;

    public WeaponProfile(string id, int pierce, int slash, int blunt, int fire, int hits,
        string skillName, int skillResolve, string skillTarget, string skillDamage, string skillNote)
    {
        this.id = id;
        this.pierce = pierce;
        this.slash = slash;
        this.blunt = blunt;
        this.fire = fire;
        this.hits = hits;
        this.skillName = skillName;
        this.skillResolve = skillResolve;
        this.skillTarget = skillTarget;
        this.skillDamage = skillDamage;
        this.skillNote = skillNote;
    }
}

public static class WeaponStatTable
{
    public static readonly WeaponProfile[] All =
    {
        new WeaponProfile("sword", 12, 0, 0, 0, 1, "穿刺", 2, "单体", "穿刺30", "无视穿刺抗性，不穿透免疫。"),
        new WeaponProfile("greatsword", 16, 0, 0, 0, 1, "镇压", 3, "单体", "穿刺36", "伤害后衰弱1层；穿刺免疫阻止衰弱。"),
        new WeaponProfile("spear", 13, 0, 0, 0, 1, "穿心", 2, "单体", "穿刺22", "命中穿刺弱点额外累计1破防；保护期无效。"),
        new WeaponProfile("dual_swords", 5, 0, 0, 0, 2, "连刺", 3, "单体", "穿刺5×4段", "逐段判断死亡与破防。"),
        new WeaponProfile("axe", 0, 14, 0, 0, 1, "顺劈", 2, "全体", "每名敌人劈砍12", "对所有存活敌人分别结算。"),
        new WeaponProfile("sickle", 0, 6, 0, 0, 2, "收割", 3, "单体", "劈砍9×2段", "攻击开始时目标已破防，本次伤害×1.30。"),
        new WeaponProfile("hammer", 0, 0, 14, 0, 1, "破甲", 2, "单体", "钝击24", "伤害后破甲2层；钝击免疫阻止破甲。"),
        new WeaponProfile("flail", 0, 0, 5, 0, 2, "缠击", 2, "单体", "钝击10×2段", "整次行动附加一次衰弱1层；钝击免疫阻止衰弱。"),
        new WeaponProfile("torch", 0, 0, 0, 10, 1, "点燃", 2, "单体", "火焰14", "伤害后点燃2层；火焰免疫阻止点燃。"),
        new WeaponProfile("poleaxe", 7, 6, 0, 0, 1, "横扫突刺", 3, "主目标＋全体", "先主目标穿刺8，再全体各劈砍6", "逐段判定；主目标最多承受两个段。"),
        new WeaponProfile("flame_tongue", 7, 0, 0, 5, 1, "焚心", 3, "单体", "穿刺12＋火焰8，1个混合段", "主角坚定1层；目标免疫不阻止自身增益。"),
        new WeaponProfile("heaven_fire", 0, 0, 7, 5, 1, "烙刑", 3, "单体", "钝击10＋火焰8，1个混合段", "破甲1层、点燃1层；钝击免疫阻止破甲，火焰免疫阻止点燃。"),
    };

    public static WeaponProfile Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id) return All[i];
        return null;
    }

    public static string AttackBrief(string id)
    {
        WeaponProfile profile = Find(id);
        if (profile == null) return "";
        var parts = new StringBuilder();
        AppendPart(parts, "穿刺", profile.pierce);
        AppendPart(parts, "劈砍", profile.slash);
        AppendPart(parts, "钝击", profile.blunt);
        AppendPart(parts, "火焰", profile.fire);
        if (parts.Length == 0) parts.Append("无伤害");
        parts.Append("×").Append(profile.hits);
        return parts.ToString();
    }

    public static string Detail(string id)
    {
        WeaponProfile profile = Find(id);
        string name = WeaponTable.NameOf(id);
        if (profile == null) return name;
        return name
            + "\n价格  " + WeaponTable.PriceOf(id) + " 罪骸"
            + "\n普攻  " + AttackBrief(id)
            + "\n技能  " + profile.skillName + "  决心 " + profile.skillResolve
            + "\n目标  " + profile.skillTarget
            + "\n伤害  " + profile.skillDamage
            + "\n" + profile.skillNote
            + "\n技能效果尚未进入战斗结算。普攻数字会在战斗中生效。";
    }

    static void AppendPart(StringBuilder parts, string label, int value)
    {
        if (value <= 0) return;
        if (parts.Length > 0) parts.Append("+");
        parts.Append(label).Append(value);
    }
}
