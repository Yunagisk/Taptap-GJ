using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSkill", menuName = "SkillData")]
public class SkillData : ScriptableObject
{
    [Header("技能信息")]
    [SerializeField] private string skillName;
    [TextArea]
    [SerializeField] private string skillDescription;
    [Header("决心消耗")]
    [Range(0,4)]
    [SerializeField] private int determinationCost;

    public string SkillName => skillName;
    public string Description => skillDescription;
    public int DeterminationCost => determinationCost;

}
