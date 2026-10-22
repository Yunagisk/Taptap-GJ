using UnityEngine;

// Keep the supplied artwork behind the battle and cover the camera without
// stretching it. Bottom alignment keeps the painted floor under the actors.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
public sealed class BattleBackground : MonoBehaviour
{
    public Camera battleCamera;
    [Range(.01f, 1)] public float groundLine = .278f;
    public float groundHeight = -1.9f;
    SpriteRenderer appearance;

    void OnEnable() => FitToCamera();
    void LateUpdate() => FitToCamera();
    void OnValidate() => FitToCamera();

    public void FitToCamera()
    {
        if (appearance == null) appearance = GetComponent<SpriteRenderer>();
        var camera = battleCamera != null ? battleCamera : Camera.main;
        if (camera == null || !camera.orthographic || appearance == null || appearance.sprite == null) return;
        Bounds image = appearance.sprite.bounds;
        if (image.size.x <= 0 || image.size.y <= 0) return;
        float height = camera.orthographicSize * 2;
        float width = height * camera.aspect;
        float bottom = camera.transform.position.y - height * .5f;
        float floorHeight = Mathf.Max(height, (groundHeight - bottom) / Mathf.Max(.01f, groundLine));
        float scale = Mathf.Max(width / image.size.x, floorHeight / image.size.y);
        var size = new Vector3(scale, scale, 1);
        var position = new Vector3(camera.transform.position.x - image.center.x * scale,
            bottom + image.size.y * scale * .5f - image.center.y * scale, transform.position.z);
        if ((transform.localScale - size).sqrMagnitude > .000001f) transform.localScale = size;
        if ((transform.position - position).sqrMagnitude > .000001f) transform.position = position;
    }
}
