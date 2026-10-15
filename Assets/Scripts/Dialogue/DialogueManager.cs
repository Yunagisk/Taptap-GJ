using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    public DialogueUI ui;
    private DialogueData data;
    private DialogueNode currentNode;
    public bool IsPlaying { get; private set; }
    public event Action<DialogueData> DialogueCompleted;

    private void Awake()
    {
        Instance = this;
        if (ui != null) ui.manager = this;
    }

    public void StartDialogue(DialogueData dialogue)
    {
        if (IsPlaying) return;
        if (dialogue == null || dialogue.startNode == null || ui == null)
        {
            Debug.LogWarning("Assign dialogue data and UI before starting a dialogue.", this);
            return;
        }
        data = dialogue;
        currentNode = data.startNode;
        IsPlaying = true;
        ShowNode();
    }

    public void ShowNode()
    {
        if (!IsPlaying || currentNode == null) return;
        ui.ShowDialogue(currentNode);
    }

    public void Next()
    {
        if (!IsPlaying || currentNode == null || HasChoice()) return;
        if (string.IsNullOrEmpty(currentNode.nextNodeID))
        {
            EndDialogue();
            return;
        }
        var next = FindNode(currentNode.nextNodeID);
        if (next == null) return;
        currentNode = next;
        ShowNode();
    }

    public void Choose(int index)
    {
        if (!IsPlaying || currentNode == null || currentNode.choices == null
            || index < 0 || index >= currentNode.choices.Count) return;
        var choice = currentNode.choices[index];
        if (choice == null) return;
        if (string.IsNullOrEmpty(choice.targetNodeID))
        {
            EndDialogue();
            return;
        }
        var next = FindNode(choice.targetNodeID);
        if (next == null) return;
        currentNode = next;
        ShowNode();
    }

    // Leaving an unfinished conversation must not apply its completion effect.
    public void CancelDialogue()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        currentNode = null;
        data = null;
        if (ui != null) ui.Hide();
    }

    private DialogueNode FindNode(string id)
    {
        if (data.startNode != null && data.startNode.id == id) return data.startNode;
        if (data.nodes != null)
            foreach (var node in data.nodes)
                if (node != null && node.id == id) return node;
        Debug.LogError("Dialogue node not found: " + id, this);
        return null;
    }

    private void EndDialogue()
    {
        if (!IsPlaying) return;
        var completed = data;
        IsPlaying = false;
        currentNode = null;
        if (GameRoot.I != null && completed != null)
            MainStoryRules.TryCompleteDialogue(GameRoot.I.state,
                string.IsNullOrEmpty(completed.dialogueId) ? completed.name : completed.dialogueId,
                completed.completionEffect);
        ui.Hide();
        DialogueCompleted?.Invoke(completed);
    }

    public bool HasChoice()
    {
        return IsPlaying && currentNode != null && currentNode.choices != null
            && currentNode.choices.Count > 0;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
