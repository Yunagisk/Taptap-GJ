using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NodeView : MonoBehaviour
{
    public string nodeId;        // 节点ID，手动填
    public string type;          // 节点类型：村庄/战斗/事件/火堆/Boss/普通
    public int stage = 1;        // 属于哪一层
    public bool cleared = false; // 是否已清理，代码自动改

    public Button button;
    public TMP_Text label;

    void Awake()
    {
        if (label != null) label.text = type;
        if (button != null) button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (cleared) return;
        if (MapController.Instance != null)
            MapController.Instance.OnNodeClicked(this);
    }
}
