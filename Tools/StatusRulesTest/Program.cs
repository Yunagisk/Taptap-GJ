using System;
using System.IO;

static class Program
{
    static int failures;

    static void Main()
    {
        TestCapsAndPurge();
        TestDurationClock();
        TestRefreshExemption();
        TestPoisonRegenIgnite();
        TestDeathStopsLaterTicks();
        TestDirectModifiers();
        TestResolve();
        TestBreakGauge();
        TestIgniteBreak();
        TestWeaponSkills();
        TestEnemyMoves();
        TestSkillAssets();

        if (failures > 0)
        {
            Console.Error.WriteLine(failures + " failed");
            Environment.Exit(1);
        }

        Console.WriteLine("status rules ok");
    }

    static void TestCapsAndPurge()
    {
        StatusSet set = new StatusSet();
        set.Apply(StatusId.Confuse, 5, 1);
        Eq(set.Stacks(StatusId.Confuse), 2, "混乱上限 2");
        set.Apply(StatusId.Poison, 5, 1);
        Eq(set.Stacks(StatusId.Poison), 3, "中毒上限 3");
        Eq(set.Purge(StatusId.Poison) ? 1 : 0, 1, "净化中毒");
        Eq(set.Stacks(StatusId.Poison), 0, "中毒已清除");
        set.Apply(StatusId.Regen, 2, 1);
        Eq(set.Purge(StatusId.Regen) ? 1 : 0, 0, "净化不能删再生");
        Eq(set.Stacks(StatusId.Regen), 2, "再生还在");
        set.TickDuration(1);
        set.TickDuration(4);
        Eq(set.Stacks(StatusId.Regen), 2, "再生不吃轮末减层");
        set.Apply(StatusId.Steadfast, 2, 1);
        set.TickDuration(3);
        Eq(set.Stacks(StatusId.Steadfast), 2, "坚定不吃轮末减层");
    }

    static void TestDurationClock()
    {
        StatusSet set = new StatusSet();
        set.Apply(StatusId.Sunder, 1, 1);
        set.TickDuration(1);
        Eq(set.Stacks(StatusId.Sunder), 1, "破甲施加当轮不减");
        Eq(StatusRules.ScaleDirectDamage(100, false, set.Has(StatusId.Sunder)), 120, "第2轮仍破甲");
        set.TickDuration(2);
        Eq(set.Stacks(StatusId.Sunder), 0, "破甲第2轮末移除");

        set.Apply(StatusId.Haste, 2, 1);
        Eq(StatusRules.ExtraBasicSegments(set), 1, "2层加速也只多1段");
        set.TickDuration(1);
        Eq(set.Stacks(StatusId.Haste), 2, "加速当轮不减");
        set.TickDuration(2);
        Eq(set.Stacks(StatusId.Haste), 1, "加速第2轮末减1");
        Eq(StatusRules.ExtraBasicSegments(set), 1, "剩1层仍然多1段");
        set.TickDuration(3);
        Eq(set.Stacks(StatusId.Haste), 0, "加速第3轮末移除");
        Eq(StatusRules.ExtraBasicSegments(set), 0, "加速没了不多段");
    }

    static void TestRefreshExemption()
    {
        StatusSet set = new StatusSet();
        set.Apply(StatusId.Weak, 1, 1);
        set.TickDuration(1);
        set.Apply(StatusId.Weak, 1, 2);
        Eq(set.Stacks(StatusId.Weak), 2, "叠层合并");
        Eq(set.AppliedRound(StatusId.Weak), 2, "整组刷新施加轮");
        set.TickDuration(2);
        Eq(set.Stacks(StatusId.Weak), 2, "刷新当轮免扣");
        set.TickDuration(3);
        Eq(set.Stacks(StatusId.Weak), 1, "下一轮末才减");

        set.Apply(StatusId.Haste, 5, 1);
        Eq(set.Stacks(StatusId.Haste), 3, "加速上限 3");
        set.Apply(StatusId.Haste, 1, 2);
        Eq(set.Stacks(StatusId.Haste), 3, "顶满再施加不超上限");
        set.TickDuration(2);
        Eq(set.Stacks(StatusId.Haste), 3, "顶满再施加仍刷新免扣");
    }

    static void TestPoisonRegenIgnite()
    {
        FakeBody poison = new FakeBody();
        poison.Statuses.Apply(StatusId.Poison, 3, 1);
        StatusSettlement.SettleOwnTurn(poison, 1);
        StatusSettlement.SettleOwnTurn(poison, 2);
        StatusSettlement.SettleOwnTurn(poison, 3);
        Eq(poison.Hp, 94, "3层中毒共 6 点");
        Eq(poison.Statuses.Stacks(StatusId.Poison), 0, "中毒结算后减层");

        FakeBody both = new FakeBody { Hp = 10 };
        both.Statuses.Apply(StatusId.Poison, 3, 1);
        both.Statuses.Apply(StatusId.Regen, 3, 1);
        StatusSettlement.SettleOwnTurn(both, 1);
        Eq(both.Hp, 10, "先中毒再再生");
        Eq(both.Statuses.Stacks(StatusId.Poison), 2, "中毒减到 2");
        Eq(both.Statuses.Stacks(StatusId.Regen), 2, "再生减到 2");

        FakeBody ignite = new FakeBody();
        ignite.Statuses.Apply(StatusId.Ignite, 2, 1);
        StatusSettlement.SettleOwnTurn(ignite, 1);
        ignite.Statuses.TickDuration(1);
        Eq(ignite.Hp, 95, "点燃第1轮扣 5");
        Eq(ignite.Statuses.Stacks(StatusId.Ignite), 2, "点燃当轮不减层");
        StatusSettlement.SettleOwnTurn(ignite, 2);
        ignite.Statuses.TickDuration(2);
        Eq(ignite.Hp, 90, "点燃第2轮再扣 5");
        Eq(ignite.Statuses.Stacks(StatusId.Ignite), 1, "第2轮末剩 1 层");
        StatusSettlement.SettleOwnTurn(ignite, 3);
        ignite.Statuses.TickDuration(3);
        Eq(ignite.Hp, 85, "点燃共 15 点");
        Eq(ignite.Statuses.Stacks(StatusId.Ignite), 0, "第3轮末移除");
    }

    static void TestDeathStopsLaterTicks()
    {
        FakeBody body = new FakeBody { Hp = 3 };
        body.Statuses.Apply(StatusId.Poison, 5, 1);
        body.Statuses.Apply(StatusId.Ignite, 2, 1);
        body.Statuses.Apply(StatusId.Regen, 3, 1);
        Eq(body.Statuses.Stacks(StatusId.Poison), 3, "中毒先被上限截成 3");
        StatusSettlement.SettleOwnTurn(body, 1);
        Eq(body.Hp, 0, "中毒致死");
        Eq(body.Statuses.Stacks(StatusId.Poison), 2, "中毒已经结算并减层");
        Eq(body.Statuses.Stacks(StatusId.Ignite), 2, "死后不结算点燃");
        Eq(body.Statuses.Stacks(StatusId.Regen), 3, "死后不再生");
    }

    static void TestDirectModifiers()
    {
        Eq(StatusRules.ScaleDirectDamage(100, false, true), 120, "100 破甲");
        Eq(StatusRules.ScaleDirectDamage(12, false, true), 14, "12 破甲四舍五入到 14");
        Eq(StatusRules.ScaleDirectDamage(100, true, false), 80, "100 衰弱");
        Eq(StatusRules.ScaleDirectDamage(100, true, true), 96, "衰弱和破甲相乘");
        Eq(StatusRules.ScaleDirectDamage(0, true, true), 0, "0 伤害保持 0");
        int afterDefense = (int)Math.Floor(StatusRules.ScaleDirectDamage(100, false, true) * 0.5);
        Eq(afterDefense, 60, "破甲后再防御 50%");
        Eq(StatusRules.BreakFromSegment(new[]
        {
            new SegmentPart(TypeRelation.Weak, 7),
            new SegmentPart(TypeRelation.Weak, 5)
        }), 1, "混合段两种弱点只 +1");
        Eq(StatusRules.BreakFromSegment(new[]
        {
            new SegmentPart(TypeRelation.Immune, 12)
        }), 0, "整段免疫不加破防");
        Eq(StatusRules.IgniteLoss(100, TypeRelation.Resistant), 2, "点燃遇抗性再减半");
        Eq(StatusRules.IgniteLoss(100, TypeRelation.Immune), 0, "点燃遇免疫为 0");
        Eq(StatusRules.IgniteLoss(32, TypeRelation.Normal), 1, "32 生命点燃向下取整");
    }

    static void TestResolve()
    {
        ResolveGrant full = StatusRules.GrantResolve(4, 4, 1, false, 1);
        Eq(full.Granted, 0, "满决心不加");
        Eq(full.ConsumeSteadfast ? 1 : 0, 0, "满决心不消耗坚定");

        ResolveGrant exact = StatusRules.GrantResolve(3, 4, 1, false, 1);
        Eq(exact.Granted, 1, "基础恰好到上限");
        Eq(exact.ConsumeSteadfast ? 1 : 0, 0, "恰好到上限不消耗坚定");

        ResolveGrant extra = StatusRules.GrantResolve(2, 4, 1, false, 1);
        Eq(extra.Granted, 2, "坚定多 1 点");
        Eq(extra.ConsumeSteadfast ? 1 : 0, 1, "额外点数进池才消耗");

        ResolveGrant tired = StatusRules.GrantResolve(0, 4, 1, true, 2);
        Eq(tired.Granted, 0, "疲惫收益为 0");
        Eq(tired.ConsumeSteadfast ? 1 : 0, 0, "疲惫不消耗坚定");
    }

    static void TestBreakGauge()
    {
        BreakGauge gauge = new BreakGauge(2);
        Eq(gauge.TryAdd(1, 4) ? 1 : 0, 0, "1 点未到阈值");
        Eq(gauge.Progress, 1, "进度 1");
        Eq(gauge.TryAdd(1, 4) ? 1 : 0, 1, "达到阈值破防");
        Eq(gauge.Progress, 0, "破防后进度归零");
        Eq(gauge.ProtectedThroughRound, 5, "保护到下一轮");
        Eq(gauge.IsProtected(4) ? 1 : 0, 1, "破防当轮保护");
        Eq(gauge.IsProtected(5) ? 1 : 0, 1, "下一轮仍保护");
        Eq(gauge.IsProtected(6) ? 1 : 0, 0, "再下一轮解除");
        Eq(gauge.TryAdd(1, 5) ? 1 : 0, 0, "保护期不能累计");
        Eq(gauge.Progress, 0, "保护期进度不变");
        Eq(gauge.ConsumeSkip() ? 1 : 0, 1, "跳过一次行动");
        Eq(gauge.ConsumeSkip() ? 1 : 0, 0, "不能刷新第二次跳过");
        Eq(gauge.TryAdd(1, 6) ? 1 : 0, 0, "解除后从 0 重新累计");
        Eq(gauge.Progress, 1, "新进度 1");
    }

    static void TestIgniteBreak()
    {
        FakeBody body = new FakeBody();
        body.Break = new BreakGauge(2);
        body.Fire = TypeRelation.Weak;
        body.Statuses.Apply(StatusId.Ignite, 3, 4);
        TurnEndResult end = StatusSettlement.SettleOwnTurn(body, 4);
        Eq(end.IgniteBroke ? 1 : 0, 0, "1 次点燃不够 2 点阈值");
        Eq(body.Break.Progress, 1, "点燃只 +1，不按层数");
        Eq(body.Hp, 95, "点燃照常扣血");
        Eq(body.Statuses.Stacks(StatusId.Ignite), 3, "回合末不减点燃");

        body.Break = new BreakGauge(1);
        end = StatusSettlement.SettleOwnTurn(body, 4);
        Eq(end.IgniteBroke ? 1 : 0, 1, "达到阈值");
        Eq(body.Break.SkipPending ? 1 : 0, 1, "行动后破防，跳过下一次");
        Eq(body.Break.ProtectedThroughRound, 5, "从第4轮保护到第5轮");

        FakeBody resist = new FakeBody { Fire = TypeRelation.Resistant, Break = new BreakGauge(1) };
        resist.Statuses.Apply(StatusId.Ignite, 1, 1);
        end = StatusSettlement.SettleOwnTurn(resist, 1);
        Eq(resist.Hp, 98, "抗性点燃扣 2");
        Eq(end.IgniteBroke ? 1 : 0, 0, "抗性不是弱点，不加破防");
    }

    static void TestWeaponSkills()
    {
        Eq(WeaponSkillTable.All.Length, 12, "12 把武器技能");
        TypeRelation[] normal = NormalRelations();

        WeaponSkillDef sword = WeaponSkillTable.Find("sword");
        Eq(sword.Cost, 2, "剑消耗 2");
        TypeRelation[] resist = NormalRelations();
        resist[0] = TypeRelation.Resistant;
        Eq(SkillRules.SegmentDamage(sword.Spec.Segment, resist, sword.Spec.IgnoreResistance, 1000, false, false), 30, "剑无视穿刺抗性");
        Eq(SkillRules.SegmentDamage(sword.Spec.Segment, resist, false, 1000, false, false), 15, "抗性减半");
        TypeRelation[] immune = NormalRelations();
        immune[0] = TypeRelation.Immune;
        Eq(SkillRules.SegmentDamage(sword.Spec.Segment, immune, true, 1000, false, false), 0, "剑不穿透免疫");

        WeaponSkillDef greatsword = WeaponSkillTable.Find("greatsword");
        Eq(greatsword.Cost, 3, "大剑消耗 3");
        Eq(SkillRules.SegmentDamage(greatsword.Spec.Segment, normal, false, 1000, false, false), 36, "镇压 36");
        Eq((int)greatsword.Spec.Statuses[0].status, (int)StatusId.Weak, "镇压衰弱");
        Eq(greatsword.Spec.Statuses[0].stacks, 1, "镇压 1 层");

        WeaponSkillDef spear = WeaponSkillTable.Find("spear");
        TypeRelation[] weak = NormalRelations();
        weak[0] = TypeRelation.Weak;
        Eq(SkillRules.SegmentDamage(spear.Spec.Segment, normal, false, 1000, false, false), 22, "穿心 22");
        Eq(SkillRules.SegmentBreak(spear.Spec.Segment, weak, spear.Spec.ExtraBreakOnWeak, false), 2, "穿心弱点额外破防");
        Eq(SkillRules.SegmentBreak(spear.Spec.Segment, weak, spear.Spec.ExtraBreakOnWeak, true), 0, "保护期穿心不加");
        Eq(SkillRules.SegmentBreak(spear.Spec.Segment, normal, spear.Spec.ExtraBreakOnWeak, false), 0, "没打中弱点不加");

        WeaponSkillDef dual = WeaponSkillTable.Find("dual_swords");
        Eq(dual.Cost, 3, "双剑消耗 3");
        Eq(dual.Spec.SegmentCount, 4, "连刺 4 段");
        Eq(SkillRules.SegmentDamage(dual.Spec.Segment, normal, false, 1000, false, false), 5, "连刺每段 5");

        WeaponSkillDef axe = WeaponSkillTable.Find("axe");
        Eq((int)axe.Spec.Aim, (int)SkillAim.EachLiving, "顺劈打每个存活敌人");
        Eq(SkillRules.SegmentDamage(axe.Spec.Segment, normal, false, 1000, false, false), 12, "顺劈 12");

        WeaponSkillDef sickle = WeaponSkillTable.Find("sickle");
        Eq(sickle.Spec.SegmentCount, 2, "收割 2 段");
        Eq(SkillRules.SegmentDamage(sickle.Spec.Segment, normal, false, sickle.Spec.CoefficientMilli, false, false), 12, "已破防收割一段 12");
        Eq(SkillRules.SegmentDamage(sickle.Spec.Segment, normal, false, 1000, false, false), 9, "未破防收割一段 9");

        WeaponSkillDef hammer = WeaponSkillTable.Find("hammer");
        Eq(SkillRules.SegmentDamage(hammer.Spec.Segment, normal, false, 1000, false, false), 24, "破甲技能 24");
        Eq((int)hammer.Spec.Statuses[0].status, (int)StatusId.Sunder, "锤施加破甲");
        Eq(hammer.Spec.Statuses[0].stacks, 2, "锤破甲 2 层");

        WeaponSkillDef flail = WeaponSkillTable.Find("flail");
        Eq(flail.Spec.SegmentCount, 2, "缠击 2 段");
        Eq(flail.Spec.Statuses.Length, 1, "缠击整次只加一次状态");
        Eq(flail.Spec.Statuses[0].stacks, 1, "缠击衰弱 1 层");

        WeaponSkillDef torch = WeaponSkillTable.Find("torch");
        Eq((int)torch.Spec.Statuses[0].status, (int)StatusId.Ignite, "火把点燃");
        Eq(torch.Spec.Statuses[0].stacks, 2, "火把点燃 2 层");

        WeaponSkillDef pole = WeaponSkillTable.Find("poleaxe");
        Eq((int)pole.Spec.Aim, (int)SkillAim.PrimaryThenEach, "斧枪先主目标再全体");
        Eq(SkillRules.SegmentDamage(pole.Spec.Segment, normal, false, 1000, false, false), 8, "斧枪穿刺 8");
        Eq(SkillRules.SegmentDamage(pole.Spec.FollowSegment, normal, false, 1000, false, false), 6, "斧枪劈砍 6");

        WeaponSkillDef flame = WeaponSkillTable.Find("flame_tongue");
        Eq(SkillRules.SegmentDamage(flame.Spec.Segment, normal, false, 1000, false, false), 20, "焚心 12+8");
        Eq(flame.Spec.Statuses[0].onSelf ? 1 : 0, 1, "焚心坚定给自己");
        Eq((int)flame.Spec.Statuses[0].status, (int)StatusId.Steadfast, "焚心是坚定");
        TypeRelation[] fireResist = NormalRelations();
        fireResist[3] = TypeRelation.Resistant;
        Eq(SkillRules.SegmentDamage(flame.Spec.Segment, fireResist, false, 1000, false, false), 16, "焚心火焰抗性先减半");

        WeaponSkillDef heaven = WeaponSkillTable.Find("heaven_fire");
        Eq(heaven.Spec.Statuses.Length, 2, "烙刑两个状态");
        Eq((int)heaven.Spec.Statuses[0].status, (int)StatusId.Sunder, "烙刑破甲");
        Eq((int)heaven.Spec.Statuses[1].status, (int)StatusId.Ignite, "烙刑点燃");
        Eq(SkillRules.SegmentDamage(heaven.Spec.Segment, normal, false, 1000, true, true), 17, "烙刑再吃衰弱和破甲");

        Eq(SkillRules.CanPay(2, 2, false) ? 1 : 0, 1, "决心刚好够");
        Eq(SkillRules.CanPay(1, 2, false) ? 1 : 0, 0, "决心不够不扣");
        Eq(SkillRules.CanPay(4, 2, true) ? 1 : 0, 0, "疲惫不能放技能");
    }

    static void TestEnemyMoves()
    {
        Eq(EnemyAttackRules.UsesBasicDamage(EnemyMoveKind.Strike) ? 1 : 0, 1, "直接攻击进基础伤害");
        Eq(EnemyAttackRules.UsesBasicDamage(EnemyMoveKind.Gasp) ? 1 : 0, 0, "喘息不进伤害");
        Eq(EnemyAttackRules.UsesBasicDamage(EnemyMoveKind.Prepare) ? 1 : 0, 0, "准备不进伤害");
        Eq(EnemyAttackRules.AppliesRider(EnemyMoveKind.Strike, true, 1) ? 1 : 0, 1, "带毒攻击挂 1 层");
        Eq(EnemyAttackRules.AppliesRider(EnemyMoveKind.Strike, true, 0) ? 1 : 0, 0, "0 层不挂");
        Eq(EnemyAttackRules.AppliesRider(EnemyMoveKind.Gasp, true, 1) ? 1 : 0, 0, "喘息不挂状态");
        Eq(EnemyAttackRules.AppliesRider(EnemyMoveKind.Prepare, true, 1) ? 1 : 0, 0, "准备不挂状态");
    }

    static void TestSkillAssets()
    {
        string root = RepoRoot();
        foreach (WeaponSkillDef def in WeaponSkillTable.All)
        {
            string path = Path.Combine(root, "Assets", "Data", "Skills", "SK_" + def.WeaponId + ".asset");
            string text = File.ReadAllText(path);
            Eq(text.Contains("determinationCost: " + def.Cost) ? 1 : 0, 1, def.WeaponId + " 资源消耗");
            Eq(text.Contains("damage: " + def.Spec.Segment[0].Amount) ? 1 : 0, 1, def.WeaponId + " 资源伤害");
        }

        string poison = File.ReadAllText(Path.Combine(root, "Assets", "Data", "EnemyAttacks", "KBM02", "KBM02.asset"));
        Eq(poison.Contains("applyStatus: 1") ? 1 : 0, 1, "带毒攻击会挂状态");
        Eq(poison.Contains("statusStacks: 1") ? 1 : 0, 1, "带毒攻击 1 层");
        Eq(poison.Contains("statusOnHit: 3") ? 1 : 0, 1, "带毒攻击是中毒");
        string gasp = File.ReadAllText(Path.Combine(root, "Assets", "Data", "EnemyAttacks", "KBM02", "KBM03.asset"));
        Eq(gasp.Contains("moveKind: 2") ? 1 : 0, 1, "伏击喘息");
        string prepare = File.ReadAllText(Path.Combine(root, "Assets", "Data", "EnemyAttacks", "KBM03", "KBM02.asset"));
        Eq(prepare.Contains("moveKind: 1") ? 1 : 0, 1, "重击准备");
        string heavyGasp = File.ReadAllText(Path.Combine(root, "Assets", "Data", "EnemyAttacks", "KBM03", "KBM04.asset"));
        Eq(heavyGasp.Contains("moveKind: 2") ? 1 : 0, 1, "重击喘息");
    }

    static TypeRelation[] NormalRelations()
    {
        return new[]
        {
            TypeRelation.Normal,
            TypeRelation.Normal,
            TypeRelation.Normal,
            TypeRelation.Normal
        };
    }

    static string RepoRoot()
    {
        DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string skills = Path.Combine(dir.FullName, "Assets", "Data", "Skills");
            if (Directory.Exists(skills))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("找不到技能资源目录");
    }

    static void Eq(int actual, int expected, string name)
    {
        if (actual == expected)
            return;
        Console.Error.WriteLine(name + " expected " + expected + " actual " + actual);
        failures++;
    }

    sealed class FakeBody : IStatusBody
    {
        public int MaxHp = 100;
        public int Hp = 100;
        public TypeRelation Fire = TypeRelation.Normal;
        public StatusSet Statuses = new StatusSet();
        public BreakGauge Break = null;

        public bool IsDead => Hp <= 0;
        int IStatusBody.MaxHp => MaxHp;
        StatusSet IStatusBody.Statuses => Statuses;
        BreakGauge IStatusBody.Break => Break;
        TypeRelation IStatusBody.FireRelation => Fire;

        public int LoseRaw(int amount)
        {
            if (IsDead || amount <= 0)
                return 0;
            int before = Hp;
            Hp = Math.Max(0, Hp - amount);
            return before - Hp;
        }

        public int HealRaw(int amount)
        {
            if (IsDead || amount <= 0)
                return 0;
            int before = Hp;
            Hp = Math.Min(MaxHp, Hp + amount);
            return Hp - before;
        }
    }
}
