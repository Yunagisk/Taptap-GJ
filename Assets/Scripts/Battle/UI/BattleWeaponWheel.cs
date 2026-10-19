using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattleWeaponWheel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, ICancelHandler
{
    [Serializable] public struct Art { public string id; public Sprite sprite; }
    public Image[] icons;
    public BattleHudGraphic[] selectionFrames;
    public TMP_Text caption;
    public Art[] weaponArt;
    [Min(1)] public float pixelsPerStep = 92;
    readonly List<int> slots = new List<int>(4);
    readonly List<int> nextSlots = new List<int>(4);
    readonly List<KeyValuePair<int, float>> drawOrder = new List<KeyValuePair<int, float>>(4);
    readonly BattleWeaponWheelGesture gesture = new BattleWeaponWheelGesture();
    PlayerWeaponLoadout loadout;
    BattleWeaponController controller;
    float cursor, nextRefresh;
    int pointer = int.MinValue;
    bool locked;
    public bool IsDragging => gesture.Dragging;

    public void Bind(PlayerWeaponLoadout weapons, BattleWeaponController switching, BattleFlowController battle)
    { loadout = weapons; controller = switching; RefreshSlots(); }
    public void SetLocked(bool value) { locked = value; if (value) CancelDrag(); }
    bool CanSwitch => !locked && controller != null && controller.CanSwitchWeapon;
    void Update()
    {
        if (gesture.Dragging && !CanSwitch) CancelDrag();
        if (Time.unscaledTime >= nextRefresh)
        { nextRefresh = Time.unscaledTime + .1f; RefreshSlots(); }
        int selected = slots.IndexOf(controller != null ? controller.CurrentIndex : -1);
        float target = gesture.Dragging ? gesture.Cursor : BattleWeaponWheelMath.NearestCursor(cursor, Mathf.Max(0, selected), slots.Count);
        cursor = gesture.Dragging ? target : Mathf.Lerp(cursor, target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
        Present();
    }
    void RefreshSlots()
    {
        var weapons = loadout != null ? loadout.EquippedWeapons : null;
        nextSlots.Clear();
        for (int i = 0; weapons != null && i < weapons.Length && i < 4; i++) if (weapons[i] != null) nextSlots.Add(i);
        bool changed = nextSlots.Count != slots.Count;
        for (int i = 0; i < nextSlots.Count && !changed; i++) changed = nextSlots[i] != slots[i];
        if (changed) { CancelDrag(); slots.Clear(); slots.AddRange(nextSlots); cursor = Mathf.Max(0, slots.IndexOf(controller != null ? controller.CurrentIndex : -1)); }
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
        int centered = BattleWeaponWheelMath.CenteredIndex(cursor, slots.Count);
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
    float LocalX(PointerEventData data)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, data.position, data.pressEventCamera, out var point);
        return point.x;
    }
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || !CanSwitch || gesture.Dragging) return;
        RefreshSlots(); pointer = data.pointerId;
        gesture.Begin(LocalX(data), Mathf.Max(0, slots.IndexOf(controller.CurrentIndex)), slots.Count);
    }
    public void OnBeginDrag(PointerEventData data) { }
    public void OnDrag(PointerEventData data)
    { if (data.pointerId == pointer && CanSwitch) gesture.Drag(LocalX(data), pixelsPerStep); }
    public void OnPointerUp(PointerEventData data)
    { if (data.pointerId == pointer) { gesture.Drag(LocalX(data), pixelsPerStep); Commit(); } }
    public void OnEndDrag(PointerEventData data) { if (data.pointerId == pointer) Commit(); }
    void Commit()
    {
        int index = gesture.Release(CanSwitch, slots.Count); pointer = int.MinValue;
        if (index >= 0) controller.SwitchWeapon(slots[index]);
        RefreshSlots();
    }
    public void CancelDrag() { gesture.Cancel(); pointer = int.MinValue; }
    public void OnCancel(BaseEventData data) => CancelDrag();
    void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
    void OnDisable() => CancelDrag();
}
