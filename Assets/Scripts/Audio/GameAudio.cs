using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One 2D audio host survives scene loads so transition sounds can finish.
[DisallowMultipleComponent]
public sealed class GameAudio : MonoBehaviour
{
    static GameAudio instance;
    static bool quitting;
    [SerializeField] GameAudioLibrary library;
    AudioSource music, effects, uiConfirm;
    double nextConfirmTime;
    Selectable[] selectables = new Selectable[128];
    int lastButtonFrame = -1, lastConfirmFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { instance = null; quitting = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() { Ensure(); }

    static GameAudio Ensure()
    {
        if (!Application.isPlaying || quitting) return null;
        if (instance != null) return instance;
        instance = FindObjectOfType<GameAudio>();
        if (instance == null) instance = new GameObject("GameAudio").AddComponent<GameAudio>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        library = Resources.Load<GameAudioLibrary>("GameAudioLibrary");
        music = CreateSource("MenuMusic", true);
        effects = CreateSource("SoundEffects", false);
        uiConfirm = CreateSource("UiConfirm", false);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Canvas.willRenderCanvases += BindButtons;
        if (library == null) Debug.LogWarning("GameAudioLibrary is missing from Resources.", this);
        RefreshMusic();
    }

    AudioSource CreateSource(string name, bool loop)
    {
        var child = new GameObject(name); child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        return source;
    }

    // Keep each dialogue visible until its confirmation sound has finished.
    public static float DialogueMinimumDisplayTime
    {
        get
        {
            var audio = Ensure();
            var clip = audio != null && audio.library != null ? audio.library.uiConfirm : null;
            return clip != null ? Mathf.Max(.55f, clip.length + .08f) : .55f;
        }
    }

    public static bool UiConfirmReady
    {
        get
        {
            var audio = Ensure();
            return audio == null || audio.uiConfirm == null ||
                (!audio.uiConfirm.isPlaying && Time.unscaledTimeAsDouble >= audio.nextConfirmTime);
        }
    }

    public static void Play(GameAudioCue cue)
    {
        var audio = Ensure();
        if (audio == null || audio.library == null) return;
        var clip = audio.library.GetClip(cue);
        if (clip == null) return;
        // Drop rapid confirmations instead of queueing or layering them.
        if (cue == GameAudioCue.UiConfirm)
        {
            if (audio.lastConfirmFrame == Time.frameCount || !UiConfirmReady) return;
            audio.lastConfirmFrame = Time.frameCount;
            audio.nextConfirmTime = Time.unscaledTimeAsDouble + clip.length + .08;
            audio.uiConfirm.volume = audio.library.effectsVolume;
            audio.uiConfirm.clip = clip;
            audio.uiConfirm.Play();
            return;
        }
        audio.effects.volume = audio.library.effectsVolume;
        audio.effects.PlayOneShot(clip);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { RefreshMusic(); }
    void OnActiveSceneChanged(Scene previous, Scene next) { RefreshMusic(); }

    void RefreshMusic()
    {
        if (library == null || music == null) return;
        bool inMenu = SceneManager.GetActiveScene().name == "MainMenu";
        if (!inMenu || library.menuMusic == null) { music.Stop(); music.clip = null; return; }
        music.volume = library.musicVolume;
        if (music.clip == library.menuMusic && music.isPlaying) return;
        music.clip = library.menuMusic;
        music.Play();
    }

    void BindButtons()
    {
        if (lastButtonFrame == Time.frameCount) return;
        lastButtonFrame = Time.frameCount;
        int count = Selectable.allSelectableCount;
        if (selectables.Length < count) selectables = new Selectable[Mathf.NextPowerOfTwo(count)];
        count = Selectable.AllSelectablesNoAlloc(selectables);
        for (int i = 0; i < count; i++)
        {
            var button = selectables[i] as Button;
            selectables[i] = null;
            if (button == null) continue;
            var sound = button.GetComponent<GameAudioButton>();
            if (sound == null) sound = button.gameObject.AddComponent<GameAudioButton>();
            sound.Bind(button);
        }
    }

    void OnApplicationQuit() { if (instance == this) quitting = true; }
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Canvas.willRenderCanvases -= BindButtons;
        if (instance == this) instance = null;
    }
}
