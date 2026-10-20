#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FullRouteDriver
{
    [Serializable] class Fight { public string node, weapon, enemies; public int hpBefore,hpAfter,rounds; }
    [Serializable] class Result { public bool finished; public int seed,afterGuideHp,finalHp,worldTime; public List<Fight> fights; }
    static readonly List<Fight> fights=new List<Fight>();
    static IEnumerator Wait(Func<bool> predicate,string name)
    {
        float end=Time.realtimeSinceStartup+60;
        while(!predicate()&&Time.realtimeSinceStartup<end) yield return null;
        if(!predicate()) throw new Exception("Full run timed out: "+name);
    }
    static IEnumerator Dialogue()
    {
        yield return null;
        var view=UnityEngine.Object.FindObjectsOfType<GuideDialogueView>().FirstOrDefault(v=>v.IsOpen);
        if(view==null) yield break;
        var manager=view.Manager;
        for(int i=0;i<300&&view!=null&&view.IsOpen&&manager.IsPlaying;i++)
        {
            if(manager.ui.CanChoose) manager.ui.choiceParent.GetComponentsInChildren<Button>(false)[0].onClick.Invoke();
            else manager.Next();
            yield return null;
        }
        if(view!=null&&view.IsOpen&&manager.IsPlaying) throw new Exception("Full run dialogue did not terminate");
    }
    static IEnumerator MapReady()
    {
        yield return Wait(()=>SceneManager.GetActiveScene().name=="MainMap","map scene");
        yield return null; yield return null;
        yield return Wait(()=>!MapSceneTransition.IsBusy&&UnityEngine.Object.FindObjectOfType<WorldMapSceneView>()!=null
            &&!UnityEngine.Object.FindObjectOfType<WorldMapSceneView>().IsRevealing,"map animation");
    }
    public static IEnumerator Run()
    {
        var root=GameRoot.I;
        int afterGuideHp=root.state.player.hp;
        var village=UnityEngine.Object.FindObjectOfType<VillageScreen>();
        if(village==null||!village.CanUseFacilities) throw new Exception("Full run guide did not release services");
        // Use the real inn before departure. No test writes to health, enemy HP, clocks or cleared nodes.
        village.ClosePanels(); village.OpenInn(); yield return null;
        village.ConfirmInn();
        yield return Wait(()=>!village.IsResting,"normal inn recovery");
        village.ClosePanels();
        village.LeaveToMap(); yield return MapReady();
        var map=UnityEngine.Object.FindObjectOfType<WorldMapController>();
        var layout=map.Progression.Layout;
        string goal=root.state.story.slothNodeId;
        var previous=new Dictionary<string,string>{{root.state.map.currentNodeId,null}};
        var queue=new Queue<string>();queue.Enqueue(root.state.map.currentNodeId);
        while(queue.Count>0&&!previous.ContainsKey(goal))
        {
            string id=queue.Dequeue();
            foreach(string next in map.Progression.GetNeighbors(id))
            {
                var node=map.Progression.GetNode(next);
                if(previous.ContainsKey(next)||node.kind==MapNodeKind.Boss&&next!=goal)continue;
                previous[next]=id;queue.Enqueue(next);
            }
        }
        var route=new List<string>();
        for(string at=goal;at!=root.state.map.currentNodeId;at=previous[at])route.Add(at);
        route.Reverse();
        foreach(string target in route)
        {
            yield return MapReady();map=UnityEngine.Object.FindObjectOfType<WorldMapController>();
            var node=map.Progression.GetNode(target);
            bool spent=root.state.map.clearedNodes.Contains(target);
            map.MoveToNode(target);
            if(map.IsFastTravelConfirmationOpen)map.ConfirmFastTravel();
            if(spent){yield return Wait(()=>root.state.map.currentNodeId==target&&!MapSceneTransition.IsBusy,"known route");continue;}
            if(node.kind==MapNodeKind.Camp)
            {
                yield return Wait(()=>map.IsCampOpen&&map.CampUI.CanRest&&map.CampUI.restButton.IsInteractable(),"camp input ready");
                yield return new WaitForSecondsRealtime(.25f);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("../full-run/camp-before-rest.png"));
                var camp=map.CampUI;
                string campDetails="CanRest="+camp.CanRest+" buttonEnabled="+camp.restButton.isActiveAndEnabled
                    +" buttonInteractable="+camp.restButton.IsInteractable()+" entering="+map.IsEnteringNode+" busy="+MapSceneTransition.IsBusy
                    +" listening="+typeof(MapCampUI).GetField("listening",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(camp);
                camp.restButton.onClick.Invoke();
                yield return null;
                campDetails+="; after UI click open="+map.IsCampOpen;
                if(map.IsCampOpen) { map.RestAtCamp();campDetails+="; after controller action open="+map.IsCampOpen; }
                File.WriteAllText(Path.GetFullPath("../full-run/camp-input.txt"),campDetails);
                yield return Wait(()=>!map.IsCampOpen,"real camp recovery");
                continue;
            }
            if(node.kind==MapNodeKind.Event)
            {
                yield return Wait(()=>map.IsWildernessEventOpen,"event entry");
                map.ChooseWildernessEvent(0);
                if(root.state.map.wildernessEvents.nodes.Any(n=>n.encounterPending))map.ResolveWildernessEncounter(true);
                map.CloseWildernessEvent();continue;
            }
            if(node.kind==MapNodeKind.Boss)
            {
                yield return Wait(()=>UnityEngine.Object.FindObjectsOfType<GuideDialogueView>().Any(v=>v.IsOpen),"boss confrontation");
                yield return Dialogue();
            }
            yield return FightAt(target,node.kind==MapNodeKind.Boss);
        }
        yield return MapReady();map=UnityEngine.Object.FindObjectOfType<WorldMapController>();
        map.MoveToNode(layout.villageNodeId);
        if(map.IsFastTravelConfirmationOpen)map.ConfirmFastTravel();
        yield return Wait(()=>!MapSceneTransition.IsBusy,"home route");
        if(SceneManager.GetActiveScene().name=="MainMap")
        {
            map.MoveToNode(layout.villageNodeId);
            if(map.IsVillageEntryConfirmationOpen)map.ConfirmVillageEntry();
        }
        yield return Wait(()=>SceneManager.GetActiveScene().name=="Village","homecoming");
        yield return Wait(()=>UnityEngine.Object.FindObjectsOfType<GuideDialogueView>().Any(v=>v.IsOpen),"epilogue");
        yield return Dialogue();
        yield return Wait(()=>SlothChapterRules.IsFinished(root.state),"chapter ending");
        File.WriteAllText(Path.GetFullPath("../full-run/balance.json"),JsonUtility.ToJson(new Result {
            finished=true,seed=root.state.map.randomSeed,afterGuideHp=afterGuideHp,finalHp=root.state.player.hp,worldTime=root.state.world.time,fights=fights},true));
    }
    static IEnumerator FightAt(string node,bool boss)
    {
        yield return Wait(()=>SceneManager.GetActiveScene().name=="CombatScene","combat scene");
        yield return null;yield return null;
        var flow=UnityEngine.Object.FindObjectOfType<BattleFlowController>();
        yield return Wait(()=>flow!=null&&flow.CurrentState==BattleState.PlayerTurn,"combat ready");
        var record=new Fight{node=node,hpBefore=flow.Player.CurrentHP,
            enemies=string.Join("+",flow.Enemies.Select(e=>e.DisplayName))};
        UnityEngine.Object.FindObjectOfType<BattleWeaponController>().SwitchWeapon(0);
        record.weapon=flow.Player.CurrentWeapon.WeaponId;
        for(int turns=0;turns<100&&flow.CurrentState!=BattleState.Victory;turns++)
        {
            if(flow.CurrentState==BattleState.Defeat)throw new Exception("Full run was defeated at "+node+" round "+flow.RoundNumber);
            var target=flow.Enemies.Where(e=>!e.IsDead).OrderBy(e=>e.Definition?.kind==SmallMonsterKind.Crow?0:e.Definition?.kind==SmallMonsterKind.Rat?1:2).First();
            bool heavy=flow.Enemies.Any(e=>!e.IsDead&&e.UpcomingAction!=null&&e.UpcomingAction.Kind==EnemyActionKind.Attack&&e.UpcomingAction.Damage>=10);
            // A simple player policy: block heavy attacks, otherwise use available skills or ordinary attacks.
            if(heavy)flow.PlayerDefend();
            else if(flow.CanUseWeaponSkill)flow.PlayerSkill();
            else flow.PlayerAttack();
            if(flow.CurrentState==BattleState.SelectingTarget)flow.AttackTarget(target);
            yield return Wait(()=>flow.CurrentState==BattleState.PlayerTurn||flow.CurrentState==BattleState.Victory||flow.CurrentState==BattleState.Defeat,"combat action");
        }
        if(flow.CurrentState!=BattleState.Victory)throw new Exception("Full run fight did not win: "+node);
        record.hpAfter=flow.Player.CurrentHP;record.rounds=flow.RoundNumber;fights.Add(record);
        File.WriteAllText(Path.GetFullPath("../full-run/partial-balance.json"),JsonUtility.ToJson(new Result {
            finished=false,seed=GameRoot.I.state.map.randomSeed,finalHp=flow.Player.CurrentHP,
            worldTime=GameRoot.I.state.world.time,fights=fights},true));
        yield return Wait(()=>!EnemyDefeatSpritePlayback.AnyPlayingIn(flow.gameObject.scene),"death animation");
        UnityEngine.Object.FindObjectOfType<BattleResultUI>().ReturnToWorldMap();
        if(boss)
        {
            yield return Wait(()=>UnityEngine.Object.FindObjectsOfType<GuideDialogueView>().Any(v=>v.IsOpen),"Theodore fate");
            yield return Dialogue();
        }
        yield return MapReady();
    }
}
#endif
