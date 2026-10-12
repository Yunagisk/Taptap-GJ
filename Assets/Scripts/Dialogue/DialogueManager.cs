using UnityEngine;
using UnityEngine.SceneManagement;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    public DialogueUI ui;
    private DialogueData data;
    private DialogueNode currentNode;

    private void Awake()
    {
        Instance = this;
    }

    public void StartDialogue(DialogueData dialogue)
    {
        data = dialogue;
        currentNode = data.startNode;
        ShowNode();
    }

    public void ShowNode()
    {
        ui.ShowDialogue(currentNode);
    }

    public void Next()
    {
        if (string.IsNullOrEmpty(currentNode.nextNodeID))
        {

            EndDialogue();
            return;
        }

        currentNode =FindNode(currentNode.nextNodeID);

        ShowNode();
    }

    public void Choose(int index)
    {

        DialogueChoice choice =currentNode.choices[index];

        currentNode =FindNode(choice.targetNodeID);
        ShowNode();

    }

    private DialogueNode FindNode(string id)
    {
        foreach (DialogueNode node in data.nodes)
        {

            if (node.id == id)
                return node;

        }
        Debug.LogError("找不到剧情节点:" + id);

        return null;
    }

    void EndDialogue()
    {
        Debug.Log("剧情结束");
        //进入游戏
        SceneManager.LoadScene("Village");
    }
    public bool HasChoice()
    {
        return currentNode.choices.Count > 0;
    }
}
