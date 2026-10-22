using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The complete guide shares the current run with the existing village.
public sealed class VillageIntroController : MonoBehaviour
{
    public const string ContentResource = "VillageIntroduction";
    VillageScreen owner;
    VillageIntroContent content;
    GuideDialogueView view;
    GameObject navigation;
    TMP_Text objective;
    DialogueData activeDialogue;
    readonly Dictionary<Button, bool> disabledButtons = new Dictionary<Button, bool>();
    public bool IsOpen => view != null && view.IsOpen;
    public bool IsPaused => view != null && view.IsPaused;
    public DialogueManager Manager => view != null ? view.Manager : null;
    public DialogueData ActiveDialogue => activeDialogue;
    public Sprite Background => view != null ? view.Background : null;
    public Button MayorButton { get; private set; }
    public Button BlacksmithButton { get; private set; }
    public static VillageIntroController Attach(VillageScreen screen)
    {
        if (!VillageIntroRules.IsGuideRun(GameRoot.I != null ? GameRoot.I.state : null)) return null;
        var data = Resources.Load<VillageIntroContent>(ContentResource);
        if (data == null || data.dialoguePrefab == null || data.arrivalDialogue == null || data.streetDialogue == null)
        { Debug.LogError("Village guide content is missing.", screen); return null; }
        var controller = screen.gameObject.AddComponent<VillageIntroController>();
        controller.owner = screen; controller.content = data; return controller;
    }
    void Start()
    {
        view = GuideDialogueView.Create(transform, content); BuildNavigation(); RefreshFacilities();
        var state = GameRoot.I.state;
        if (VillageIntroRules.IsPending(state)) Play(content.arrivalDialogue, content.arrivalBackground);
        else if (VillageIntroRules.NeedsReport(state) && !state.story.completedDialogueIds.Contains(content.returnDialogue.dialogueId))
            Play(content.returnDialogue, content.streetBackground);
        else RefreshNavigation();
    }
    void Update() { if (navigation != null) RefreshNavigation(); }
    void RefreshNavigation()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        navigation.SetActive(!IsOpen && !SlothChapterPresentation.BlocksVillage
            && VillageIntroRules.IsGuideRun(state) && !owner.IsLoading
            && !owner.IsResting && (owner.panelRoot == null || !owner.panelRoot.activeSelf));
        objective.text = MainStoryRules.GoalText(state);
        MayorButton.gameObject.SetActive(VillageIntroRules.IsAwaitingMayor(state) || VillageIntroRules.NeedsReport(state));
        BlacksmithButton.gameObject.SetActive(VillageIntroRules.NeedsBlacksmith(state) || state.story.guideCompleted);
        BlacksmithButton.GetComponentInChildren<TMP_Text>().text = state.story.guideCompleted
            ? "西城区铁匠铺\n购买装备" : "西城区铁匠铺\n领取武器";
    }
    void Play(DialogueData dialogue, Sprite sprite, string startNodeId = null)
    {
        if (IsOpen || dialogue == null) return;
        owner.ClosePanels(); activeDialogue = dialogue; navigation.SetActive(false);
        view.Play(dialogue, sprite, OnDialogueCompleted, startNodeId);
    }
    void OnDialogueCompleted(DialogueData dialogue)
    {
        activeDialogue = null;
        if (dialogue == content.arrivalDialogue) { Play(content.streetDialogue, content.streetBackground); return; }
        owner.RefreshStatus(); RefreshFacilities(); RefreshNavigation();
        var focus = MayorButton.gameObject.activeInHierarchy ? MayorButton : BlacksmithButton;
        if (focus.gameObject.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(focus.gameObject);
    }
    public void TogglePause() { if (view != null) view.TogglePause(); }
    public void OpenMayorHouse()
    {
        if (IsOpen || owner.IsLoading || owner.IsResting || SlothChapterPresentation.BlocksVillage) return;
        if (VillageIntroRules.IsAwaitingMayor(GameRoot.I.state)) Play(content.mayorApproachDialogue, content.mayorBackground);
        else if (VillageIntroRules.NeedsReport(GameRoot.I.state)) Play(content.reportDialogue, content.mayorBackground);
    }
    public void OpenBlacksmith()
    {
        if (SlothChapterPresentation.BlocksVillage) return;
        if (!IsOpen && GameRoot.I.state.story.guideCompleted) { owner.OpenShop(); return; }
        if (!IsOpen && !owner.IsLoading && !owner.IsResting && VillageIntroRules.NeedsBlacksmith(GameRoot.I.state))
            Play(content.blacksmithDialogue, content.blacksmithBackground,
                !string.IsNullOrEmpty(GameRoot.I.state.story.guideSecondWeaponId) ? "weigh" : null);
    }
    void RefreshFacilities()
    {
        foreach (var entry in disabledButtons) if (entry.Key != null) entry.Key.interactable = entry.Value;
        disabledButtons.Clear();
        var state = GameRoot.I.state;
        if (!VillageIntroRules.IsRestricted(state)) return;
        var canvas = owner.hpText != null ? owner.hpText.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return;
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) != owner) continue;
                string method = button.onClick.GetPersistentMethodName(i);
                bool blocked = method == nameof(VillageScreen.OpenInn) || method == nameof(VillageScreen.OpenShop)
                    || method == nameof(VillageScreen.OpenTavern) || method == nameof(VillageScreen.OpenEvent)
                    || method == nameof(VillageScreen.LeaveToMap) && !VillageIntroRules.CanLeaveVillage(state);
                if (!blocked) continue;
                disabledButtons[button] = button.interactable; button.interactable = false; break;
            }
    }
    void BuildNavigation()
    {
        var canvas = GuideUI.Canvas(transform, "VillageGuideObjective", 30); navigation = canvas.gameObject;
        var stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>(); stage.SetParent(canvas.transform, false);
        GuideUI.Center(stage, Vector2.zero, new Vector2(1920, 1080));
        var goal = GuideUI.Image("Objective", stage, new Color32(232, 226, 210, 230));
        GuideUI.Center(goal.rectTransform, new Vector2(0, 365), new Vector2(760, 78)); goal.raycastTarget = false;
        objective = GuideUI.Label("Goal", goal.transform, "", Vector2.zero, new Vector2(730, 58), content.font);
        MayorButton = GuideUI.Button("MayorHouse", stage, "哈罗德宅邸\n前往交谈", new Vector2(80, 170),
            new Vector2(210, 76), content.font, OpenMayorHouse);
        BlacksmithButton = GuideUI.Button("Blacksmith", stage, "西城区铁匠铺\n领取武器", new Vector2(-530, 250),
            new Vector2(230, 76), content.font, OpenBlacksmith);
        navigation.SetActive(false);
    }
}
