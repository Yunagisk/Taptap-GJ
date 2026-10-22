using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleHudView : MonoBehaviour
{
    [Serializable] public struct TurnSlot { public Image portrait; public BattleHudGraphic frame; }
    public Image playerPortrait, playerHealth;
    public TMP_Text healthText, statusText, prompt, roundText;
    public BattleHudGraphic[] resolve;
    public TurnSlot[] turnSlots;
    public BattleHudEnemyView enemyTemplate;
    public RectTransform enemyContainer, worldTagTemplate;
    public Sprite[] damageIcons;
    public Material actorOutlineMaterial;
    public Button attack, defend, skill, items, closePopup;
    public BattleWeaponWheel wheel;
    public GameObject popup;
    public TMP_Text popupTitle, popupBody;
    public BattleFlowController Flow { get; private set; }
    public EnemyCombatant SelectedEnemy { get; private set; }
    public RectTransform Root => (RectTransform)transform;
    readonly List<BattleHudEnemyView> rows = new List<BattleHudEnemyView>();
    readonly List<Combatant> order = new List<Combatant>();
    float nextRefresh;
    float enemyRowHeight;
    TMP_Text skillLabel;
    readonly List<Button> itemButtons = new List<Button>();
    bool showingItems;
    BattleActionTooltip actionTooltip;
    BattleTargetingArrow targetingArrow;
    BattlePlayerStatusView playerStatuses;
    PlayerCombatant healthSource;
    public BattleTargetingArrow TargetingArrow => targetingArrow;
    public IReadOnlyList<BattleHudEnemyView> EnemyRows => rows;
    public EnemyCombatant HighlightedEnemy => targetingArrow != null ? targetingArrow.HoveredEnemy : null;
    public bool ShowEnemyName(EnemyCombatant enemy) => Flow != null && !popup.activeSelf
        && (Flow.CurrentState == BattleState.SelectingTarget || HighlightedEnemy == enemy);

    public bool CanShowActionInfo => Flow != null && !popup.activeSelf
        && (Flow.CurrentState == BattleState.PlayerTurn || Flow.CurrentState == BattleState.SelectingTarget);

    public void Bind(BattleFlowController flow, PlayerWeaponLoadout loadout, BattleWeaponController switching)
    {
        Flow = flow;
        wheel.Bind(loadout, switching, flow);
        attack.onClick.AddListener(Attack);
        defend.onClick.AddListener(() => { if (CanAct) Flow.PlayerDefend(); });
        skill.onClick.AddListener(UseSkill);
        skillLabel = skill.GetComponentInChildren<TMP_Text>();
        skillLabel.enableAutoSizing = true;
        skillLabel.fontSizeMin = 17;
        skillLabel.fontSizeMax = 24;
        items.onClick.AddListener(ShowItems);
        closePopup.onClick.AddListener(ClosePopup);
        popup.SetActive(false);
        prompt.gameObject.SetActive(false);
        enemyTemplate.gameObject.SetActive(false);
        worldTagTemplate.gameObject.SetActive(false);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            label.font = SharedUIFont.Get(label.font);
        actionTooltip = GetComponent<BattleActionTooltip>();
        if (actionTooltip == null) actionTooltip = gameObject.AddComponent<BattleActionTooltip>();
        actionTooltip.Bind(this);
        OutlineNumber(healthText);
        if (flow == null) return;
        int index = 0;
        foreach (var enemy in flow.Enemies)
        {
            if (enemy == null) continue;
            var row = Instantiate(enemyTemplate, enemyContainer);
            row.name = "Enemy_" + (++index);
            row.gameObject.SetActive(true);
            row.Bind(enemy, index, this, EnemyLabel(enemy));
            rows.Add(row);
            if (SelectedEnemy == null) SelectedEnemy = enemy;
            var hover = enemy.GetComponent<BattleHudTargetHover>();
            if (hover == null) hover = enemy.gameObject.AddComponent<BattleHudTargetHover>();
            hover.Bind(this, enemy);
            enemy.GetComponent<EnemyTargetSelector>()?.UseExternalIndicator(true);
        }
        // Fit extra enemies into the top-right column without running below the actions.
        enemyRowHeight = Mathf.Min(142, 510f / Mathf.Max(1, rows.Count));
        for (int i = 0; i < rows.Count; i++)
        {
            var rect = (RectTransform)rows[i].transform;
            rows[i].MoveTo(new Vector2(0, -i * enemyRowHeight), true);
            rect.localScale = Vector3.one * Mathf.Min(1, enemyRowHeight / 142);
        }
        flow.OnBattleStateChanged += StateChanged;
        healthSource = flow.Player;
        if (healthSource != null) healthSource.OnHpChanged += RefreshPlayerHealth;
        targetingArrow = GetComponent<BattleTargetingArrow>();
        if (targetingArrow == null) targetingArrow = gameObject.AddComponent<BattleTargetingArrow>();
        targetingArrow.Bind(this);
        BindOutline(flow.Player);
        playerStatuses = gameObject.AddComponent<BattlePlayerStatusView>();
        playerStatuses.Bind(this);
        if (flow.Player != null && flow.Player.GetComponent<PlayerStatusEffects>() == null)
            flow.Player.gameObject.AddComponent<PlayerStatusEffects>();
        foreach (var enemy in flow.Enemies) BindOutline(enemy);
        Refresh();
    }
    string EnemyLabel(EnemyCombatant enemy)
    {
        int count = 0, number = 0;
        // Use the encounter roster so deaths and card reflow keep each name stable.
        foreach (var other in Flow.Enemies)
        {
            if (other == null) continue;
            bool sameKind = enemy.Definition != null && other.Definition != null
                ? enemy.Definition.kind == other.Definition.kind
                : enemy.Definition == other.Definition && enemy.DisplayName == other.DisplayName;
            if (!sameKind) continue;
            count++;
            if (other == enemy) number = count;
        }
        return count > 1 ? enemy.DisplayName + " " + number : enemy.DisplayName;
    }
    internal void RemoveEnemyCard(BattleHudEnemyView row)
    {
        if (!rows.Remove(row)) return;
        row.gameObject.SetActive(false);
        Destroy(row.gameObject);
        int slot = 0;
        foreach (var remaining in rows)
            if (remaining != null && !remaining.IsDeparting)
                remaining.MoveTo(new Vector2(0, -slot++ * enemyRowHeight));
        Refresh();
    }
    void BindOutline(Combatant actor)
    {
        if (actor == null || actorOutlineMaterial == null) return;
        var outline = actor.GetComponent<BattleActorOutline>();
        if (outline == null) outline = actor.gameObject.AddComponent<BattleActorOutline>();
        outline.Bind(actorOutlineMaterial);
    }
    bool CanAct => Flow != null && Flow.CurrentState == BattleState.PlayerTurn && !popup.activeSelf;
    void Attack()
    {
        if (Flow == null || popup.activeSelf) return;
        if (Flow.CurrentState == BattleState.SelectingTarget) Flow.CancelTargetSelection();
        else if (CanAct) Flow.PlayerAttack();
    }
    public void SelectEnemy(EnemyCombatant enemy, bool confirm = false)
    {
        if (enemy == null || enemy.IsDead || Flow == null || popup.activeSelf) return;
        if (Flow.CurrentState != BattleState.PlayerTurn && Flow.CurrentState != BattleState.SelectingTarget) return;
        SelectedEnemy = enemy;
        if (confirm && Flow.CurrentState == BattleState.SelectingTarget) Flow.AttackTarget(enemy);
        Refresh();
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            actionTooltip.Hide();
            if (popup.activeSelf) ClosePopup();
            else Flow?.CancelTargetSelection();
        }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .08f;
        Refresh();
    }
    void StateChanged(BattleState state)
    {
        if (state != BattleState.PlayerTurn) ClosePopup();
        actionTooltip.Hide();
        targetingArrow?.Synchronize();
        Refresh();
    }
    public void RefreshTargetHighlights()
    { foreach (var row in rows) if (row != null) row.Refresh(); }
    void RefreshPlayerHealth(Combatant player)
    {
        // Death destroys the actor before the next polling refresh. Paint zero now.
        SetHealth(playerHealth, player.CurrentHP, player.MaxHP);
        healthText.text = player.CurrentHP + " / " + player.MaxHP;
        if (player.IsDead) statusText.text = "";
    }
    void Refresh()
    {
        if (Flow == null || Flow.Player == null) return;
        var player = Flow.Player;
        RefreshPlayerHealth(player);
        statusText.text = !player.IsDead && player.IsDefending ? "防御中" : "";
        for (int i = 0; i < resolve.Length; i++)
        {
            resolve[i].gameObject.SetActive(i < player.MaxDetermination);
            resolve[i].fill = i < player.CurrentDetermination ? new Color32(219, 171, 68, 255) : new Color32(56, 47, 37, 255);
            resolve[i].SetVerticesDirty();
        }
        bool targeting = Flow.CurrentState == BattleState.SelectingTarget;
        attack.interactable = !popup.activeSelf && (CanAct || targeting) && player.CurrentWeapon != null && Flow.GuideAllowsAction("attack");
        defend.interactable = CanAct && Flow.GuideAllowsAction("defend");
        items.interactable = CanAct && Flow.GuideAllowsAction("items");
        skill.interactable = !popup.activeSelf && (Flow.IsSelectingSkill || CanAct && Flow.CanUseWeaponSkill);
        var skillData = player.CurrentWeapon != null ? player.CurrentWeapon.Skill : null;
        if (skillLabel != null) skillLabel.text = skillData != null
            ? skillData.SkillName + "\n<size=70%>消耗 " + skillData.DeterminationCost + " 决心</size>" : "技能";
        RefreshActionHighlights();
        roundText.text = "第 " + Flow.RoundNumber + " 回合";
        if (showingItems && popup.activeSelf) RefreshItems();
        if (SelectedEnemy == null || SelectedEnemy.IsDead)
        {
            SelectedEnemy = null;
            foreach (var enemy in Flow.Enemies) if (enemy != null && !enemy.IsDead) { SelectedEnemy = enemy; break; }
        }
        foreach (var row in rows) row.Refresh();
        RefreshOrder();
    }
    public void RefreshActionHighlights()
    {
        var hovered = actionTooltip != null ? actionTooltip.ActiveButton : null;
        bool targeting = Flow != null && Flow.CurrentState == BattleState.SelectingTarget;
        if (attack != null) attack.GetComponent<BattleHudGraphic>()?.Highlight(hovered == attack
            || targeting && !Flow.IsSelectingSkill && !Flow.IsSelectingLantern);
        if (defend != null) defend.GetComponent<BattleHudGraphic>()?.Highlight(hovered == defend);
        if (skill != null) skill.GetComponent<BattleHudGraphic>()?.Highlight(hovered == skill || Flow != null && Flow.IsSelectingSkill);
    }
    void RefreshOrder()
    {
        order.Clear();
        foreach (var enemy in Flow.Enemies) if (enemy != null && !enemy.IsDead) order.Add(enemy);
        // Stable priority ordering, matching TurnManager for tied enemies.
        for (int i = 1; i < order.Count; i++)
        {
            var unit = order[i]; int j = i - 1;
            while (j >= 0 && ((EnemyCombatant)order[j]).Priority < ((EnemyCombatant)unit).Priority)
            { order[j + 1] = order[j]; j--; }
            order[j + 1] = unit;
        }
        if (!Flow.Player.IsDead) order.Insert(0, Flow.Player);
        int start = Mathf.Max(0, order.IndexOf(Flow.ActiveCombatant));
        for (int i = 0; i < turnSlots.Length; i++)
        {
            bool visible = order.Count > 0;
            turnSlots[i].frame.gameObject.SetActive(visible);
            if (!visible) continue;
            var actor = order[(start + i) % order.Count];
            turnSlots[i].portrait.sprite = actor == Flow.Player ? playerPortrait.sprite : PortraitOf(actor);
            turnSlots[i].frame.Highlight(i == 0 && Flow.ActiveCombatant != null);
        }
    }
    public static Sprite PortraitOf(Combatant actor)
    {
        var animation = actor != null ? actor.GetComponent<EnemySpriteAnimation>() : null;
        if (animation != null && animation.HudPortrait != null) return animation.HudPortrait;
        var renderer = actor != null ? actor.GetComponentInChildren<SpriteRenderer>() : null;
        return renderer != null ? renderer.sprite : null;
    }
    public static void SetHealth(Image fill, int current, int maximum)
    { fill.rectTransform.localScale = new Vector3(maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0, 1, 1); }
    public static void OutlineNumber(TMP_Text text)
    { text.outlineColor = new Color32(20, 19, 18, 255); text.outlineWidth = .18f; }
    void UseSkill()
    {
        if (Flow == null || popup.activeSelf) return;
        if (Flow.IsSelectingSkill) Flow.CancelTargetSelection();
        else if (CanAct) Flow.PlayerSkill();
    }
    void ShowItems()
    {
        if (!CanAct) return;
        OpenPopup("道具 · 成功使用占用本回合", "");
        showingItems = true;
        popupBody.gameObject.SetActive(false);
        if (itemButtons.Count == 0)
            for (int i = 0; i < ItemTable.All.Length; i++)
            {
                string id = ItemTable.All[i].id;
                itemButtons.Add(ItemUi.Button(popupBody.transform.parent, popupBody.font,
                    "Use_" + id, new Vector2(0, 66 - i * 74), new Vector2(490, 66), () => UseItem(id)));
                var itemButton = itemButtons[itemButtons.Count - 1];
                itemButton.GetComponent<Image>().color = new Color32(66, 54, 43, 255);
                itemButton.GetComponentInChildren<TMP_Text>().color = new Color32(225, 212, 185, 255);
            }
        RefreshItems();
    }

    void RefreshItems()
    {
        for (int i = 0; i < itemButtons.Count; i++)
        {
            var item = ItemTable.All[i];
            var state = Flow.ItemState;
            int count = item.id == ItemTable.Lantern ? state.player.lanternCharges : ItemRules.Count(state.inventory, item.id);
            var check = Flow.CheckItem(item.id, item.id == ItemTable.Lantern);
            var button = itemButtons[i];
            button.gameObject.SetActive(true); button.interactable = check.ok;
            button.GetComponentInChildren<TMP_Text>().text = item.displayName + (item.id == ItemTable.Lantern
                ? "  " + count + "/" + state.player.maxLanternCharges : "  × " + count)
                + "\n" + (check.ok ? item.description : check.message);
        }
    }

    void UseItem(string id)
    {
        ClosePopup();
        Flow.PlayerUseItem(id);
        Refresh();
    }
    void OpenPopup(string title, string body)
    { actionTooltip.Hide(); showingItems = false; foreach (var button in itemButtons) button.gameObject.SetActive(false);
        popupBody.gameObject.SetActive(true); popupTitle.text = title; popupBody.text = body;
        popup.SetActive(true); popup.transform.SetAsLastSibling(); wheel.SetLocked(true); Refresh(); }
    void ClosePopup() { showingItems = false; if (popup != null) popup.SetActive(false); if (wheel != null) wheel.SetLocked(false); }
    void OnDestroy()
    {
        if (Flow != null) Flow.OnBattleStateChanged -= StateChanged;
        if (!ReferenceEquals(healthSource, null)) healthSource.OnHpChanged -= RefreshPlayerHealth;
    }
}
