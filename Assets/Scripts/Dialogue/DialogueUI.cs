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

    private DialogueManager Owner => manager != null ? manager : DialogueManager.Instance;

    private void OnEnable()
    {
        if (continueButton != null) continueButton.onClick.AddListener(Continue);
    }

    private void OnDisable()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
    }

    private void Continue()
    {
        if (Owner != null) Owner.Next();
    }

    public void ShowDialogue(DialogueNode node)
    {
        if (node == null) return;
        if (visibleRoot != null) visibleRoot.SetActive(true);
        if (speakerText != null) speakerText.text = node.speaker;
        if (contentText != null) contentText.text = node.content;
        if (avatarImage != null)
        {
            avatarImage.sprite = node.avatar;
            avatarImage.enabled = node.avatar != null;
        }
        ClearChoice();
        bool hasChoices = node.choices != null && node.choices.Count > 0;
        if (continueButton != null) continueButton.gameObject.SetActive(!hasChoices);
        if (!hasChoices || choiceParent == null || choicePrefab == null) return;
        for (int i = 0; i < node.choices.Count; i++)
        {
            var choice = node.choices[i];
            if (choice == null) continue;
            var button = Instantiate(choicePrefab, choiceParent);
            button.gameObject.SetActive(true);
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = choice.choiceText;
            int index = i;
            button.onClick.AddListener(() => { if (Owner != null) Owner.Choose(index); });
        }
    }

    public void Hide()
    {
        ClearChoice();
        if (visibleRoot != null) visibleRoot.SetActive(false);
    }

    private void ClearChoice()
    {
        if (choiceParent == null) return;
        foreach (Transform child in choiceParent) Destroy(child.gameObject);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Owner != null && Owner.IsPlaying && !Owner.HasChoice()) Owner.Next();
    }
}
