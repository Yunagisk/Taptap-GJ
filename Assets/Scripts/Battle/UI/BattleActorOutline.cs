using System.Collections.Generic;
using UnityEngine;

// Apply the same screen-pixel rim to sprite frames and animated body meshes.
[DisallowMultipleComponent, DefaultExecutionOrder(500)]
public sealed class BattleActorOutline : MonoBehaviour
{
    Material template;
    readonly List<Renderer> renderers = new List<Renderer>();
    readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
    readonly Dictionary<Renderer, Material> originals = new Dictionary<Renderer, Material>();

    public void Bind(Material material) { template = material; Apply(); }
    void LateUpdate() => Apply();

    public void Apply()
    {
        if (template == null) return;
        GetComponentsInChildren(true, renderers);
        foreach (var renderer in renderers)
        {
            if (!(renderer is SpriteRenderer) && !(renderer is MeshRenderer)) continue;
            // Overlaid fingers and transient effects retain their source material.
            if (renderer.name == "GripFingers" || renderer.name == "DefenseShieldFlash"
                || renderer.name == "SwordInkEffects" || renderer.name == "HeavyInkEffects"
                || renderer.name == "PlayerStatusParticles") continue;
            var source = renderer.sharedMaterial;
            if (source == null || source.shader == null || (source.shader.name != "Sprites/Default"
                && source.shader.name != "Taptap/Enemy Artwork")) continue;
            if (!materials.TryGetValue(source, out var outlined))
            {
                outlined = new Material(template) { name = source.name + " (Battle Outline)", hideFlags = HideFlags.DontSave };
                if (source.HasProperty("_Color")) outlined.SetColor("_Color", source.GetColor("_Color"));
                if (source.HasProperty("_MainTex")) outlined.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                if (source.HasProperty("_ArtworkGamma")) outlined.SetFloat("_ArtworkGamma", source.GetFloat("_ArtworkGamma"));
                materials.Add(source, outlined);
            }
            originals[renderer] = source;
            renderer.sharedMaterial = outlined;
        }
    }

    void OnDisable()
    {
        foreach (var binding in originals)
            if (binding.Key != null && materials.TryGetValue(binding.Value, out var outlined)
                && binding.Key.sharedMaterial == outlined) binding.Key.sharedMaterial = binding.Value;
        originals.Clear();
    }
    void OnDestroy()
    { foreach (var material in materials.Values) if (material != null) Destroy(material); }
}
