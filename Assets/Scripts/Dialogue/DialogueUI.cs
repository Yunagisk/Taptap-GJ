using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour, IPointerClickHandler
{
    public TMP_Text speakerText;
    public TMP_Text contentText;
    public Image avatarImage;
    public Transform choiceParent;
    public Button choicePrefab;
    public Button continueButton;
    public DialogueManager manager;
    public GameObject visibleRoot;

    public int CurrentPage { get; private set; } = 1;
    public int PageCount { get; private set; } = 1;
    public bool CanChoose => current != null && CurrentPage >= PageCount && HasValidChoices();

    // Real time keeps dialogue usable while battle/tutorial time is paused.
    public bool CanAcceptInput => isActiveAndEnabled && current != null
        && (visibleRoot == null || visibleRoot.activeInHierarchy)
        && Time.unscaledTimeAsDouble >= inputAllowedAt && GameAudio.UiConfirmReady;

    private DialogueManager Owner => manager != null ? manager : DialogueManager.Instance;
    readonly List<Button> choiceButtons = new List<Button>();
    RpgDialogueLayout layout;
    DialogueNode current;
    GameObject previousSelection;
    int lastInputFrame = -1;
    double inputAllowedAt;

    void Awake() { layout = RpgDialogueLayout.Ensure(this); }

    private void OnEnable()
    {
        if (continueButton != null)
        {
            GameAudioButton.UseManualConfirmation(continueButton);
            continueButton.onClick.AddListener(Advance);
        }
    }

    private void OnDisable()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Advance);
    }

    void Update()
    {
        if (current == null || visibleRoot == null || !visibleRoot.activeInHierarchy) return;
        // Submit (including Space) and arrow navigation use the EventSystem.
        if (!CanChoose || !CanAcceptInput) return;
        for (int i = 0; i < choiceButtons.Count && i < 9; i++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))
                || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
            {
                choiceButtons[i].onClick.Invoke();
                break;
            }
    }

    public void Advance()
    {
        if (!CanAcceptInput || lastInputFrame == Time.frameCount) return;
        lastInputFrame = Time.frameCount;
        if (Owner != null && Owner.IsPlaying && !CanChoose)
        {
            Owner.Next();
        }
    }

    public void ShowDialogue(DialogueNode node)
    {
        if (node == null) return;
        if (layout == null) layout = RpgDialogueLayout.Ensure(this);
        if (current == null && EventSystem.current != null)
            previousSelection = EventSystem.current.currentSelectedGameObject;
        current = node;
        ClearChoice();
        if (visibleRoot != null) visibleRoot.SetActive(true);
        if (speakerText != null) speakerText.text = node.speaker ?? "";
        CharacterPortraitStyle.Apply(avatarImage, node.avatar, node.IsProtagonist);
        layout.Present(node);
        CurrentPage = 1;
        if (contentText != null)
        {
            contentText.text = node.content ?? "";
            contentText.overflowMode = TextOverflowModes.Page;
            contentText.pageToDisplay = 1;
            Canvas.ForceUpdateCanvases();
            contentText.ForceMeshUpdate();
            PageCount = Mathf.Max(1, contentText.textInfo.pageCount);
        }
        else PageCount = 1;
        RefreshControls();
        HoldCurrentPage();
    }

    void HoldCurrentPage()
    { inputAllowedAt = Time.unscaledTimeAsDouble + GameAudio.DialogueMinimumDisplayTime; }

    // Page before advancing the story, including when a node contains choices.
    public bool TryAdvancePage()
    {
        if (!CanAcceptInput || CurrentPage >= PageCount) return false;
        CurrentPage++;
        if (contentText != null) contentText.pageToDisplay = CurrentPage;
        RefreshControls();
        HoldCurrentPage();
        return true;
    }

    void RefreshControls()
    {
        bool choicesReady = CanChoose;
        if (continueButton != null) continueButton.gameObject.SetActive(!choicesReady);
        layout.SetChoicesVisible(choicesReady);
        layout.SetPage(CurrentPage, PageCount, choicesReady);
        if (!choicesReady) { Select(continueButton); return; }
        if (choiceParent == null || choicePrefab == null || choiceButtons.Count > 0) return;
        for (int i = 0; i < current.choices.Count; i++)
        {
            var choice = current.choices[i];
            if (choice == null) continue;
            var button = Instantiate(choicePrefab, choiceParent);
            GameAudioButton.UseManualConfirmation(button);
            button.gameObject.SetActive(true);
            layout.StyleChoice(button, choice.choiceText ?? "", choiceButtons.Count + 1);
            int index = i;
            button.onClick.AddListener(() => {
                if (!CanChoose || !CanAcceptInput || Owner == null || lastInputFrame == Time.frameCount) return;
                lastInputFrame = Time.frameCount;
                Owner.Choose(index);
            });
            choiceButtons.Add(button);
        }
        // Keep navigation inside the modal conversation.
        for (int i = 0; i < choiceButtons.Count; i++)
            choiceButtons[i].navigation = new Navigation {
                mode = Navigation.Mode.Explicit,
                selectOnUp = choiceButtons[(i + choiceButtons.Count - 1) % choiceButtons.Count],
                selectOnDown = choiceButtons[(i + 1) % choiceButtons.Count]
            };
        Canvas.ForceUpdateCanvases();
        layout.FitChoices();
        if (choiceButtons.Count > 0) Select(choiceButtons[0]);
    }

    bool HasValidChoices() => current != null && current.choices != null
        && current.choices.Exists(choice => choice != null);

    static void Select(Button button)
    {
        if (button != null && button.gameObject.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    public void Hide()
    {
        current = null;
        ClearChoice();
        if (visibleRoot != null) visibleRoot.SetActive(false);
        if (EventSystem.current != null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && (selected.transform.IsChildOf(transform)
                || (visibleRoot != null && selected.transform.IsChildOf(visibleRoot.transform))))
                EventSystem.current.SetSelectedGameObject(previousSelection != null
                    && previousSelection.activeInHierarchy ? previousSelection : null);
        }
        previousSelection = null;
    }

    private void ClearChoice()
    {
        choiceButtons.Clear();
        if (choiceParent == null) return;
        for (int i = choiceParent.childCount - 1; i >= 0; i--)
        {
            var child = choiceParent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Advance();
    }
}
