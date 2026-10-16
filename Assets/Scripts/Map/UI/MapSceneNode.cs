using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapSceneNode : MonoBehaviour
{
    public string nodeId;
    public Button button;
    public TMP_Text label;
    public Outline outline;
    WorldMapController controller;
    bool available;
    bool revealInputBlocked;
    bool isRevealing;
    MapNodeRevealEffect revealEffect;
    MapNodeCompletionEffect completionEffect;
    bool isCompleting;
    float completionProgress;

    static readonly Color32 ActiveColor = new Color32(248, 248, 244, 250);
    static readonly Color32 InactiveOutline = new Color32(112, 114, 109, 255);
    Color activeOutline;

    public void Bind(WorldMapController owner)
    {
        controller = owner;
        button.onClick.AddListener(Move);
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Move);
    }

    void Move()
    {
        if (!revealInputBlocked && !isRevealing && controller != null) controller.MoveToNode(nodeId);
    }

    public void SetRevealInputBlocked(bool blocked)
    {
        revealInputBlocked = blocked;
        UpdateInteraction();
    }

    void UpdateInteraction() => button.interactable = available && !revealInputBlocked && !isRevealing
        && (controller == null || !controller.IsEnteringNode);

    public void PrepareReveal()
    {
        if (revealEffect == null) revealEffect = GetComponent<MapNodeRevealEffect>();
        if (revealEffect == null) revealEffect = gameObject.AddComponent<MapNodeRevealEffect>();
        isRevealing = true;
        revealEffect.enabled = true;
        revealEffect.SetReveal(0, Vector2.right, MapRoadGeometry.Seed(nodeId, "reveal"));
        UpdateInteraction();
    }

    public void SetRevealProgress(float progress, Vector3 incomingWorldDirection)
    {
        if (progress >= 1) { CompleteReveal(); return; }
        if (!isRevealing) PrepareReveal();
        Vector2 localDirection = button.image.rectTransform.InverseTransformVector(incomingWorldDirection);
        revealEffect.SetReveal(progress, localDirection, MapRoadGeometry.Seed(nodeId, "reveal"));
    }

    public void CompleteReveal()
    {
        isRevealing = false;
        if (revealEffect != null) revealEffect.CompleteReveal();
        UpdateInteraction();
    }

    public void PrepareCompletion()
    {
        isCompleting = true;
        SetCompletionProgress(0);
    }

    public void SetCompletionProgress(float progress)
    {
        completionProgress = Mathf.Clamp01(progress);
        EnsureCompletionEffect();
        completionEffect.enabled = true;
        completionEffect.SetCompletion(completionProgress);
        outline.effectColor = Color.Lerp(activeOutline, InactiveOutline, MapRevealMath.Smooth(completionProgress));
        if (completionProgress >= 1) isCompleting = false;
    }

    void EnsureCompletionEffect()
    {
        if (completionEffect == null) completionEffect = GetComponent<MapNodeCompletionEffect>();
        if (completionEffect == null) completionEffect = gameObject.AddComponent<MapNodeCompletionEffect>();
    }

    public void Refresh(MapProgression map)
    {
        var node = map.GetNode(nodeId);
        bool current = map.State.currentNodeId == nodeId;
        bool cleared = map.State.clearedNodes.Contains(nodeId);
        available = map.CanMove(nodeId) || map.CanReenterVillage(nodeId);
        gameObject.SetActive(map.IsVisible(nodeId));
        UpdateInteraction();
        button.image.color = ActiveColor;
        activeOutline = current ? new Color32(192, 138, 25, 255)
            : available ? new Color32(38, 106, 75, 255)
            : node.kind == MapNodeKind.Boss ? new Color32(155, 43, 44, 255) : new Color32(95, 98, 91, 255);
        // The village is a reusable destination, not an exhausted encounter.
        bool exhausted = cleared && node.kind != MapNodeKind.Village;
        outline.effectColor = exhausted
            ? Color.Lerp(activeOutline, InactiveOutline, MapRevealMath.Smooth(isCompleting ? completionProgress : 1))
            : activeOutline;
        if (exhausted && !isCompleting) SetCompletionProgress(1);
        else if (!exhausted && completionEffect != null)
        {
            isCompleting = false;
            completionEffect.SetCompletion(0);
            completionEffect.enabled = false;
        }
        if (label != null) label.gameObject.SetActive(false);
    }
}
