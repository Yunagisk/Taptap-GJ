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

    public void Bind(WorldMapController owner)
    {
        controller = owner;
        button.onClick.AddListener(Move);
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Move);
    }

    void Move() => controller.MoveToNode(nodeId);

    public void Refresh(MapProgression map)
    {
        var node = map.GetNode(nodeId);
        bool current = map.State.currentNodeId == nodeId;
        bool cleared = map.State.clearedNodes.Contains(nodeId);
        bool available = map.CanMove(nodeId) || map.CanReenterVillage(nodeId);
        gameObject.SetActive(map.IsVisible(nodeId));
        button.interactable = available;
        button.image.color = cleared ? new Color32(199, 202, 196, 245) : new Color32(248, 248, 244, 250);
        outline.effectColor = current ? new Color32(192, 138, 25, 255)
            : available ? new Color32(38, 106, 75, 255)
            : node.kind == MapNodeKind.Boss ? new Color32(155, 43, 44, 255) : new Color32(95, 98, 91, 255);
        label.color = node.kind == MapNodeKind.Boss ? new Color32(155, 43, 44, 255) : new Color32(30, 33, 29, 255);
        string status = current ? "当前位置" : cleared ? "已完成" : available ? "可前往" : "";
        label.text = node.displayName + (status.Length == 0 ? "" : "\n<size=14>" + status + "</size>");
    }
}
