using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameFlowController : MonoBehaviour
{
    public const string MenuScene = "MainMenu";
    public const string MapScene = "SampleScene_MapWhitebox";
    public bool isMainMenu;
    public Canvas canvas;
    public TMP_FontAsset font;
    public WorldMapTemplate template;
    public GameObject mainPage;
    public GameObject weaponPage;
    public GameObject villagePage;
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
    public TMP_Text villageWeaponText;
    public Slider volumeSlider;
    public TMP_Text volumeText;
    public Toggle fullscreenToggle;
    public CanvasGroup worldMapGroup;
    public MapPlayerPanelView playerPanel;

    public string SelectedWeapon { get; private set; }
    public bool IsLoading { get; private set; }
    bool settingsFromPause;
    bool confirmQuit;
    TMP_FontAsset runtimeFont;

    void Start()
    {
        if (GameRoot.I == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        Set(settingsOverlay, false);
        Set(pauseOverlay, false);
        Set(confirmOverlay, false);
        Set(loadingOverlay, false);
        if (isMainMenu)
        {
            if (font != null && font.sourceFontFile != null)
            {
                runtimeFont = TMP_FontAsset.CreateFontAsset(font.sourceFontFile);
                foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
            }
            Set(mainPage, true);
            Set(weaponPage, false);
            continueButton.interactable = false;
        }
        else
        {
            bool village = GameRoot.I.enterVillageOnLoad;
            GameRoot.I.enterVillageOnLoad = false;
            if (village) ShowVillage();
            else ShowMap();
        }
    }

    void Update()
    {
        if (!IsLoading && Input.GetKeyDown(KeyCode.Escape)) HandleBack();
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
        SelectedWeapon = null;
        errorText.text = "";
        selectedWeaponText.text = "";
        startButton.interactable = false;
        foreach (var image in weaponOptions) image.color = new Color32(237, 239, 243, 255);
        Set(mainPage, false);
        Set(weaponPage, true);
    }

    public void SelectWeapon(string id)
    {
        if (IsLoading || Array.IndexOf(NewRunFactory.StartingWeapons, id) < 0) return;
        SelectedWeapon = id;
        selectedWeaponText.text = "已选：" + NewRunFactory.WeaponName(id);
        for (int i = 0; i < weaponOptions.Length; i++)
            weaponOptions[i].color = NewRunFactory.StartingWeapons[i] == id
                ? new Color32(196, 222, 207, 255) : new Color32(237, 239, 243, 255);
        startButton.interactable = true;
    }

    public void StartSelectedGame()
    {
        if (IsLoading || string.IsNullOrEmpty(SelectedWeapon)) return;
        try
        {
            if (template == null) throw new InvalidOperationException("No map template is assigned.");
            var fresh = NewRunFactory.Create(template.Load(), SelectedWeapon, Guid.NewGuid().GetHashCode());
            if (!Application.CanStreamedLevelBeLoaded(MapScene))
                throw new InvalidOperationException("The map scene is missing from Build Settings.");
            var previous = GameRoot.I.state;
            bool previousEntry = GameRoot.I.enterVillageOnLoad;
            try
            {
                GameRoot.I.state = fresh;
                GameRoot.I.enterVillageOnLoad = true;
                IsLoading = true;
                startButton.interactable = false;
                Set(loadingOverlay, true);
                SceneManager.LoadSceneAsync(MapScene);
            }
            catch
            {
                GameRoot.I.state = previous;
                GameRoot.I.enterVillageOnLoad = previousEntry;
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
        Set(villagePage, true);
        WorldMapVisible(false);
        villageWeaponText.text = "当前武器：" + NewRunFactory.WeaponName(GameRoot.I.state.player.currentWeaponId);
    }

    public void ShowMap()
    {
        Set(villagePage, false);
        WorldMapVisible(true);
    }

    void WorldMapVisible(bool visible)
    {
        if (worldMapGroup == null) return;
        worldMapGroup.alpha = visible ? 1 : 0;
        worldMapGroup.interactable = worldMapGroup.blocksRaycasts = visible;
    }

    public void OpenInventory() { playerPanel.OpenInventory(); }

    public void OpenPause()
    {
        if (!IsLoading) Set(pauseOverlay, true);
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
        confirmationText.text = "本局进度不会保留，返回主菜单？";
        Set(confirmOverlay, true);
    }

    public void AskQuit()
    {
        confirmQuit = true;
        confirmationText.text = isMainMenu ? "退出游戏？" : "本局进度不会保留，退出游戏？";
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
        SceneManager.LoadSceneAsync(MenuScene);
    }

    static void Set(GameObject obj, bool visible)
    {
        if (obj != null) obj.SetActive(visible);
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
