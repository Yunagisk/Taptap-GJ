using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class GameRoot : MonoBehaviour
{
    public static GameRoot I;
    public GameState state;
    public bool enterVillageOnLoad;
    [System.NonSerialized] public WorldMapTemplate villageMapTemplate;
    [System.NonSerialized] public string villageReturnScene;
    [System.NonSerialized] public WorldMapTemplate battleMapTemplate;
    [System.NonSerialized] public string battleRequestId;
    [System.NonSerialized] public string battleReturnScene;
    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        state = new GameState();
        DontDestroyOnLoad(gameObject);
    }
    void OnDestroy()
    {
        if (I == this) I = null;
    }
}
