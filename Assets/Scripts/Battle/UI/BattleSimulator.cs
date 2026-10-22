using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-1500)]
public sealed class BattleSimulator : MonoBehaviour
{
    [Serializable] public sealed class EnemyOption
    {
        public string label;
        public GameObject prefab;
    }
    public WeaponLibrary weaponLibrary;
    public BattleSimulatorRound roundPrefab;
    public EnemyOption[] enemies;
    public BattleHudView hudPrefab;
    public TMP_FontAsset font;
    public float groundY = -3.05f;
    readonly List<WeaponData> weapons = new List<WeaponData>();
    readonly List<Button> weaponButtons = new List<Button>(), enemyButtons = new List<Button>();
    BattleSimulatorRound currentRound;
    BattleHudView hud;
    EnemyCombatant enemy;
    GameRoot root;
    GameState previousState;
    BattleFlowController previousBattle;
    bool ownsRoot, ready;
    int weaponIndex, enemyIndex, generation;
    TMP_Text heading, status;
    Canvas controls;
    public IReadOnlyList<WeaponData> Weapons => weapons;
    public int SelectedWeapon => weaponIndex;
    public int SelectedEnemy => enemyIndex;
    public BattleFlowController Flow => currentRound != null ? currentRound.flow : null;
    public EnemyCombatant Enemy => enemy;
    public bool Ready => ready;

    void Awake()
    {
        root = GameRoot.I;
        if (root == null) { root = new GameObject("BattleSimulatorRun").AddComponent<GameRoot>(); ownsRoot = true; }
        previousState = root.state; previousBattle = root.activeBattle;
        var ids = new HashSet<string>();
        if (weaponLibrary != null && weaponLibrary.weapons != null)
            foreach (var weapon in weaponLibrary.weapons)
                if (weapon != null && WeaponTable.Find(weapon.WeaponId) != null && ids.Add(weapon.WeaponId)) weapons.Add(weapon);
        BuildControls();
    }
    void Start() => RestartBattle();

    public void SelectWeapon(int index)
    {
        if (index < 0 || index >= weapons.Count) return;
        weaponIndex = index; RestartBattle();
    }
    public void SelectEnemy(int index)
    {
        if (enemies == null || index < 0 || index >= enemies.Length || enemies[index].prefab == null) return;
        enemyIndex = index; RestartBattle();
    }
    public void NextWeapon() { if (weapons.Count > 0) SelectWeapon((weaponIndex + 1) % weapons.Count); }
    public void NextEnemy() { if (enemies != null && enemies.Length > 0) SelectEnemy((enemyIndex + 1) % enemies.Length); }

    public void RestartBattle()
    {
        if (roundPrefab == null || hudPrefab == null || weapons.Count == 0 || enemies == null || enemies.Length == 0) return;
        ++generation; StopAllCoroutines(); ready = false;
        if (hud != null) { hud.gameObject.SetActive(false); Destroy(hud.gameObject); hud = null; }
        if (currentRound != null) { currentRound.gameObject.SetActive(false); Destroy(currentRound.gameObject); currentRound = null; }
        // Corpses detach from dead actors to finish playing. A new comparison starts with a clean stage.
        EnemyDefeatSpritePlayback.ClearIn(gameObject.scene);
        enemy = null;
        root.state = new GameState();
        root.state.player.hp = root.state.player.maxHp = 1000;
        root.state.player.resolve = root.state.player.maxResolve = StatusRules.DefaultMaxResolve;
        root.state.player.currentWeaponId = weapons[weaponIndex].WeaponId;
        foreach (var weapon in weapons) if (!root.state.inventory.weapons.Contains(weapon.WeaponId)) root.state.inventory.weapons.Add(weapon.WeaponId);
        root.state.inventory.items.Add(new ItemStack(ItemTable.Bandage, 1));
        root.state.inventory.items.Add(new ItemStack(ItemTable.HolyWater, 1));
        currentRound = Instantiate(roundPrefab, transform);
        currentRound.name = "SimulationRound";
        for (int i = 0; i < EquipmentRules.SlotCount; i++) currentRound.loadout.EquipWeapon(i, i == 0 ? weapons[weaponIndex] : null);
        currentRound.player.EquipWeapon(weapons[weaponIndex]);
        var actor = Instantiate(enemies[enemyIndex].prefab, currentRound.transform);
        actor.name = enemies[enemyIndex].label;
        enemy = actor.GetComponent<EnemyCombatant>();
        PositionEnemy(actor);
        currentRound.flow.ConfigureParticipants(currentRound.player, new[] { enemy });
        currentRound.gameObject.SetActive(true);
        UpdateHighlights();
        StartCoroutine(FinishSetup(generation));
    }

    IEnumerator FinishSetup(int version)
    {
        // Actor OnEnable builds the visual meshes; Start initializes health and intent before HUD binding.
        yield return null;
        if (version != generation || currentRound == null) yield break;
        hud = Instantiate(hudPrefab, currentRound.transform);
        hud.name = "SimulationHUD";
        hud.Bind(currentRound.flow, currentRound.loadout, currentRound.switching);
        hud.attack.onClick.RemoveAllListeners(); hud.attack.onClick.AddListener(Attack);
        hud.skill.onClick.RemoveAllListeners(); hud.skill.onClick.AddListener(Skill);
        hud.roundText.transform.parent.gameObject.SetActive(false);
        var animation = currentRound.player.GetComponent<PlayerAttackAnimation>();
        if (animation == null || !animation.Supports(currentRound.player.CurrentWeapon))
            heading.text += "（现有基础表现）";
        ready = true;
    }

    void PositionEnemy(GameObject actor)
    {
        var view = actor.GetComponent<EnemySpriteAnimation>();
        if (view != null && view.animations != null)
        {
            var set = view.animations; var sprite = set.Portrait; var b = set.artworkBounds;
            float width = sprite.rect.width / sprite.pixelsPerUnit;
            float height = sprite.rect.height / sprite.pixelsPerUnit;
            float pivotX = sprite.pivot.x / sprite.pixelsPerUnit, pivotY = sprite.pivot.y / sprite.pixelsPerUnit;
            float scale = 3.1f / ((b.w - b.y) * height * view.visualScale.y);
            actor.transform.localScale = new Vector3(scale, scale, 1);
            actor.transform.localPosition = new Vector3(4.6f - (view.visualOffset.x + ((b.x + b.z) * .5f * width - pivotX) * view.visualScale.x) * scale,
                groundY - (view.visualOffset.y + (b.y * height - pivotY) * view.visualScale.y) * scale, 0);
        }
        else
        {
            var collider = actor.GetComponent<BoxCollider2D>();
            Vector2 center = collider != null ? collider.offset : Vector2.zero;
            float foot = collider != null ? center.y - collider.size.y * .5f : 0f;
            actor.transform.localPosition = new Vector3(4.6f - center.x * actor.transform.localScale.x,
                groundY - foot * actor.transform.localScale.y, 0);
        }
    }

    public void Attack()
    {
        if (!ready || Flow == null || enemy == null) return;
        Flow.PlayerAttack();
        if (Flow.CurrentState == BattleState.SelectingTarget) Flow.AttackTarget(enemy);
    }
    public void Skill()
    {
        if (!ready || Flow == null || enemy == null) return;
        Flow.PlayerSkill();
        if (Flow.CurrentState == BattleState.SelectingTarget) Flow.AttackTarget(enemy);
    }
    public void Defend() { if (ready && Flow != null) Flow.PlayerDefend(); }
    public void DefeatEnemy()
    {
        if (!ready || enemy == null || enemy.IsDead || Flow == null) return;
        if (Flow.CurrentState == BattleState.SelectingTarget) Flow.CancelTargetSelection();
        enemy.TakeDamage(enemy.CurrentHP);
        if (Flow.CurrentState == BattleState.PlayerTurn) Flow.PlayerDefend();
    }

    void Update()
    {
        if (status == null) return;
        if (!ready || Flow == null) { status.text = "准备中…"; return; }
        string text = Flow.CurrentState == BattleState.Victory ? "战斗胜利 · 可以重新开战或切换配置"
            : Flow.CurrentState == BattleState.Defeat ? "战斗失败 · 可以重新开战或切换配置"
            : Flow.CurrentState == BattleState.PlayerTurn ? "玩家回合" : "行动中";
        status.text = "第 " + Flow.RoundNumber + " 回合 · " + text + "    玩家 " + Flow.Player.CurrentHP + "/" + Flow.Player.MaxHP
            + (enemy != null ? "    敌人 " + enemy.CurrentHP + "/" + enemy.MaxHP : "");
    }

    void UpdateHighlights()
    {
        heading.text = "战斗模拟    " + weapons[weaponIndex].WeaponName + "  ×  " + enemies[enemyIndex].label;
        for (int i = 0; i < weaponButtons.Count; i++) weaponButtons[i].image.color = i == weaponIndex ? new Color32(100, 76, 42, 255) : new Color32(40, 48, 61, 255);
        for (int i = 0; i < enemyButtons.Count; i++) enemyButtons[i].image.color = i == enemyIndex ? new Color32(100, 76, 42, 255) : new Color32(40, 48, 61, 255);
    }

    void BuildControls()
    {
        var canvas = new GameObject("SimulatorControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, false); controls = canvas.GetComponent<Canvas>();
        controls.renderMode = RenderMode.ScreenSpaceOverlay; controls.sortingOrder = 200;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var panel = new GameObject("Configuration", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<Image>().color = new Color32(20, 25, 33, 245);
        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -12); rect.sizeDelta = new Vector2(910, 254);
        heading = Label(panel.transform, "战斗模拟", new Vector2(15, -8), new Vector2(880, 32), 23);
        for (int i = 0; i < weapons.Count; i++)
        {
            int index = i;
            weaponButtons.Add(Button(panel.transform, "Weapon_" + weapons[i].WeaponId, weapons[i].WeaponName,
                new Vector2(15 + i % 6 * 146, -47 - i / 6 * 40), new Vector2(138, 34), () => SelectWeapon(index)));
        }
        for (int i = 0; enemies != null && i < enemies.Length; i++)
        {
            int index = i;
            enemyButtons.Add(Button(panel.transform, "Enemy_" + i, enemies[i].label,
                new Vector2(15 + i * 146, -131), new Vector2(138, 34), () => SelectEnemy(index)));
        }
        Button(panel.transform, "NextWeapon", "下一把武器", new Vector2(601, -131), new Vector2(138, 34), NextWeapon);
        Button(panel.transform, "NextEnemy", "下一个敌人", new Vector2(747, -131), new Vector2(138, 34), NextEnemy);
        Label(panel.transform, "切换即重开对局 · 仅一个敌人", new Vector2(15, -173), new Vector2(870, 25), 18);
        status = Label(panel.transform, "准备中…", new Vector2(15, -210), new Vector2(870, 28), 19);
        var actions = new GameObject("SimulationActions", typeof(RectTransform)); actions.transform.SetParent(canvas.transform, false);
        var actionRect = actions.GetComponent<RectTransform>(); actionRect.anchorMin = actionRect.anchorMax = new Vector2(.5f, 0);
        actionRect.pivot = new Vector2(.5f, 0); actionRect.anchoredPosition = new Vector2(0, 285); actionRect.sizeDelta = new Vector2(385, 42);
        Button(actions.transform, "Death", "查看敌人死亡", new Vector2(0, 0), new Vector2(185, 40), DefeatEnemy);
        Button(actions.transform, "Restart", "重新开战", new Vector2(200, 0), new Vector2(185, 40), RestartBattle);
    }

    TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = SharedUIFont.Get(font);
        label.text = text; label.fontSize = fontSize; label.color = new Color32(232, 228, 216, 255);
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        return label;
    }
    Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); button.image.color = new Color32(40, 48, 61, 255);
        button.onClick.AddListener(action);
        var label = Label(go.transform, text, Vector2.zero, size, 19); label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    void OnDestroy()
    {
        if (root == null) return;
        if (ownsRoot) Destroy(root.gameObject);
        else { root.state = previousState; root.activeBattle = previousBattle; }
    }
}
