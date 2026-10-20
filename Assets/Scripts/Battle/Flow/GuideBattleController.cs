using TMPro;
using UnityEngine;

public enum GuideBattleStep { Switch, Lantern, Resolve, Break, Items, Free }

public sealed class GuideBattleController : MonoBehaviour
{
    BattleFlowController flow;
    VillageIntroContent content;
    DialogueData pending;
    string initialWeapon;
    TMP_Text goal;
    bool broke;
    public GuideBattleStep Step { get; private set; }
    public GuideDialogueView View { get; private set; }
    public string Instruction => Step == GuideBattleStep.Switch ? "点击左下箭头，切换一次武器。"
        : Step == GuideBattleStep.Lantern ? "打开道具，使用提灯并选择敌人。"
        : Step == GuideBattleStep.Resolve ? "进行普通攻击或防御，积攒决心。"
        : Step == GuideBattleStep.Break ? "命中弱点，令敌人触发破防。" : "按需使用道具，击败旧磨坊的怪物。";
    public static GuideBattleController Attach(BattleFlowController battle)
    {
        var root = GameRoot.I;
        if (root == null || root.battleRequestId != root.state.map.pendingRequestId
            || !VillageIntroRules.IsTutorialBattle(root.state, root.state.map.pendingNodeId)) return null;
        var guide = battle.gameObject.AddComponent<GuideBattleController>(); guide.flow = battle;
        guide.content = Resources.Load<VillageIntroContent>(VillageIntroController.ContentResource);
        guide.View = GuideDialogueView.Create(guide.transform, guide.content);
        guide.initialWeapon = root.state.player.currentWeaponId;
        guide.PrepareNextLesson();
        battle.Player.OnWeaponChanged += guide.WeaponChanged;
        foreach (var enemy in battle.Enemies) if (enemy?.Break != null) enemy.Break.Changed += guide.BreakChanged;
        var canvas = GuideUI.Canvas(guide.transform, "BattleGuideObjective", 45);
        var card = GuideUI.Image("Card", canvas.transform, new Color32(245, 243, 235, 245));
        GuideUI.Center(card.rectTransform, new Vector2(0, 330), new Vector2(760, 62)); card.raycastTarget = false;
        guide.goal = GuideUI.Label("Goal", card.transform, "", Vector2.zero, new Vector2(730, 54), guide.content.font, 24);
        return guide;
    }
    public bool Allows(string action)
    {
        if (View.IsOpen) return false;
        if (action == "switch") return true;
        if (Step == GuideBattleStep.Switch) return false;
        if (Step == GuideBattleStep.Lantern) return action == "items" || action == ItemTable.Lantern;
        if (Step == GuideBattleStep.Resolve) return action == "attack" || action == "defend";
        return true;
    }
    void Update()
    {
        goal.text = "提灯人的第一课 · " + Instruction;
        goal.transform.parent.gameObject.SetActive(!View.IsOpen && flow.CurrentState != BattleState.Victory
            && flow.CurrentState != BattleState.Defeat);
    }
    void WeaponChanged(WeaponData weapon)
    {
        if (Step != GuideBattleStep.Switch || View.IsOpen || weapon == null || weapon.WeaponId == initialWeapon
            || flow.CurrentState != BattleState.PlayerTurn) return;
        GameRoot.I.state.story.guideLessons |= GuideLesson.Switch;
        PrepareNextLesson(); ReadyForPlayer();
    }
    void BreakChanged()
    {
        foreach (var enemy in flow.Enemies) if (enemy?.Break != null && enemy.Break.SkipPending) broke = true;
    }
    public void ActionCompleted(string action)
    {
        if (Step == GuideBattleStep.Lantern && action == ItemTable.Lantern)
        { GameRoot.I.state.story.guideLessons |= GuideLesson.Lantern; PrepareNextLesson(); }
        else if (Step == GuideBattleStep.Resolve && (action == "attack" || action == "defend"))
        { GameRoot.I.state.story.guideLessons |= GuideLesson.Resolve; PrepareNextLesson(); }
        else if (Step == GuideBattleStep.Break && broke)
        { GameRoot.I.state.story.guideLessons |= GuideLesson.Break; PrepareNextLesson(); }
    }
    void PrepareNextLesson()
    {
        var story = GameRoot.I.state.story;
        // Species discovery survives defeat. Do not demand a lantern use that can no longer have an effect.
        bool unknown = false;
        foreach (var enemy in flow.Enemies)
            if (enemy != null && enemy.GetComponent<DamageResistance>()?.UnknownWeaknessCount > 0) unknown = true;
        if (!unknown) story.guideLessons |= GuideLesson.Lantern;
        if ((story.guideLessons & GuideLesson.Switch) == 0) { Step = GuideBattleStep.Switch; pending = content.switchTutorial; }
        else if ((story.guideLessons & GuideLesson.Lantern) == 0) { Step = GuideBattleStep.Lantern; pending = content.lanternTutorial; }
        else if ((story.guideLessons & GuideLesson.Resolve) == 0) { Step = GuideBattleStep.Resolve; pending = content.resolveTutorial; }
        else if ((story.guideLessons & GuideLesson.Break) == 0) { Step = GuideBattleStep.Break; pending = content.breakTutorial; }
        else if ((story.guideLessons & GuideLesson.Items) == 0) { Step = GuideBattleStep.Items; pending = content.itemTutorial; }
        else { Step = GuideBattleStep.Free; pending = null; }
    }
    public void ReadyForPlayer()
    {
        if (pending == null || View.IsOpen) return;
        var data = pending; pending = null;
        View.Play(data, null, _ =>
        {
            if (Step != GuideBattleStep.Items) return;
            GameRoot.I.state.story.guideLessons |= GuideLesson.Items;
            PrepareNextLesson();
        });
    }
    void OnDestroy()
    {
        if (flow == null) return;
        if (flow.Player != null) flow.Player.OnWeaponChanged -= WeaponChanged;
        foreach (var enemy in flow.Enemies) if (enemy?.Break != null) enemy.Break.Changed -= BreakChanged;
    }
}
