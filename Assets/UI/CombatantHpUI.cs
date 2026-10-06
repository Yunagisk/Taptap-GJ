using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatantHpUI : MonoBehaviour
{
    [Header("监听的战斗单位")]
    [SerializeField] private Combatant target;

    [Header("UI组件")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text hpText;

    private void OnEnable()
    {
        if(target != null)
        {
            target.OnHpChanged += UpdateHPUI;
        }
    }
    private void Start()
    {
        if(target != null)
        {
            UpdateHPUI(target);
        }
    }

    private void OnDisable()
    {
        if(target != null)
        {
            target.OnHpChanged -= UpdateHPUI;
        }
    }

    private void UpdateHPUI(Combatant combatant)
    {
        hpSlider.maxValue=combatant.MaxHP;
        hpSlider.value=combatant.CurrentHP;

        hpText.text =
           $"{combatant.CurrentHP} / {combatant.MaxHP}";
    }
}
