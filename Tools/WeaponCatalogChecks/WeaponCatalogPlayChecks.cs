#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class WeaponCatalogPlayChecks : MonoBehaviour
{
    readonly List<string> checks = new List<string>();
    readonly List<string> errors = new List<string>();
    readonly string output = Path.GetFullPath("..");
    [Serializable] sealed class Result { public bool passed; public string[] checks, errors; }
    void Awake() { Application.logMessageReceived += Logged; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }
    void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks.Add(description);
    }
    IEnumerator Start()
    {
        var work = Exercise();
        while (true)
        {
            object next;
            try { if (!work.MoveNext()) break; next = work.Current; }
            catch (Exception error) { errors.Add(error.ToString()); break; }
            yield return next;
        }
        File.WriteAllText(Path.Combine(output, "catalogue-result.json"), JsonUtility.ToJson(new Result {
            passed = errors.Count == 0, checks = checks.ToArray(), errors = errors.ToArray() }, true));
        EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
    }
    IEnumerator Exercise()
    {
        yield return null; yield return null;
        var opening = FindObjectOfType<MainMenuOpening>();
        if (opening != null) opening.SkipOpening();
        var menu = FindObjectOfType<GameFlowController>();
        var template = menu.template;
        var layout = template.Load();
        var library = AssetDatabase.LoadAssetAtPath<WeaponLibrary>("Assets/Data/Weapons/WeaponLibrary.asset");
        Check(library.weapons.Length == 11 && library.weapons.All(weapon => weapon != null
            && WeaponTable.Find(weapon.WeaponId) != null) && library.Find("dual_swords") == null,
            "installed library contains exactly eleven active weapons and no missing entries");
        var greatsword = library.Find("greatsword");
        Check(greatsword.DamageComponents.All(part => part.damageType == PlayerDamageType.Slash)
            && greatsword.Skill.Stages.All(stage => stage.damageComponents.All(part => part.damageType == PlayerDamageType.Slash))
            && greatsword.Skill.StatusEffects.All(effect => effect.requiredDamageType == PlayerDamageType.Slash),
            "native greatsword basic, skill and status rider all use slash");
        Check(menu.weaponOptions.Length == 3
            && menu.weaponPage.GetComponentsInChildren<Transform>(true).All(child => child.name != "Choose_dual_swords"),
            "installed menu has three starting choices and no retired button");
        menu.weaponPage.SetActive(true);
        foreach (string id in NewRunFactory.StartingWeapons)
        {
            var button = menu.weaponPage.GetComponentsInChildren<Button>(true).Single(child => child.name == "Choose_" + id);
            button.onClick.Invoke();
            Check(menu.SelectedWeapon == id, "installed starting choice selects " + id);
        }
        menu.SelectWeapon("dual_swords");
        Check(menu.SelectedWeapon == "hammer", "a stale new-game callback cannot select dual swords");

        var state = NewRunFactory.Create(layout, "sword", 35791);
        state.story.useVillageIntroduction = true;
        state.story.guideCompleted = state.story.guideEquipmentReady = state.story.guideOutsideReady = true;
        state.story.stage = MainStoryStage.TutorialCompleted; state.story.slothHookSeen = true;
        state.world.sinRemnant = 100;
        var legacy = DayStartSaveStore.Copy(state);
        legacy.inventory.weapons.Clear(); legacy.inventory.weapons.Add("dual_swords");
        legacy.inventory.seenWeaponIds.Add("dual_swords");
        legacy.inventory.equippedWeaponIds[0] = "dual_swords";
        legacy.player.currentWeaponId = "dual_swords";
        legacy.shop.availableWeaponIds.Add("dual_swords");
        var slot = new DayStartSaveStore(Path.Combine(output, "saves", Guid.NewGuid().ToString("N") + ".json"));
        Check(slot.TrySave(legacy, layout, out var saveError), "legacy save fixture writes: " + saveError);
        Check(slot.TryLoad(layout, out var restored, out var loadError), "legacy save loads: " + loadError);
        Check(!restored.inventory.weapons.Contains("dual_swords") && !restored.inventory.seenWeaponIds.Contains("dual_swords")
            && !restored.inventory.equippedWeaponIds.Contains("dual_swords") && !restored.shop.availableWeaponIds.Contains("dual_swords")
            && restored.player.currentWeaponId == "sword", "reading a legacy slot removes dual swords and equips its safe replacement");
        Check(restored.world.sinRemnant == 100 && restored.world.time == 0 && restored.world.sin == state.world.sin,
            "legacy migration preserves the clock, sin and currency");
        GameRoot.I.state = state; GameRoot.I.SaveSlot = slot; GameRoot.I.villageMapTemplate = template;
        var loading = SceneManager.LoadSceneAsync("Village");
        while (!loading.isDone) yield return null;
        yield return null; yield return null;
        var village = FindObjectOfType<VillageScreen>();
        village.OpenShop(); yield return null;
        var shop = village.Shop;
        Check(shop.offers.Count(offer => !offer.consumable) == 9
            && shop.offers.All(offer => offer.id != "dual_swords"), "installed shop has nine ordinary weapon offers and no dual swords");
        var offered = new HashSet<string>();
        for (int page = 0; page < 2; page++)
        {
            foreach (var card in shop.cards.Where(card => card.gameObject.activeInHierarchy))
            {
                card.button.onClick.Invoke();
                offered.Add(shop.SelectedId);
                Check(shop.SelectedId != "dual_swords" && !shop.itemName.text.Contains("双剑"), "shop card selects an active weapon");
            }
            shop.ChangePage(1);
        }
        Check(offered.SetEquals(state.shop.availableWeaponIds), "every stocked weapon has a visible shop card");
        shop.ChangePage(-1);
        bool bought = false;
        for (int page = 0; page < 2 && !bought; page++)
        {
            for (int index = 0; index < shop.cards.Length; index++)
            {
                if (!shop.cards[index].gameObject.activeInHierarchy) continue;
                shop.cards[index].button.onClick.Invoke();
                if (shop.SelectedId != "greatsword") continue;
                Check(shop.description.text.Contains("挥砍") && !shop.description.text.Contains("突刺"), "greatsword shop copy describes slash");
                shop.buyButton.onClick.Invoke(); bought = true; break;
            }
            if (!bought) shop.ChangePage(1);
        }
        Check(bought && state.inventory.weapons.Contains("greatsword") && state.world.sinRemnant == 92,
            "actual shop controls purchase greatsword once at its unchanged price");
        string before = JsonUtility.ToJson(state);
        Check(!ShopRules.TryBuy(state, "dual_swords", 0).ok && !ShopRules.TryBuy(state, "dual_swords", 8).ok
            && JsonUtility.ToJson(state) == before, "stale purchase requests cannot grant dual swords or spend resources");
        village.ClosePanels();
        state.inventory.weapons.Add("dual_swords"); state.inventory.equippedWeaponIds[0] = "dual_swords";
        state.player.currentWeaponId = "dual_swords";
        village.OpenBag(); yield return new WaitForSecondsRealtime(1);
        var panel = village.panelBag.GetComponent<EquipmentPanelView>();
        Check(!state.inventory.weapons.Contains("dual_swords") && panel.inventoryLayout.weaponArt.Length == 11,
            "opening the installed backpack removes legacy dual swords and uses eleven active icons");
        panel.inventoryLayout.categorySlots[1].card.button.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.25f);
        var rows = panel.inventoryLayout.weaponContent.GetComponentsInChildren<InventoryEntryView>(false);
        Check(rows.Any(row => row.name == "Weapon_greatsword") && rows.All(row => row.name != "Weapon_dual_swords"),
            "greatsword appears in the slash picker and dual swords have no row");
    }
}
#endif
