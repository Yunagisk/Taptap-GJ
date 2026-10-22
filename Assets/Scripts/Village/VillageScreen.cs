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
    public WeaponLibrary weaponLibrary;

    GameObject[] _panels;
    TMP_FontAsset runtimeFont;
    MapLayout villageLayout;
    EquipmentPanelView closingBag;
    public VillageRestTransition restTransition;
    public VillageShopUI Shop { get; private set; }
    public VillageInnUI Inn { get; private set; }
    public bool IsResting => restTransition != null && restTransition.IsRunning;
    public bool CanUseFacilities => !IsLoading && !IsResting && FacilitiesAvailable;
    public bool IsLoading { get; private set; }
    public VillageIntroController Introduction { get; private set; }
    public SlothChapterPresentation SlothChapter { get; private set; }
    public Canvas UICanvas => panelRoot != null ? panelRoot.GetComponentInParent<Canvas>(true)
        : GetComponentInChildren<Canvas>(true);

    bool FacilitiesAvailable => !DayStartLoadTransition.IsBusy
        && !VillageIntroRules.IsRestricted(GameRoot.I != null ? GameRoot.I.state : null)
        && !SlothChapterPresentation.BlocksVillage
        && (Introduction == null || !Introduction.IsOpen);

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
            var progression = new MapProgression(template.Load(), root.state.map, root.state.world,
                string.IsNullOrEmpty(root.state.map.templateId) ? Guid.NewGuid().GetHashCode() : root.state.map.randomSeed);
            villageLayout = progression.Layout;
            MainStoryRules.EnsureInitialized(root.state, progression.Layout);
            EquipmentRules.EnsureInitialized(root.state);
            VillageEventRules.InitializeForRun(root.state, progression.Layout);
        }
        catch (Exception error) { Debug.LogException(error, this); }
        if (font != null)
        {
            runtimeFont = SharedUIFont.Get(font);
            runtimeFont.isMultiAtlasTexturesEnabled = true;
            var canvas = UICanvas;
            if (canvas != null)
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
        }
        _panels = new[] { panelInn, panelShop, panelBag, panelEvent };
        Shop = panelShop != null ? panelShop.GetComponent<VillageShopUI>() : null;
        Inn = panelInn != null ? panelInn.GetComponent<VillageInnUI>() : null;
        if (Shop != null) Shop.Bind(this, runtimeFont);
        if (Inn != null) Inn.Bind(this, runtimeFont);
        ClosePanels();
        RefreshStatus();
        Introduction = VillageIntroController.Attach(this);
        SlothChapter = SlothChapterPresentation.Attach(this);
    }

    public void RefreshStatus()
    {
        if (GameRoot.I == null) return;
        var state = GameRoot.I.state;
        if (hpText != null) hpText.text = "生命 " + state.player.hp + "/" + state.player.maxHp;
        if (lanternText != null) lanternText.text = "提灯 " + state.player.lanternCharges + "/" + state.player.maxLanternCharges;
        if (sinText != null) sinText.text = "罪骸 " + state.world.sinRemnant + "    罪恶 " + state.world.sin + "/100";
        if (timeText != null) timeText.text = WorldResourceRules.TimeLabel(state.world);
        if (weaponText != null) weaponText.text = string.IsNullOrEmpty(state.player.currentWeaponId)
            ? "尚未领取武器" : "当前武器：" + NewRunFactory.WeaponName(state.player.currentWeaponId);
    }

    void Update()
    {
        RefreshStatus();
        if (SlothChapterPresentation.BlocksVillage) return;
        if (Introduction != null && Introduction.IsOpen) return;
        if (IsLoading || IsResting || DayStartLoadTransition.IsBusy || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (panelConflict != null && panelConflict.activeSelf) CloseConflict();
        else if (panelRoot != null && panelRoot.activeSelf && !ReturnToTavernEvents()) ClosePanels();
    }

    void LateUpdate()
    {
        if (closingBag != null && !closingBag.IsOpen) FinishClosingPanels();
    }

    public void OpenInn()
    {
        if (!CanUseFacilities) return;
        Show(panelInn, focusInnConfirm);
        if (Inn != null) { Inn.Refresh(); Select(Inn.PreferredFocus); }
    }

    public void OpenShop()
    {
        if (!CanUseFacilities) return;
        Show(panelShop, focusBuy);
        if (Shop != null) Shop.Open();
    }

    public void OpenBag()
    {
        if (IsLoading || IsResting || SlothChapterPresentation.BlocksVillage
            || VillageIntroRules.IsPending(GameRoot.I != null ? GameRoot.I.state : null)
            || Introduction != null && Introduction.IsOpen) return;
        Show(panelBag, focusSlot);
        var equipment = panelBag != null ? panelBag.GetComponent<EquipmentPanelView>() : null;
        if (equipment != null) equipment.Open();
    }

    public void OpenTavern()
    {
        if (!CanUseFacilities) return;
        Show(panelEvent, focusOption);
        var story = panelEvent != null ? panelEvent.GetComponent<VillageStoryPanelUI>() : null;
        var browser = panelEvent != null ? panelEvent.GetComponent<TavernEventBrowserUI>() : null;
        if (browser != null)
        {
            browser.Refresh();
            Select(browser.GetPreferredFocus());
        }
        else if (story != null)
        {
            story.Refresh();
            Select(story.GetPreferredFocus());
        }
    }

    // Keep existing scene/button bindings compatible with the tavern entry.
    public void OpenEvent()
    {
        OpenTavern();
    }

    bool ReturnToTavernEvents()
    {
        if (panelEvent == null || !panelEvent.activeInHierarchy) return false;
        var story = panelEvent.GetComponent<VillageStoryPanelUI>();
        return story != null && story.ReturnToEventList();
    }

    public void ClosePanels()
    {
        if (SlothChapter != null && (SlothChapter.IsOpen || SlothChapter.IsEndingOpen)) return;
        if (IsResting) return;
        var equipment = panelBag != null && panelBag.activeInHierarchy
            ? panelBag.GetComponent<EquipmentPanelView>() : null;
        if (equipment != null && equipment.IsOpen)
        {
            equipment.Close();
            if (equipment.IsOpen)
            {
                // Keep the modal alive until the parchment has finished rolling up.
                closingBag = equipment;
                return;
            }
        }
        FinishClosingPanels();
    }

    // Story takes exclusive focus immediately; a bag roll-up must not select a
    // village button after the modal dialogue has already opened.
    public void PrepareStoryPresentation()
    {
        FinishClosingPanels();
    }

    void FinishClosingPanels()
    {
        closingBag = null;
        bool closingTavern = panelEvent != null && panelEvent.activeInHierarchy;
        SetBagButtons(true);
        if (panelConflict != null)
            panelConflict.SetActive(false);
        if (panelRoot != null)
            panelRoot.SetActive(false);
        Select(closingTavern ? focusTavern : focusInn);
    }

    public void OpenConflict()
    {
        if (IsResting || IsLoading || SlothChapterPresentation.BlocksVillage) return;
        if (panelConflict != null)
            panelConflict.SetActive(true);
        SetBagButtons(false);
        Select(focusConflict);
    }

    public void CloseConflict()
    {
        if (SlothChapterPresentation.BlocksVillage) return;
        if (panelConflict != null)
            panelConflict.SetActive(false);
        SetBagButtons(true);
        Select(focusSlot);
    }

    public void ConfirmInn()
    {
        if (!CanUseFacilities) return;
        // Ignore callbacks after closing the inn or while changing scenes.
        if (IsLoading || panelRoot == null || !panelRoot.activeInHierarchy
            || panelInn == null || !panelInn.activeInHierarchy) return;
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (!VillageInnRules.CanRest(state))
        {
            Say("生命和提灯次数已满，无需休息。");
            ClosePanels();
            return;
        }

        if (restTransition == null || !restTransition.Begin(this, state, villageLayout))
            Say("旅店转场尚未就绪，请重新进入村庄。");
    }

    public void HidePanelsForRest() { if (IsResting) FinishClosingPanels(); }

    public void CompleteInnPresentation(string message)
    {
        RefreshStatus(); Say(message); Select(focusInn);
    }

    public void ConfirmBuy()
    {
        if (SlothChapterPresentation.BlocksVillage) return;
        if (Shop != null) Shop.Purchase();
    }

    public void ConfirmOptionA()
    {
        if (SlothChapterPresentation.BlocksVillage) return;
        Say("已作出选择。");
        ClosePanels();
    }

    public void ConfirmOptionB()
    {
        if (SlothChapterPresentation.BlocksVillage) return;
        Say("已作出选择。");
        ClosePanels();
    }

    public void ConfirmUseItem()
    {
        if (SlothChapterPresentation.BlocksVillage) return;
        var equipment = panelBag != null ? panelBag.GetComponent<EquipmentPanelView>() : null;
        if (equipment != null) equipment.UseSelectedItem();
    }

    public void ConfirmConflict()
    {
        if (SlothChapterPresentation.BlocksVillage) return;
        Say("已更换装备。");
        CloseConflict();
    }

    public void LeaveToMap()
    {
        if (IsLoading || IsResting || SlothChapterPresentation.BlocksVillage
            || Introduction != null && Introduction.IsOpen
            || !VillageIntroRules.CanLeaveVillage(GameRoot.I != null ? GameRoot.I.state : null)) return;
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (VillageIntroRules.IsRestricted(state))
        {
            EquipmentRules.EnsureInitialized(state);
            if (state.inventory.equippedWeaponIds.FindAll(id => !string.IsNullOrEmpty(id)).Count < 2)
            { Say("请在背包中装备两种武器，再前往旧磨坊。"); return; }
        }
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
        if (IsLoading || IsResting) return;
        closingBag = null;
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
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }
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
