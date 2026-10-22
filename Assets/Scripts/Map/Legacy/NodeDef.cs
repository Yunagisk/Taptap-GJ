using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/NodeDef")]
public class NodeDef : ScriptableObject
{
    public string id;               // 节点ID
    public string displayName;      // 节点名字
    public int stage = 1;           // 属于哪一层
    public Vector2 uiPosition;      // 在地图上的位置（X, Y）
    public List<string> neighbors = new List<string>(); // 连向哪些节点ID
}
