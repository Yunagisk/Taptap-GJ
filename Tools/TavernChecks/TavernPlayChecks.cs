#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Exercises installed scenes and prefabs; fixtures supply appeared events, never replacement UI.
public sealed class TavernPlayChecks : MonoBehaviour
{
    readonly string output = Path.GetFullPath("..");
    readonly List<string> checks = new List<string>();
    readonly List<string> errors = new List<string>();
    string saveDirectory;
    int fixtureSequence;
    WorldMapTemplate template;
    MapLayout layout;
    VillageScreen village;
    TavernEventBrowserUI browser;
    GameState State => GameRoot.I.state;

    [Serializable] sealed class Result { public bool passed; public string[] checks, errors; }
    void Awake()
    {
        Directory.CreateDirectory(output);
        saveDirectory = Path.Combine(output, "saves", Guid.NewGuid().ToString("N"));
        Application.logMessageReceived += Logged;
    }
    void OnDestroy() { Application.logMessageReceived -= Logged; }
    void Logged(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }
    void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks.Add(message);
    }
    IEnumerator Start()
    {
        var work = new Stack<IEnumerator>(); work.Push(Exercise());
        while (work.Count > 0)
        {
            object next;
            try
            {
                if (!work.Peek().MoveNext()) { work.Pop(); continue; }
                next = work.Peek().Current;
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(); yield break; }
            if (next is IEnumerator nested) work.Push(nested); else yield return next;
        }
        Finish();
    }
    void Finish()
    {
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result {
            passed = errors.Count == 0, checks = checks.ToArray(), errors = errors.ToArray() }, true));
        EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
    }

    GameState Fresh()
    {
        var state = NewRunFactory.Create(layout, "sword", 24680);
        state.story.useVillageIntroduction = true;
        state.story.guideCompleted = state.story.guideEquipmentReady = state.story.guideOutsideReady = true;
        state.story.stage = MainStoryStage.TutorialCompleted;
        state.story.slothHookSeen = true;
        state.world.time = 2; // Every choice crosses dawn and exercises its saved settlement.
        state.world.sin = 40;
        state.player.hp = 35;
        state.world.villageEvents = new VillageEventState {
            initialized = true, randomSeed = state.map.randomSeed, lastProcessedTime = 2
        };
        return state;
    }
    VillageEventOccurrence Open(string id, int lifetime = 99)
    {
        village.ClosePanels();
        GameRoot.I.state = Fresh();
        // Independent cases have independent slots; avoid a previous case's dawn masking a failed save.
        GameRoot.I.SaveSlot = new DayStartSaveStore(Path.Combine(saveDirectory, (++fixtureSequence) + "-morning.json"));
        var occurrence = new VillageEventOccurrence {
            id = "test:" + id, definitionId = id, appearedTime = State.world.time,
            expiresTime = State.world.time + lifetime
        };
        State.world.villageEvents.occurrences.Add(occurrence);
        village.OpenTavern();
        browser.SelectEvent(1);
        return occurrence;
    }
    Button Row(string title)
    {
        return browser.eventListContent.GetComponentsInChildren<Button>(false).FirstOrDefault(button =>
            button.name.StartsWith("TimedEvent_")
            && button.transform.Find("EventTitle").GetComponent<TMP_Text>().text == title);
    }
    void Click(Button button)
    {
        Check(button != null && button.isActiveAndEnabled && button.IsInteractable(), "control accepts input");
        EventSystem.current.SetSelectedGameObject(button.gameObject);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }
    void CheckReleased(string title)
    {
        Check(string.IsNullOrEmpty(browser.SelectedOccurrenceId), title + ": clears timed selection");
        Check(browser.choiceButtons.All(button => !button.gameObject.activeSelf), title + ": hides all old choices");
        Check(browser.detailTitle.text == "村民闲谈" && browser.actionButton.isActiveAndEnabled,
            title + ": restores the previously selected conversation");
        Check(Row(title) == null, title + ": removes the old list row");
        Check(EventSystem.current.currentSelectedGameObject == browser.GetPreferredFocus(),
            title + ": moves keyboard focus to a usable control");
    }

    IEnumerator Exercise()
    {
        yield return null; yield return null;
        var opening = FindObjectOfType<MainMenuOpening>();
        if (opening != null) opening.SkipOpening();
        template = FindObjectOfType<GameFlowController>().template;
        layout = template.Load();
        GameRoot.I.SaveSlot = new DayStartSaveStore(Path.Combine(saveDirectory, "initial-morning.json"));
        GameRoot.I.state = Fresh();
        GameRoot.I.villageMapTemplate = template;
        var loading = SceneManager.LoadSceneAsync("Village");
        while (!loading.isDone) yield return null;
        yield return null; yield return null;
        village = FindObjectOfType<VillageScreen>();
        browser = village.panelEvent.GetComponent<TavernEventBrowserUI>();
        Check(village.CanUseFacilities && browser != null, "installed village opens its tavern browser");

        int choices = 0;
        for (int number = 1; number <= 13; number++)
        {
            var definition = VillageEventRules.FindDefinition("v" + number.ToString("00"));
            for (int index = 0; index < definition.choices.Length; index++)
            {
                var occurrence = Open(definition.id);
                string before = JsonUtility.ToJson(State);
                Click(Row(definition.title));
                Check(JsonUtility.ToJson(State) == before, definition.title + ": preview is free");
                Check(browser.SelectedOccurrenceId == occurrence.id, definition.title + ": selects the real row");
                var choice = definition.choices[index];
                var button = browser.choiceButtons[index];
                if (definition.id == "v08" && index == 0) Capture("scapegoat-before");
                Click(button);
                choices++;
                Check(occurrence.resolved && occurrence.chosenIndex == index,
                    definition.title + "/" + index + ": resolves the selected option");
                Check(State.world.time == 2 + choice.timeCost && State.world.sin == 40 + choice.sinDelta
                    && State.player.hp == 35 + choice.healAmount,
                    definition.title + "/" + index + ": applies time, sin and healing once");
                CheckReleased(definition.title);
                Check(browser.story.messageText.text.Contains(definition.title)
                    && browser.story.messageText.text.Contains(choice.label), "keeps settlement feedback visible");
                string settled = JsonUtility.ToJson(State);
                button.onClick.Invoke(); browser.ChooseTimedEvent(index); browser.SelectTimedEvent(occurrence.id);
                Check(JsonUtility.ToJson(State) == settled && string.IsNullOrEmpty(browser.SelectedOccurrenceId),
                    "stale and duplicate input cannot charge or reopen a resolved event");
                Check(string.IsNullOrEmpty(GameRoot.I.LastSaveError), "dawn snapshot writes: " + GameRoot.I.LastSaveError);
                Check(GameRoot.I.SaveSlot.TryLoad(layout, out var morning, out var error), "dawn snapshot loads: " + error);
                var savedOccurrence = morning.world.villageEvents.occurrences.Find(item => item.id == occurrence.id);
                Check(savedOccurrence != null && savedOccurrence.resolved
                    && morning.world.sin == State.world.sin
                    && JsonUtility.ToJson(morning.inventory) == JsonUtility.ToJson(State.inventory),
                    "dawn snapshot contains the resolved choice and its actual rewards");
                village.ClosePanels(); village.OpenTavern();
                CheckReleased(definition.title);
                Check(JsonUtility.ToJson(State) == settled, "reopening is free and preserves settlement");
                if (definition.id == "v08" && index == 0) Capture("scapegoat-after");
                yield return null;
            }
        }
        Check(choices == 27, "all 27 options across all 13 timed events pass native UI checks");

        var restricted = Open("v01");
        Click(Row("粮仓分配"));
        State.story.guideCompleted = false;
        browser.Refresh();
        string blocked = JsonUtility.ToJson(State);
        browser.ChooseTimedEvent(1); browser.WaitOneUnit(); browser.SelectEvent(2);
        Check(!village.CanUseFacilities && JsonUtility.ToJson(State) == blocked
            && browser.SelectedOccurrenceId == restricted.id, "facility restrictions block stale tavern actions");

        var live = Open("v08", 3);
        Click(Row("替罪羊"));
        string preview = JsonUtility.ToJson(State);
        browser.ChooseTimedEvent(-1); browser.ChooseTimedEvent(3);
        Check(JsonUtility.ToJson(State) == preview && browser.SelectedOccurrenceId == live.id,
            "invalid choices preserve the live selection without spending resources");
        village.ClosePanels(); village.OpenTavern();
        Check(browser.SelectedOccurrenceId == live.id && JsonUtility.ToJson(State) == preview,
            "reopening preserves a live selection without rerolling events");
        Click(browser.waitButton);
        Check(browser.SelectedOccurrenceId == live.id && !live.expired && !live.resolved
            && State.world.time == 3 && State.world.sin == 40, "waiting preserves an event until its deadline");
        village.ClosePanels();
        string closed = JsonUtility.ToJson(State);
        browser.choiceButtons[0].onClick.Invoke(); browser.WaitOneUnit();
        Check(JsonUtility.ToJson(State) == closed, "closed tavern ignores stale choice and wait callbacks");
        State.world.time = live.expiresTime;
        village.OpenTavern();
        CheckReleased("替罪羊");

        var expiring = Open("v08", 1);
        Click(Row("替罪羊"));
        Click(browser.waitButton);
        Check(expiring.expired && !expiring.resolved && State.world.time == 3 && State.world.sin == 40,
            "waiting expires an event without applying its option or natural village sin");
        CheckReleased("替罪羊");

        expiring = Open("v08", 1);
        Click(Row("替罪羊"));
        State.world.time = expiring.expiresTime;
        yield return new WaitForSecondsRealtime(.2f);
        CheckReleased("替罪羊");

        var conditional = Open("v13");
        Click(Row("酒馆密谋"));
        State.map.defeatedBossNodes.Add(State.story.slothNodeId);
        browser.Refresh();
        Check(conditional.expired && !conditional.resolved, "living-Sloth condition expires the conditional event");
        CheckReleased("酒馆密谋");

        Open("v01");
        ItemRules.TryGrant(State, ItemTable.Bandage);
        Click(Row("粮仓分配"));
        Check(browser.choiceLabels[1].text.Contains("携带已满"), "full reward is disclosed before choosing");
        Click(browser.choiceButtons[1]);
        Check(ItemRules.Count(State.inventory, ItemTable.Bandage) == 1
            && browser.story.messageText.text.Contains("未领取奖励"), "full bag produces an honest settlement receipt");
        CheckReleased("粮仓分配");

        yield return Conversations();
        yield return Investigation();
    }

    IEnumerator Conversations()
    {
        Open("v01");
        State.story.useVillageIntroduction = false;
        State.story.stage = MainStoryStage.ArrivedVillage;
        for (int index = 0; index < 3; index++)
        {
            browser.SelectEvent(index);
            string before = JsonUtility.ToJson(State);
            Click(browser.actionButton);
            var manager = browser.story.dialogueManager;
            Check(manager.IsPlaying && !browser.story.eventListRoot.activeSelf, "conversation owns the visible tavern UI");
            browser.ChooseTimedEvent(0); browser.WaitOneUnit();
            Check(JsonUtility.ToJson(State) == before, "event and wait callbacks are blocked during conversation");
            Check(browser.story.ReturnToEventList(), "unfinished conversation returns to the event list");
            Check(JsonUtility.ToJson(State) == before, "cancelling a conversation does not advance the quest");
            browser.Refresh();
            Click(browser.actionButton);
            yield return Walk(manager);
            Check(State.world.time == 2 && State.world.sin == 40, "completed conversation costs no time or sin");
            Check(State.story.stage == MainStoryStage.VillageIntroductionComplete,
                index == 0 ? "welcome advances the main quest" : "side conversation preserves the main quest");
            Check(browser.story.eventListRoot.activeInHierarchy && browser.actionButton.isActiveAndEnabled,
                "completed conversation restores the event browser");
        }
        browser.SelectEvent(0); Click(browser.actionButton);
        yield return Walk(browser.story.dialogueManager);
        Check(State.story.stage == MainStoryStage.CommissionAccepted, "mayor conversation accepts the commission");
        Click(browser.actionButton); yield return Walk(browser.story.dialogueManager);
        Check(State.story.stage == MainStoryStage.CommissionAccepted, "repeated commission conversation does not skip progression");
    }
    IEnumerator Investigation()
    {
        Open("v01"); browser.SelectEvent(0); Click(browser.actionButton);
        Check(village.SlothChapter.IsOpen && !village.panelEvent.activeInHierarchy,
            "post-tutorial tavern entry opens the current chapter investigation");
        string before = JsonUtility.ToJson(State);
        browser.ChooseTimedEvent(0); browser.WaitOneUnit();
        Check(JsonUtility.ToJson(State) == before, "hidden tavern callbacks cannot settle during chapter presentation");
        yield return Walk(village.SlothChapter.Manager);
        Check(State.story.slothInvestigated, "chapter investigation completes through real dialogue");
        village.OpenTavern(); browser.SelectEvent(1);
        Check(browser.actionButton.interactable, "tavern remains usable after chapter investigation");
    }
    IEnumerator Walk(DialogueManager manager)
    {
        int investigationQuestions = 0;
        for (int i = 0; i < 200 && manager.IsPlaying; i++)
        {
            if (manager.ui.CanChoose)
            {
                // The investigation's first two questions deliberately return to the menu.
                int index = manager.CurrentNodeId == "questions" ? Math.Min(2, investigationQuestions++) : 0;
                Click(manager.ui.choiceParent.GetComponentsInChildren<Button>(false)[index]);
            }
            else Click(manager.ui.continueButton);
            yield return null;
        }
        Check(!manager.IsPlaying, "conversation reaches its authored ending");
    }
    void Capture(string name)
    {
        var camera = Camera.main;
        var canvases = FindObjectsOfType<Canvas>().Where(canvas => canvas.isRootCanvas).ToArray();
        var modes = canvases.Select(canvas => canvas.renderMode).ToArray();
        var cameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        var distances = canvases.Select(canvas => canvas.planeDistance).ToArray();
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = new RenderTexture(1920, 1080, 24); target.Create();
        var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            foreach (var canvas in canvases)
            { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++)
            { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            Destroy(pixels); target.Release(); Destroy(target);
        }
    }
}
#endif
