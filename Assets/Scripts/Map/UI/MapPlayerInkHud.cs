using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The portrait's cloth motion runs alongside the wings' shared ink front.
[DisallowMultipleComponent]
public sealed class MapPlayerInkHud : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Serializable]
    public struct WeaponIcon
    {
        public string weaponId;
        public Sprite sprite;
    }

    public MapPlayerPanelView playerPanel;
    public CanvasGroup details;
    public Image hoverSurface;
    public RectTransform weaponWing;
    public RectTransform statusWing;
    public MapHudInkRevealGroup weaponReveal;
    public MapHudInkRevealGroup statusReveal;
    public MapHudPortraitRevealEffect portraitReveal;
    public MapHudHoodAnimator hoodAnimator;
    public WeaponLibrary weaponLibrary;
    public WeaponIcon[] weaponIcons;
    [Tooltip("Actual equipped weapons, sorted by primary damage: Pierce, Slash, Blunt, Fire.")]
    public Image[] weaponImages;
    public TMP_Text[] weaponLabels;
    public TMP_Text[] weaponCounts;
    public MapHudWeaponArcGraphic weaponArc;
    public MapHudWeaponPlateGraphic[] weaponBackings;
    public Image[] resolveFills;
    public CanvasGroup[] resolvePips;
    [Min(0.01f)] public float animationDuration = 0.7f;
    [Min(0.05f)] public float hoodAnimationDuration = 1.05f;
    [Min(0)] public float collapseDelay = 0.12f;

    MapHudInkTimeline revealTimeline;
    MapHudHoodTimeline hoodTimeline;
    bool pointerInside;
    float closeAt, nextRefresh;
    readonly List<MapHudWeaponDisplay.Entry> displayedWeapons = new List<MapHudWeaponDisplay.Entry>(4);
    Graphic[] layoutGraphics;

    public bool IsExpanded => revealTimeline.Progress > 0;

    // Ignore the hidden/revealing alpha when finding the fully open position.
    // Inactive weapon slots are omitted, but the complete current loadout is
    // measured even while the details CanvasGroup is collapsed.
    public bool TryGetExpandedBounds(Transform space, out Rect bounds)
    {
        bounds = default;
        if (details == null || playerPanel == null || playerPanel.portrait == null) return false;
        bounds = MapUITransform.Bounds(playerPanel.portrait.rectTransform, space);
        if (layoutGraphics == null) layoutGraphics = details.GetComponentsInChildren<Graphic>(true);
        foreach (var graphic in layoutGraphics)
        {
            if (graphic == null || !graphic.enabled || !graphic.gameObject.activeSelf
                || graphic.color.a <= 0 || graphic is TMP_SubMeshUI) continue;
            Rect local;
            bool bleed = !(graphic is TMP_Text);
            if (graphic is TMP_Text label)
            {
                if (!TryGetTextInkBounds(label, out local)) continue;
            }
            else if (graphic is MapHudWeaponArcGraphic arc)
                local = MapHudWeaponArcLayout.ContentBounds(arc.SlotCount);
            else local = graphic.rectTransform.rect;
            // Reserve the existing wet bleed, which can reach 1.7 local pixels
            // outside a graphic. Static/animated placement remains identical.
            if (bleed) local = Rect.MinMaxRect(local.xMin - 1.7f, local.yMin - 1.7f,
                local.xMax + 1.7f, local.yMax + 1.7f);
            Rect visible = MapHudScreenLayout.InSpace(local, MapUITransform.RelativeMatrix(graphic.transform, space));
            bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, visible.xMin), Mathf.Min(bounds.yMin, visible.yMin),
                Mathf.Max(bounds.xMax, visible.xMax), Mathf.Max(bounds.yMax, visible.yMax));
        }
        return true;
    }

    static bool TryGetTextInkBounds(TMP_Text label, out Rect bounds)
    {
        bounds = default;
        if (string.IsNullOrWhiteSpace(label.text) || label.font == null) return false;
        if (label.havePropertiesChanged || label.textInfo == null || label.textInfo.characterCount == 0)
            label.ForceMeshUpdate(true);
        // Glyph vertices include SDF padding and fallback fonts, without the
        // label rectangle's blank space. Reveal only changes their colours;
        // include every vertex so the result stays fixed during the animation.
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        var info = label.textInfo;
        if (info == null) return false;
        for (int material = 0; material < info.materialCount; material++)
        {
            var mesh = info.meshInfo[material];
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                Vector2 point = mesh.vertices[i];
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }
        if (min.x >= max.x || min.y >= max.y) return false;
        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    void Awake()
    {
        weaponReveal = FindReveal(weaponWing, weaponReveal, true, 1729);
        statusReveal = FindReveal(statusWing, statusReveal, false, 2441);
    }

    void OnEnable()
    {
        ResetHover();
        nextRefresh = 0;
        ApplyPresentation();
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playerPanel != null && playerPanel.IsInventoryOpen) return;
        pointerInside = true;
        closeAt = 0;
        // Bridge the spaces between the avatar, weapons and status graphics.
        if (hoverSurface != null) hoverSurface.raycastTarget = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        closeAt = Time.unscaledTime + collapseDelay;
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) { pointerInside = false; closeAt = 0; }
    }

    void Update()
    {
        bool blocked = playerPanel != null && playerPanel.IsInventoryOpen;
        if (blocked) { pointerInside = false; closeAt = 0; }
        bool expand = !blocked && (pointerInside || Time.unscaledTime < closeAt);
        revealTimeline.Step(expand, Time.unscaledDeltaTime, animationDuration);
        hoodTimeline.Step(expand, Time.unscaledDeltaTime, hoodAnimationDuration);
        ApplyPresentation();
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        Refresh();
    }

    void ApplyPresentation()
    {
        bool visible = IsExpanded;
        bool interactive = visible && (playerPanel == null || !playerPanel.IsInventoryOpen);
        if (weaponReveal != null) weaponReveal.SetReveal(revealTimeline.Progress);
        if (statusReveal != null) statusReveal.SetReveal(revealTimeline.Progress);
        if (hoodAnimator != null) hoodAnimator.SetPose(hoodTimeline.Progress);
        else if (portraitReveal != null) portraitReveal.SetReveal(revealTimeline.Progress);
        if (details != null)
        {
            details.alpha = visible ? 1 : 0;
            details.blocksRaycasts = interactive;
            details.interactable = interactive;
        }
        // The full panel starts receiving hover only after entering the avatar.
        if (hoverSurface != null)
            hoverSurface.raycastTarget = (pointerInside || visible)
                && (playerPanel == null || !playerPanel.IsInventoryOpen);
    }

    MapHudInkRevealGroup FindReveal(RectTransform wing, MapHudInkRevealGroup effect, bool radial, int seed)
    {
        if (wing == null) return effect;
        if (effect == null) effect = wing.GetComponent<MapHudInkRevealGroup>();
        if (effect == null)
        {
            effect = wing.gameObject.AddComponent<MapHudInkRevealGroup>();
            effect.radial = radial;
            effect.noiseSeed = seed;
            effect.origin = playerPanel != null && playerPanel.portrait != null ? playerPanel.portrait.rectTransform : null;
        }
        return effect;
    }

    public void Refresh()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state == null || state.player == null)
        {
            RefreshWeapons(null);
            return;
        }
        var player = state.player;
        int maximum = Mathf.Clamp(player.maxResolve, 0, 4);
        int current = Mathf.Clamp(player.resolve, 0, maximum);
        for (int i = 0; resolveFills != null && i < resolveFills.Length; i++)
            if (resolveFills[i] != null) resolveFills[i].enabled = i < current;
        for (int i = 0; resolvePips != null && i < resolvePips.Length; i++)
            if (resolvePips[i] != null) resolvePips[i].alpha = 1;

        RefreshWeapons(state.inventory != null ? state.inventory.equippedWeaponIds : null);
    }

    void RefreshWeapons(IReadOnlyList<string> equipped)
    {
        MapHudWeaponDisplay.Build(equipped, DamageOf, displayedWeapons);
        int count = displayedWeapons.Count;
        if (weaponArc != null) weaponArc.SetSlotCount(count);
        for (int slot = 0; slot < MapHudWeaponArcLayout.Capacity; slot++)
        {
            bool visible = slot < count;
            Vector2 position = visible ? MapHudWeaponArcLayout.Position(slot, count) : Vector2.zero;
            string selected = visible ? displayedWeapons[slot].id : null;
            Sprite sprite = FindIcon(selected);
            if (weaponBackings != null && slot < weaponBackings.Length)
                Present(weaponBackings[slot], visible, position);
            if (weaponImages != null && slot < weaponImages.Length && weaponImages[slot] != null)
            {
                var image = weaponImages[slot];
                Present(image, visible, position);
                image.sprite = sprite;
                image.enabled = sprite != null;
            }
            if (weaponLabels != null && slot < weaponLabels.Length && weaponLabels[slot] != null)
            {
                Present(weaponLabels[slot], visible, position);
                weaponLabels[slot].text = selected == null ? string.Empty : sprite != null ? string.Empty
                    : weaponLibrary != null ? weaponLibrary.NameOf(selected) : WeaponTable.NameOf(selected);
            }
            if (weaponCounts != null && slot < weaponCounts.Length && weaponCounts[slot] != null)
            {
                weaponCounts[slot].text = string.Empty;
                Present(weaponCounts[slot], false, position);
            }
        }
    }

    IReadOnlyList<DamageComponent> DamageOf(string id)
    {
        var weapon = weaponLibrary != null ? weaponLibrary.Find(id) : null;
        return weapon != null ? weapon.DamageComponents : null;
    }

    static void Present(Graphic graphic, bool visible, Vector2 position)
    {
        if (graphic == null) return;
        if (graphic.gameObject.activeSelf != visible) graphic.gameObject.SetActive(visible);
        if (visible && graphic.rectTransform.anchoredPosition != position)
            graphic.rectTransform.anchoredPosition = position;
    }

    public static PlayerDamageType PrimaryDamage(IReadOnlyList<DamageComponent> components)
        => MapHudWeaponDisplay.PrimaryDamage(components);

    Sprite FindIcon(string id)
    {
        if (!string.IsNullOrEmpty(id) && weaponIcons != null)
            for (int i = 0; i < weaponIcons.Length; i++)
                if (weaponIcons[i].weaponId == id) return weaponIcons[i].sprite;
        return null;
    }

    void OnDisable()
    {
        ResetHover();
        ApplyPresentation();
    }

    void ResetHover()
    {
        pointerInside = false;
        closeAt = 0;
        revealTimeline.Reset();
        hoodTimeline.Reset();
    }
}
