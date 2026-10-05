using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MapController : MonoBehaviour
{
    public static MapController Instance;
    public RectTransform mapRoot;
    public TMP_Text txtStage;

    readonly List<NodeView> allNodes = new List<NodeView>();

    void Awake()
    {
        Instance = this;
        allNodes.AddRange(mapRoot.GetComponentsInChildren<NodeView>(true));
        foreach (var n in allNodes)
        {
            n.gameObject.SetActive(n.stage <= 1);
        }
    }

    void Start()
    {
        Refresh();
    }
    void OnEnable()
    {
        if (GameRoot.I != null && GameRoot.I.state != null)
            Refresh();
    }
    public void OnNodeClicked(NodeView node)
    {
        node.cleared = true;
        node.button.interactable = false;

        int currentStage = GameRoot.I.state.world.stage;
        foreach (var n in allNodes)
        {
            if (n.stage == currentStage && !n.cleared) return;
        }
        GameRoot.I.state.world.stage++;
        Refresh();
    }

    void Refresh()
    {
        if (GameRoot.I == null || GameRoot.I.state == null) return;
        int currentStage = GameRoot.I.state.world.stage;
        txtStage.text = currentStage == 1 ? "当前：初期"
                       : currentStage == 2 ? "当前：中期"
                       : "当前：后期";
        foreach (var n in allNodes)
        {
            n.gameObject.SetActive(n.stage <= currentStage);
        }
    }
}