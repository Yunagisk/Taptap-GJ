using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Weapons", menuName = "WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("武器名称")]
    [SerializeField]private string weaponName;

    [Header("每次命中的伤害")]
    [Tooltip("每项独立计算抗性，然后合并为一次伤害。列表为空时使用旧配置。")]
    [SerializeField]
    private List<DamageComponent> damageComponents = new();

    [Header("攻击次数")]
    [Min(1)]
    [SerializeField]private int attackCount=1;

    [Header("武器技能")]
    [SerializeField]private SkillData skill;

    [Header("武器价格")]
    [SerializeField]private int price;



    [SerializeField, HideInInspector] private int attack;
    [SerializeField, HideInInspector] private PlayerDamageType damageType;

    public string WeaponName => weaponName;  
    public int AttackCount => attackCount;
    public SkillData Skill => skill;
    public int Price => price;

    public IReadOnlyList<DamageComponent> DamageComponents
    {
        get
        {
            if (damageComponents != null && damageComponents.Count > 0)
            {
                return damageComponents;
            }

            // 旧武器兼容：把原来的单属性攻击包装成一个分量。
            return new DamageComponent[]
            {
                new DamageComponent(damageType, attack)
            };
        }
    }
}
