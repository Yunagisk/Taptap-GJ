using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueNode 
{
    public string id;

    [Header("说话人")]
    public string speaker;

    [TextArea(3, 5)]
    public string content;


    [Header("头像")]
    public Sprite avatar;



    [Header("选择")]
    public List<DialogueChoice> choices;


    [Header("自动下一句")]
    public string nextNodeID;
}
