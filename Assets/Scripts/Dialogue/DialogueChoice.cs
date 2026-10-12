using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueChoice
{

    public string choiceText;

    //选择后跳转哪个剧情节点
    public string targetNodeID;


    //以后扩展
    //例如增加傲慢值
    //public int sinValue;

}
