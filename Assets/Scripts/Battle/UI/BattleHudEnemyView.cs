using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleHudEnemyView : MonoBehaviour
{
    public Image portrait, health;
    public TMP_Text enemyName, healthText, statusText, unknownWeakness;
    public Image[] weaknesses;
    public Button select;
    public BattleHudGraphic frame;
    EnemyCombatant enemy;
    BattleHudView hud;
    RectTransform worldTag, intent, arrow, worldName, dizzy;
    TMP_Text intentText, intentEffect, intentTitle;
    RectTransform intentDetails;
    EnemyIntentIconGraphic intentIcon;
    bool intentHovered;
    public bool IntentExpanded => intentDetails != null && intentDetails.gameObject.activeSelf;
    Renderer[] renderers;
    SpriteRenderer paintedRenderer;
    EnemySpriteAnimation enemyAnimation;
    Canvas hudCanvas;
    DamageResistance resistance;
    TMP_Text weaknessLabel;
    readonly Vector3[] cardCorners = new Vector3[4];
    public EnemyCombatant Enemy => enemy;
    public bool IsDeparting { get; private set; }
    public bool IsMoving { get; private set; }
    int maximumHealth;
    Vector2 moveFrom, moveTo;
    double moveStartedAt;

    public void Bind(EnemyCombatant unit, int index, BattleHudView view)
    {
        enemy = unit; hud = view; resistance = unit.GetComponent<DamageResistance>();
        maximumHealth = unit.MaxHP;
        unit.OnDied += EnemyDied;
        hudCanvas = view.GetComponent<Canvas>();
        enemyName.text = enemy.DisplayName + " " + index;
        portrait.sprite = BattleHudView.PortraitOf(unit);
        BattleHudView.OutlineNumber(healthText);
        select.onClick.AddListener(() => hud.SelectEnemy(enemy, true));
        renderers = unit.GetComponentsInChildren<Renderer>();
        enemyAnimation = unit.GetComponent<EnemySpriteAnimation>();
        foreach (var renderer in renderers)
            if (renderer is SpriteRenderer sprite && sprite.name == "EnemyAnimationVisual") paintedRenderer = sprite;
        worldTag = Instantiate(hud.worldTagTemplate, hud.Root);
        worldTag.name = "WorldTag_" + index;
        worldTag.gameObject.SetActive(true);
        intent = (RectTransform)worldTag.Find("Intent");
        arrow = (RectTransform)worldTag.Find("Arrow");
        worldName = (RectTransform)worldTag.Find("Name");
        worldName.GetComponent<TMP_Text>().text = enemy.DisplayName + " " + index;
        BattleHudView.OutlineNumber(worldName.GetComponent<TMP_Text>());
        intentText = intent.Find("Amount").GetComponent<TMP_Text>();
        intentEffect = intent.Find("SpecialEffect").GetComponent<TMP_Text>();
        CreateIntentDetails();
        var icon = new GameObject("DamageType", typeof(RectTransform), typeof(CanvasRenderer), typeof(EnemyIntentIconGraphic));
        icon.layer = worldTag.gameObject.layer;
        icon.transform.SetParent(intent, false);
        intentIcon = icon.GetComponent<EnemyIntentIconGraphic>();
        intentIcon.raycastTarget = false;
        intentIcon.rectTransform.sizeDelta = new Vector2(22, 22);
        intentText.fontSize = 22;
        intentText.enableWordWrapping = false;
        var marker = new GameObject("BreakDizzy", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleDizzyGraphic));
        marker.layer = worldTag.gameObject.layer;
        marker.transform.SetParent(worldTag, false);
        dizzy = (RectTransform)marker.transform;
        dizzy.sizeDelta = new Vector2(110, 48);
        marker.GetComponent<BattleDizzyGraphic>().raycastTarget = false;
        for (int i = 0; i < weaknesses.Length; i++) weaknesses[i].sprite = hud.damageIcons[i];
        weaknessLabel = transform.Find("KnownWeakness")?.GetComponent<TMP_Text>();
        if (weaknessLabel != null)
        {
            weaknessLabel.text = "怪物弱点";
            PlaceWeaknessRect(weaknessLabel.rectTransform, new Vector2(132, -82), new Vector2(92, 28), new Vector2(0, .5f));
            weaknessLabel.enableWordWrapping = false;
            weaknessLabel.overflowMode = TextOverflowModes.Ellipsis;
        }
        PlaceWeaknessRect(unknownWeakness.rectTransform, new Vector2(230, -82), new Vector2(120, 28), new Vector2(0, .5f));
        unknownWeakness.fontSize = 18;
        Refresh();
    }
    void CreateIntentDetails()
    {
        var root = new GameObject("Details", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic), typeof(CanvasGroup));
        root.layer = intent.gameObject.layer;
        root.transform.SetParent(intent, false);
        intentDetails = (RectTransform)root.transform;
        intentDetails.anchorMin = intentDetails.anchorMax = new Vector2(.5f, 1);
        intentDetails.pivot = new Vector2(.5f, 0);
        intentDetails.anchoredPosition = new Vector2(0, 10);
        root.GetComponent<BattleHudGraphic>().raycastTarget = false;
        var group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        var titleRoot = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleRoot.layer = intent.gameObject.layer;
        titleRoot.transform.SetParent(intentDetails, false);
        intentTitle = titleRoot.GetComponent<TextMeshProUGUI>();
        intentTitle.font = intentText.font;
        intentTitle.fontSize = 20;
        intentTitle.color = new Color32(232, 203, 144, 255);
        intentTitle.raycastTarget = false;
        intentTitle.enableWordWrapping = false;
        intentTitle.alignment = TextAlignmentOptions.TopLeft;
        intentEffect.transform.SetParent(intentDetails, false);
        intentEffect.gameObject.SetActive(true);
        intentEffect.fontSize = 17;
        intentEffect.enableWordWrapping = true;
        intentEffect.alignment = TextAlignmentOptions.TopLeft;
        intentEffect.raycastTarget = false;
        foreach (var text in new[] { intentTitle, intentEffect })
        {
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1);
            text.rectTransform.pivot = new Vector2(0, 1);
        }
        intentDetails.gameObject.SetActive(false);
        // The fixed compact pill is the hover target; expanded content never blocks clicks.
        intent.GetComponent<BattleHudGraphic>().raycastTarget = true;
        intent.gameObject.AddComponent<BattleIntentHover>().Bind(this);
    }

    public void SetIntentHovered(bool value, bool refresh = true)
    { if (intentHovered == value) return; intentHovered = value; if (refresh) Refresh(); }
    static void PlaceWeaknessRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
    public void Refresh()
    {
        if (hud == null || IsDeparting) return;
        // HUD binding runs in Awake, before StartBattle initializes current HP.
        if (enemy != null && hud.Flow != null && hud.Flow.RoundNumber == 0) return;
        // Destruction can happen between HUD polls. The death event handles the
        // normal path; this also removes cards whose actor was destroyed directly.
        if (enemy == null || enemy.IsDead)
        {
            BeginDeath();
            return;
        }
        maximumHealth = enemy.MaxHP;
        BattleHudView.SetHealth(health, enemy.CurrentHP, enemy.MaxHP);
        healthText.text = enemy.CurrentHP + " / " + enemy.MaxHP;
        statusText.text = enemy.IsDead ? "已击败" : (enemy.IsGuarding ? "防御中 " : "")
            + (enemy.IsCharging ? "蓄力中 " : "") + enemy.Statuses.Format() + " "
            + (enemy.Break != null ? enemy.Break.Format(hud.Flow.RoundNumber) : "");
        frame.Highlight(enemy == hud.HighlightedEnemy && !enemy.IsDead);
        select.interactable = !enemy.IsDead && (hud.Flow.CurrentState == BattleState.PlayerTurn || hud.Flow.CurrentState == BattleState.SelectingTarget);
        int known = 0;
        for (int i = 0; i < weaknesses.Length; i++)
        {
            bool visible = resistance != null && resistance.IsWeaknessKnown((PlayerDamageType)i);
            weaknesses[i].transform.parent.gameObject.SetActive(visible);
            if (visible)
            {
                PlaceWeaknessRect((RectTransform)weaknesses[i].transform.parent,
                    new Vector2(240 + known * 34, -82), new Vector2(28, 28), new Vector2(.5f, .5f));
                known++;
            }
        }
        unknownWeakness.gameObject.SetActive(known == 0);
        unknownWeakness.text = resistance != null && resistance.UnknownWeaknessCount > 0 ? "未探明" : "无";
        var action = enemy.UpcomingAction;
        bool skip = enemy.Break != null && enemy.Break.SkipPending;
        intent.gameObject.SetActive(!skip);
        dizzy.gameObject.SetActive(skip && !enemy.IsDead);
        bool expanded = !skip && !enemy.IsDead && action != null && !hud.popup.activeSelf
            && (hud.ShowEnemyName(enemy) || intentHovered);
        intentDetails.gameObject.SetActive(expanded);
        if (expanded)
        {
            const float detailWidth = 232;
            intentTitle.text = action.AttackName;
            intentEffect.text = action.IntentDetailText;
            float bodyHeight = Mathf.Max(24, intentEffect.GetPreferredValues(intentEffect.text, detailWidth - 24, 0).y);
            intentDetails.sizeDelta = new Vector2(detailWidth, bodyHeight + 54);
            intentTitle.rectTransform.anchoredPosition = new Vector2(12, -8);
            intentTitle.rectTransform.sizeDelta = new Vector2(detailWidth - 24, 28);
            intentEffect.rectTransform.anchoredPosition = new Vector2(12, -36);
            intentEffect.rectTransform.sizeDelta = new Vector2(detailWidth - 24, bodyHeight + 4);
        }
        intentText.text = action == null ? "—" : action.CompactIntentText;
        float amountWidth = Mathf.Max(18, intentText.GetPreferredValues(intentText.text).x);
        float groupWidth = 22 + 8 + amountWidth;
        float width = Mathf.Max(76, groupWidth + 20);
        intent.sizeDelta = new Vector2(width, 34);
        intentIcon.gameObject.SetActive(action != null);
        intentIcon.SetIntent(action);
        intentIcon.rectTransform.anchoredPosition = new Vector2(-groupWidth * .5f + 11, 0);
        intentText.rectTransform.anchoredPosition = new Vector2(groupWidth * .5f - amountWidth * .5f, 0);
        intentText.rectTransform.sizeDelta = new Vector2(amountWidth + 4, 28);
        worldTag.gameObject.SetActive(!enemy.IsDead);
        worldName.gameObject.SetActive(!enemy.IsDead && hud.ShowEnemyName(enemy));
        arrow.gameObject.SetActive(!enemy.IsDead && enemy == hud.HighlightedEnemy);
    }
    void EnemyDied(Combatant unit) { maximumHealth = unit.MaxHP; BeginDeath(); }

    void BeginDeath()
    {
        if (IsDeparting) return;
        IsDeparting = true;
        IsMoving = false;
        BattleHudView.SetHealth(health, 0, maximumHealth);
        healthText.text = "0 / " + maximumHealth;
        statusText.text = "";
        frame.Highlight(false);
        select.interactable = false;
        if (worldTag != null) worldTag.gameObject.SetActive(false);
        foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        BattleEnemyCardShatter.Play((RectTransform)transform, () =>
        {
            if (hud != null && this != null) hud.RemoveEnemyCard(this);
        });
        group.alpha = 0;
        hud.TargetingArrow?.Synchronize();
    }

    public void MoveTo(Vector2 position, bool immediate = false)
    {
        var rect = (RectTransform)transform;
        moveFrom = rect.anchoredPosition;
        moveTo = position;
        moveStartedAt = Time.realtimeSinceStartupAsDouble;
        IsMoving = !immediate && (moveFrom - moveTo).sqrMagnitude > .01f;
        if (!IsMoving) rect.anchoredPosition = moveTo;
    }
    void Update()
    {
        if (!IsMoving) return;
        float progress = Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble - moveStartedAt) / .26f);
        ((RectTransform)transform).anchoredPosition = Vector2.Lerp(moveFrom, moveTo, Mathf.SmoothStep(0, 1, progress));
        if (progress >= 1) IsMoving = false;
    }
    void LateUpdate()
    {
        if (enemy == null || enemy.IsDead || worldTag == null || hud == null || Camera.main == null) return;
        TryGetTargetBounds(out var bounds);
        Vector2 top = ScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        Vector2 foot = ScreenPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        float halfWidth = hud.Root.rect.width * .5f, halfHeight = hud.Root.rect.height * .5f;
        float x = Mathf.Clamp(top.x, -halfWidth + 60, halfWidth - 60);
        Vector2 intentPosition = new Vector2(x, Mathf.Min(top.y + 50, halfHeight - 200));
        bool besideHead = false;
        foreach (var row in hud.EnemyRows)
        {
            if (row == null || !row.gameObject.activeInHierarchy) continue;
            ((RectTransform)row.transform).GetWorldCorners(cardCorners);
            Vector2 min = hud.Root.InverseTransformPoint(cardCorners[0]);
            Vector2 max = hud.Root.InverseTransformPoint(cardCorners[2]);
            var card = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            var paper = new Rect(intentPosition - intent.sizeDelta * .5f, intent.sizeDelta);
            if (!card.Overlaps(paper)) continue;
            intentPosition.y = min.y - 12 - intent.sizeDelta.y * .5f;
            besideHead = true;
        }
        // Tall enemies can stand under the third card. Keep the intent below
        // that card and beside the target marker, rather than covering either.
        if (besideHead) intentPosition.x -= intent.sizeDelta.x * .5f + 24;
        intent.anchoredPosition = intentPosition;
        if (IntentExpanded)
        {
            float detailX = Mathf.Clamp(intentPosition.x, -halfWidth + intentDetails.sizeDelta.x * .5f + 16,
                halfWidth - intentDetails.sizeDelta.x * .5f - 16);
            intentDetails.anchoredPosition = new Vector2(detailX - intentPosition.x, 10);
        }
        dizzy.anchoredPosition = new Vector2(x, Mathf.Min(top.y + 72, halfHeight - 200));
        arrow.anchoredPosition = new Vector2(x, Mathf.Min(top.y + 22, halfHeight - 245));
        worldName.anchoredPosition = new Vector2(x, Mathf.Max(foot.y - 20, -halfHeight + 185));
    }
    public bool TryGetTargetBounds(out Bounds bounds)
    {
        bounds = enemy != null ? new Bounds(enemy.transform.position, Vector3.one) : default;
        if (enemy == null || enemy.IsDead) return false;
        bool found = false;
        if (paintedRenderer != null && enemyAnimation != null && enemyAnimation.animations != null
            && enemyAnimation.animations.Portrait != null)
        {
            bounds = PaintedBounds(paintedRenderer, enemyAnimation.animations);
            found = true;
        }
        if (!found) foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (renderer is SpriteRenderer sprite && sprite.sprite == null) continue;
            if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }
    static Bounds PaintedBounds(SpriteRenderer renderer, EnemySpriteAnimationSet set)
    {
        var sprite = set.Portrait;
        var area = set.artworkBounds;
        float x0 = (area.x * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit;
        float x1 = (area.z * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit;
        float y0 = (area.y * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit;
        float y1 = (area.w * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit;
        if (renderer.flipX) { float old = x0; x0 = -x1; x1 = -old; }
        if (renderer.flipY) { float old = y0; y0 = -y1; y1 = -old; }
        var root = renderer.transform;
        var bounds = new Bounds(root.TransformPoint(new Vector3(x0, y0, 0)), Vector3.zero);
        bounds.Encapsulate(root.TransformPoint(new Vector3(x0, y1, 0)));
        bounds.Encapsulate(root.TransformPoint(new Vector3(x1, y0, 0)));
        bounds.Encapsulate(root.TransformPoint(new Vector3(x1, y1, 0)));
        return bounds;
    }
    Vector2 ScreenPoint(Vector3 world)
    {
        Camera uiCamera = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? hudCanvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(hud.Root, Camera.main.WorldToScreenPoint(world), uiCamera, out var point);
        return point;
    }
    void OnDestroy()
    {
        if (!ReferenceEquals(enemy, null)) enemy.OnDied -= EnemyDied;
        if (worldTag == null) return;
        if (Application.isPlaying) Destroy(worldTag.gameObject);
        else DestroyImmediate(worldTag.gameObject);
    }
}
