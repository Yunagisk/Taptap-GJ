using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelToggler : MonoBehaviour
{
    public CanvasGroup target;
    public void Show()
    {
        if (target == null) return;
        target.alpha = 1;
        target.blocksRaycasts = true;
        target.interactable = true;
    }
    public void Hide()
    {
        if (target == null) return;
        target.alpha = 0;
        target.blocksRaycasts = false;
        target.interactable = false;
    }
}
