using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Selecting a row only previews it. The rules settle time/resources once, when an option is confirmed.
public class TavernEventBrowserUI : MonoBehaviour
{
    public VillageStoryPanelUI story;
    public VillageScreen villageScreen;
    public Button[] eventButtons = new Button[3];
    public Image[] eventPortraits = new Image[3];
    public TMP_Text[] eventTitles = new TMP_Text[3];
    public TMP_Text[] eventSummaries = new TMP_Text[3];
    public Image[] selectionFrames = new Image[3];
    public TMP_Text detailTitle;
    public TMP_Text detailDescription;
    public TMP_Text detailMetaText;
    public TMP_Text resourcesText;
    public TMP_Text rewardHistoryText;
    public TMP_Text actionLabel;
    public Image detailPortrait;
    public Button actionButton;
    public RectTransform eventListContent;
    public Button timedEventTemplate;
    public TMP_Text timedEventEmptyText;
    public Button waitButton;
    public Button[] choiceButtons = new Button[3];
    public TMP_Text[] choiceLabels = new TMP_Text[3];
    public Sprite chiefPortrait;
    public Sprite villagerPortrait;
    public Sprite travelerPortrait;
    public DialogueData villageChatter;
    public DialogueData tavernRumor;

    public int SelectedEventIndex { get { return selectedEventIndex; } }
    public string SelectedOccurrenceId { get { return selectedOccurrenceId; } }

    sealed class TimedRow
    {
        public Button button;
        public TMP_Text title, summary;
        public Image portrait, frame;
        public string occurrenceId;
        public UnityAction select;
        public bool bound;
    }

    int selectedEventIndex;
    string selectedOccurrenceId = "";
    float nextRefresh;
    bool settling;
    UnityAction[] selectionActions, choiceActions;
    readonly Button[] boundEventButtons = new Button[3];
    readonly Button[] boundChoiceButtons = new Button[3];
    readonly List<TimedRow> rows = new List<TimedRow>();
    Button boundActionButton, boundWaitButton;
    DialogueManager boundDialogueManager;
    WorldMapTemplate loadedTemplate;
    MapLayout cachedLayout;
    bool layoutLoadAttempted;
    bool readableLayoutApplied;
    double inputAllowedAt;

    void OnEnable()
    {
        ApplyReadableLayout();
        loadedTemplate = null;
        cachedLayout = null;
        layoutLoadAttempted = false;
        if (selectionActions == null)
        {
            selectionActions = new UnityAction[3];
            choiceActions = new UnityAction[3];
            for (int i = 0; i < selectionActions.Length; i++)
            {
                int index = i;
                selectionActions[i] = () => SelectEvent(index);
                choiceActions[i] = () => ChooseTimedEvent(index);
            }
        }
        for (int i = 0; i < boundEventButtons.Length; i++)
        {
            boundEventButtons[i] = At(eventButtons, i);
            if (boundEventButtons[i] != null)
            {
                GameAudioButton.UseManualConfirmation(boundEventButtons[i]);
                boundEventButtons[i].onClick.AddListener(selectionActions[i]);
            }
            boundChoiceButtons[i] = At(choiceButtons, i);
            if (boundChoiceButtons[i] != null)
            {
                GameAudioButton.UseManualConfirmation(boundChoiceButtons[i]);
                boundChoiceButtons[i].onClick.AddListener(choiceActions[i]);
            }
        }
        boundActionButton = actionButton;
        if (boundActionButton != null)
        {
            GameAudioButton.UseManualConfirmation(boundActionButton);
            boundActionButton.onClick.AddListener(StartSelectedEvent);
        }
        boundWaitButton = waitButton;
        if (boundWaitButton != null)
        {
            GameAudioButton.UseManualConfirmation(boundWaitButton);
            boundWaitButton.onClick.AddListener(WaitOneUnit);
        }
        foreach (var row in rows) BindRow(row);
        boundDialogueManager = story != null ? story.dialogueManager : null;
        if (boundDialogueManager != null) boundDialogueManager.DialogueCompleted += OnDialogueCompleted;
        selectedEventIndex = Mathf.Clamp(selectedEventIndex, 0, 2);
        nextRefresh = 0;
        HoldCurrentPresentation();
        Refresh();
    }

    void ApplyReadableLayout()
    {
        if (readableLayoutApplied) return;
        readableLayoutApplied = true;
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            label.enableAutoSizing = false;
            label.fontSize = Mathf.Max(24, label.fontSize);
        }
        foreach (var button in GetComponentsInChildren<Button>(true))
            if (button.targetGraphic is Image paper) paper.pixelsPerUnitMultiplier = 12;
        foreach (var button in eventButtons) ReadableEventRow(button);
        ReadableEventRow(timedEventTemplate);
        if (detailMetaText != null) detailMetaText.rectTransform.sizeDelta = new Vector2(605, 32);
        if (detailDescription != null)
        {
            detailDescription.fontSize = 24;
            detailDescription.rectTransform.sizeDelta = new Vector2(404, 120);
            ScrollableTextRegion.Ensure(detailDescription);
        }
        if (detailPortrait != null) detailPortrait.rectTransform.sizeDelta = new Vector2(184, 120);
        if (story != null && story.messageText != null) ScrollableTextRegion.Ensure(story.messageText);
        if (story != null && story.questText != null)
        {
            story.questText.rectTransform.anchoredPosition = new Vector2(46, -784);
            story.questText.rectTransform.sizeDelta = new Vector2(1025, 64);
            ScrollableTextRegion.Ensure(story.questText);
        }
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null) continue;
            var rect = (RectTransform)choiceButtons[i].transform;
            rect.anchoredPosition = new Vector2(26, -244 - i * 90);
            rect.sizeDelta = new Vector2(612, 88);
        }
    }

    static void ReadableEventRow(Button button)
    {
        if (button == null) return;
        var layout = button.GetComponent<LayoutElement>();
        if (layout != null) layout.minHeight = layout.preferredHeight = 108;
        var title = Child<TMP_Text>(button, "EventTitle");
        if (title != null) title.rectTransform.sizeDelta = new Vector2(234, 34);
        var summary = Child<TMP_Text>(button, "EventSummary");
        if (summary != null)
        {
            summary.rectTransform.anchoredPosition = new Vector2(85, -48);
            summary.rectTransform.sizeDelta = new Vector2(236, 56);
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < boundEventButtons.Length; i++)
        {
            if (boundEventButtons[i] != null && selectionActions != null)
                boundEventButtons[i].onClick.RemoveListener(selectionActions[i]);
            if (boundChoiceButtons[i] != null && choiceActions != null)
                boundChoiceButtons[i].onClick.RemoveListener(choiceActions[i]);
            boundEventButtons[i] = null;
            boundChoiceButtons[i] = null;
        }
        if (boundActionButton != null) boundActionButton.onClick.RemoveListener(StartSelectedEvent);
        if (boundWaitButton != null) boundWaitButton.onClick.RemoveListener(WaitOneUnit);
        boundActionButton = boundWaitButton = null;
        foreach (var row in rows)
        {
            if (row.button != null && row.bound) row.button.onClick.RemoveListener(row.select);
            row.bound = false;
        }
        if (boundDialogueManager != null) boundDialogueManager.DialogueCompleted -= OnDialogueCompleted;
        boundDialogueManager = null;
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.15f;
        Refresh();
    }

    public void SelectEvent(int index)
    {
        if (index < 0 || index > 2 || !CanInteract()) return;
        var button = At(eventButtons, index);
        if (button == null || !button.isActiveAndEnabled || !button.interactable) return;
        selectedEventIndex = index;
        selectedOccurrenceId = "";
        Say("");
        HoldCurrentPresentation();
        GameAudio.Play(GameAudioCue.UiConfirm);
        Refresh();
    }

    public void SelectTimedEvent(string occurrenceId)
    {
        if (string.IsNullOrEmpty(occurrenceId) || !CanInteract()) return;
        var state = CurrentState();
        var layout = Layout();
        if (state == null || layout == null) return;
        var active = VillageEventRules.GetActiveEvents(state, layout);
        if (active.Find(item => item.id == occurrenceId) == null) return;
        selectedOccurrenceId = occurrenceId;
        Say("");
        HoldCurrentPresentation();
        GameAudio.Play(GameAudioCue.UiConfirm);
        Refresh();
    }

    public void ChooseTimedEvent(int index)
    {
        var button = At(choiceButtons, index);
        if (!CanInteract() || string.IsNullOrEmpty(selectedOccurrenceId) || button == null
            || !button.isActiveAndEnabled || !button.interactable) return;
        var state = CurrentState();
        var layout = Layout();
        if (state == null || layout == null) return;
        settling = true;
        try
        {
            var result = VillageEventRules.TryChoose(state, layout, selectedOccurrenceId, index);
            Say(result.message);
            GameAudio.Play(GameAudioCue.UiConfirm);
        }
        finally { settling = false; }
        Refresh();
        FocusPreferredControl();
    }

    public void WaitOneUnit()
    {
        if (!CanInteract() || waitButton == null || !waitButton.isActiveAndEnabled || !waitButton.interactable) return;
        var state = CurrentState();
        var layout = Layout();
        if (state == null || state.world == null || state.map == null || layout == null) return;
        settling = true;
        try
        {
            WorldResourceRules.SpendTime(layout, state.map, state.world, 1, true);
            Say("在酒馆等候了 1 单位时间。村内不会自然增加罪恶。");
            GameAudio.Play(GameAudioCue.UiConfirm);
        }
        finally { settling = false; }
        Refresh();
    }

    public void Refresh()
    {
        if (story != null && story.isActiveAndEnabled) story.Refresh();
        var state = CurrentState();
        var layout = Layout();
        bool canInteract = CanInteract(false);
        var active = state != null && layout != null
            ? VillageEventRules.GetActiveEvents(state, layout) : new List<VillageEventOccurrence>();
        var selectedOccurrence = active.Find(item => item.id == selectedOccurrenceId);
        bool selectionEnded = !string.IsNullOrEmpty(selectedOccurrenceId) && selectedOccurrence == null;
        // Completion, expiry and reopening must all release the old detail and its choices.
        if (selectionEnded)
        {
            selectedOccurrenceId = "";
            HoldCurrentPresentation();
        }
        var stage = state != null && state.story != null ? state.story.stage : MainStoryStage.ArrivedVillage;
        string mainTitle, mainSummary, mainDescription;
        DescribeMainEvent(stage, out mainTitle, out mainSummary, out mainDescription);
        Sprite mainPortrait = stage == MainStoryStage.ArrivedVillage ? villagerPortrait : chiefPortrait;
        bool timedSelected = selectedOccurrence != null;
        for (int i = 0; i < 3; i++)
        {
            SetText(At(eventTitles, i), i == 0 ? mainTitle : i == 1 ? "村民闲谈" : "酒馆见闻");
            SetText(At(eventSummaries, i), i == 0 ? mainSummary
                : i == 1 ? "听听村里人的生活。" : "向旅人打听村外消息。");
            SetPortrait(At(eventPortraits, i), i == 0 ? mainPortrait : i == 1 ? villagerPortrait : travelerPortrait);
            var frame = At(selectionFrames, i);
            if (frame != null) frame.enabled = !timedSelected && i == selectedEventIndex;
            var button = At(eventButtons, i);
            if (button != null) button.interactable = canInteract;
        }
        RefreshTimedRows(active, canInteract);
        if (waitButton != null) waitButton.interactable = canInteract && state != null && layout != null;
        if (state != null && state.world != null && state.player != null)
        {
            SetText(resourcesText, WorldResourceRules.TimeLabel(state.world) + "    生命 "
                + state.player.hp + "/" + state.player.maxHp + "    罪恶 " + state.world.sin
                + "/100    罪骸 " + state.world.sinRemnant);
            SetText(rewardHistoryText, "");
            SetActive(rewardHistoryText, false);
        }
        if (timedSelected) RefreshTimedDetail(state, selectedOccurrence, canInteract);
        else
        {
            SetActive(actionButton, true);
            for (int i = 0; i < 3; i++) SetActive(At(choiceButtons, i), false);
            SetText(detailTitle, selectedEventIndex == 0 ? mainTitle : selectedEventIndex == 1 ? "村民闲谈" : "酒馆见闻");
            string description = selectedEventIndex == 0 ? mainDescription
                : selectedEventIndex == 1 ? "靠近炉火坐一会儿。\n\n村民压低声音，谈起田地、夜里的风声，以及迟迟没有归来的人。"
                : "门边的旅人还未拂去斗篷上的尘土。\n\n也许他听说过村外的道路，以及雾中那些不该靠近的地方。";
            bool available = HasSelectedConversation(state, stage);
            if (!available) description += "\n\n此刻无人应答，稍后再来坐坐吧。";
            SetText(detailDescription, description);
            SetText(detailMetaText, "交谈不消耗时间");
            SetPortrait(detailPortrait, selectedEventIndex == 0 ? mainPortrait : selectedEventIndex == 1 ? villagerPortrait : travelerPortrait);
            SetText(actionLabel, !available ? "暂时无法交谈" : selectedEventIndex != 0 ? "交谈"
                : stage == MainStoryStage.SlothDefeated ? "交付委托" : "开始交谈");
            if (actionButton != null) actionButton.interactable = canInteract && available;
        }
        if (layout == null) SetText(detailMetaText, "酒馆配置暂不可用，请稍后重新打开。");
        if (villageScreen != null) villageScreen.RefreshStatus();
        if (selectionEnded && canInteract) FocusPreferredControl();
    }

    void RefreshTimedRows(List<VillageEventOccurrence> active, bool canInteract)
    {
        SetActive(timedEventEmptyText, active.Count == 0);
        SetText(timedEventEmptyText, "此刻没有待处理的限时事件。\n晚些时候再来看看吧。");
        if (eventListContent == null || timedEventTemplate == null) return;
        for (int i = 0; i < active.Count; i++)
        {
            TimedRow row;
            if (i < rows.Count) row = rows[i];
            else
            {
                var button = Instantiate(timedEventTemplate, eventListContent);
                button.name = "TimedEvent_" + i;
                button.onClick = new Button.ButtonClickedEvent();
                row = new TimedRow { button = button,
                    title = Child<TMP_Text>(button, "EventTitle"), summary = Child<TMP_Text>(button, "EventSummary"),
                    portrait = Child<Image>(button, "Portrait"), frame = Child<Image>(button, "SelectionFrame") };
                var captured = row;
                row.select = () => SelectTimedEvent(captured.occurrenceId);
                rows.Add(row);
                BindRow(row);
            }
            var occurrence = active[i];
            var definition = VillageEventRules.FindDefinition(occurrence.definitionId);
            row.occurrenceId = occurrence.id;
            row.button.transform.SetAsLastSibling();
            SetActive(row.button, true);
            row.button.interactable = canInteract;
            SetText(row.title, definition != null ? definition.title : occurrence.definitionId);
            SetText(row.summary, "限时 · " + ExpiryLabel(occurrence));
            SetPortrait(row.portrait, occurrence.definitionId == "v13" ? travelerPortrait : villagerPortrait);
            if (row.frame != null) row.frame.enabled = occurrence.id == selectedOccurrenceId;
        }
        for (int i = active.Count; i < rows.Count; i++) SetActive(rows[i].button, false);
    }

    void RefreshTimedDetail(GameState state, VillageEventOccurrence occurrence, bool canInteract)
    {
        SetActive(actionButton, false);
        var definition = occurrence != null ? VillageEventRules.FindDefinition(occurrence.definitionId) : null;
        SetText(detailTitle, definition != null ? definition.title : "限时事件");
        SetText(detailDescription, definition != null ? definition.description : "该事件当前不可用。");
        SetText(detailMetaText, ExpiryLabel(occurrence));
        SetPortrait(detailPortrait, definition != null && definition.id == "v13" ? travelerPortrait : villagerPortrait);
        float choiceHeight = definition != null && definition.choices.Length <= 2 ? 112 : 88;
        for (int i = 0; i < 3; i++)
        {
            bool exists = definition != null && definition.choices != null && i < definition.choices.Length;
            var button = At(choiceButtons, i);
            SetActive(button, exists);
            if (button != null)
            {
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(26, -244 - i * (choiceHeight + 2));
                rect.sizeDelta = new Vector2(612, choiceHeight);
            }
            if (button != null) button.interactable = exists && canInteract;
            if (exists) SetText(At(choiceLabels, i), ChoiceLabel(definition.choices[i], state));
        }
    }

    public GameObject GetPreferredFocus()
    {
        if (story != null && story.dialogueManager != null && story.dialogueManager.IsPlaying)
            return story.GetPreferredFocus();
        if (!string.IsNullOrEmpty(selectedOccurrenceId) && choiceButtons != null)
            foreach (var button in choiceButtons)
                if (button != null && button.isActiveAndEnabled && button.interactable) return button.gameObject;
        if (actionButton != null && actionButton.isActiveAndEnabled && actionButton.interactable) return actionButton.gameObject;
        return waitButton != null ? waitButton.gameObject : null;
    }

    bool CanInteract(bool checkInputDelay = true)
    {
        if (checkInputDelay && (Time.unscaledTimeAsDouble < inputAllowedAt || !GameAudio.UiConfirmReady)) return false;
        var state = CurrentState();
        var layout = Layout();
        return !settling && state != null && state.map != null && layout != null
            && state.map.currentNodeId == layout.villageNodeId
            && string.IsNullOrEmpty(state.map.pendingRequestId) && string.IsNullOrEmpty(state.map.pendingNodeId)
            && isActiveAndEnabled && story != null && story.isActiveAndEnabled
            && (villageScreen == null || villageScreen.CanUseFacilities)
            && story.dialogueManager != null && !story.dialogueManager.IsPlaying
            && (story.eventListRoot == null || story.eventListRoot.activeInHierarchy);
    }

    GameState CurrentState() { return GameRoot.I != null ? GameRoot.I.state : null; }

    MapLayout Layout()
    {
        var template = story != null ? story.template : null;
        if (template == null) return null;
        if (layoutLoadAttempted && template == loadedTemplate) return cachedLayout;
        loadedTemplate = template;
        layoutLoadAttempted = true;
        cachedLayout = null;
        try
        {
            var parsed = template.Load();
            MapLayoutValidator.Validate(parsed);
            cachedLayout = parsed;
        }
        catch (Exception)
        {
            // A broken configuration disables time/event actions until the tavern is reopened.
            // Keep the failed attempt cached so the periodic refresh never repeats parsing or logs.
            return null;
        }
        return cachedLayout;
    }

    void BindRow(TimedRow row)
    {
        if (row.button == null || row.bound) return;
        GameAudioButton.UseManualConfirmation(row.button);
        row.button.onClick.AddListener(row.select);
        row.bound = true;
    }

    bool HasSelectedConversation(GameState state, MainStoryStage stage)
    {
        if (story == null || story.dialogueManager == null || story.dialogueManager.ui == null) return false;
        DialogueData data;
        if (selectedEventIndex == 0)
        {
            if (state == null || state.story == null) return false;
            switch (stage)
            {
                case MainStoryStage.ArrivedVillage: data = story.welcomeDialogue; break;
                case MainStoryStage.VillageIntroductionComplete:
                case MainStoryStage.CommissionAccepted: data = story.commissionDialogue; break;
                case MainStoryStage.TutorialCompleted: data = story.afterTutorialDialogue; break;
                case MainStoryStage.SlothDefeated: data = story.turnInDialogue; break;
                case MainStoryStage.CommissionTurnedIn: data = story.afterTurnInDialogue; break;
                default: return false;
            }
        }
        else
        {
            data = selectedEventIndex == 1 ? villageChatter : tavernRumor;
            if (data != null && data.completionEffect != StoryDialogueEffect.None) return false;
        }
        return data != null && data.startNode != null;
    }

    void StartSelectedEvent()
    {
        if (!CanInteract() || !string.IsNullOrEmpty(selectedOccurrenceId) || actionButton == null
            || !actionButton.isActiveAndEnabled || !actionButton.interactable) return;
        var state = CurrentState();
        var stage = state != null && state.story != null ? state.story.stage : MainStoryStage.ArrivedVillage;
        if (!HasSelectedConversation(state, stage)) { Refresh(); return; }
        if (selectedEventIndex == 0) story.TalkToVillage();
        else
        {
            Say("");
            story.dialogueManager.StartDialogue(selectedEventIndex == 1 ? villageChatter : tavernRumor);
        }
        GameAudio.Play(GameAudioCue.UiConfirm);
        Refresh();
        FocusPreferredControl();
    }

    void HoldCurrentPresentation()
    { inputAllowedAt = Time.unscaledTimeAsDouble + GameAudio.DialogueMinimumDisplayTime; }

    void Say(string message)
    {
        if (story != null) SetText(story.messageText, message);
        if (!string.IsNullOrEmpty(message)) HoldCurrentPresentation();
    }
    void OnDialogueCompleted(DialogueData data) { Refresh(); }

    void FocusPreferredControl()
    {
        var target = GetPreferredFocus();
        if (EventSystem.current != null && target != null && target.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(target);
    }

    static string ChoiceLabel(VillageEventChoice choice, GameState state)
    {
        string text = choice.label + "\n" + choice.timeCost + " 单位时间 · 罪恶 "
            + (choice.sinDelta >= 0 ? "+" : "") + choice.sinDelta;
        if (choice.healAmount > 0) text += " · 生命 +" + choice.healAmount;
        if (choice.nextDirectHitReductionPercent > 0) text += " · 首次直接受击 -" + choice.nextDirectHitReductionPercent + "%";
        if (choice.nextAttackBonusPercent > 0) text += " · 下次完整攻击 +" + choice.nextAttackBonusPercent + "%";
        if (choice.nextCampRecoveryBonusPercent > 0) text += " · 下次营地恢复 +" + choice.nextCampRecoveryBonusPercent + "%";
        if (!string.IsNullOrEmpty(choice.itemId))
        {
            var item = ItemTable.Find(choice.itemId);
            bool full = item != null && ItemRules.Count(state?.inventory, choice.itemId) >= item.carryLimit;
            text += "\n" + choice.rewardText + (full ? "（携带已满，无法领取）" : "");
        }
        if (choice.resolveGain > 0) text += "\n" + choice.rewardText + "（上限 3）";
        return text;
    }

    static string ExpiryLabel(VillageEventOccurrence occurrence)
    {
        string[] periods = { "早晨", "下午", "夜间" };
        int time = Math.Max(0, occurrence.expiresTime);
        return "至第 " + (time / 3 + 1) + " 天" + periods[time % 3] + "前";
    }

    static T Child<T>(Button button, string name) where T : Component
    {
        var child = button.transform.Find(name);
        return child != null ? child.GetComponent<T>() : null;
    }

    static T At<T>(T[] array, int index) where T : class
    {
        return array != null && index >= 0 && index < array.Length ? array[index] : null;
    }

    static void SetText(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
        {
            target.text = value;
            ScrollableTextRegion.Reset(target);
        }
    }

    static void SetActive(Component target, bool active)
    {
        if (target != null && target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
    }

    static void SetPortrait(Image target, Sprite sprite)
    {
        if (target == null) return;
        CharacterPortraitStyle.Apply(target, sprite);
        target.enabled = sprite != null;
    }

    static void DescribeMainEvent(MainStoryStage stage, out string title, out string summary, out string description)
    {
        switch (stage)
        {
            case MainStoryStage.VillageIntroductionComplete:
                title = "村长的委托";
                summary = "村长有件事想拜托你。";
                description = "村长在角落里等着你。\n\n村外的威胁日渐逼近，他需要一个愿意走出村庄的人。听听他的委托，再决定接下来的路。";
                break;
            case MainStoryStage.CommissionAccepted:
                title = "清剿的约定";
                summary = "出发前，再确认一次委托。";
                description = "村长仍在等候清剿的消息。\n\n先前往村外的战场，完成第一次清剿。若对委托还有疑问，可以再与他交谈。";
                break;
            case MainStoryStage.TutorialCompleted:
                title = "教堂的求救";
                summary = "西奥多让求医的人明天再来。";
                description = "有人正在等一碗药，西奥多却带着信徒跪在暮林的祭所。\n\n听哈罗德与埃德蒙神父说说事情的原委，再去见那个不肯站起来的人。";
                break;
            case MainStoryStage.SlothDefeated:
                title = "归来的消息";
                summary = "向村长报告懒惰已被击败。";
                description = "懒惰已经倒下。\n\n把这个消息带给村长，让村庄知道你在外面完成了什么。这次交谈将交付清剿委托。";
                break;
            case MainStoryStage.CommissionTurnedIn:
                title = "懒惰篇·完";
                summary = "村庄重新有人挑水、生火。";
                description = "懒惰的故事已结束。\n\n感谢游玩，后续更新敬请期待。";
                break;
            default:
                title = "初来村庄";
                summary = "与村民交谈，了解这里。";
                description = "炉火旁的村民抬起头，打量着陌生的来客。\n\n先听听这里发生了什么，也许能从这些谨慎的话语中找到进入村庄的第一条线索。";
                break;
        }
    }
}
