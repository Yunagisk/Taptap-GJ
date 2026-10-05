using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRoot : MonoBehaviour
{
    public static GameRoot I;
    public GameState state;
    void Start()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        state = new GameState();
        DontDestroyOnLoad(gameObject);
    }
    void Update()
    {
        Debug.Log($"游戏初始化成功！");
    }
}
