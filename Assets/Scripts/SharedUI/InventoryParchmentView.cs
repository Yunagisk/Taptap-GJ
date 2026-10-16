using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryParchmentView : MonoBehaviour
{
    public CanvasGroup visibility;
    public CanvasGroup ink;
    public RectMask2D inkMask;
    public Image sheet;
    public Image backdrop;
    public ParchmentRollGraphic roll;
    public ParchmentRollGraphic contactShadow;
    public Button firstSelection;

    readonly ParchmentMotion motion = new ParchmentMotion();
    GameObject returnSelection;
    bool wasInteractive;
    public bool IsVisible => motion.IsVisible;
    public bool CanInteract => motion.CanInteract;

    void Awake() { HideImmediate(); }
    void OnDisable() { HideImmediate(); }

    public void Open()
    {
        if (!motion.IsVisible && EventSystem.current != null)
        {
            returnSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(null);
        }
        motion.Request(true);
        Apply(); // Immediately block the map, including the first opening frame.
    }

    public void Close()
    {
        motion.Request(false);
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
        Apply();
    }

    public void HideImmediate()
    {
        motion.Reset();
        Apply();
    }

    void Update()
    {
        motion.Step(Time.unscaledDeltaTime);
        Apply();
    }

    void LateUpdate() { ApplyGeometry(); } // Also handles a window resize mid-roll.

    void Apply()
    {
        bool visible = motion.IsVisible, interactive = motion.CanInteract;
        if (visibility != null)
        {
            visibility.alpha = visible ? 1 : 0;
            visibility.blocksRaycasts = visible;
            visibility.interactable = interactive;
        }
        if (ink != null)
        {
            ink.alpha = motion.Phase > 0 ? 1 : 0;
            ink.blocksRaycasts = interactive;
            ink.interactable = interactive;
        }
        if (backdrop != null)
            backdrop.color = new Color(0, 0, 0, .32f * ParchmentMotion.Backdrop(motion.Phase));
        ApplyGeometry();
        if (interactive && !wasInteractive && EventSystem.current != null && firstSelection != null)
            firstSelection.Select();
        if (!visible && returnSelection != null)
        {
            if (EventSystem.current != null && returnSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(returnSelection);
            returnSelection = null;
        }
        wasInteractive = interactive;
    }

    void ApplyGeometry()
    {
        if (sheet == null) return;
        Rect area = sheet.rectTransform.rect;
        float p = motion.Phase;
        var pose = ParchmentMotion.Evaluate(p, area.width, area.height);
        // Filled Image crops its original UVs. The underlying paper never scales
        // or slides while unrolling, so ink remains registered to its fibers.
        sheet.fillAmount = pose.paperFill;
        if (inkMask != null)
        {
            inkMask.padding = new Vector4(pose.inkInset, 0, 0, 0);
        }
        Place(roll, pose.front, pose.width, pose.height, pose.alpha, p);
        Place(contactShadow, pose.front + pose.width * .24f, pose.width * 1.5f, pose.height,
            .24f * pose.alpha, p);
    }

    static void Place(ParchmentRollGraphic graphic, float x, float width, float height, float alpha, float phase)
    {
        if (graphic == null) return;
        graphic.rectTransform.anchoredPosition = new Vector2(x, 0);
        graphic.rectTransform.sizeDelta = new Vector2(Mathf.Max(.01f, width), height);
        graphic.color = graphic.shadow ? new Color(0, 0, 0, alpha) : new Color(1, 1, 1, alpha);
        graphic.SetPhase(phase);
    }

    public void FitContent(RectTransform content)
    {
        if (sheet == null || content == null) return;
        Vector2 size = sheet.rectTransform.rect.size;
        float margin = Mathf.Min(size.x, size.y) * .075f;
        content.localScale = Vector3.one * Mathf.Max(.01f,
            Mathf.Min((size.x - margin * 2) / 960f, (size.y - margin * 2) / 640f));
    }
}
