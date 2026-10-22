using UnityEngine;

// Apply the offset only while this camera renders. Camera controls and later
// frames always see the original position, even when the effect is interrupted.
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class BattleCameraShake : MonoBehaviour
{
    [Range(0f, .02f)] public float strength = .006f;
    [Min(.01f)] public float duration = .16f;
    [Min(1f)] public float frequency = 27f;

    Camera view;
    float started = -100f;
    int lastHitFrame = -1;
    Vector3 appliedOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Subscribe()
    {
        DamageSystem.DirectHit -= PlayHit;
        DamageSystem.DirectHit += PlayHit;
    }

    public static void PlayHit()
    {
        var camera = Camera.main;
        if (camera == null) return;
        var effect = camera.GetComponent<BattleCameraShake>();
        if (effect == null) effect = camera.gameObject.AddComponent<BattleCameraShake>();
        if (!effect.isActiveAndEnabled || effect.lastHitFrame == Time.frameCount) return;
        // Area attacks share a single pulse; subsequent combo hits refresh it.
        effect.lastHitFrame = Time.frameCount;
        effect.started = Time.time;
    }

    void OnEnable() => view = GetComponent<Camera>();

    void OnPreCull()
    {
        Restore();
        float age = Time.time - started;
        if (view == null || age < 0f || age >= duration) return;
        float envelope = 1f - age / duration;
        float amplitude = strength * (view.orthographic ? view.orthographicSize : 5f) * envelope * envelope;
        float phase = age * frequency * Mathf.PI * 2f;
        appliedOffset = (transform.right * (Mathf.Sin(phase + 1.1f) * .85f)
            + transform.up * (Mathf.Sin(phase * 1.31f - .65f) * .55f)) * amplitude;
        transform.position += appliedOffset;
    }

    void OnPostRender() => Restore();
    void OnDisable() => Restore();

    void Restore()
    {
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
    }
}
