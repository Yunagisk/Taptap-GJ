using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

internal static partial class Program
{
    record SideEffect(string Id, int Choice, int Outcome = 0, int Time = 0, int Sin = 0, int Heal = 0,
        int Damage = 0, int Resolve = 0, int Remnant = 0, string Item = null, bool Cleanse = false,
        int Hit = 0, int Attack = 0, int Camp = 0, string Weapon = null, int Weight = 100);

    static readonly SideEffect[] SideEffects = {
        new("side01",0,Resolve:1), new("side01",1),
        new("side02",0,Damage:2), new("side02",1,Resolve:1,Weight:70), new("side02",1,1,Resolve:-1,Weight:30),
        new("side03",0,Hit:15), new("side03",1,Sin:-3,Damage:4),
        new("side04",0,Item:"holy_water",Weight:50), new("side04",0,1,Heal:8,Cleanse:true,Weight:50), new("side04",1),
        new("side05",0), new("side05",1,Resolve:1),
        new("side06",0,Sin:-4), new("side06",1,Sin:5,Attack:15),
        new("side07",0,Attack:10), new("side07",1),
        new("side08",0), new("side08",1,Resolve:-1),
        new("side09",0,Sin:-5), new("side09",1),
        new("side10",0,Time:1,Heal:8), new("side10",1),
        new("side11",0), new("side11",1,Remnant:1),
        new("side13",0,Time:1,Heal:10,Camp:20), new("side13",1),
        new("side14",0,Sin:6,Attack:20), new("side14",1,Sin:-3,Resolve:1),
        new("side15",0,Time:2,Heal:20), new("side15",1,Resolve:1),
        new("side16",0,Time:1,Damage:6,Weapon:"heaven_fire"), new("side16",1),
        new("side17",0,Time:1,Weapon:"flame_tongue"), new("side17",1)
    };

    static WildernessEventCatalog ProductionCatalog() => JsonSerializer.Deserialize<WildernessEventCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "WildernessEvents.json")), Json);

    static void CheckProductionSidequests()
    {
        int before = checks;
        var source = ProductionCatalog();
        WildernessEventRules.ValidateCatalog(source);
        var active = source.templates.Where(t => WildernessEventRules.IsGenerationTemplate(source,t.id)).ToList();
        Check(source.templates.Count == 26 && active.Count == 16, "sixteen active stories plus ten retained legacy definitions");
        Check(active.Sum(t => t.choices.Count) == 32 && active.Sum(t => t.choices.Sum(c => c.outcomes.Count)) == 34,
            "production stories contain thirty-two choices and thirty-four outcomes");
        Check(!active.Any(t => t.id == "side12"), "document numbering gap is preserved");
        Check(active.All(t => t.choices.Any(c => c.outcomes.Any(WildernessEventRules.HasActualEffect))),
            "every production story has at least one actual reward or penalty");
        Check(active.SelectMany(t => t.choices).Any(c => c.timeCost == 0 && c.outcomes.All(o => !WildernessEventRules.HasActualEffect(o))),
            "neutral choices remain available");
        foreach (var want in SideEffects)
        {
            var template = source.templates.Single(t => t.id == want.Id);
            var choice = template.choices[want.Choice]; var outcome = choice.outcomes[want.Outcome];
            string key = want.Id + "/" + want.Choice + "/" + want.Outcome;
            Check(choice.timeCost == want.Time && outcome.weight == want.Weight && outcome.sinDelta == want.Sin
                && outcome.healAmount == want.Heal && outcome.damageAmount == want.Damage && outcome.resolveDelta == want.Resolve
                && outcome.remnantDelta == want.Remnant && outcome.itemId == want.Item && outcome.cleanseDebuffs == want.Cleanse
                && outcome.itemCount == (want.Item == null ? 0 : 1) && outcome.directHitReductionPercent == want.Hit
                && outcome.attackBonusPercent == want.Attack && outcome.campRecoveryBonusPercent == want.Camp
                && outcome.weaponId == want.Weapon && !outcome.encounter && !outcome.repairBridge, key + " independently specified effects");
            Check(!string.IsNullOrWhiteSpace(template.description) && !string.IsNullOrWhiteSpace(outcome.narrative), key + " complete story and response");
            var (level,node) = Case(source, template.title); Force(source,level,node,want.Choice,want.Outcome); Enter(level,node);
            var resources = Resources(level.seed); resources.inventory.items.Clear();
            resources.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Poison,2));
            resources.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Haste,1));
            string equipment = Save(resources.inventory.equippedWeaponIds); string current = resources.player.currentWeaponId;
            var result = WildernessEventRules.TryChoose(level,source,resources,Layout(),want.Choice);
            Check(result.success && result.message.Contains(outcome.narrative) && !result.encounterPending, key + " settles original prose");
            Check(resources.player.hp == 50 + want.Heal - want.Damage && resources.player.resolve == 1 + want.Resolve
                && resources.world.sinRemnant == 4 + want.Remnant && resources.world.time == want.Time
                && resources.map.sinRollCount == want.Time && resources.world.sin == 40 + 3*want.Time + want.Sin,
                key + " real resources and outdoor time");
            Check(resources.player.nextDirectHitReductionPercent == Math.Max(7,want.Hit)
                && resources.player.nextAttackBonusPercent == Math.Max(9,want.Attack)
                && resources.player.nextCampRecoveryBonusPercent == want.Camp, key + " one-use bonuses persist");
            Check(ItemRules.Count(resources.inventory,"holy_water") == (want.Item == null ? 0 : 1), key + " actual consumable inventory");
            Check(resources.player.ordinaryStatuses.Any(s => s.id == StatusId.Poison) != want.Cleanse
                && resources.player.ordinaryStatuses.Any(s => s.id == StatusId.Haste), key + " cleansing preserves positive statuses");
            Check(resources.inventory.weapons.Count == (want.Weapon == null ? 1 : 2)
                && (want.Weapon == null || resources.inventory.weapons.Contains(want.Weapon)), key + " actual weapon reward");
            Check(equipment == Save(resources.inventory.equippedWeaponIds) && current == resources.player.currentWeaponId,
                key + " receiving a weapon does not replace the loadout");
            string snapshot = Snapshot(level,resources);
            Check(!WildernessEventRules.TryChoose(level,source,resources,Layout(),want.Choice).success
                && Snapshot(level,resources) == snapshot, key + " repeat callback never duplicates effects");
            var restoredLevel = Copy(level); var restoredResources = Copy(resources);
            Check(!WildernessEventRules.TryChoose(restoredLevel,source,restoredResources,Layout(),want.Choice).success
                && Snapshot(restoredLevel,restoredResources) == snapshot, key + " save reload retains settled effects");
        }
        CheckSidequestBoundaries(source);
        CheckSidequestGeneration(source);
        CheckSidequestMapFlow(source);
        Console.WriteLine($"Production sidequests: {checks-before} assertions; 16 stories / 32 choices / 34 outcomes.");
    }

    static (GameState State, WildernessChoiceResult Result) SideBoundary(WildernessEventCatalog source,
        string id, int choice, int outcome, Action<GameState> prepare)
    {
        var definition = source.templates.Single(t => t.id == id);
        var (level,node) = Case(source,definition.title); Force(source,level,node,choice,outcome); Enter(level,node);
        var state = Resources(level.seed); state.inventory.items.Clear(); prepare(state);
        var result = WildernessEventRules.TryChoose(level,source,state,Layout(),choice);
        Check(result.success, "boundary settles " + id); return (state,result);
    }

    static void CheckSidequestBoundaries(WildernessEventCatalog source)
    {
        foreach (string id in new[]{"side03","side16"})
        {
            var test = SideBoundary(source,id,id=="side03"?1:0,0,s=>s.player.hp=1);
            Check(test.State.player.hp == 1 && test.Result.message.Contains("未再扣除生命"), "roadside damage cannot bypass revival " + id);
        }
        var heal = SideBoundary(source,"side15",0,0,s=>s.player.hp=99);
        Check(heal.State.player.hp==100 && heal.Result.message.Contains("恢复 1 生命"), "healing receipt reports actual capped recovery");
        heal = SideBoundary(source,"side15",0,0,s=>s.player.hp=100);
        Check(heal.State.player.hp==100 && heal.Result.message.Contains("生命已满") && heal.State.world.time==2,
            "full health still pays the chosen rest time");
        var resolve = SideBoundary(source,"side01",0,0,s=>s.player.resolve=s.player.maxResolve);
        Check(resolve.State.player.resolve==resolve.State.player.maxResolve && resolve.Result.message.Contains("决心已满"), "resolve reward caps");
        resolve = SideBoundary(source,"side08",1,0,s=>s.player.resolve=0);
        Check(resolve.State.player.resolve==0 && resolve.Result.message.Contains("决心已为 0"), "resolve penalty floors");
        var coin = SideBoundary(source,"side11",1,0,s=>s.world.sinRemnant=int.MaxValue);
        Check(coin.State.world.sinRemnant==int.MaxValue && coin.Result.message.Contains("罪骸已达上限"), "currency cannot overflow");
        var item = SideBoundary(source,"side04",0,0,s=>s.inventory.items.Add(new ItemStack("holy_water",1)));
        Check(ItemRules.Count(item.State.inventory,"holy_water")==1 && item.Result.message.Contains("未领取"), "capped item does not fabricate a reward");
        var buff = SideBoundary(source,"side03",0,0,s=>s.player.nextDirectHitReductionPercent=30);
        Check(buff.State.player.nextDirectHitReductionPercent==30, "weaker blessing never replaces stronger reduction");
        foreach (var pair in new[]{("side16","heaven_fire"),("side17","flame_tongue")})
        {
            var owned = SideBoundary(source,pair.Item1,0,0,s=>s.inventory.weapons.Add(pair.Item2));
            Check(owned.State.inventory.weapons.Count(id=>id==pair.Item2)==1 && owned.Result.message.Contains("已拥有"), "old owned quest weapon not duplicated");
            var full = SideBoundary(source,pair.Item1,0,0,s=> { for(int i=0;i<11;i++)s.inventory.weapons.Add("legacy-"+i); });
            Check(full.State.inventory.weapons.Count==12 && !full.State.inventory.weapons.Contains(pair.Item2)
                && full.Result.message.Contains("背包已满"), "weapon capacity has an honest receipt");
        }
        var bad = Copy(source); bad.templates.Single(t=>t.id=="side16").choices[0].outcomes[0].weaponId="invalid";
        Reject(()=>WildernessEventRules.ValidateCatalog(bad), "invalid weapon rewards rejected before generation");
        var fixture = Case(source,"不肯熄灭的炉子"); Enter(fixture.Level,fixture.Node); var resources = Resources();
        string snapshot = Snapshot(fixture.Level,resources);
        Check(!WildernessEventRules.TryChoose(fixture.Level,bad,resources,Layout(),0).success
            && Snapshot(fixture.Level,resources)==snapshot, "invalid reward cannot spend time or resolve the event");
        resources.inventory=null; snapshot=Snapshot(fixture.Level,resources);
        Check(!WildernessEventRules.TryChoose(fixture.Level,source,resources,Layout(),0).success
            && Snapshot(fixture.Level,resources)==snapshot, "missing inventory rejected atomically");
    }

    static void CheckSidequestGeneration(WildernessEventCatalog source)
    {
        foreach(string mapName in new[]{"CenteredThreeSinWhitebox.json","ThreeSinWhitebox.json"})
        {
            var layout = JsonSerializer.Deserialize<MapLayout>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,mapName)),Json);
            var catalog = MapWildernessRules.BuildCatalog(source,layout); var seen = new HashSet<string>();
            for(int seed=0;seed<300;seed++)
            {
                var first = WildernessEventRules.Generate(catalog,seed,1); var next = WildernessEventRules.Generate(catalog,seed,1);
                Check(first.nodes.Count==6 && first.nodes.Select(n=>n.templateId).Distinct().Count()==6, "six non-repeated stories " + mapName);
                Check(first.nodes.All(n=>catalog.generationTemplateIds.Contains(n.templateId)), "old placeholders excluded from new runs");
                Check(Save(first)==Save(next), "question stories and branch rolls deterministic");
                foreach(var node in first.nodes)seen.Add(node.templateId);
                WildernessEventRules.ValidateLevel(Copy(first),catalog);
            }
            Check(seen.SetEquals(source.generationTemplateIds), "all sixteen including both weapon quests are reachable " + mapName);
            var legacy = WildernessEventRules.Generate(MapWildernessRules.BuildCatalog(Catalog(),layout),9,1);
            WildernessEventRules.ValidateLevel(legacy,catalog);
            Check(legacy.nodes.All(n=>n.templateId.StartsWith("w")), "previous generated save still validates with production content");
        }
        Check(WildernessEventRules.Generate(source,10,1).nodes.Count==7, "standalone content preview still generates seven locations");
        var oldLevel = WildernessEventRules.Generate(Catalog(),17,1); var nodeToSettle=oldLevel.nodes[0]; Enter(oldLevel,nodeToSettle);
        Check(WildernessEventRules.TryChoose(oldLevel,source,Resources(),Layout(),0).success, "previous save can settle its original choice under the new catalog");
    }

    static void CheckSidequestMapFlow(WildernessEventCatalog source)
    {
        foreach(var pair in new[]{("side16","heaven_fire"),("side17","flame_tongue")})
        {
            MapFixture fixture=null; WildernessNodeState quest=null;
            for(int seed=0;seed<100 && quest==null;seed++)
            {
                var layout=Layout(); var state=Resources(seed); var map=new MapProgression(layout,state.map,state.world,seed);
                var catalog=MapWildernessRules.BuildCatalog(source,layout); MapWildernessRules.EnsureInitialized(state,layout,catalog);
                fixture=new MapFixture{Layout=layout,State=state,Map=map,Catalog=catalog};
                quest=fixture.Level.nodes.Find(n=>n.templateId==pair.Item1);
            }
            Check(quest!=null, "map fixture contains " + pair.Item1);
            var request=WalkTo(fixture,quest.id); Check(MapWildernessRules.TryOpen(fixture.State,fixture.Layout,fixture.Catalog,request), "actual movement opens weapon quest");
            int before=fixture.State.world.time;
            var reward=WildernessEventRules.TryChoose(fixture.Level,fixture.Catalog,fixture.State,fixture.Layout,0);
            Check(reward.success && fixture.State.inventory.weapons.Contains(pair.Item2) && fixture.State.world.time==before+1,
                "actual map request grants weapon and pays one action");
            WildernessEventRules.Leave(fixture.Level); Check(fixture.Map.FinishEvent(request.requestId)
                && fixture.State.world.time==before+1 && fixture.State.map.clearedNodes.Contains(quest.id), "closing quest clears node without another time charge");
            Check(EquipmentRules.TryEquip(fixture.State,1,pair.Item2) && EquipmentRules.TrySelectWeapon(fixture.State,pair.Item2), "earned weapon can be equipped and selected");
        }
    }
}
