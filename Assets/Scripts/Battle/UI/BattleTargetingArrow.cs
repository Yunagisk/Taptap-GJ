using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattleTargetingArrow : MonoBehaviour
{
    BattleHudView hud;
    Canvas canvas;
    PointerEventData pointerData;
    EventSystem pointerSystem;
    readonly List<RaycastResult> hits = new List<RaycastResult>();
    BattleTargetingGraphic graphic;
    public EnemyCombatant HoveredEnemy { get; private set; }
    public BattleTargetingGraphic Graphic => graphic;
    public bool IsVisible => graphic != null && graphic.gameObject.activeSelf;
    public bool IsTargeting => hud != null && hud.Flow != null && !hud.popup.activeSelf
        && hud.Flow.CurrentState == BattleState.SelectingTarget;
    public RectTransform SourceButton => (RectTransform)(hud.Flow.IsSelectingSkill ? hud.skill.transform
        : hud.Flow.IsSelectingLantern ? hud.items.transform : hud.attack.transform);
    Camera UiCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    public void Bind(BattleHudView owner)
    {
        hud = owner; canvas = owner.GetComponent<Canvas>();
        if (graphic == null)
        {
            var root = new GameObject("TargetingArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleTargetingGraphic));
            root.layer = owner.gameObject.layer;
            root.transform.SetParent(owner.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            graphic = root.GetComponent<BattleTargetingGraphic>();
            graphic.raycastTarget = false;
        }
        Hide();
    }

    void Update()
    {
        if (!IsTargeting) return;
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { CancelSelection(); return; }
        // Recompute on mouse-down rather than using the previous frame's hover.
        if (Input.GetMouseButtonDown(0)) { UpdatePointer(Input.mousePosition); ConfirmHoveredTarget(); }
    }
    void LateUpdate() { UpdatePointer(Input.mousePosition); }

    public void UpdatePointer(Vector2 screenPosition)
    {
        bool canInspect = hud != null && hud.Flow != null && !hud.popup.activeSelf
            && (hud.Flow.CurrentState == BattleState.PlayerTurn || hud.Flow.CurrentState == BattleState.SelectingTarget);
        var target = canInspect ? FindTarget(screenPosition) : null;
        SetHovered(target);
        if (!IsTargeting) { if (graphic != null) graphic.gameObject.SetActive(false); return; }
        graphic.gameObject.SetActive(true);
        graphic.transform.SetAsLastSibling();
        Vector3 originWorld = SourceButton.TransformPoint(new Vector3(SourceButton.rect.center.x, SourceButton.rect.yMax + 10, 0));
        Vector2 from = hud.Root.InverseTransformPoint(originWorld);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(hud.Root, screenPosition, UiCamera, out var to);
        if (target != null)
        {
            if (hud.SelectedEnemy != target) hud.SelectEnemy(target);
        }
        var r = hud.Root.rect;
        to.x = Mathf.Clamp(to.x, r.xMin + 20, r.xMax - 20);
        to.y = Mathf.Clamp(to.y, r.yMin + 20, r.yMax - 20);
        graphic.SetCurve(from, to, target != null, Time.unscaledTime);
    }

    EnemyCombatant FindTarget(Vector2 screenPosition)
    {
        // Respect HUD panels and popups. Enemy cards remain valid targets.
        var system = EventSystem.current;
        if (system != null)
        {
            if (pointerData == null || pointerSystem != system)
            { pointerSystem = system; pointerData = new PointerEventData(system); }
            pointerData.position = screenPosition;
            hits.Clear(); system.RaycastAll(pointerData, hits);
            foreach (var hit in hits)
            {
                var intent = hit.gameObject.GetComponentInParent<BattleIntentHover>();
                if (intent != null && IsLivingTarget(intent.Enemy)) return intent.Enemy;
                var row = hit.gameObject.GetComponentInParent<BattleHudEnemyView>();
                if (row != null && IsLivingTarget(row.Enemy)) return row.Enemy;
                if (hit.module is GraphicRaycaster) return null;
            }
        }
        EnemyCombatant nearest = null;
        float nearestDistance = float.PositiveInfinity;
        if (Camera.main == null) return null;
        foreach (var row in hud.EnemyRows)
        {
            if (!IsLivingTarget(row.Enemy) || !row.TryGetTargetBounds(out var bounds)) continue;
            Vector3 min = Camera.main.WorldToScreenPoint(bounds.min), max = Camera.main.WorldToScreenPoint(bounds.max);
            var area = Rect.MinMaxRect(Mathf.Min(min.x, max.x) - 6, Mathf.Min(min.y, max.y) - 6,
                Mathf.Max(min.x, max.x) + 6, Mathf.Max(min.y, max.y) + 6);
            if (!area.Contains(screenPosition)) continue;
            float distance = ((screenPosition - area.center) / Mathf.Max(1, area.height)).sqrMagnitude;
            if (distance < nearestDistance) { nearestDistance = distance; nearest = row.Enemy; }
        }
        return nearest;
    }

    bool IsLivingTarget(EnemyCombatant enemy)
    {
        if (enemy == null || enemy.IsDead) return false;
        foreach (var member in hud.Flow.Enemies) if (member == enemy) return true;
        return false;
    }
    void SetHovered(EnemyCombatant enemy)
    {
        if (HoveredEnemy == enemy) return;
        HoveredEnemy = enemy;
        hud.RefreshTargetHighlights();
    }
    public bool ConfirmHoveredTarget()
    {
        if (!IsTargeting || !IsLivingTarget(HoveredEnemy)) return false;
        hud.SelectEnemy(HoveredEnemy, true);
        bool committed = hud.Flow.CurrentState != BattleState.SelectingTarget;
        Synchronize();
        return committed;
    }
    public void CancelSelection()
    { if (IsTargeting) hud.Flow.CancelTargetSelection(); Hide(); }
    public void Synchronize()
    { UpdatePointer(Input.mousePosition); }
    void Hide()
    {
        if (graphic != null) graphic.gameObject.SetActive(false);
        if (hud != null) SetHovered(null);
    }
    void OnDisable() { Hide(); }
    void OnApplicationFocus(bool focused) { if (!focused) CancelSelection(); }
}
