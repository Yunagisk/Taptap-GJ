using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;        

public class NodeView : MonoBehaviour
{
    public string nodeId;       
    public string type;          
    public int stage = 1;        
    public bool cleared = false; 

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
