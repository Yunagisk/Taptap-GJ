using TMPro;
using UnityEngine;
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
    public DialogueData welcomeDialogue;
    public DialogueData commissionDialogue;
    public DialogueData afterTutorialDialogue;
    public DialogueData turnInDialogue;
    public DialogueData afterTurnInDialogue;

    float nextRefresh;

    void Awake()
    {
        if (talkButton != null) talkButton.onClick.AddListener(TalkToVillage);
    }

    void OnEnable()
    {
        if (dialogueManager != null) dialogueManager.DialogueCompleted += OnDialogueCompleted;
        nextRefresh = 0;
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
    }

    public void Refresh()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (questText != null) questText.text = "当前目标\n" + MainStoryRules.GoalText(state);
        bool busy = dialogueManager != null && dialogueManager.IsPlaying;
        if (talkButton != null) talkButton.interactable = state != null && !busy;
        if (talkLabel != null) talkLabel.text = state == null || state.story == null
            || state.story.stage == MainStoryStage.ArrivedVillage ? "与村民交谈" : "与村长交谈";
    }

    void OnDialogueCompleted(DialogueData data) { Refresh(); }

    void OnDisable()
    {
        if (dialogueManager != null) dialogueManager.DialogueCompleted -= OnDialogueCompleted;
    }
}
