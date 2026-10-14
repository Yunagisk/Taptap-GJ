// 九种普通状态。诅咒、美德、防御、护盾、破防和保护期不在这里。
// 破甲才是直接伤害加深（易伤）。破防是敌人身上的累计条，不加成伤害。
// 容器允许双方都挂这 9 种。已写明的效果：
// 再生、中毒、衰弱、破甲、点燃双方都结算。
// 加速只加玩家普攻段。坚定、疲惫只改玩家决心和武器技能。混乱只改玩家单体普攻目标。
// 破防进度和破防阈值只在敌人身上。主角没有破防条。
public enum StatusId
{
    Regen,      // 再生：自身回合末按层数回血，然后减 1 层
    Haste,      // 加速：普攻多 1 段，多层不加段
    Steadfast,  // 坚定：决心真正多 1 点才减 1 层

    Poison,     // 中毒：自身回合末按层数扣血，然后减 1 层
    Weak,       // 衰弱：直接伤害 -20%，多层不提高比例
    Sunder,     // 破甲：受到的直接伤害 +20%，与破防无关
    Exhaust,    // 疲惫：不能用武器技能，也不能获得决心
    Confuse,    // 混乱：玩家的单体伤害攻击改为随机敌人
    Ignite      // 点燃：自身回合末掉最大生命 5%，轮末才减层
}

public enum StatusClock
{
    IntensityOnOwnTurnEnd,
    DurationOnRoundEnd,
    TriggerCount
}

// 伤害类型和弱点的关系。和战斗里的 DamageReaction 一一对应。
public enum TypeRelation
{
    Weak,
    Normal,
    Resistant,
    Immune
}
