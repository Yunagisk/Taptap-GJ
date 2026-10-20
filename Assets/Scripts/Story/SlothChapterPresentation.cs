using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One modal presentation can be active at a time, across village, map and battle.
// Returning to the village is the chapter's final action.
public sealed class SlothChapterPresentation : MonoBehaviour
{
    static SlothChapterPresentation active;
    VillageScreen village;
    VillageIntroContent content;
    GuideDialogueView view;
    GameObject navigation, ending;
    DialogueData activeDialogue;
    GameState presentationRun;
    Action completed;
    bool loadingMenu;

    public static bool IsBlocking => active != null
        && (active.IsOpen || active.IsEndingOpen || active.loadingMenu);
    public static bool BlocksVillage
    {
        get
        {
            var state = GameRoot.I != null ? GameRoot.I.state : null;
            return IsBlocking || SlothChapterRules.CanEndAtVillage(state)
                || SlothChapterRules.IsFinished(state)
                || state?.story != null && state.story.stage == MainStoryStage.SlothDefeated
                    && state.map != null && state.map.currentNodeId == state.story.villageNodeId;
        }
    }
    public bool IsOpen => view != null && view.IsOpen;
    public bool IsEndingOpen => ending != null && ending.activeInHierarchy;
    public DialogueManager Manager => view != null ? view.Manager : null;
    public DialogueData ActiveDialogue => activeDialogue;
    public Button InvestigationButton { get; private set; }
    public Button ReturnToMenuButton { get; private set; }
    public Button QuitButton { get; private set; }

    public static SlothChapterPresentation Attach(VillageScreen screen)
    {
        if (screen == null) return null;
        var presenter = screen.GetComponent<SlothChapterPresentation>();
        if (presenter == null) presenter = screen.gameObject.AddComponent<SlothChapterPresentation>();
        presenter.village = screen;
        if (GameRoot.I?.state?.map != null) GameRoot.I.state.map.slothChapterOnly = true;
        presenter.EnsureContent();
        return presenter;
    }

    public static bool PlayInvestigation(Transform owner)
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state?.story == null || state.story.stage != MainStoryStage.TutorialCompleted
            || VillageIntroRules.IsRestricted(state) || IsBlocking) return false;
        var presenter = ForOwner(owner);
        if (presenter == null) return false;
        return presenter.Play(SlothChapterDialogue.Investigation(), presenter.content.mayorBackground, () => {
            state.story.slothHookSeen = true;
            state.story.slothInvestigated = true;
        });
    }

    public static bool PlayConfrontation(Transform owner, Action onComplete)
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state?.story == null || IsBlocking) return false;
        var presenter = ForOwner(owner);
        if (presenter == null) return false;
        return presenter.Play(SlothChapterDialogue.Confrontation(state.story.slothInvestigated),
            presenter.content.slothShrineBackground, () => {
                state.story.slothConfronted = true;
                onComplete?.Invoke();
            });
    }

    public static bool PlayAfterBattle(Transform owner, Action onComplete)
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state?.story == null || IsBlocking) return false;
        var presenter = ForOwner(owner);
        if (presenter == null) return false;
        return presenter.Play(SlothChapterDialogue.AfterBattle(state.story.slothFate),
            presenter.content.slothShrineBackground, () => {
                // A failed/stale choice may never settle the victory.
                if (state.story.slothFate != SlothFate.Undecided) onComplete?.Invoke();
            });
    }

    static SlothChapterPresentation ForOwner(Transform owner)
    {
        if (owner == null) return null;
        // VillageStoryPanelUI lives inside a closable panel. Host its conversation
        // on the village itself so closing the event list cannot hide the dialogue.
        var screen = owner.GetComponentInParent<VillageScreen>();
        if (screen == null) screen = FindObjectOfType<VillageScreen>();
        if (screen != null)
        {
            var attached = Attach(screen);
            return attached.EnsureContent() ? attached : null;
        }
        SlothChapterPresentation presenter = null;
        foreach (var candidate in FindObjectsOfType<SlothChapterPresentation>())
            if (candidate.village == null && candidate.gameObject.scene == owner.gameObject.scene)
            { presenter = candidate; break; }
        if (presenter == null)
        {
            // Map controllers may sit beneath a scaled map Canvas. A root host
            // keeps the shared 1920 x 1080 dialogue at its authored screen size.
            var host = new GameObject("SlothChapterPresentation");
            SceneManager.MoveGameObjectToScene(host, owner.gameObject.scene);
            presenter = host.AddComponent<SlothChapterPresentation>();
        }
        return presenter.EnsureContent() ? presenter : null;
    }

    bool EnsureContent()
    {
        if (content == null) content = Resources.Load<VillageIntroContent>(VillageIntroController.ContentResource);
        if (content == null || content.dialoguePrefab == null) return false;
        if (view == null)
        {
            view = GuideDialogueView.Create(transform, content);
            view.gameObject.name = "SlothChapterDialogue";
            view.Manager.NodeShown += OnNodeShown;
        }
        return true;
    }

    void Start()
    {
        if (village != null && EnsureContent()) BuildNavigation();
    }

    void Update()
    {
        if (village == null) return;
        RefreshNavigation();
        if (village.IsLoading || village.IsResting || village.Introduction != null && village.Introduction.IsOpen
            || IsOpen || IsEndingOpen || IsBlocking || !EnsureContent()) return;
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state?.story == null || VillageIntroRules.IsRestricted(state)) return;
        if (SlothChapterRules.IsFinished(state)) { ShowEnding(); return; }
        if (SlothChapterRules.CanEndAtVillage(state)) { PlayVillageEnding(state); return; }
        if (state.story.stage == MainStoryStage.SlothDefeated
            && state.story.slothFate == SlothFate.Undecided
            && state.map.currentNodeId == state.story.villageNodeId)
        {
            // Legacy saves may have completed the node before the judgement existed.
            PlayAfterBattle(transform, () => {
                if (SlothChapterRules.CanEndAtVillage(state)) PlayVillageEnding(state);
            });
            return;
        }
        if (state.story.stage == MainStoryStage.TutorialCompleted && !state.story.slothHookSeen)
            Play(SlothChapterDialogue.Hook(), content.mayorBackground, () => state.story.slothHookSeen = true);
    }

    void LateUpdate()
    {
        if (!IsEndingOpen || loadingMenu || EventSystem.current == null || ReturnToMenuButton == null) return;
        var selection = EventSystem.current.currentSelectedGameObject;
        if (selection == null || !selection.transform.IsChildOf(ending.transform))
            EventSystem.current.SetSelectedGameObject(ReturnToMenuButton.gameObject);
    }

    bool Play(DialogueData dialogue, Sprite background, Action onComplete)
    {
        if (dialogue == null || IsBlocking || !EnsureContent())
        {
            if (dialogue != null) Destroy(dialogue);
            return false;
        }
        if (village != null) village.PrepareStoryPresentation();
        if (navigation != null) navigation.SetActive(false);
        active = this;
        presentationRun = GameRoot.I.state;
        activeDialogue = dialogue;
        completed = onComplete;
        view.Play(dialogue, background, OnCompleted);
        if (!view.IsOpen || !view.Manager.IsPlaying)
        {
            activeDialogue = null; completed = null; presentationRun = null;
            if (active == this) active = null;
            Destroy(dialogue);
            return false;
        }
        return true;
    }

    void OnNodeShown(DialogueNode node)
    {
        if (activeDialogue == null || activeDialogue.dialogueId != "sloth_after_battle"
            || GameRoot.I == null || GameRoot.I.state != presentationRun || node == null
            || presentationRun.story.slothFate != SlothFate.Undecided) return;
        if (node.id == "fate_spared")
            SlothChapterRules.TryChooseFate(presentationRun, SlothFate.Spared);
        else if (node.id == "fate_killed")
            SlothChapterRules.TryChooseFate(presentationRun, SlothFate.Killed);
    }

    void OnCompleted(DialogueData dialogue)
    {
        if (dialogue != activeDialogue) return;
        var callback = completed;
        bool currentRun = GameRoot.I != null && GameRoot.I.state == presentationRun;
        activeDialogue = null; completed = null; presentationRun = null;
        if (active == this) active = null;
        Destroy(dialogue);
        if (currentRun) callback?.Invoke();
        RefreshNavigation();
        if (village != null) village.RefreshStatus();
        if (!IsBlocking && InvestigationButton != null && InvestigationButton.gameObject.activeInHierarchy
            && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(InvestigationButton.gameObject);
    }

    void PlayVillageEnding(GameState state)
    {
        Play(SlothChapterDialogue.ReturnToVillage(state.story.slothFate), content.streetBackground, () => {
            if (SlothChapterRules.TryFinishChapter(state) || SlothChapterRules.IsFinished(state)) ShowEnding();
        });
    }

    void BuildNavigation()
    {
        if (navigation != null) return;
        var canvas = GuideUI.Canvas(transform, "SlothChapterVillageEntry", 31);
        navigation = canvas.gameObject;
        var stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
        stage.SetParent(canvas.transform, false);
        GuideUI.Center(stage, Vector2.zero, new Vector2(1920, 1080));
        InvestigationButton = GuideUI.Button("SlothInvestigation", stage, "教堂的求救\n与哈罗德交谈",
            new Vector2(80, 170), new Vector2(260, 80), content.font, OpenInvestigation);
        RefreshNavigation();
    }

    public void OpenInvestigation()
    {
        if (village == null || village.IsLoading || village.IsResting
            || village.Introduction != null && village.Introduction.IsOpen || BlocksVillage) return;
        PlayInvestigation(transform);
    }

    void RefreshNavigation()
    {
        if (navigation == null || village == null) return;
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        bool visible = state?.story != null && state.story.stage == MainStoryStage.TutorialCompleted
            && !VillageIntroRules.IsRestricted(state) && !IsBlocking && !village.IsLoading && !village.IsResting
            && (village.Introduction == null || !village.Introduction.IsOpen)
            && (village.panelRoot == null || !village.panelRoot.activeSelf);
        navigation.SetActive(visible);
        if (visible && InvestigationButton != null)
            InvestigationButton.GetComponentInChildren<TMP_Text>().text = state.story.slothInvestigated
                ? "教堂的求救\n重听调查线索" : "教堂的求救\n与哈罗德交谈";
    }

    void ShowEnding()
    {
        if (ending != null) return;
        if (village != null) village.PrepareStoryPresentation();
        if (navigation != null) navigation.SetActive(false);
        active = this;
        var canvas = GuideUI.Canvas(transform, "SlothChapterEnding", 300);
        ending = canvas.gameObject;
        var shade = GuideUI.Image("Blocker", canvas.transform, new Color(.025f, .02f, .015f, .84f));
        GuideUI.Stretch(shade.rectTransform);
        var panel = GuideUI.Image("EndingPaper", canvas.transform, new Color32(241, 229, 204, 255));
        GuideUI.Center(panel.rectTransform, Vector2.zero, new Vector2(1060, 650));
        var parchment = village != null && village.panelBag != null
            ? village.panelBag.GetComponentInChildren<InventoryParchmentView>(true) : null;
        if (parchment != null && parchment.sheet != null && parchment.sheet.sprite != null)
        { panel.sprite = parchment.sheet.sprite; panel.type = Image.Type.Sliced; panel.color = Color.white; }
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(79, 64, 43, 255); outline.effectDistance = new Vector2(2, -2);
        GuideUI.Label("Chapter", panel.transform, "懒惰篇 · 完", new Vector2(0, 220), new Vector2(900, 65), content.font, 38);
        GuideUI.Label("ThankYou", panel.transform, "感谢游玩", new Vector2(0, 115), new Vector2(900, 100), content.font, 64);
        GuideUI.Label("Message", panel.transform, "埃尔南多的故事，暂时告一段落。\n后续更新，敬请期待。", new Vector2(0, -5),
            new Vector2(850, 100), content.font, 30);
        GuideUI.Label("ClosingLine", panel.transform, "愿你我都不必，把该做的事留到明天。", new Vector2(0, -105),
            new Vector2(850, 60), content.font, 25);
        ReturnToMenuButton = GuideUI.Button("ReturnToMenu", panel.transform, "返回主菜单", new Vector2(-190, -230),
            new Vector2(300, 72), content.font, ReturnToMenu);
        QuitButton = GuideUI.Button("QuitGame", panel.transform, "退出游戏", new Vector2(190, -230),
            new Vector2(300, 72), content.font, QuitGame);
        ReturnToMenuButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnRight = QuitButton, selectOnLeft = QuitButton, selectOnUp = QuitButton, selectOnDown = QuitButton };
        QuitButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnRight = ReturnToMenuButton, selectOnLeft = ReturnToMenuButton,
            selectOnUp = ReturnToMenuButton, selectOnDown = ReturnToMenuButton };
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(ReturnToMenuButton.gameObject);
    }

    public void ReturnToMenu()
    {
        if (!IsEndingOpen || loadingMenu || !Application.CanStreamedLevelBeLoaded(GameFlowController.MenuScene)) return;
        loadingMenu = true;
        ReturnToMenuButton.interactable = QuitButton.interactable = false;
        if (GameRoot.I != null)
        {
            GameRoot.I.state = new GameState();
            GameRoot.I.enterVillageOnLoad = false;
            GameRoot.I.villageMapTemplate = null;
            GameRoot.I.villageReturnScene = null;
        }
        Time.timeScale = 1;
        SceneManager.LoadSceneAsync(GameFlowController.MenuScene);
    }

    public void QuitGame()
    {
        if (!IsEndingOpen || loadingMenu) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        if (view != null && view.Manager != null) view.Manager.NodeShown -= OnNodeShown;
        if (active == this) active = null;
        if (activeDialogue != null) Destroy(activeDialogue);
    }
}
