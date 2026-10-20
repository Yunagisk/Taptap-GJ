using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Installed only in the isolated validation project, never in the game's Assets.
public sealed class InventoryUiPlayChecks : MonoBehaviour
{
    int checks, errors;
    void Awake() { Application.logMessageReceived += Logged; Application.targetFrameRate = 60; }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string text, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception("FAILED: " + reason); }
    static void Set(object target, string field, object value)
    { target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value); }
    IEnumerator Start()
    {
        var run = Run();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception error)
            {
                Debug.LogException(error);
                File.WriteAllText("../native-failure.txt", error.ToString());
                UnityEditor.EditorApplication.Exit(1); yield break;
            }
            yield return next;
        }
        File.WriteAllText("../native-result.json", "{\"passed\":true,\"checks\":" + checks + ",\"runtimeErrors\":" + errors + "}");
        UnityEditor.EditorApplication.Exit(0);
    }
    IEnumerator Run()
    {
        var camera = new GameObject("Preview Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true; camera.orthographicSize = 5;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(49,77,121,255);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var root = new GameObject("Run").AddComponent<GameRoot>();
        root.state.inventory.weapons.Clear();
        foreach (var weapon in WeaponTable.All) if (weapon.id != "flame_tongue") root.state.inventory.weapons.Add(weapon.id);
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600,900);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/EquipmentPanel.prefab");
        var instance = Instantiate(prefab, canvas.transform);
        var owner = instance.GetComponent<EquipmentPanelView>();
        var layout = owner.inventoryLayout;
        owner.Open();
        yield return new WaitForSecondsRealtime(1.1f);
        Check(owner.parchment.CanInteract && owner.IsOpen, "parchment opens normally");
        Check(layout.categorySlots.Length == 4 && owner.slotButtons.Length == 0, "four category cards replace the old equipment bar");
        Check(instance.GetComponentsInChildren<Transform>(true).All(x => x.name != "OwnedTitle" && x.name != "SlotsTitle"), "old equipment captions removed");
        Check(layout.categorySlots[0].card.title.text == "剑" && layout.categorySlots[0].card.icon.sprite != null, "equipped category displays real weapon art");
        Check(layout.categorySlots.Skip(1).All(x => x.card.title.text == "未装备"), "empty category cards remain visible");
        Check(layout.categorySlots.All(x => x.newDot.gameObject.activeSelf), "acquisitions mark every unread category");
        Check(instance.GetComponentsInChildren<TMP_Text>(true).All(x => x.fontSharedMaterial.shader.isSupported), "font shaders compile and render");
        Capture("inventory-category-cards.png");
        layout.categorySlots[0].card.button.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.25f);
        Check(layout.IsWeaponPickerOpen && layout.pickerVisibility.interactable, "clicking category opens the right picker");
        Check(layout.pickerTitle.text == "穿刺武器", "picker is titled with the selected category");
        var rows = layout.weaponContent.GetComponentsInChildren<InventoryEntryView>();
        Check(rows.Length == 3 && rows.All(x => x.name != "Weapon_axe" && x.name != "Weapon_torch"
            && x.name != "Weapon_greatsword" && x.name != "Weapon_dual_swords"), "pierce picker excludes slash greatsword and retired dual swords");
        var parent = (RectTransform)layout.weaponPicker.parent;
        Check(Fits(layout.weaponPicker,parent), "picker stays inside parchment bounds");
        Check(!layout.categorySlots[0].newDot.gameObject.activeSelf && layout.categorySlots[1].newDot.gameObject.activeSelf,
            "viewing one category only clears its notification");
        Capture("inventory-picker-pierce.png");
        rows.Single(x => x.name == "Weapon_spear").button.onClick.Invoke();
        Check(root.state.inventory.equippedWeaponIds[0] == "spear" && root.state.player.currentWeaponId == "spear", "selecting candidate equips it and preserves the active category");
        Check(!layout.IsWeaponPickerOpen && root.state.inventory.weapons.Count == 10, "selection closes picker without deleting inventory");
        owner.Close(); yield return new WaitForSecondsRealtime(.8f); owner.Open(); yield return new WaitForSecondsRealtime(1);
        Check(!layout.categorySlots[0].newDot.gameObject.activeSelf, "cleared notification survives closing and reopening");
        root.state.inventory.weapons.Add("flame_tongue"); owner.Refresh();
        Check(layout.categorySlots[0].newDot.gameObject.activeSelf, "new purchase relights its category notification");
        layout.categorySlots[1].card.button.onClick.Invoke(); yield return new WaitForSecondsRealtime(.25f);
        layout.weaponContent.GetComponentsInChildren<InventoryEntryView>().Single(x => x.name == "Weapon_greatsword").button.onClick.Invoke();
        Check(root.state.inventory.equippedWeaponIds[1] == "greatsword" && root.state.player.currentWeaponId == "spear", "greatsword equips in the slash category without replacing the current pierce weapon");
        layout.categorySlots[1].card.button.onClick.Invoke(); yield return new WaitForSecondsRealtime(.25f);
        layout.weaponContent.GetComponentsInChildren<InventoryEntryView>().Single(x => x.name == "Weapon_axe").button.onClick.Invoke();
        Check(root.state.inventory.equippedWeaponIds[1] == "axe" && root.state.player.currentWeaponId == "spear", "other category equipment is independent of the current weapon");
        owner.selectWeaponButton.onClick.Invoke();
        Check(root.state.player.currentWeaponId == "axe", "set-current button still works");
        layout.categorySlots[3].card.button.onClick.Invoke(); yield return new WaitForSecondsRealtime(.25f);
        Check(layout.weaponContent.GetComponentsInChildren<InventoryEntryView>().Length == 1, "fire category contains torch, without duplicating mixed weapons");
        Check(Fits(layout.weaponPicker,parent), "rightmost category picker also fits");
        Capture("inventory-picker-fire.png");
        layout.closePickerButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.25f);
        Check(!layout.IsWeaponPickerOpen && !layout.weaponPicker.gameObject.activeSelf, "close button finishes closing the picker");
        root.state.inventory.weapons.RemoveAll(id => id == "hammer" || id == "flail" || id == "heaven_fire");
        owner.Refresh(); layout.categorySlots[2].card.button.onClick.Invoke(); yield return new WaitForSecondsRealtime(.25f);
        Check(layout.emptyWeapons.gameObject.activeSelf && layout.weaponContent.GetComponentsInChildren<InventoryEntryView>().Length == 0, "empty category presents an empty-state message");
        owner.Close(); yield return new WaitForSecondsRealtime(.8f);
        Destroy(instance); Destroy(canvas); yield return null;

        // Bind the actual HUD to a small isolated battle; intent reads never execute attacks.
        var player = new GameObject("Player").AddComponent<PlayerCombatant>();
        var enemy = new GameObject("Enemy").AddComponent<EnemyCombatant>();
        var action = ScriptableObject.CreateInstance<EnemyAttackData>();
        Set(action,"damage",18); Set(action,"attackCount",2);
        Set(enemy,"attackDatas",new List<EnemyAttackData>{action});
        var flow = new GameObject("Flow").AddComponent<BattleFlowController>(); flow.enabled=false;
        Set(flow,"player",player); Set(flow,"enemies",new List<EnemyCombatant>{enemy});
        var round = new GameObject("Round",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        Set(flow,"roundText",round); flow.Start();
        var hudPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CombatHUD.prefab");
        var hud = Instantiate(hudPrefab).GetComponent<BattleHudView>(); hud.Bind(flow,null,null);
        yield return null;
        var tag=hud.Root.Find("WorldTag_1/Intent");
        Check(tag != null, "bound enemy creates its world intent tag");
        Check(tag.Find("DamageType") != null,"compact intent includes a damage type icon");
        Check(tag.Find("Amount").GetComponent<TMP_Text>().text=="18×2", "damage and hit count remain visible");
        var rowView = hud.EnemyRows.Single();
        Check(!rowView.IntentExpanded, "intent details begin collapsed");
        Set(action,"applyStatus",true); Set(action,"statusOnHit",StatusId.Ignite); Set(action,"statusStacks",2);
        rowView.SetIntentHovered(true);
        Check(rowView.IntentExpanded && tag.Find("Details/SpecialEffect").GetComponent<TMP_Text>().text.Contains("点燃"), "hover reveals the status rider in intent details");
        Check(tag.Find("Amount").GetComponent<TMP_Text>().text=="18×2", "special effect does not alter displayed direct damage");
        Capture("battle-numeric-intent.png");
        enemy.Break.TryAdd(100,1);
        foreach(var row in hud.GetComponentsInChildren<BattleHudEnemyView>()) row.Refresh();
        Check(!tag.gameObject.activeSelf && hud.Root.Find("WorldTag_1/BreakDizzy").gameObject.activeSelf,
            "break replaces the attack intent with its dizzy indicator");
        yield return null;
        Check(errors==0,"native interaction produced no runtime errors");
    }
    static bool Fits(RectTransform rect, RectTransform parent)
    {
        // Scroll content extends below the viewport; only the clipped panel bounds matter.
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        foreach(var world in corners)
        {
            var point=parent.InverseTransformPoint(world);
            if(point.x<parent.rect.xMin-.1f || point.x>parent.rect.xMax+.1f
                || point.y<parent.rect.yMin-.1f || point.y>parent.rect.yMax+.1f) return false;
        }
        return true;
    }
    void Capture(string name)
    {
        var camera=Camera.main;
        var canvases=FindObjectsOfType<Canvas>().Where(x=>x.isRootCanvas).ToArray();
        var modes=canvases.Select(x=>x.renderMode).ToArray();
        var cameras=canvases.Select(x=>x.worldCamera).ToArray();
        var previous=RenderTexture.active;
        var previousTarget=camera.targetTexture;
        var render=new RenderTexture(1600,900,24); render.Create();
        var image=new Texture2D(1600,900,TextureFormat.RGBA32,false);
        try
        {
            foreach(var canvas in canvases)
            { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1; }
            camera.targetTexture=render; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active=render;
            image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
            File.WriteAllBytes("../"+name,image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous; camera.targetTexture=previousTarget;
            for(int i=0;i<canvases.Length;i++) { canvases[i].renderMode=modes[i]; canvases[i].worldCamera=cameras[i]; }
            Destroy(image);render.Release();Destroy(render);
        }
    }
}
