using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueNode 
{
    public string id;

    [Header("说话人")]
    public string speaker;

    [Header("显示类型（旁白和提示不显示立绘）")]
    public DialogueLineKind lineKind;

    [TextArea(3, 5)]
    public string content;
    [Header("引导任务到达此句时发放一次的奖励")]
    public GuideReward guideReward;


    [Header("角色立绘（建议使用透明背景半身像）")]
    public Sprite avatar;

    [Tooltip("主角保留原色；自定义主角姓名时勾选。猎魔人、主角、你会自动识别。")]
    public bool isProtagonist;

    public bool IsProtagonist
    {
        get
        {
            string label = (speaker ?? "").Trim().TrimEnd(':', '：').TrimEnd();
            return isProtagonist || label == "猎魔人" || label == "主角" || label == "你";
        }
    }



    [Header("选择")]
    public List<DialogueChoice> choices;


    [Header("自动下一句")]
    public string nextNodeID;
}

public enum DialogueLineKind { Dialogue, Narration, System }
