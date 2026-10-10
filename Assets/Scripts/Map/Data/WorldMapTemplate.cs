using UnityEngine;

[CreateAssetMenu(menuName = "Game/World Map Template")]
public class WorldMapTemplate : ScriptableObject
{
    public TextAsset definition;

    public MapLayout Load()
    {
        if (definition == null) throw new System.InvalidOperationException("Assign a map definition JSON.");
        return JsonUtility.FromJson<MapLayout>(definition.text);
    }
}
