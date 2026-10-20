using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared by the three actions, including disabled skills so their reason is visible.
public sealed class BattleActionTooltip : MonoBehaviour
{
    public enum ActionKind { Attack, Defend, Skill }
    public Button ActiveButton { get; private set; }
    BattleHudView hud;
    ActionKind action;
    RectTransform panel;
    TMP_Text title, description;
    CanvasGroup group;
    float nextRefresh;

    public void Bind(BattleHudView view)
    {
        hud = view;
        if (panel == null)
        {
            var root = new GameObject("ActionTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic), typeof(CanvasGroup));
            root.layer = view.gameObject.layer;
            root.transform.SetParent(view.transform, false);
            panel = (RectTransform)root.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.pivot = new Vector2(.5f, 0);
            var frame = root.GetComponent<BattleHudGraphic>();
            frame.fill = new Color32(34, 28, 23, 250);
            frame.ink = new Color32(218, 188, 116, 255);
            frame.stroke = 1.5f;
            frame.raycastTarget = false;
            group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            title = Label("Title", 24, new Color32(255, 216, 123, 255));
            description = Label("Description", 21, new Color32(245, 239, 222, 255));
        }
        Hook(view.attack, ActionKind.Attack);
        Hook(view.defend, ActionKind.Defend);
        Hook(view.skill, ActionKind.Skill);
        Hide();
    }

    void Hook(Button button, ActionKind kind)
    {
        var hover = button.GetComponent<BattleActionHover>();
        if (hover == null) hover = button.gameObject.AddComponent<BattleActionHover>();
        hover.Bind(this, button, kind);
    }

    TMP_Text Label(string name, float size, Color tint)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.layer = hud.gameObject.layer;
        root.transform.SetParent(panel, false);
        var text = root.GetComponent<TextMeshProUGUI>();
        text.font = SharedUIFont.Get(hud.prompt.font);
        text.fontSize = size;
        text.color = tint;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1);
        text.rectTransform.pivot = new Vector2(0, 1);
        return text;
    }

    public void Show(Button button, ActionKind kind)
    {
        if (hud == null || !hud.CanShowActionInfo || hud.Flow.Player == null) return;
        ActiveButton = button;
        action = kind;
        RefreshContent();
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
        group.alpha = 0;
        hud.RefreshActionHighlights();
    }

    public void Hide(Button source = null)
    {
        if (source != null && source != ActiveButton) return;
        ActiveButton = null;
        if (panel != null) panel.gameObject.SetActive(false);
        if (hud != null) hud.RefreshActionHighlights();
    }

    void LateUpdate()
    {
        if (ActiveButton == null) return;
        if (hud == null || !hud.CanShowActionInfo || !ActiveButton.gameObject.activeInHierarchy)
        { Hide(); return; }
        if (Time.unscaledTime >= nextRefresh)
        { nextRefresh = Time.unscaledTime + .08f; RefreshContent(); }
        group.alpha = Mathf.MoveTowards(group.alpha, 1, Time.unscaledDeltaTime * 12f);
        PositionPanel();
    }

    void RefreshContent()
    {
        var player = hud.Flow.Player;
        var weapon = player.CurrentWeapon;
        var text = new StringBuilder();
        switch (action)
        {
            case ActionKind.Attack:
                title.text = "攻击" + (weapon != null ? " · " + WeaponTable.NameOf(weapon.WeaponId) : "");
                if (weapon == null) text.Append("未装备武器，无法攻击。");
                else
                {
                    text.Append(player.AttackCount).Append(" 段攻击\n每段基础伤害：");
                    bool first = true;
                    foreach (var part in weapon.DamageComponents)
                    {
                        if (part.damage <= 0) continue;
                        if (!first) text.Append(" + ");
                        text.Append(part.damage).Append(' ').Append(InventoryDisplayRules.DamageNames[(int)part.damageType]);
                        first = false;
                    }
                    text.Append("\n").Append(ResolveEffect(player));
                }
                break;
            case ActionKind.Defend:
                title.text = "防御";
                text.Append("受到的直接伤害降低 ").Append(Mathf.RoundToInt((1f - player.DefenseDamageRate) * 100))
                    .Append("%，持续至下次玩家回合。\n").Append(ResolveEffect(player));
                break;
            default:
                var skill = weapon != null ? weapon.Skill : null;
                title.text = skill != null ? skill.SkillName : "武器技能";
                if (skill == null) text.Append("当前武器未配置技能。");
                else
                {
                    text.Append("消耗 ").Append(skill.DeterminationCost).Append(" 点决心 · 当前 ").Append(player.CurrentDetermination)
                        .Append('\n').Append(skill.Description);
                    if (!hud.Flow.IsSelectingSkill && hud.Flow.SkillUnavailableReason != null)
                        text.Append("\n<color=#FFBE91>").Append(hud.Flow.SkillUnavailableReason).Append("</color>");
                }
                break;
        }
        description.text = text.ToString();
        const float width = 420;
        float titleHeight = Mathf.Max(32, title.GetPreferredValues(title.text, width - 40, 0).y);
        float bodyHeight = description.GetPreferredValues(description.text, width - 40, 0).y;
        panel.sizeDelta = new Vector2(width, titleHeight + bodyHeight + 48);
        title.rectTransform.anchoredPosition = new Vector2(20, -16);
        title.rectTransform.sizeDelta = new Vector2(width - 40, titleHeight);
        description.rectTransform.anchoredPosition = new Vector2(20, -titleHeight - 26);
        description.rectTransform.sizeDelta = new Vector2(width - 40, bodyHeight + 4);
        PositionPanel();
    }

    static string ResolveEffect(PlayerCombatant player)
    {
        var grant = StatusRules.GrantResolve(player.CurrentDetermination, player.MaxDetermination, 1,
            player.Statuses.Has(StatusId.Exhaust), player.Statuses.Stacks(StatusId.Steadfast));
        if (player.Statuses.Has(StatusId.Exhaust)) return "疲惫：本次无法获得决心。";
        if (grant.Granted == 0) return "完成行动后获得 1 点决心（当前已满）。";
        return "完成行动后获得 " + grant.Granted + " 点决心" + (grant.ConsumeSteadfast ? "（坚定，消耗 1 层）。" : "。");
    }

    void PositionPanel()
    {
        if (ActiveButton == null) return;
        var anchor = (RectTransform)ActiveButton.transform;
        Vector3 point = hud.Root.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMax, 0)));
        Rect root = hud.Root.rect;
        float halfWidth = panel.sizeDelta.x * .5f;
        panel.localPosition = new Vector3(Mathf.Clamp(point.x, root.xMin + halfWidth + 16, root.xMax - halfWidth - 16),
            Mathf.Clamp(point.y + 18, root.yMin + 16, root.yMax - panel.sizeDelta.y - 16), 0);
    }

    void OnDisable() => Hide();
    void OnApplicationFocus(bool focused) { if (!focused) Hide(); }
}
