using UnityEngine;

// Only the isolated editor preview receives extra HP. Formal battle data and
// the player's active run are never replaced by the animation review scene.
[DefaultExecutionOrder(-1100)]
public sealed class CombatAnimationPreviewSetup : MonoBehaviour
{
    void Start()
    {
        if (!Application.isEditor || gameObject.scene.name != "CombatAnimationPreview" || GameRoot.I != null) return;
        var root = new GameObject("AnimationPreviewRun").AddComponent<GameRoot>();
        root.state.player.maxHp = root.state.player.hp = 1000;
    }
}
