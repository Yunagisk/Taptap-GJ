using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VillageScreen : MonoBehaviour
{
    public GameObject panelRoot;
    public GameObject panelInn;
    public GameObject panelShop;
    public GameObject panelBag;
    public GameObject panelEvent;
    public GameObject panelConflict;

    public GameObject focusInn;
    public GameObject focusShop;
    public GameObject focusTavern;
    public GameObject focusInnConfirm;
    public GameObject focusBuy;
    public GameObject focusSlot;
    public GameObject focusOption;
    public GameObject focusConflict;

    public TMP_Text toast;

    GameObject[] _panels;

    void Awake()
    {
        _panels = new[] { panelInn, panelShop, panelBag, panelEvent };
        ClosePanels();
    }

    public void OpenInn()
    {
        Show(panelInn, focusInnConfirm);
    }

    public void OpenShop()
    {
        Show(panelShop, focusBuy);
    }

    public void OpenBag()
    {
        Show(panelBag, focusSlot);
    }

    public void OpenEvent()
    {
        Show(panelEvent, focusOption);
    }

    public void ClosePanels()
    {
        SetBagButtons(true);
        if (panelConflict != null)
            panelConflict.SetActive(false);
        if (panelRoot != null)
            panelRoot.SetActive(false);
        Select(focusInn);
    }

    public void OpenConflict()
    {
        if (panelConflict != null)
            panelConflict.SetActive(true);
        SetBagButtons(false);
        Select(focusConflict);
    }

    public void CloseConflict()
    {
        if (panelConflict != null)
            panelConflict.SetActive(false);
        SetBagButtons(true);
        Select(focusSlot);
    }

    public void ConfirmInn()
    {
        Say("旅馆已提交一次。关闭后再开不会重复结算。未写存档。");
        ClosePanels();
    }

    public void ConfirmBuy()
    {
        Say("购买已提交一次。未改地图。");
        ClosePanels();
    }

    public void ConfirmOptionA()
    {
        Say("选项甲已提交一次。关掉再开不会重抽。");
        ClosePanels();
    }

    public void ConfirmOptionB()
    {
        Say("选项乙已提交一次。关掉再开不会重抽。");
        ClosePanels();
    }

    public void ConfirmUseItem()
    {
        Say("使用道具已提交一次。");
    }

    public void ConfirmConflict()
    {
        Say("装备替换已提交一次。");
        CloseConflict();
    }

    public void LeaveToMap()
    {
        Say("已请求返回世界地图。单独场景不加载主地图，资源不变。");
    }

    void Show(GameObject panel, GameObject focus)
    {
        if (_panels != null)
        {
            foreach (var item in _panels)
            {
                if (item != null)
                    item.SetActive(item == panel);
            }
        }

        if (panelConflict != null)
            panelConflict.SetActive(false);
        SetBagButtons(true);
        if (panelRoot != null)
            panelRoot.SetActive(true);
        Select(focus);
    }

    void SetBagButtons(bool interactable)
    {
        if (panelBag == null)
            return;
        var buttons = panelBag.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].interactable = interactable;
    }

    void Say(string message)
    {
        if (toast != null)
            toast.text = message;
    }

    void Select(GameObject target)
    {
        if (target == null || EventSystem.current == null)
            return;
        EventSystem.current.SetSelectedGameObject(target);
    }
}
