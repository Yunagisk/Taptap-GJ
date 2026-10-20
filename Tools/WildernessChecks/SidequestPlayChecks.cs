#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SidequestPlayChecks : MonoBehaviour
{
    readonly string output=Path.GetFullPath("../sidequests");
    readonly List<string> checks=new List<string>();
    int errors;
    WorldMapTemplate template;
    WildernessEventCatalog source;
    GameState state;
    WorldMapController map;
    MapWildernessEventUI ui;
    void Awake(){Directory.CreateDirectory(output);Application.logMessageReceived+=Logged;}
    void OnDestroy(){Application.logMessageReceived-=Logged;}
    void Logged(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    void Check(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);checks.Add(name);}
    IEnumerator Start()
    {
        var work=new Stack<IEnumerator>();work.Push(Exercise());
        while(work.Count>0)
        {
            object next;
            try{if(!work.Peek().MoveNext()){work.Pop();continue;}next=work.Peek().Current;}
            catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Debug.LogException(e);UnityEditor.EditorApplication.Exit(1);yield break;}
            if(next is IEnumerator nested)work.Push(nested);else yield return next;
        }
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result{passed=true,checks=checks.ToArray(),runtimeErrors=errors},true));
        UnityEditor.EditorApplication.Exit(0);
    }
    [Serializable]sealed class Result{public bool passed;public string[] checks;public int runtimeErrors;}
    IEnumerator Wait(Func<bool> condition,string message)
    {
        float deadline=Time.realtimeSinceStartup+40;
        while(!condition()&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(condition(),message);
    }
    IEnumerator Exercise()
    {
        yield return null;yield return null;
        if(GameRoot.I==null)new GameObject("GameRoot").AddComponent<GameRoot>();
        template=UnityEditor.AssetDatabase.LoadAssetAtPath<WorldMapTemplate>("Assets/Data/EnemyAttacks/Maps/CenteredThreeSinWhitebox.asset");
        source=JsonUtility.FromJson<WildernessEventCatalog>(UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Wilderness/WildernessEvents.json").text);
        Check(template!=null&&source.generationTemplateIds.Count==16,"native JSON deserializes all sixteen active events");

        yield return Open("side16");
        yield return CheckLeaveWarning();
        Check(ui.eventTitle.text=="不肯熄灭的炉子"&&!ui.choiceLabels[0].text.Contains("天火"),"heaven fire quest keeps the reward hidden until choosing");
        Capture("01-heaven-fire-choices.png"); ui.resultScroll.verticalNormalizedPosition=0;yield return null;
        Capture("02-heaven-fire-story.png");
        yield return Choose(0);
        Check(state.inventory.weapons.Contains("heaven_fire")&&state.player.hp==44&&state.world.time==2,"heaven fire grants actual weapon, costs six HP and one extra time");
        Check(ui.resultText.text.Contains("屋顶一定同意")&&ui.resultText.text.Contains("获得武器「天火」"),"heaven fire response and receipt appear together");
        Capture("03-heaven-fire-reward.png");
        yield return Close();yield return InspectEarnedWeapon("heaven_fire");

        yield return Open("side17");Capture("04-flame-tongue-choices.png");
        yield return Choose(0);
        Check(state.inventory.weapons.Contains("flame_tongue")&&state.player.hp==50&&state.world.time==2,"flame tongue grants the existing weapon without hidden damage");
        Check(ui.resultText.text.Contains("退休")&&ui.resultText.text.Contains("获得武器「焰舌」"),"flame tongue response has the actual inventory receipt");
        Capture("05-flame-tongue-reward.png");yield return Close();yield return InspectEarnedWeapon("flame_tongue");

        yield return Open("side17");yield return Choose(1);
        Check(!state.inventory.weapons.Contains("flame_tongue")&&state.world.time==1,"declining a weapon is neutral and charges only movement");yield return Close();
        yield return Open("side03");yield return Choose(0);
        Check(state.player.nextDirectHitReductionPercent==15,"statue prayer persists real next-hit blessing");yield return Close();
        yield return Open("side03");yield return Choose(1);
        Check(state.player.hp==46&&state.world.sin==40,"touching blue fire loses health and offsets three sin after movement");
        Capture("06-blue-fire-damage.png");yield return Close();
        yield return Open("side04",0,0);yield return Choose(0);
        Check(ItemRules.Count(state.inventory,"holy_water")==1&&ui.resultText.text.Contains("获得圣水"),"priest gives actual capped holy water");yield return Close();
        yield return Open("side04",0,0,s=>s.inventory.items.Add(new ItemStack("holy_water",1)));yield return Choose(0);
        Check(ItemRules.Count(state.inventory,"holy_water")==1&&ui.resultText.text.Contains("未领取"),"full inventory is explained instead of fabricating a second item");yield return Close();
        yield return Open("side04",0,1,s=>{s.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Poison,2));s.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Haste,1));});
        yield return Choose(0);Check(state.player.hp==58&&!state.player.ordinaryStatuses.Any(s=>s.id==StatusId.Poison)
            &&state.player.ordinaryStatuses.Any(s=>s.id==StatusId.Haste),"priest blessing heals and purges only debuffs");yield return Close();
        yield return Open("side08");yield return Choose(1);Check(state.player.resolve==0,"odor response reduces actual resolve");yield return Close();
        yield return Open("side15");yield return Choose(0);Check(state.player.hp==70&&state.world.time==3,"oversleeping heals twenty and costs two extra time");
        Capture("07-oversleep-result.png");yield return Close();
        yield return Open("side11",0,0,s=>{s.player.maxResolve=4;s.player.resolve=4;});
        Check(state.player.maxResolve==3&&state.player.resolve==3&&ui.resourcesText.text.Contains("决心 3/3"),"legacy four-point state migrates on the actual map and event UI");
        var inkHud=FindObjectOfType<MapPlayerInkHud>();inkHud.Refresh();
        Check(inkHud.resolvePips.Count(pip=>pip.gameObject.activeSelf)==3,"map HUD displays exactly three determination slots");
        Check(ui.resultText.text.Contains("低沉")&&ui.resultScroll.content.rect.height>ui.resultScroll.viewport.rect.height,"long original narrative is available in a scrollable viewport");
        float preferred=ui.resultScroll.content.rect.height;ui.resultScroll.verticalNormalizedPosition=0;yield return null;
        Check(ui.resultScroll.content.anchoredPosition.y>0&&preferred>0,"mouse/controller scroll can reach the narrative ending");
        yield return Choose(1);Check(state.world.sinRemnant==5,"direction-giving grants actual currency");yield return Close();
        yield return Open("side17");
        var abandoned=WildernessEventRules.FindNode(state.map.wildernessEvents,state.map.wildernessEvents.activeNodeId);
        int beforeLeave=state.world.time;Submit(ui.closeButton);yield return null;Submit(ui.LeaveWarning.ConfirmButton);yield return null;
        Check(!map.IsWildernessEventOpen&&abandoned.abandoned&&!abandoned.resolved&&state.world.time==beforeLeave
            &&state.map.clearedNodes.Contains(abandoned.id)&&string.IsNullOrEmpty(state.map.pendingRequestId),"confirmed leave abandons only this event without payment or rewards");
        string left=JsonUtility.ToJson(state);map.ConfirmLeaveWildernessEvent();
        Check(JsonUtility.ToJson(state)==left&&!WildernessEventRules.TryEnter(state.map.wildernessEvents,abandoned.id),"confirmed event cannot reopen or settle twice");
        state=NewRunFactory.Create(template.Load(),"sword",901);state.player.maxResolve=4;state.player.resolve=4;
        GameRoot.I.state=state;GameRoot.I.battleMapTemplate=template;GameRoot.I.battleRequestId=null;
        var battleLoad=SceneManager.LoadSceneAsync("CombatScene");while(!battleLoad.isDone)yield return null;
        yield return null;yield return null;
        var battle=FindObjectOfType<BattleFlowController>();var battleHud=FindObjectOfType<BattleHudView>();
        Check(battle.Player.MaxDetermination==3&&battle.Player.CurrentDetermination==3&&state.player.maxResolve==3
            &&state.player.resolve==3,"production combat migrates and persists a four-point save as three points");
        battleHud.SendMessage("Refresh");
        Check(battleHud.resolve.Count(pip=>pip.gameObject.activeSelf)==3&&battle.Player.AddDetermination(1)==0,"combat HUD and resource grants stop at three points");
        Capture("15-resolve-three-combat.png");
        Check(errors==0,"native map, dialogue, inventory and shop have no runtime errors");
    }

    IEnumerator CheckLeaveWarning()
    {
        Check(!ui.eventMeta.gameObject.activeSelf,"reading/abandonment text is absent from the event footer");
        string snapshot=JsonUtility.ToJson(state);
        ExecuteEvents.Execute(ui.closeButton.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);yield return null;
        Check(ui.LeaveWarning.IsHintVisible&&JsonUtility.ToJson(state)==snapshot,"hovering leave shows a warning without changing progress");
        Capture("13-leave-hover-warning.png");
        ExecuteEvents.Execute(ui.closeButton.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);yield return null;
        Check(!ui.LeaveWarning.IsHintVisible,"leaving the button hides its warning");
        Submit(ui.closeButton);yield return null;
        Check(ui.IsLeaveConfirmationOpen&&map.IsWildernessEventOpen&&JsonUtility.ToJson(state)==snapshot,"leave click asks for confirmation before abandoning anything");
        Check(EventSystem.current.currentSelectedGameObject==ui.LeaveWarning.CancelButton.gameObject
            &&PointerTarget(ui.LeaveWarning.CancelButton),"confirmation defaults to continue and receives actual pointer input");
        Capture("14-leave-confirmation.png");
        map.ChooseWildernessEvent(0);ui.choiceButtons[0].onClick.Invoke();
        Check(JsonUtility.ToJson(state)==snapshot,"confirmation blocks underlying choices and direct callbacks");
        FindObjectOfType<GameFlowController>().HandleBack();yield return null;
        Check(!ui.IsLeaveConfirmationOpen&&map.IsWildernessEventOpen&&JsonUtility.ToJson(state)==snapshot,"Escape cancels the warning and preserves the complete event state");
        Submit(ui.closeButton);yield return null;Submit(ui.LeaveWarning.CancelButton);yield return null;
        Check(!ui.IsLeaveConfirmationOpen&&map.IsWildernessEventOpen&&JsonUtility.ToJson(state)==snapshot,"continue-event button resumes the unchanged choices");
    }

    IEnumerator Open(string id,int choice=0,int outcome=0,Action<GameState> prepare=null)
    {
        var layout=template.Load();var catalog=MapWildernessRules.BuildCatalog(source,layout);
        WildernessLevelState level=null;WildernessNodeState question=null;int seed;
        for(seed=0;seed<1000;seed++)
        {
            level=WildernessEventRules.Generate(catalog,seed,1);
            question=level.nodes.Find(n=>n.templateId==id&&n.rolls[choice].outcomeIndex==outcome);
            if(question!=null)break;
        }
        Check(question!=null,"fixed seeded map contains "+id+" outcome "+outcome);
        state=NewRunFactory.Create(layout,"sword",seed);state.player.hp=50;state.inventory.items.Clear();
        string origin=layout.nodes.Find(n=>n.id==question.id).neighbors[0];
        state.map.currentNodeId=origin;level.currentNodeId=origin;state.map.wildernessEvents=level;
        if(!state.map.clearedNodes.Contains(origin))state.map.clearedNodes.Add(origin);
        if(!state.map.visitedNodes.Contains(origin))state.map.visitedNodes.Add(origin);
        if(!state.map.revealedNodes.Contains(origin))state.map.revealedNodes.Add(origin);
        if(!state.map.revealedNodes.Contains(question.id))state.map.revealedNodes.Add(question.id);
        prepare?.Invoke(state);GameRoot.I.state=state;GameRoot.I.pendingMapReveal=null;
        foreach(string boss in state.map.spawnedBossNodes)GameRoot.I.mapBossIntroductions.MarkShown(state.map,boss);
        GameRoot.I.villageMapTemplate=template;GameRoot.I.villageReturnScene="Assets/Scenes/MainMap.unity";
        var load=SceneManager.LoadSceneAsync("MainMap");while(!load.isDone)yield return null;
        yield return null;yield return null;map=FindObjectOfType<WorldMapController>();ui=map.wildernessEventUI;
        yield return Wait(()=>!MapSceneTransition.IsBusy&&!map.GetComponent<WorldMapSceneView>().IsRevealing,"map ready to enter "+id);
        map.MoveToNode(question.id);
        yield return Wait(()=>map.IsWildernessEventOpen&&!MapSceneTransition.IsBusy,"actual question entry opens "+id);
        yield return null;Canvas.ForceUpdateCanvases();
        Check(ui.resultScroll.gameObject.activeInHierarchy&&ui.resultText.text==source.templates.Find(t=>t.id==id).description,"full original prose appears in scroll view "+id);
        Check(EventSystem.current.currentSelectedGameObject==ui.choiceButtons[0].gameObject,"initial keyboard focus is first story option");
        var choices=source.templates.Find(t=>t.id==id).choices;
        for(int i=0;i<choices.Count;i++)
        {
            string hint=choices[i].timeCost>0?"额外消耗 "+choices[i].timeCost+" 单位时间":"直接结束事件";
            Check(ui.choiceLabels[i].text==choices[i].label+"\n"+hint,"choice displays only its authored line and time/finish hint");
        }
        Check(ui.resourcesText.text.Contains("决心")&&state.world.time==1,"status shows resolve and movement pays once");
        Check(PointerTarget(ui.choiceButtons[0]),"story choices receive real pointer input");
    }
    IEnumerator Choose(int index)
    {
        var slots=string.Join(",",state.inventory.equippedWeaponIds);string current=state.player.currentWeaponId;
        Submit(ui.choiceButtons[index]);yield return null;
        var level=state.map.wildernessEvents;var node=WildernessEventRules.FindNode(level,level.activeNodeId);
        Check(node.resolved&&node.chosenIndex==index&&!ui.choiceButtons[0].gameObject.activeSelf,"choice closes options and opens the result");
        Check(ui.resultScroll.gameObject.activeSelf&&ui.resultScroll.GetComponent<RectTransform>().sizeDelta.y==430,"result uses the expanded scroll viewport");
        Check(string.Join(",",state.inventory.equippedWeaponIds)==slots&&state.player.currentWeaponId==current,"reward preserves the player's chosen loadout");
        string snapshot=JsonUtility.ToJson(state);ui.choiceButtons[index].onClick.Invoke();map.ChooseWildernessEvent(index);
        Check(JsonUtility.ToJson(state)==snapshot,"repeated UI and stale controller callbacks cannot duplicate effects");
        Check(EventSystem.current.currentSelectedGameObject==ui.closeButton.gameObject,"result keyboard focus moves to continue");
    }
    IEnumerator Close()
    {
        int time=state.world.time;string id=state.map.currentNodeId;Submit(ui.closeButton);yield return null;
        Check(!map.IsWildernessEventOpen&&string.IsNullOrEmpty(state.map.pendingRequestId)
            &&state.map.clearedNodes.Contains(id)&&state.world.time==time,"continue clears the map request without extra payment");
        yield return Wait(()=>!MapSceneTransition.IsBusy&&!map.GetComponent<WorldMapSceneView>().IsRevealing,"completion reveal finishes");
    }
    IEnumerator InspectEarnedWeapon(string id)
    {
        // Enter the production village with an existing stock list containing the retired offers.
        state.world.sinRemnant=100;state.shop.availableWeaponIds.Add("heaven_fire");state.shop.availableWeaponIds.Add("flame_tongue");
        var load=SceneManager.LoadSceneAsync("Village");while(!load.isDone)yield return null;
        yield return null;yield return null;
        var village=FindObjectOfType<VillageScreen>();village.OpenShop();yield return null;
        var shop=village.Shop;shop.Refresh();
        Check(shop.offers.Count(o=>!o.consumable)==9&&!state.shop.availableWeaponIds.Contains("heaven_fire")
            &&!state.shop.availableWeaponIds.Contains("flame_tongue")&&!state.shop.availableWeaponIds.Contains("dual_swords"),
            "old stock migrates to nine current shop offers and excludes retired/event weapons");
        shop.ChangePage(1);Check(!shop.cards.Where(c=>c.gameObject.activeSelf).Any(c=>c.title.text=="天火"||c.title.text=="焰舌"),"neither event weapon appears on the actual shop shelf");
        Capture(id=="heaven_fire"?"08-shop-event-weapons-removed.png":"09-shop-remaining-weapons.png");
        Check(!ShopRules.TryBuy(state,"heaven_fire",0).ok&&!ShopRules.TryBuy(state,"flame_tongue",12).ok,"direct purchase cannot bypass exclusive event rewards");
        Check(village.weaponLibrary.Find(id)!=null,"weapon's existing gameplay asset remains in the library");
        village.ClosePanels();village.OpenBag();yield return new WaitForSecondsRealtime(.8f);
        var bag=village.panelBag.GetComponent<EquipmentPanelView>();
        state.player.maxResolve=4;state.player.resolve=4;bag.Refresh();
        Check(state.player.maxResolve==3&&state.player.resolve==3&&bag.inventoryLayout.resolveReadout.text=="3 / 3"
            &&bag.inventoryLayout.resolvePips.Count(pip=>pip.gameObject.activeSelf)==3,"backpack migrates old caps and displays exactly three filled determination slots");
        int column=id=="heaven_fire"?2:0;
        Check(InventoryEquipmentRules.HasUnread(state.inventory,column,weapon=>WeaponCategory(village.weaponLibrary,weapon)),"new reward triggers existing backpack notification");
        bag.SelectSlot(column);bag.SelectOwnedWeapon(id);bag.EquipSelected();bag.UseSelectedSlot();
        Check(state.inventory.equippedWeaponIds.Contains(id)&&state.player.currentWeaponId==id,"earned weapon equips and selects through the real backpack UI");
        Capture(id=="heaven_fire"?"10-heaven-fire-inventory.png":"11-flame-tongue-inventory.png");
        village.ClosePanels();yield return new WaitForSecondsRealtime(.8f);
    }
    static int WeaponCategory(WeaponLibrary library,string id)
    {
        var weapon=library.Find(id);return weapon==null||weapon.DamageComponents.Count==0?-1:(int)weapon.DamageComponents[0].damageType;
    }
    static void Submit(Button button)
    {EventSystem.current.SetSelectedGameObject(button.gameObject);ExecuteEvents.Execute(button.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);}
    static bool PointerTarget(Button button)
    {
        var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        return hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button;
    }
    void Capture(string name)
    {
        var camera=Camera.main;var canvases=FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();
        var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
        var target=new RenderTexture(1920,1080,24);target.Create();var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
        var texture=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
        try
        {
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name),texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
            camera.targetTexture=oldTarget;RenderTexture.active=previous;Destroy(texture);target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
