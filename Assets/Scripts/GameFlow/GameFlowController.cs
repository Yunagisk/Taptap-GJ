using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameFlowController : MonoBehaviour
{
    public const string MenuScene = "MainMenu";
    public const string MapScene = "MainMap";
    public const string VillageScene = "Village";
    public bool isMainMenu;
    public Canvas canvas;
    public TMP_FontAsset font;
    public Sprite menuPaper;
    public WorldMapTemplate template;
    public GameObject mainPage;
    public GameObject weaponPage;
    public GameObject settingsOverlay;
    public GameObject pauseOverlay;
    public GameObject confirmOverlay;
    public GameObject loadingOverlay;
    public RectTransform[] fittedPanels;
    public Button startButton;
    public Button continueButton;
    public Image[] weaponOptions;
    public TMP_Text selectedWeaponText;
    public TMP_Text errorText;
    public TMP_Text confirmationText;
    public Slider volumeSlider;
    public TMP_Text volumeText;
    public Toggle fullscreenToggle;
    public CanvasGroup worldMapGroup;
    public MapPlayerPanelView playerPanel;
    public VillageEntryConfirmationUI villageEntryConfirmation;
    public MapWildernessEventUI wildernessEventUI;

    public string SelectedWeapon { get; private set; }
    public bool IsLoading { get; private set; }
    bool settingsFromPause;
    bool confirmQuit;
    TMP_FontAsset runtimeFont;
    Button loadDayButton;

    void Start()
    {
        if (GameRoot.I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        BindLoadButtons();
        GameFlowMenuStyle.Apply(this);
        Set(settingsOverlay, false);
        Set(pauseOverlay, false);
        Set(confirmOverlay, false);
        Set(loadingOverlay, false);
        if (isMainMenu)
        {
            if (font != null)
            {
                runtimeFont = SharedUIFont.Get(font);
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
            }
            Set(mainPage, true);
            Set(weaponPage, false);
            RefreshSaveAvailability();
        }
        else
        {
            bool village = GameRoot.I.enterVillageOnLoad || SlothChapterRules.CanEndAtVillage(GameRoot.I.state)
                || SlothChapterRules.IsFinished(GameRoot.I.state);
            GameRoot.I.enterVillageOnLoad = false;
            if (village) ShowVillage();
            else ShowMap();
        }
    }

    void Update()
    {
        if (!IsLoading && !MapSceneTransition.IsBusy && Input.GetKeyDown(KeyCode.Escape)) HandleBack();
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        Vector2 size = ((RectTransform)canvas.transform).rect.size;
        foreach (var panel in fittedPanels)
            if (panel != null) panel.localScale = Vector3.one * Mathf.Max(0.1f,
                Mathf.Min(1f, (size.x - 64) / panel.sizeDelta.x, (size.y - 64) / panel.sizeDelta.y));
    }

    public void HandleBack()
    {
        if (SlothChapterPresentation.IsBlocking || !isMainMenu && SlothChapterRules.IsFinished(GameRoot.I?.state)) return;
        var guide = FindObjectOfType<GuideDialogueView>();
        if (guide != null && guide.IsOpen) return;
        if (MapSceneTransition.IsBusy || DayStartLoadTransition.IsBusy) return;
        var map = worldMapGroup != null ? worldMapGroup.GetComponent<WorldMapController>() : null;
        if (map == null) map = FindObjectOfType<WorldMapController>();
        if (map != null && map.IsFastTravelConfirmationOpen) { map.CancelFastTravel(); return; }
        if (wildernessEventUI != null && wildernessEventUI.IsOpen)
        {
            wildernessEventUI.HandleBack();
            return;
        }
        if (villageEntryConfirmation != null && villageEntryConfirmation.IsOpen)
        {
            villageEntryConfirmation.controller.CancelVillageEntry();
            return;
        }
        if (playerPanel != null && playerPanel.IsInventoryOpen) { playerPanel.CloseInventory(); return; }
        if (confirmOverlay.activeSelf) { CancelConfirmation(); return; }
        if (settingsOverlay.activeSelf) { CloseSettings(); return; }
        if (isMainMenu)
        {
            if (weaponPage.activeSelf) BackToTitle();
            return;
        }
        if (pauseOverlay.activeSelf) ClosePause();
        else OpenPause();
    }

    public void BeginNewGame()
    {
        if (IsLoading) return;
        StartNewRun(true);
    }

    public void SelectWeapon(string id)
    {
        if (IsLoading || Array.IndexOf(NewRunFactory.StartingWeapons, id) < 0) return;
        SelectedWeapon = id;
        selectedWeaponText.text = "已选：" + NewRunFactory.WeaponName(id);
        for (int i = 0; i < weaponOptions.Length && i < NewRunFactory.StartingWeapons.Length; i++)
            if (weaponOptions[i] != null) weaponOptions[i].color = NewRunFactory.StartingWeapons[i] == id
                ? new Color32(196, 222, 207, 255) : new Color32(237, 239, 243, 255);
        startButton.interactable = true;
    }

    public void StartSelectedGame()
    {
        if (IsLoading || string.IsNullOrEmpty(SelectedWeapon)) return;
        StartNewRun(false);
    }

    void StartNewRun(bool introduction)
    {
        try
        {
            if (template == null) throw new InvalidOperationException("No map template is assigned.");
            int seed = Guid.NewGuid().GetHashCode();
            var fresh = introduction ? NewRunFactory.CreateForIntroduction(template.Load(), seed)
                : NewRunFactory.Create(template.Load(), SelectedWeapon, seed);
            if (!Application.CanStreamedLevelBeLoaded(MapScene))
                throw new InvalidOperationException("The map scene is missing from Build Settings.");
            if (!Application.CanStreamedLevelBeLoaded(VillageScene))
                throw new InvalidOperationException("The village scene is missing from Build Settings.");
            if (!GameRoot.I.SaveMorning(fresh, template.Load()))
                throw new InvalidOperationException(GameRoot.I.LastSaveError);
            var previous = GameRoot.I.state;
            bool previousEntry = GameRoot.I.enterVillageOnLoad;
            var previousTemplate = GameRoot.I.villageMapTemplate;
            string previousReturnScene = GameRoot.I.villageReturnScene;
            try
            {
                GameRoot.I.state = fresh;
                GameRoot.I.enterVillageOnLoad = false;
                GameRoot.I.villageMapTemplate = template;
                GameRoot.I.villageReturnScene = MapScene;
                IsLoading = true;
                startButton.interactable = false;
                Set(loadingOverlay, true);
                SceneManager.LoadSceneAsync(VillageScene);
            }
            catch
            {
                GameRoot.I.state = previous;
                GameRoot.I.enterVillageOnLoad = previousEntry;
                GameRoot.I.villageMapTemplate = previousTemplate;
                GameRoot.I.villageReturnScene = previousReturnScene;
                throw;
            }
        }
        catch (Exception error)
        {
            IsLoading = false;
            Set(loadingOverlay, false);
            startButton.interactable = true;
            errorText.text = "无法开始新游戏：" + error.Message;
            Debug.LogWarning(error.Message, this);
        }
    }

    public void BackToTitle()
    {
        if (IsLoading) return;
        Set(weaponPage, false);
        Set(mainPage, true);
    }

    public void ShowVillage()
    {
        if (IsLoading) return;
        if (!Application.CanStreamedLevelBeLoaded(VillageScene))
        {
            Debug.LogError("The village scene is missing from Build Settings.", this);
            return;
        }
        var root = GameRoot.I;
        root.villageMapTemplate = template;
        root.villageReturnScene = gameObject.scene.path;
        root.enterVillageOnLoad = false;
        try
        {
            IsLoading = true;
            WorldMapVisible(false);
            Set(loadingOverlay, true);
            MapSceneTransition.LoadSceneAsync(VillageScene);
        }
        catch (Exception error)
        {
            IsLoading = false;
            Set(loadingOverlay, false);
            WorldMapVisible(true);
            Debug.LogException(error, this);
        }
    }

    public void ShowMap()
    {
        WorldMapVisible(true);
    }

    void WorldMapVisible(bool visible)
    {
        if (worldMapGroup == null) return;
        worldMapGroup.alpha = visible ? 1 : 0;
        worldMapGroup.interactable = worldMapGroup.blocksRaycasts = visible;
    }

    public void OpenInventory()
    {
        if (!SlothChapterPresentation.IsBlocking && !SlothChapterRules.IsFinished(GameRoot.I?.state))
            playerPanel.OpenInventory();
    }

    public void OpenPause()
    {
        if (!IsLoading && !SlothChapterPresentation.IsBlocking && !SlothChapterRules.IsFinished(GameRoot.I?.state))
        {
            RefreshSaveAvailability();
            Set(pauseOverlay, true);
        }
    }

    void BindLoadButtons()
    {
        if (isMainMenu && continueButton != null) continueButton.onClick.AddListener(ContinueGame);
        if (isMainMenu || pauseOverlay == null) return;
        var resume = pauseOverlay.transform.Find("Window/Btn_Resume")?.GetComponent<Button>();
        if (resume == null) return;
        loadDayButton = Instantiate(resume, resume.transform.parent);
        loadDayButton.name = "Btn_LoadDay";
        loadDayButton.onClick = new Button.ButtonClickedEvent();
        loadDayButton.onClick.AddListener(ContinueGame);
        loadDayButton.GetComponentInChildren<TMP_Text>().text = "回到今天早晨";
    }

    void RefreshSaveAvailability()
    {
        GameState morning = null;
        string error = "暂无可用存档";
        bool available = false;
        try { available = template != null && GameRoot.I.SaveSlot.TryLoad(template.Load(), out morning, out error); }
        catch (Exception exception) { error = "存档无法读取：" + exception.Message; }
        if (loadDayButton != null) loadDayButton.interactable = available;
        if (!isMainMenu || continueButton == null) return;
        continueButton.interactable = available;
        foreach (var label in mainPage.GetComponentsInChildren<TMP_Text>(true))
            if (label.name == "ContinueStatus")
            {
                label.text = available ? "第 " + WorldResourceRules.Day(morning.world) + " 天早晨 · 自动存档"
                    : error == "暂无可用存档" ? error : "存档无法读取";
                label.enableAutoSizing = true;
                label.fontSizeMin = 20;
                label.fontSizeMax = 22;
            }
        var continueText = continueButton.GetComponentInChildren<TMP_Text>();
        var newGameText = continueButton.transform.parent.Find("Btn_NewGame")?.GetComponentInChildren<TMP_Text>();
        if (continueText != null && newGameText != null)
            continueText.color = available ? newGameText.color : new Color32(145, 151, 162, 255);
    }

    public void ContinueGame()
    {
        if (IsLoading || DayStartLoadTransition.IsBusy || MapSceneTransition.IsBusy) return;
        bool started = DayStartLoadTransition.TryBegin(template, error =>
        {
            IsLoading = false;
            if (errorText != null) errorText.text = error;
            Debug.LogWarning(error, this);
        });
        if (started) IsLoading = true;
    }

    public void ClosePause() { Set(pauseOverlay, false); }

    public void OpenSettings()
    {
        settingsFromPause = pauseOverlay != null && pauseOverlay.activeSelf;
        Set(pauseOverlay, false);
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        volumeText.text = Mathf.RoundToInt(AudioListener.volume * 100) + "%";
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        Set(settingsOverlay, true);
    }

    public void CloseSettings()
    {
        Set(settingsOverlay, false);
        if (settingsFromPause) Set(pauseOverlay, true);
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        volumeText.text = Mathf.RoundToInt(AudioListener.volume * 100) + "%";
    }

    public void SetFullscreen(bool value) { Screen.fullScreen = value; }

    public void AskReturnToTitle()
    {
        confirmQuit = false;
        confirmationText.text = "进度已保留至今天早晨，之后的行动不会保存。返回主菜单？";
        Set(confirmOverlay, true);
    }

    public void AskQuit()
    {
        confirmQuit = true;
        confirmationText.text = isMainMenu ? "退出游戏？" : "进度已保留至今天早晨，之后的行动不会保存。退出游戏？";
        Set(confirmOverlay, true);
    }

    public void CancelConfirmation() { Set(confirmOverlay, false); }

    public void AcceptConfirmation()
    {
        if (IsLoading) return;
        if (confirmQuit)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(MenuScene)) return;
        IsLoading = true;
        Set(loadingOverlay, true);
        GameRoot.I.state = new GameState();
        GameRoot.I.enterVillageOnLoad = false;
        GameRoot.I.villageMapTemplate = null;
        GameRoot.I.villageReturnScene = null;
        SceneManager.LoadSceneAsync(MenuScene);
    }

    static void Set(GameObject obj, bool visible)
    {
        if (obj != null) obj.SetActive(visible);
    }

}
