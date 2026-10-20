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
    RectTransform worldTag, intent, arrow, worldName;
    TMP_Text intentText, intentEffect;
    bool hasIntentEffect;
    Renderer[] renderers;
    DamageResistance resistance;

    public void Bind(EnemyCombatant unit, int index, BattleHudView view)
    {
        enemy = unit; hud = view; resistance = unit.GetComponent<DamageResistance>();
        enemyName.text = "敌人 " + index;
        portrait.sprite = BattleHudView.PortraitOf(unit);
        BattleHudView.OutlineNumber(healthText);
        select.onClick.AddListener(() => hud.SelectEnemy(enemy, true));
        renderers = unit.GetComponentsInChildren<Renderer>();
        worldTag = Instantiate(hud.worldTagTemplate, hud.Root);
        worldTag.name = "WorldTag_" + index;
        worldTag.gameObject.SetActive(true);
        intent = (RectTransform)worldTag.Find("Intent");
        arrow = (RectTransform)worldTag.Find("Arrow");
        worldName = (RectTransform)worldTag.Find("Name");
        worldName.GetComponent<TMP_Text>().text = "敌人 " + index;
        BattleHudView.OutlineNumber(worldName.GetComponent<TMP_Text>());
        intentText = intent.Find("Amount").GetComponent<TMP_Text>();
        intentEffect = intent.Find("SpecialEffect").GetComponent<TMP_Text>();
        for (int i = 0; i < weaknesses.Length; i++) weaknesses[i].sprite = hud.damageIcons[i];
        Refresh();
    }
    public void Refresh()
    {
        if (enemy == null || hud == null) return;
        BattleHudView.SetHealth(health, enemy.CurrentHP, enemy.MaxHP);
        healthText.text = enemy.CurrentHP + " / " + enemy.MaxHP;
        statusText.text = enemy.IsDead ? "已击败" : enemy.Statuses.Format() + " " + (enemy.Break != null ? enemy.Break.Format(hud.Flow.RoundNumber) : "");
        frame.Highlight(enemy == hud.SelectedEnemy && !enemy.IsDead);
        select.interactable = !enemy.IsDead && (hud.Flow.CurrentState == BattleState.PlayerTurn || hud.Flow.CurrentState == BattleState.SelectingTarget);
        int known = 0;
        for (int i = 0; i < weaknesses.Length; i++)
        {
            bool visible = resistance != null && resistance.IsWeaknessKnown((PlayerDamageType)i);
            weaknesses[i].transform.parent.gameObject.SetActive(visible);
            if (visible)
            {
                ((RectTransform)weaknesses[i].transform.parent).anchoredPosition = new Vector2(184 + known * 39, -82);
                known++;
            }
        }
        unknownWeakness.gameObject.SetActive(known == 0);
        var action = enemy.UpcomingAction;
        bool skip = enemy.Break != null && enemy.Break.SkipPending;
        string effect = !skip && action != null ? action.IntentEffectText : string.Empty;
        hasIntentEffect = !string.IsNullOrWhiteSpace(effect);
        intentEffect.gameObject.SetActive(hasIntentEffect);
        intentEffect.text = effect;
        intent.sizeDelta = new Vector2(hasIntentEffect ? 150 : 90, hasIntentEffect ? 72 : 46);
        intentText.rectTransform.anchoredPosition = new Vector2(0, hasIntentEffect ? 14 : 0);
        intentText.text = skip ? "跳过" : action == null ? "—" : action.Damage + (action.AttackCount > 1 ? "×" + action.AttackCount : "");
        worldTag.gameObject.SetActive(!enemy.IsDead);
        arrow.gameObject.SetActive(!enemy.IsDead && enemy == hud.SelectedEnemy);
    }
    void LateUpdate()
    {
        if (enemy == null || enemy.IsDead || worldTag == null || hud == null || Camera.main == null) return;
        Bounds bounds = new Bounds(enemy.transform.position, Vector3.one);
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (renderer is SpriteRenderer sprite && sprite.sprite == null) continue;
            if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
        }
        Vector2 top = ScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        Vector2 foot = ScreenPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        float halfWidth = hud.Root.rect.width * .5f, halfHeight = hud.Root.rect.height * .5f;
        float x = Mathf.Clamp(top.x, -halfWidth + 60, halfWidth - 60);
        intent.anchoredPosition = new Vector2(x, Mathf.Min(top.y + (hasIntentEffect ? 84 : 72), halfHeight - 200));
        arrow.anchoredPosition = new Vector2(x, Mathf.Min(top.y + 28, halfHeight - 245));
        worldName.anchoredPosition = new Vector2(x, Mathf.Max(foot.y - 20, -halfHeight + 185));
    }
    Vector2 ScreenPoint(Vector3 world)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(hud.Root, Camera.main.WorldToScreenPoint(world), null, out var point);
        return point;
    }
    void OnDestroy()
    {
        if (worldTag == null) return;
        if (Application.isPlaying) Destroy(worldTag.gameObject);
        else DestroyImmediate(worldTag.gameObject);
    }
}
