using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleWeaponWheel : MonoBehaviour
{
    [Serializable] public struct Art { public string id; public Sprite sprite; }
    public Image[] icons;
    public BattleHudGraphic[] selectionFrames;
    public TMP_Text caption;
    public Art[] weaponArt;
    public Button previousWeapon, nextWeapon;
    readonly List<int> slots = new List<int>(4);
    readonly List<int> nextSlots = new List<int>(4);
    readonly List<KeyValuePair<int, float>> drawOrder = new List<KeyValuePair<int, float>>(4);
    PlayerWeaponLoadout loadout;
    BattleWeaponController controller;
    float cursor, targetCursor, nextRefresh;
    int lastSelected = -1;
    bool locked;

    public void Bind(PlayerWeaponLoadout weapons, BattleWeaponController switching, BattleFlowController battle)
    {
        loadout = weapons; controller = switching;
        if (previousWeapon == null) previousWeapon = CreateStepButton("PreviousWeapon", -127, -90);
        if (nextWeapon == null) nextWeapon = CreateStepButton("NextWeapon", 127, 90);
        previousWeapon.onClick.RemoveListener(PreviousWeapon);
        nextWeapon.onClick.RemoveListener(NextWeapon);
        previousWeapon.onClick.AddListener(PreviousWeapon);
        nextWeapon.onClick.AddListener(NextWeapon);
        // The wheel itself is presentation; only its arrow buttons receive input.
        var surface = GetComponent<Graphic>();
        if (surface != null) surface.raycastTarget = false;
        RefreshSlots();
        RefreshButtons();
    }
    public void SetLocked(bool value) { locked = value; RefreshButtons(); }
    bool CanSwitch => !locked && controller != null && controller.CanSwitchWeapon;
    void Update()
    {
        if (Time.unscaledTime >= nextRefresh)
        { nextRefresh = Time.unscaledTime + .1f; RefreshSlots(); }
        int selected = slots.IndexOf(controller != null ? controller.CurrentIndex : -1);
        if (selected != lastSelected)
        {
            targetCursor = BattleWeaponWheelMath.NearestCursor(cursor, Mathf.Max(0, selected), slots.Count);
            lastSelected = selected;
        }
        cursor = Mathf.Lerp(cursor, targetCursor, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
        RefreshButtons();
        Present();
    }
    void RefreshSlots()
    {
        var weapons = loadout != null ? loadout.EquippedWeapons : null;
        nextSlots.Clear();
        for (int i = 0; weapons != null && i < weapons.Length && i < 4; i++) if (weapons[i] != null) nextSlots.Add(i);
        bool changed = nextSlots.Count != slots.Count;
        for (int i = 0; i < nextSlots.Count && !changed; i++) changed = nextSlots[i] != slots[i];
        if (changed)
        {
            slots.Clear(); slots.AddRange(nextSlots);
            lastSelected = slots.IndexOf(controller != null ? controller.CurrentIndex : -1);
            cursor = targetCursor = Mathf.Max(0, lastSelected);
        }
        for (int i = 0; icons != null && i < icons.Length; i++)
        {
            bool visible = i < slots.Count;
            icons[i].gameObject.SetActive(visible);
            if (selectionFrames != null && i < selectionFrames.Length) selectionFrames[i].gameObject.SetActive(visible);
            if (!visible) continue;
            string id = weapons[slots[i]].WeaponId;
            icons[i].sprite = FindArt(id); icons[i].enabled = icons[i].sprite != null;
        }
    }
    Sprite FindArt(string id)
    { if (weaponArt != null) foreach (var art in weaponArt) if (art.id == id) return art.sprite; return null; }
    void Present()
    {
        int centered = slots.IndexOf(controller != null ? controller.CurrentIndex : -1);
        // Draw back icons first; front icons stay on top even while crossing.
        drawOrder.Clear();
        for (int i = 0; icons != null && i < slots.Count && i < icons.Length; i++)
        {
            BattleWeaponWheelMath.Pose(i, cursor, slots.Count, out float x, out float y, out float depth);
            var rect = icons[i].rectTransform; rect.anchoredPosition = new Vector2(x, y);
            rect.localScale = Vector3.one * Mathf.Lerp(.52f, 1, depth);
            icons[i].color = new Color(1, 1, 1, Mathf.Lerp(.34f, CanSwitch ? 1 : .65f, depth));
            if (selectionFrames != null && i < selectionFrames.Length)
            {
                var frame = selectionFrames[i]; frame.rectTransform.anchoredPosition = rect.anchoredPosition;
                frame.rectTransform.localScale = rect.localScale;
                frame.color = new Color(1, 1, 1, i == centered ? 1 : 0); frame.Highlight(true);
            }
            drawOrder.Add(new KeyValuePair<int, float>(i, depth));
        }
        drawOrder.Sort((a, b) => a.Value.CompareTo(b.Value));
        foreach (var pair in drawOrder)
        {
            if (selectionFrames != null && pair.Key < selectionFrames.Length) selectionFrames[pair.Key].transform.SetAsLastSibling();
            icons[pair.Key].transform.SetAsLastSibling();
        }
        if (caption != null)
        {
            var weapons = loadout != null ? loadout.EquippedWeapons : null;
            caption.text = centered >= 0 && weapons != null ? WeaponTable.NameOf(weapons[slots[centered]].WeaponId) : "未装备武器";
        }
    }
    public void PreviousWeapon() => Step(-1);
    public void NextWeapon() => Step(1);

    void Step(int direction)
    {
        RefreshSlots();
        if (!CanSwitch || slots.Count < 2) return;
        int selected = slots.IndexOf(controller.CurrentIndex);
        int index = selected < 0 ? (direction > 0 ? 0 : slots.Count - 1)
            : BattleWeaponWheelMath.Wrap(selected + direction, slots.Count);
        controller.SwitchWeapon(slots[index]);
        // Retain the requested direction even when two weapons are opposite.
        if (controller.CurrentIndex == slots[index])
        {
            targetCursor = selected < 0 ? index : BattleWeaponWheelMath.NearestCursor(targetCursor, selected, slots.Count) + direction;
            lastSelected = index;
        }
        Present();
    }

    void RefreshButtons()
    {
        bool enabled = CanSwitch && slots.Count > 1;
        if (previousWeapon != null) previousWeapon.interactable = enabled;
        if (nextWeapon != null) nextWeapon.interactable = enabled;
    }

    Button CreateStepButton(string name, float x, float rotation)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic), typeof(Button));
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchoredPosition = new Vector2(x, 64);
        rect.sizeDelta = new Vector2(38, 34);
        var frame = root.GetComponent<BattleHudGraphic>();
        frame.stroke = 1.5f;
        var button = root.GetComponent<Button>();
        button.targetGraphic = frame;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.highlightedColor = new Color32(255, 225, 143, 255);
        colors.pressedColor = new Color32(214, 179, 92, 255);
        colors.disabledColor = new Color32(160, 155, 144, 145);
        button.colors = colors;
        var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic));
        arrow.layer = gameObject.layer;
        arrow.transform.SetParent(root.transform, false);
        var arrowRect = (RectTransform)arrow.transform;
        arrowRect.sizeDelta = new Vector2(14, 17);
        arrowRect.localRotation = Quaternion.Euler(0, 0, rotation);
        var graphic = arrow.GetComponent<BattleHudGraphic>();
        graphic.shape = BattleHudGraphic.Shape.Arrow;
        graphic.fill = graphic.ink;
        graphic.stroke = 0;
        graphic.raycastTarget = false;
        return button;
    }
}
