using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class VillageServicePlayChecks : MonoBehaviour
{
    readonly List<string> checks = new List<string>();
    readonly string output = Path.GetFullPath("..");
    int errors;
    VillageScreen screen;
    GameState state;
    void Awake() { Application.logMessageReceived += Logged; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string text, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    void Check(bool condition, string message)
    { if (!condition) throw new Exception("FAILED: " + message); checks.Add(message); }

    IEnumerator Start()
    {
        var work = Exercise();
        while (true)
        {
            object next;
            try { if (!work.MoveNext()) break; next = work.Current; }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());
                Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result {
            passed = true, checks = checks.ToArray(), runtimeErrors = errors },true));
        UnityEditor.EditorApplication.Exit(0);
    }
    [Serializable] sealed class Result { public bool passed; public string[] checks; public int runtimeErrors; }

    IEnumerator Exercise()
    {
        yield return null; yield return null;
        screen = FindObjectOfType<VillageScreen>(); state = GameRoot.I.state;
        Check(screen != null && screen.Shop != null && screen.Inn != null && screen.restTransition != null,
            "production Village scene binds both service prefabs and the overnight transition");
        Check(!screen.panelRoot.activeSelf && !screen.restTransition.gameObject.activeSelf,"services and blackout start closed");
        state.world.time = 1; state.world.sinRemnant = 4;
        state.player.hp = 56; state.player.lanternCharges = 1;
        state.inventory.weapons.Clear(); state.inventory.items.Clear(); ShopRules.StockNewRun(state.shop,null);
        screen.OpenShop(); yield return null; yield return null;
        var shop = screen.Shop;
        Check(shop.balance.text == "4" && shop.SelectedId == "sword", "shop shows the actual balance and selects its first offer");
        Check(shop.cards.All(card=>card.gameObject.activeSelf && card.artwork.texture != null),"six original weapon paintings are visible");
        Check(shop.offers.Count(offer=>!offer.consumable)==9 && shop.offers.Where(offer=>!offer.consumable)
            .All(offer=>WeaponTable.IsSoldInShop(offer.id)&&offer.description.EndsWith("。")&&!offer.description.Contains("\n")),"nine shop weapons have individual one-sentence descriptions");
        Check(!shop.GetComponentsInChildren<TMP_Text>(true).Any(text=>text.text.Contains("旅途所需")||text.text.Contains("购入后放入背包")),"removed subtitle and generic description are absent");
        Check(shop.artwork.uvRect.width < 1 && shop.artwork.uvRect.height < 1,"original artwork fills its slot without modifying its texture");
        Canvas.ForceUpdateCanvases();
        Check(shop.cards[0].frame.canvasRenderer.GetMesh().vertexCount>0
            &&shop.closeButton.GetComponentInChildren<VillageServiceIcon>().canvasRenderer.GetMesh().vertexCount>0,
            "ink borders and icon-only controls submit visible native geometry");
        Check(PointerTarget(shop.cards[0].button),"shop cards receive pointer input above the village");
        Capture("01-shop.png");
        shop.buyButton.onClick.Invoke(); shop.buyButton.onClick.Invoke();
        Check(state.world.sinRemnant==0 && state.inventory.weapons.Count(id=>id=="sword")==1
            && !state.shop.availableWeaponIds.Contains("sword"),"double confirmation purchases once and updates shared stock, balance and inventory");
        screen.ClosePanels(); screen.OpenShop(); yield return null;
        Check(shop.SelectedId=="sword" && !shop.buyButton.interactable && shop.buyLabel.text=="已售罄","reopening cannot repurchase the sold weapon");
        shop.SelectCard(1);
        Check(shop.SelectedId=="axe"&&!shop.buyButton.interactable&&shop.buyLabel.text=="罪骸不足","poor players can inspect a weapon without purchasing it");
        shop.nextButton.onClick.Invoke();
        Check(shop.Page==1&&shop.cards.Where(card=>card.gameObject.activeSelf).Any(card=>card.title.text=="斧枪")
            &&!shop.cards.Where(card=>card.gameObject.activeSelf).Any(card=>card.title.text=="天火"||card.title.text=="焰舌"),
            "second weapon page excludes both event-only weapons");
        state.world.sinRemnant=2; shop.itemsTab.onClick.Invoke(); shop.buyButton.onClick.Invoke();
        Check(ItemRules.Count(state.inventory,ItemTable.Bandage)==1&&state.world.sinRemnant==0
            && ItemRules.ShopStock(state,ItemTable.Bandage)==0,"consumable purchase uses the existing item rules");
        shop.Purchase(); Check(ItemRules.Count(state.inventory,ItemTable.Bandage)==1,"consumable carry limit and duplicate purchase protection hold");
        screen.ClosePanels(); int oldTime=state.world.time;
        screen.OpenInn(); yield return null;
        Check(screen.Inn.timeRange.text.Contains("第2日")&&screen.Inn.health.text=="56 → 100","inn confirmation previews next dawn and actual recovery");
        Check(PointerTarget(screen.Inn.confirmButton),"inn confirmation receives pointer input");
        Capture("02-inn-confirm.png");
        screen.Inn.cancelButton.onClick.Invoke(); screen.ConfirmInn();
        Check(state.world.time==oldTime&&state.player.hp==56&&state.player.lanternCharges==1,"cancel and stale callbacks do not heal or advance time");

        int sin=state.world.sin; string node=state.map.currentNodeId;
        screen.OpenInn(); screen.ConfirmInn(); screen.ConfirmInn();
        Check(screen.IsResting&&state.world.time==oldTime&&state.player.hp==56,"rest starts once and settles only after fading to black");
        screen.OpenShop(); screen.OpenBag(); screen.OpenTavern(); screen.LeaveToMap(); screen.ClosePanels();
        Check(screen.IsResting&&screen.panelInn.activeInHierarchy&&!screen.panelShop.activeInHierarchy&&!screen.IsLoading,
            "other facilities, escape-close and scene exit are blocked during rest");
        // Unity registers an enabled nested Canvas before the following input frame.
        yield return null;
        Canvas.ForceUpdateCanvases();
        var hits=new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=new Vector2(Screen.width*.2f,Screen.height*.5f)},hits);
        if(hits.Count==0||hits[0].gameObject!=screen.restTransition.blackout.gameObject)
            Debug.Log("Blackout raycast: "+string.Join(", ",hits.Select(hit=>hit.gameObject.name+"/"+hit.sortingOrder))
                +" gateDepth="+screen.restTransition.blackout.depth+" culled="+screen.restTransition.blackout.canvasRenderer.cull
                +" sorted="+screen.restTransition.GetComponent<Canvas>().sortingOrder
                +" bounds="+screen.restTransition.blackout.rectTransform.rect+" active="+screen.restTransition.gameObject.activeInHierarchy);
        Check(hits.Count>0&&hits[0].gameObject==screen.restTransition.blackout.gameObject,"blackout blocks the following input frame while the fade begins");
        float deadline=Time.unscaledTime+5;
        while(!screen.restTransition.HasSettled&&Time.unscaledTime<deadline)yield return null;
        Check(screen.restTransition.HasSettled&&screen.restTransition.blackout.color.a>.99f&&screen.restTransition.dialVisibility.alpha>.99f,
            "time is settled while the screen is fully black and the map dial is visible");
        yield return new WaitForSecondsRealtime(.2f);
        Capture("03-black-time-dial.png");
        deadline=Time.unscaledTime+6;
        while(screen.IsResting&&Time.unscaledTime<deadline)yield return null;
        Check(!screen.IsResting&&!screen.restTransition.gameObject.activeSelf&&!screen.panelRoot.activeSelf,
            "fade returns to the village and releases the input gate");
        Check(state.world.time==3&&state.player.hp==100&&state.player.lanternCharges==2,"afternoon rest reaches next morning and restores both resources");
        Check(state.world.sin==sin&&state.map.currentNodeId==node&&state.map.checkpointTime==3,
            "shared settlement updates the dawn checkpoint without natural sin growth or map movement");
        Check(ItemRules.ShopStock(state,ItemTable.Bandage)==1,"daily stock replenishes at the new dawn");
        Check(Mathf.Abs(Mathf.DeltaAngle(screen.restTransition.dial.Rotation,(float)MapTimeDialMotion.Rotation(3)))<.01f,
            "dial ends at the native map morning angle");
        var original = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MapPrefabs/WorldMapHUD.prefab")
            .GetComponentInChildren<MapTimeDialView>(true);
        var native=(RectTransform)original.transform; var actual=(RectTransform)screen.restTransition.dialVisibility.transform;
        Check(actual.anchorMin==native.anchorMin&&actual.anchorMax==native.anchorMax&&actual.anchoredPosition==native.anchoredPosition
            &&actual.sizeDelta==native.sizeDelta&&screen.restTransition.dial.artwork==original.dial.artwork,
            "blackout reuses the exact native map dial artwork, size and bottom-center anchors");
        var ambience=FindObjectOfType<VillageAmbienceView>();
        Check(ambience.CurrentPeriod==0&&!ambience.IsTransitioning,"morning background is ready before the veil clears");
        Capture("04-morning-village.png");
        screen.OpenInn(); yield return null;
        Check(!screen.Inn.confirmButton.interactable&&screen.Inn.confirmLabel.text=="无需休息","full resources disable an unnecessary stay");
        screen.ConfirmInn(); Check(state.world.time==3,"full resources cannot spend another day through a stale callback");
        screen.ClosePanels();

        screen.restTransition.fadeSeconds=.02f;screen.restTransition.dialFadeSeconds=.02f;screen.restTransition.holdSeconds=0;
        Time.timeScale=0;
        foreach(int departure in new[]{6,11})
        {
            state.world.time=departure;state.player.hp=10;state.player.lanternCharges=0;
            screen.OpenInn();screen.ConfirmInn(); deadline=Time.unscaledTime+6;
            while(screen.IsResting&&Time.unscaledTime<deadline)yield return null;
            Check(!screen.IsResting&&state.world.time==departure+VillageInnRules.TimeCost(departure)
                &&state.player.hp==100&&state.player.lanternCharges==2,
                "rest from period "+departure%3+" advances correctly with gameplay paused");
        }
        Time.timeScale=1;
        state.world.sinRemnant=12; screen.OpenShop();shop.Refresh();shop.SetCategory(true);yield return null;
        Check(shop.buyLabel.text=="携带已满"&&!shop.buyButton.interactable,"restocking does not bypass consumable carry limits");
        screen.ClosePanels();
        state.story.useVillageIntroduction=true;state.story.stage=MainStoryStage.ArrivedVillage;
        screen.OpenInn();screen.OpenShop();Check(!screen.panelRoot.activeSelf,"mandatory introduction still blocks service entry");
        state.story.useVillageIntroduction=false;
        screen.OpenBag();yield return null;Check(screen.panelBag.activeInHierarchy,"existing equipment panel remains connected");
        screen.ClosePanels();yield return new WaitForSecondsRealtime(1);
        screen.OpenTavern();yield return null;Check(screen.panelEvent.activeInHierarchy,"existing tavern panel remains connected");
        screen.ClosePanels();
        Check(errors==0,"no native runtime errors throughout service interaction and rendering");
    }

    bool PointerTarget(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var hits=new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)},hits);
        return hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button;
    }

    void Capture(string file)
    {
        var canvas=screen.UICanvas.rootCanvas;
        var camera=Camera.main;
        var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var target=new RenderTexture(1920,1080,24);
        var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
        try
        {
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=active;camera.targetTexture=previous;
            canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;
            Destroy(target);Destroy(image);
        }
    }
}
