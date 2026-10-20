using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Native integration checks for status application, stacking, settlement,
// cleansing and persistence, plus the actual equipment window's resource pips.
public static class PlayerStatusNativeChecks
{
    static readonly StatusId[] Sequence = { StatusId.Poison, StatusId.Ignite, StatusId.Weak,
        StatusId.Sunder, StatusId.Exhaust, StatusId.Confuse, StatusId.Regen, StatusId.Haste, StatusId.Steadfast };
    static int phase = -1;
    static double readyAt;
    static Color healthyBar;
    static readonly Dictionary<SpriteRenderer, Color> artwork = new Dictionary<SpriteRenderer, Color>();
    static EquipmentPanelView equipment;

    public static bool Step(BattleFlowController flow, BattleHudView hud, Action<string> capture, Action<bool, string> check)
    {
        var player = flow.Player;
        var view = hud.GetComponent<BattlePlayerStatusView>();
        var effects = player.GetComponent<PlayerStatusEffects>();
        if (phase == -1)
        {
            check(new PlayerState().maxResolve == 3 && player.MaxDetermination == 3,
                "normal run and combat share the three-point default determination cap");
            check(hud.resolve.Count(pip => pip.gameObject.activeSelf) == 3,
                "combat HUD hides the unsupported fourth determination slot");
            var cap = typeof(PlayerCombatant).GetField("maxDetermination", BindingFlags.Instance | BindingFlags.NonPublic);
            cap.SetValue(player, 4); hud.SendMessage("Refresh");
            check(hud.resolve.All(pip => pip.gameObject.activeSelf), "combat HUD can still present an explicit four-point cap");
            cap.SetValue(player, 3); hud.SendMessage("Refresh");
            healthyBar = hud.playerHealth.color;
            foreach (var sprite in player.GetComponentsInChildren<SpriteRenderer>()) artwork[sprite] = sprite.color;
            Time.timeScale = 0;
            capture("status-clean.png");
            phase = 0;
            player.ApplyStatus(Sequence[phase], 2);
            readyAt = Time.realtimeSinceStartupAsDouble + .35;
            return false;
        }
        if (Time.realtimeSinceStartupAsDouble < readyAt) return false;
        if (phase < Sequence.Length)
        {
            var id = Sequence[phase];
            view.Refresh();
            check(effects.HasVisibleEffects, id + " has a visible native effect around the character");
            var mesh = player.transform.Find("PlayerStatusParticles").GetComponent<MeshFilter>().sharedMesh;
            check(mesh.vertexCount > 20, id + " produces actual particle geometry");
            var badge = hud.transform.Find("PlayerStatus/PlayerStatusMarkers/" + id);
            check(badge != null && badge.gameObject.activeSelf
                && badge.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("×2"), id + " marker displays its stack count");
            check(artwork.All(pair => pair.Key != null && pair.Key.color == pair.Value),
                id + " preserves the original sprite colors without a color filter");
            check(hud.playerHealth.color != healthyBar, id + " is also reflected in the health bar");
            if (id == StatusId.Poison) check(hud.playerHealth.color.g > hud.playerHealth.color.r * 1.2f,
                "poison changes the health fill to green while preserving its numeric readout");
            capture("status-" + id.ToString().ToLowerInvariant() + ".png");
            if (id == StatusId.Poison)
            {
                int before = player.CurrentHP;
                StatusSettlement.SettleOwnTurn(new CombatantStatusHost(player), flow.RoundNumber);
                hud.SendMessage("Refresh");
                check(player.CurrentHP == before - 2 && hud.healthText.text == (before - 2) + " / " + player.MaxHP,
                    "real poison settlement updates the health value and green fill together");
                check(badge.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("×1"),
                    "poison marker immediately updates after a stack is consumed");
                player.HealRaw(2);
            }
            player.Statuses.ClearOrdinary();
            phase++;
            if (phase < Sequence.Length) player.ApplyStatus(Sequence[phase], 2);
            else
            {
                player.ApplyStatus(StatusId.Poison, 2); player.ApplyStatus(StatusId.Ignite, 1);
                player.ApplyStatus(StatusId.Sunder, 1); player.ApplyStatus(StatusId.Confuse, 1);
            }
            readyAt = Time.realtimeSinceStartupAsDouble + .35;
            return false;
        }
        if (phase == 9)
        {
            var markers = hud.transform.Find("PlayerStatus/PlayerStatusMarkers");
            check(markers.Cast<Transform>().Count(badge => badge.gameObject.activeSelf) == 4,
                "several simultaneous conditions retain independent status markers");
            check(hud.playerHealth.color.g > hud.playerHealth.color.r * 1.2f,
                "poison remains legible in the health bar when combined with other statuses");
            capture("status-multiple.png");
            check(player.Statuses.PurgeAllDebuffs() == 4, "real cleansing removes all four negative conditions");
            phase = 10; readyAt = Time.realtimeSinceStartupAsDouble + .4;
            return false;
        }
        if (phase == 10)
        {
            check(!effects.HasVisibleEffects && hud.playerHealth.color == healthyBar,
                "cleansing removes particles and exactly restores the normal health color");
            check(!hud.transform.Find("PlayerStatus/PlayerStatusMarkers").Cast<Transform>().Any(badge => badge.gameObject.activeSelf),
                "cleansed status markers disappear without leaving empty slots");
            capture("status-cleansed.png");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/EquipmentPanel.prefab");
            equipment = UnityEngine.Object.Instantiate(prefab, hud.Root).GetComponent<EquipmentPanelView>();
            equipment.Open();
            phase = 11; readyAt = Time.realtimeSinceStartupAsDouble + 1.1;
            return false;
        }
        if (phase == 11)
        {
            var layout = equipment.inventoryLayout;
            equipment.Refresh();
            check(layout.resolveReadout.text == "1 / 3" && layout.resolvePips.Take(3).All(pip => pip.gameObject.activeSelf)
                && !layout.resolvePips[3].gameObject.activeSelf,
                "actual equipment window displays exactly three determination diamonds and matching denominator");
            capture("inventory-resolve-three.png");
            GameRoot.I.state.player.maxResolve = 4; GameRoot.I.state.player.resolve = 4; equipment.Refresh();
            check(layout.resolveReadout.text == "4 / 4" && layout.resolvePips.All(pip => pip.gameObject.activeSelf && pip.alpha == 1)
                && layout.resolveFills.All(fill => fill.enabled), "explicit four-point run displays four full diamonds in the equipment window");
            capture("inventory-resolve-four.png");
            GameRoot.I.state.player.maxResolve = 3; GameRoot.I.state.player.resolve = 1;
            player.ApplyStatus(StatusId.Poison, 2); equipment.Refresh();
            check(layout.healthFill.color.g > layout.healthFill.color.r * 1.2f,
                "persisted poison also colors the equipment window's health bar green");
            capture("inventory-poison.png");
            player.Statuses.ClearOrdinary();
            UnityEngine.Object.Destroy(equipment.gameObject);
            Time.timeScale = 1;
            phase = 12; readyAt = Time.realtimeSinceStartupAsDouble + .3;
            return false;
        }
        return true;
    }
}
