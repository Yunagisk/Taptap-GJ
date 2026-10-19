using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Copied into an isolated Unity project by the validation harness, not shipped in Assets.
public class ItemPlayModeChecks : MonoBehaviour
{
    int checks;
    int runtimeErrors;
    void Awake() { Application.logMessageReceived += TrackErrors; }
    void OnDestroy() { Application.logMessageReceived -= TrackErrors; }
    void TrackErrors(string text, string trace, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) runtimeErrors++; }
    void Check(bool ok, string reason)
    { checks++; if (!ok) throw new Exception("FAILED: " + reason); }
    static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        var run = Checks();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception error)
            {
                Debug.LogException(error);
                File.WriteAllText("../native-failure.txt", error.ToString());
                UnityEditor.EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        File.WriteAllText("../native-result.json", "{\"passed\":true,\"checks\":" + checks + "}");
        Debug.Log("PASS: " + checks + " native item, UI and battle flow checks");
        UnityEditor.EditorApplication.Exit(0);
    }

    IEnumerator Checks()
    {
        yield return null;
        var flow = FindObjectOfType<BattleFlowController>();
        var hud = FindObjectOfType<BattleHudView>();
        Check(flow != null && hud != null && flow.CurrentState == BattleState.PlayerTurn, "battle and HUD start");
        Set(flow, "enemyActionDelay", 0f);
        foreach (var enemy in flow.Enemies)
            Set(enemy, "actionSelector", new CycleActionSelector(Array.Empty<EnemyAttackData>()));
        var state = flow.ItemState;
        var player = flow.Player;
        var target = flow.Enemies[0];
        var resistance = target.GetComponent<DamageResistance>();
        resistance.SetStageRules(new[] {
            new DamageResistance.DamageRule { damageType=PlayerDamageType.Pierce, damageReaction=DamageReaction.Weak },
            new DamageResistance.DamageRule { damageType=PlayerDamageType.Slash, damageReaction=DamageReaction.Normal },
            new DamageResistance.DamageRule { damageType=PlayerDamageType.Blunt, damageReaction=DamageReaction.Resistant },
            new DamageResistance.DamageRule { damageType=PlayerDamageType.Fire, damageReaction=DamageReaction.Immune }
        });
        player.LoseRaw(30);
        ItemRules.TryGrant(state, ItemTable.Bandage);
        hud.items.onClick.Invoke();
        var button = hud.popup.transform.Find("Panel/Use_bandage").GetComponent<Button>();
        Check(button.interactable, "battle bandage button enabled when wounded");
        int round = flow.RoundNumber;
        button.onClick.Invoke();
        Check(flow.CurrentState != BattleState.PlayerTurn && !hud.popup.activeSelf, "item button closes menu and locks action");
        Check(!flow.PlayerUseItem(ItemTable.Bandage).ok, "rapid second click rejected");
        for (int i = 0; i < 240 && flow.CurrentState != BattleState.PlayerTurn; i++) yield return null;
        Check(flow.CurrentState == BattleState.PlayerTurn && flow.RoundNumber == round + 1, "successful item advances exactly one round");
        Check(player.CurrentHP == player.MaxHP - 15 && state.player.hp == player.CurrentHP, "native healing and shared HP agree");
        Check(ItemRules.Count(state.inventory, ItemTable.Bandage) == 0, "native healing consumes once");

        player.HealRaw(100);
        ItemRules.TryGrant(state, ItemTable.Bandage);
        round = flow.RoundNumber;
        Check(!flow.PlayerUseItem(ItemTable.Bandage).ok && flow.RoundNumber == round
            && flow.CurrentState == BattleState.PlayerTurn, "invalid use preserves round and action");
        ItemRules.TryGrant(state, ItemTable.HolyWater);
        player.ApplyStatus(StatusId.Poison, 3); player.ApplyStatus(StatusId.Ignite, 2);
        player.ApplyStatus(StatusId.Haste, 2);
        Check(flow.PlayerUseItem(ItemTable.HolyWater).ok, "holy water accepted");
        for (int i = 0; i < 240 && flow.CurrentState != BattleState.PlayerTurn; i++) yield return null;
        Check(player.Statuses.DebuffCount == 0 && player.Statuses.Has(StatusId.Haste), "native cleansing preserves buffs");
        Check(state.player.ordinaryStatuses.Count == 1, "status snapshot follows native changes");

        int charges = state.player.lanternCharges;
        round = flow.RoundNumber;
        Check(flow.PlayerUseItem(ItemTable.Lantern).ok && flow.IsSelectingLantern, "lantern enters selection");
        Check(state.player.lanternCharges == charges, "selection does not spend charge");
        flow.CancelTargetSelection();
        Check(state.player.lanternCharges == charges && flow.RoundNumber == round
            && flow.CurrentState == BattleState.PlayerTurn, "cancel preserves charge and round");
        flow.PlayerUseItem(ItemTable.Lantern);
        hud.SelectEnemy(target, true);
        for (int i = 0; i < 240 && flow.CurrentState != BattleState.PlayerTurn; i++) yield return null;
        Check(state.player.lanternCharges == charges - 1 && resistance.IsWeaknessKnown(PlayerDamageType.Pierce), "HUD selection executes lantern");
        Check(!resistance.IsWeaknessKnown(PlayerDamageType.Slash) && !resistance.IsWeaknessKnown(PlayerDamageType.Blunt)
            && !resistance.IsWeaknessKnown(PlayerDamageType.Fire), "lantern does not disclose resistances or immunity");
        Check(!ItemAction.Validate(state, ItemTable.Lantern, player, target).ok, "fully revealed target rejected");
        resistance.SetStageRules(new[] { new DamageResistance.DamageRule {
            damageType=PlayerDamageType.Slash, damageReaction=DamageReaction.Weak } });
        Check(!resistance.IsWeaknessKnown(PlayerDamageType.Slash) && resistance.UnknownWeaknessCount == 1, "phase change requires fresh discovery");
        flow.PlayerUseItem(ItemTable.Lantern); flow.AttackTarget(target);
        for (int i = 0; i < 240 && flow.CurrentState != BattleState.PlayerTurn; i++) yield return null;
        Check(state.player.lanternCharges == charges - 2 && resistance.IsWeaknessKnown(PlayerDamageType.Slash), "lantern reveals current phase");

        hud.items.onClick.Invoke();
        var lanternButton = hud.popup.transform.Find("Panel/Use_lantern").GetComponent<Button>();
        Check(!lanternButton.interactable, "empty lantern is disabled in menu");
        Check(hud.popup.GetComponentsInChildren<Button>().Length == 4, "menu contains three items and close");
        Capture(hud.GetComponent<Canvas>(), "../combat-items.png");
        hud.closePopup.onClick.Invoke();

        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/EquipmentPanel.prefab");
        var bag = Instantiate(asset, hud.transform).GetComponent<EquipmentPanelView>();
        bag.Open();
        yield return new WaitForSeconds(.8f);
        bag.Refresh();
        Check(bag.inventoryLayout.useItemButton != null, "inventory use control is wired without manual scene edits");
        Check(!bag.inventoryLayout.useItemButton.interactable, "outdoor inventory cannot bypass combat action rules");
        flow.enabled = false;
        GameRoot.I.activeBattle = null;
        state.player.hp = 50;
        bag.Refresh();
        Check(bag.inventoryLayout.SelectedItemId == ItemTable.Bandage && bag.inventoryLayout.useItemButton.interactable,
            "outdoor inventory enables selected bandage");
        bag.inventoryLayout.useItemButton.onClick.Invoke();
        Check(state.player.hp == 65 && ItemRules.Count(state.inventory, ItemTable.Bandage) == 0,
            "inventory button actually uses bandage");
        Capture(hud.GetComponent<Canvas>(), "../inventory-items.png");

        state.world.sinRemnant = 20;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Village");
        yield return null;
        yield return null;
        var village = FindObjectOfType<VillageScreen>();
        Check(village != null && GameRoot.I.state == state, "village retains item state after battle scene unload");
        village.OpenShop();
        var buyBandage = village.panelShop.transform.Find("ShopScroll/Viewport/Content/BuyItem_bandage").GetComponent<Button>();
        Check(buyBandage.interactable, "village exposes a purchasable bandage");
        buyBandage.onClick.Invoke();
        Check(state.world.sinRemnant == 18 && ItemRules.Count(state.inventory, ItemTable.Bandage) == 1,
            "shop button grants item and deducts price");
        yield return null;
        village.OpenShop();
        yield return null;
        buyBandage = village.panelShop.transform.Find("ShopScroll/Viewport/Content/BuyItem_bandage").GetComponent<Button>();
        Check(!buyBandage.interactable && ItemRules.ShopStock(state, ItemTable.Bandage) == 0,
            "reopening village shop does not restock");
        var buyHoly = village.panelShop.transform.Find("ShopScroll/Viewport/Content/BuyItem_holy_water").GetComponent<Button>();
        buyHoly.onClick.Invoke();
        Check(state.world.sinRemnant == 12 && ItemRules.Count(state.inventory, ItemTable.HolyWater) == 1,
            "holy water can be bought through village UI");
        yield return null;
        Capture(village.panelShop.GetComponentInParent<Canvas>(), "../village-shop.png");
        state.player.hp = 50;
        village.OpenBag();
        yield return new WaitForSeconds(.8f);
        var villageBag = village.panelBag.GetComponent<EquipmentPanelView>();
        villageBag.Refresh();
        Check(villageBag.inventoryLayout.SelectedItemId == ItemTable.Bandage
            && villageBag.inventoryLayout.useItemButton.interactable, "village bag selects usable bandage");
        villageBag.inventoryLayout.useItemButton.onClick.Invoke();
        Check(state.player.hp == 65 && ItemRules.Count(state.inventory, ItemTable.Bandage) == 0,
            "village bag uses purchased bandage");
        state.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Poison, 3));
        state.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Weak, 2));
        state.player.ordinaryStatuses.Add(new StatusSnapshot(StatusId.Steadfast, 1));
        villageBag.Refresh();
        Check(villageBag.inventoryLayout.SelectedItemId == ItemTable.HolyWater
            && villageBag.inventoryLayout.useItemButton.interactable, "village bag selects holy water after bandage is spent");
        villageBag.inventoryLayout.useItemButton.onClick.Invoke();
        Check(ItemRules.Count(state.inventory, ItemTable.HolyWater) == 0
            && state.player.ordinaryStatuses.TrueForAll(x => !StatusRules.IsDebuff(x.id)), "village holy water purges ordinary debuffs");
        Check(state.player.ordinaryStatuses.Exists(x => x.id == StatusId.Steadfast), "outdoor holy water preserves buffs");
        village.ClosePanels();
        state.player.hp = state.player.maxHp; state.player.lanternCharges = 0;
        village.OpenInn(); village.ConfirmInn();
        Check(state.player.lanternCharges == state.player.maxLanternCharges, "inn replenishes lantern even at full HP");
        Check(runtimeErrors == 0, "no Unity runtime errors");
    }

    static void Capture(Canvas canvas, string path)
    {
        var camera = Camera.main;
        if (camera == null) camera = new GameObject("Preview Camera").AddComponent<Camera>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var rt = new RenderTexture(1600, 900, 24);
        camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases(); camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        Destroy(image); rt.Release(); Destroy(rt);
    }
}
