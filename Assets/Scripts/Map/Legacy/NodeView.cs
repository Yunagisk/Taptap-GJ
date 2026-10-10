using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TMPro;    

public class NodeView : MonoBehaviour
{
    public string nodeId;        // �ڵ�ID���ֶ���
    public string type;          // �ڵ����ͣ���ׯ/ս��/�¼�/���/Boss/��ͨ
    public int stage = 1;        // ������һ��
    public bool cleared = false; // �Ƿ�������������Զ���

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
