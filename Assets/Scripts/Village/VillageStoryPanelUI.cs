using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Village story entry and current objective; dialogue completion rules own all progression.
public class VillageStoryPanelUI : MonoBehaviour
{
    public WorldMapTemplate template;
    public DialogueManager dialogueManager;
    public TMP_Text questText;
    public TMP_Text talkLabel;
    public TMP_Text messageText;
    public Button talkButton;
    public GameObject eventListRoot;
    public bool externalInteraction;
    public DialogueData welcomeDialogue;
    public DialogueData commissionDialogue;
    public DialogueData afterTutorialDialogue;
    public DialogueData turnInDialogue;
    public DialogueData afterTurnInDialogue;

    float nextRefresh;

    void Awake()
    {
        if (!externalInteraction && talkButton != null) talkButton.onClick.AddListener(TalkToVillage);
    }

    void OnEnable()
    {
        if (dialogueManager != null) dialogueManager.DialogueCompleted += OnDialogueCompleted;
        nextRefresh = 0;
        Refresh();
    }

    void Start()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state != null) MainStoryRules.EnsureInitialized(state, template != null ? template.Load() : null);
        Refresh();
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.15f;
        Refresh();
    }

    public void TalkToVillage()
    {
        if (!isActiveAndEnabled) return;
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state == null || dialogueManager == null || dialogueManager.IsPlaying) return;
        MainStoryRules.EnsureInitialized(state, null);
        DialogueData data;
        switch (state.story.stage)
        {
            case MainStoryStage.ArrivedVillage: data = welcomeDialogue; break;
            case MainStoryStage.VillageIntroductionComplete: data = commissionDialogue; break;
            case MainStoryStage.CommissionAccepted: data = commissionDialogue; break;
            case MainStoryStage.TutorialCompleted: data = afterTutorialDialogue; break;
            case MainStoryStage.SlothDefeated: data = turnInDialogue; break;
            case MainStoryStage.CommissionTurnedIn: data = afterTurnInDialogue; break;
            default: data = welcomeDialogue; break;
        }
        if (data == null)
        {
            if (messageText != null) messageText.text = "当前对话尚未配置。";
            return;
        }
        if (messageText != null) messageText.text = "";
        dialogueManager.StartDialogue(data);
        Refresh();
        FocusPreferredControl();
    }

    public void Refresh()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (questText != null) questText.text = "当前目标\n" + MainStoryRules.GoalText(state);
        bool busy = dialogueManager != null && dialogueManager.IsPlaying;
        if (eventListRoot != null) eventListRoot.SetActive(!busy);
        if (!externalInteraction && talkButton != null) talkButton.interactable = state != null && !busy;
        if (!externalInteraction && talkLabel != null) talkLabel.text = state == null || state.story == null
            || state.story.stage == MainStoryStage.ArrivedVillage ? "与村民交谈" : "与村长交谈";
    }

    public GameObject GetPreferredFocus()
    {
        if (dialogueManager != null && dialogueManager.IsPlaying)
        {
            var ui = dialogueManager.ui;
            if (ui == null) return null;
            if (dialogueManager.HasChoice() && ui.choiceParent != null)
            {
                foreach (var button in ui.choiceParent.GetComponentsInChildren<Button>(false))
                    if (button.interactable) return button.gameObject;
            }
            return ui.continueButton != null ? ui.continueButton.gameObject : null;
        }
        return talkButton != null ? talkButton.gameObject : null;
    }

    public bool ReturnToEventList()
    {
        if (dialogueManager == null || !dialogueManager.IsPlaying) return false;
        dialogueManager.CancelDialogue();
        Refresh();
        FocusPreferredControl();
        return true;
    }

    void FocusPreferredControl()
    {
        var target = GetPreferredFocus();
        if (EventSystem.current != null && target != null && target.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(target);
    }

    void OnDialogueCompleted(DialogueData data)
    {
        Refresh();
        FocusPreferredControl();
    }

    void OnDisable()
    {
        if (dialogueManager != null)
        {
            dialogueManager.DialogueCompleted -= OnDialogueCompleted;
            dialogueManager.CancelDialogue();
        }
    }
}
