using UnityEngine;
using UnityEngine.UI;

// Bind to the actual click event: mouse, keyboard and numbered dialogue choices
// share one cue, and Button itself rejects disabled/non-interactable clicks.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class GameAudioButton : MonoBehaviour
{
    Button.ButtonClickedEvent boundEvent;
    bool manualConfirmation;

    // Dialogue code confirms only after its minimum display time permits input.
    public static void UseManualConfirmation(Button button)
    {
        if (button == null) return;
        var sound = button.GetComponent<GameAudioButton>();
        if (sound == null) sound = button.gameObject.AddComponent<GameAudioButton>();
        sound.manualConfirmation = true;
        sound.Bind(button);
    }

    public void Bind(Button button)
    {
        if (boundEvent == button.onClick) return;
        if (boundEvent != null) boundEvent.RemoveListener(Confirm);
        boundEvent = button.onClick;
        boundEvent.AddListener(Confirm);
    }

    void Confirm() { if (!manualConfirmation) GameAudio.Play(GameAudioCue.UiConfirm); }
    void OnDestroy() { if (boundEvent != null) boundEvent.RemoveListener(Confirm); }
}
