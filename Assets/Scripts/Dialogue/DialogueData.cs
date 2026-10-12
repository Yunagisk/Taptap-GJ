using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName ="DialogueData")]
public class DialogueData : ScriptableObject
{
    public DialogueNode startNode;

    public List<DialogueNode> nodes;
}
