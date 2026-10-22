using UnityEngine;

public enum GameAudioCue { UiConfirm, MapMove, EnterBattle, PlayerHit, PlayerHurt }

[CreateAssetMenu(menuName = "Game/Audio Library")]
public sealed class GameAudioLibrary : ScriptableObject
{
    public AudioClip menuMusic;
    public AudioClip uiConfirm;
    public AudioClip mapMove;
    public AudioClip enterBattle;
    public AudioClip playerHit;
    public AudioClip playerHurt;
    [Range(0f, 1f)] public float musicVolume = .35f;
    [Range(0f, 1f)] public float effectsVolume = .8f;

    public AudioClip GetClip(GameAudioCue cue)
    {
        switch (cue)
        {
            case GameAudioCue.UiConfirm: return uiConfirm;
            case GameAudioCue.MapMove: return mapMove;
            case GameAudioCue.EnterBattle: return enterBattle;
            case GameAudioCue.PlayerHit: return playerHit;
            case GameAudioCue.PlayerHurt: return playerHurt;
            default: return null;
        }
    }
}
