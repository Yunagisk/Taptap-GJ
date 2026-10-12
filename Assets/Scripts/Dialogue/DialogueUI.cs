using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class DialogueUI : MonoBehaviour, IPointerClickHandler
{
    public TMP_Text speakerText;
    public TMP_Text contentText;
    public Transform choiceParent;
    public Button choicePrefab;

    public Button continueButton;

    private void Awake()
    {

    }
    private void Start()
    {
        continueButton.onClick.AddListener(() =>
        {
            DialogueManager.Instance.Next();
        });
    }
    public void ShowDialogue(DialogueNode node)
    {
        speakerText.text =node.speaker;
        contentText.text =node.content;


        ClearChoice();

        //判断是否需要继续提示
        if (node.choices.Count == 0)
        {
            continueButton.gameObject.SetActive(true);
        }
        else
        {
           continueButton.gameObject.SetActive(false);
        }

        if (node.choices.Count > 0)
        {
            CreateChoices(node);
        }

    }

    private void ClearChoice()
    {
        foreach (Transform child in choiceParent)
        {
            Destroy(child.gameObject);
        }

    }

    private void CreateChoices(DialogueNode node)
    {

        foreach (var choice in node.choices)
        {
            Button btn = Instantiate(choicePrefab, choiceParent);

            btn.GetComponentInChildren<TMP_Text>().text = choice.choiceText;

            int index = node.choices.IndexOf(choice);

            btn.onClick.AddListener(() => { DialogueManager.Instance.Choose(index); });

        }

    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (DialogueManager.Instance.HasChoice())
        {
            return;
        }

        DialogueManager.Instance.Next();

    }


}