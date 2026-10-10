using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public class VillageScreen : MonoBehaviour
{
    [Header("Shared run and status")]
    public WorldMapTemplate template;
    public TMP_FontAsset font;
    public TMP_Text hpText;
    public TMP_Text lanternText;
    public TMP_Text sinText;
    public TMP_Text timeText;
    public TMP_Text weaponText;

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
    TMP_FontAsset runtimeFont;
    public bool IsLoading { get; private set; }

    void Awake()
    {
        if (GameRoot.I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        var root = GameRoot.I;
        if (root.villageMapTemplate != null) template = root.villageMapTemplate;
        root.villageMapTemplate = template;
        root.enterVillageOnLoad = false;
        if (string.IsNullOrEmpty(root.villageReturnScene))
            root.villageReturnScene = GameFlowController.MapScene;
        try
        {
            if (template == null) throw new InvalidOperationException("Assign a WorldMapTemplate to VillageScreen.");
            // Reuse the existing run. Opening the village never starts a new map.
            new MapProgression(template.Load(), root.state.map, root.state.world,
                string.IsNullOrEmpty(root.state.map.templateId) ? Guid.NewGuid().GetHashCode() : root.state.map.randomSeed);
        }
        catch (Exception error) { Debug.LogException(error, this); }
        if (font != null && font.sourceFontFile != null)
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
            var canvas = hpText != null ? hpText.GetComponentInParent<Canvas>()
                : GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
        }
        _panels = new[] { panelInn, panelShop, panelBag, panelEvent };
        ClosePanels();
        RefreshStatus();
    }

    public void RefreshStatus()
    {
        if (GameRoot.I == null) return;
        var state = GameRoot.I.state;
        if (hpText != null) hpText.text = "生命 " + state.player.hp + "/" + state.player.maxHp;
        if (lanternText != null) lanternText.text = "提灯 " + state.player.lanternCharges + "/" + state.player.maxLanternCharges;
        if (sinText != null) sinText.text = "罪恶 " + state.world.sin;
        string[] periods = { "早晨", "下午", "夜间" };
        if (timeText != null) timeText.text = "第 " + (state.world.time / 3 + 1) + " 天 " + periods[state.world.time % 3];
        if (weaponText != null) weaponText.text = "当前武器：" + NewRunFactory.WeaponName(state.player.currentWeaponId);
    }

    void Update()
    {
        if (IsLoading || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (panelConflict != null && panelConflict.activeSelf) CloseConflict();
        else if (panelRoot != null && panelRoot.activeSelf) ClosePanels();
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
        if (IsLoading) return;
        var root = GameRoot.I;
        string scene = root != null && !string.IsNullOrEmpty(root.villageReturnScene)
            ? root.villageReturnScene : GameFlowController.MapScene;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Say("世界地图场景未加入构建列表。");
            return;
        }
        try
        {
            IsLoading = true;
            if (root != null) root.enterVillageOnLoad = false;
            Say("正在进入世界地图……");
            SceneManager.LoadSceneAsync(scene);
        }
        catch (Exception error)
        {
            IsLoading = false;
            Say("进入地图失败，请重试。");
            Debug.LogException(error, this);
        }
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

    void OnDestroy()
    {
        if (runtimeFont == null) return;
        foreach (var texture in runtimeFont.atlasTextures)
            if (texture != null) Destroy(texture);
        Destroy(runtimeFont.material);
        Destroy(runtimeFont);
    }
}
