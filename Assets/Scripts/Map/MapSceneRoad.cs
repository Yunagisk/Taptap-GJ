using UnityEngine;

[ExecuteAlways]
public class MapSceneRoad : MonoBehaviour
{
    public string fromNodeId;
    public string toNodeId;
    public RectTransform from;
    public RectTransform to;

    void LateUpdate()
    {
        if (from == null || to == null) return;
        var rect = (RectTransform)transform;
        Vector2 delta = to.anchoredPosition - from.anchoredPosition;
        rect.anchoredPosition = (from.anchoredPosition + to.anchoredPosition) * 0.5f;
        rect.sizeDelta = new Vector2(delta.magnitude, 3);
        rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    public void Refresh(MapProgression map)
    {
        gameObject.SetActive(map.IsRoadVisible(fromNodeId, toNodeId));
    }
}
