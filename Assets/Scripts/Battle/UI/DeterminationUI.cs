using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeterminationUI : MonoBehaviour
{
    [Header("玩家")]
    [SerializeField] private PlayerCombatant player;

    [Header("决心图标")]
    [SerializeField] private Image[] icons;

    [SerializeField] private Sprite filledSprite;
    [SerializeField] private Sprite emptySprite;

    private void OnEnable()
    {
        if (player == null) return;

        // 监听决心值变化
        player.OnDeterminationChanged += UpdateUI;

        // 初始化显示
        UpdateUI(player.CurrentDetermination);
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.OnDeterminationChanged -= UpdateUI;
        }
    }

    private void UpdateUI(int current)
    {
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i].sprite = i < current
                ? filledSprite
                : emptySprite;
        }
    }
}
