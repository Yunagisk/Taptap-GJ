using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Copies the card's actual UI meshes, including its portrait and font atlas,
// into polygon shards. No screen capture or extra camera is needed at runtime.
public sealed class BattleEnemyCardShatter : MonoBehaviour
{
    public const float Duration = .58f;
    readonly List<BattleEnemyCardFragment> layers = new List<BattleEnemyCardFragment>();
    Action completed;
    float elapsed;
    double startedAt;
    public float Progress => Mathf.Clamp01(elapsed / Duration);

    public static BattleEnemyCardShatter Play(RectTransform card, Action onComplete)
    {
        foreach (var label in card.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        var root = new GameObject(card.name + "_Shatter", typeof(RectTransform), typeof(CanvasGroup), typeof(BattleEnemyCardShatter));
        root.layer = card.gameObject.layer;
        var rect = (RectTransform)root.transform;
        rect.SetParent(card.parent, false);
        rect.anchorMin = card.anchorMin; rect.anchorMax = card.anchorMax;
        rect.pivot = card.pivot; rect.sizeDelta = card.sizeDelta;
        rect.anchoredPosition = card.anchoredPosition;
        rect.localScale = card.localScale; rect.localRotation = card.localRotation;
        var group = root.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        var effect = root.GetComponent<BattleEnemyCardShatter>();
        effect.completed = onComplete;
        var shards = BuildShards(card.rect);
        foreach (var source in card.GetComponentsInChildren<Graphic>())
        {
            if (!source.isActiveAndEnabled || source.canvasRenderer.cull) continue;
            var mesh = source.canvasRenderer.GetMesh();
            if (mesh == null || mesh.vertexCount == 0) continue;
            var layer = new GameObject(source.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleEnemyCardFragment));
            layer.layer = root.layer;
            layer.transform.SetParent(rect, false);
            var fragment = layer.GetComponent<BattleEnemyCardFragment>();
            fragment.rectTransform.anchorMin = fragment.rectTransform.anchorMax = card.pivot;
            fragment.rectTransform.anchoredPosition = Vector2.zero;
            fragment.rectTransform.sizeDelta = card.rect.size;
            fragment.Bind(source, card, mesh, shards);
            effect.layers.Add(fragment);
        }
        effect.startedAt = Time.realtimeSinceStartupAsDouble;
        return effect;
    }

    static Vector2[][] BuildShards(Rect rect)
    {
        Vector2 Point(float x, float y) => new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, x), Mathf.Lerp(rect.yMin, rect.yMax, y));
        // Counterclockwise polygons with irregular, shared fracture edges.
        var perimeter = new[] { Point(0, 0), Point(.29f, 0), Point(.64f, 0), Point(1, 0),
            Point(1, .49f), Point(1, 1), Point(.74f, 1), Point(.43f, 1), Point(.16f, 1), Point(0, 1), Point(0, .54f) };
        var impact = Point(.53f, .48f);
        var ring = new Vector2[perimeter.Length];
        for (int i = 0; i < ring.Length; i++) ring[i] = Vector2.Lerp(impact, perimeter[i], i % 2 == 0 ? .42f : .55f);
        var shards = new Vector2[perimeter.Length * 2][];
        for (int i = 0; i < perimeter.Length; i++)
        {
            int next = (i + 1) % perimeter.Length;
            shards[i * 2] = new[] { impact, ring[i], ring[next] };
            shards[i * 2 + 1] = new[] { ring[i], perimeter[i], perimeter[next], ring[next] };
        }
        return shards;
    }

    void Update()
    {
        elapsed = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
        foreach (var layer in layers) layer.Sample(Progress);
        if (elapsed < Duration) return;
        var callback = completed;
        completed = null;
        callback?.Invoke();
        Destroy(gameObject);
    }
}
