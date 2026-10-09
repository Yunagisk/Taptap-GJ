using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Weapons", menuName = "WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("武器名称")]
    [SerializeField]private string weaponName;
    [Header("攻击属性")]
    [SerializeField]private int attack;
    [SerializeField]private int attackCount;
    [SerializeField]private DamageType damageType;
    [Header("武器技能")]
    [SerializeField]private SkillData skill;
    [Header("武器价格")]
    [SerializeField]private int price;

    public string WeaponName => weaponName;
    public int Attack => attack;
    public int AttackCount => attackCount;
    public DamageType DamageType => damageType;
    public SkillData Skill => skill;
    public int Price => price;
}
