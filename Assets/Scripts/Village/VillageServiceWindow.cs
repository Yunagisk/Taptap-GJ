using UnityEngine;

// Keep the authored parchment intact when the game view is taller or wider than 16:9.
public sealed class VillageServiceWindow : MonoBehaviour
{
    void LateUpdate()
    {
        var window = (RectTransform)transform;
        var parent = transform.parent as RectTransform;
        if (parent == null) return;
        var size = parent.rect.size - Vector2.one * 48;
        float fit = Mathf.Clamp(Mathf.Min(size.x / window.sizeDelta.x, size.y / window.sizeDelta.y), .1f, 1);
        window.localScale = Vector3.one * fit;
    }
}
