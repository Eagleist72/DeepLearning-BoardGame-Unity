using UnityEngine;

/// <summary>
/// Singleton AudioManager with zero-allocation AudioSource pooling.
/// Pre-allocates AudioSource components on Awake to avoid runtime instantiation.
/// All playback methods pull from the pool and return sources automatically when done.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Shared GameSettings asset containing audio clips and volume configuration")]
    public GameSettings gameSettings;

    // Pre-allocated pool of AudioSource components (no runtime instantiation)
    private AudioSource[] audioSourcePool;
    private AudioSource musicSource;

    // Round-robin index to distribute playback across the pool evenly
    private int nextSourceIndex;

    // Settings State
    public bool IsSFXEnabled { get; private set; } = true;
    public bool IsMusicEnabled { get; private set; } = true;
    public bool IsHapticsEnabled { get; private set; } = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadSettings();
        InitializeAudioSourcePool();
        InitializeMusicSource();
    }

    private void LoadSettings()
    {
        IsSFXEnabled = PlayerPrefs.GetInt("Setting_SFX", 1) == 1;
        IsMusicEnabled = PlayerPrefs.GetInt("Setting_Music", 1) == 1;
        IsHapticsEnabled = PlayerPrefs.GetInt("Setting_Haptics", 1) == 1;
    }

    private void InitializeMusicSource()
    {
        GameObject bgmObj = new GameObject("BackgroundMusicSource");
        bgmObj.transform.SetParent(transform);
        musicSource = bgmObj.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.volume = gameSettings != null ? gameSettings.masterVolume * 0.5f : 0.5f;
        musicSource.mute = !IsMusicEnabled;
        
        // If there's a music clip in gameSettings, play it. 
        // (Assuming you'll add it to GameSettings, or we just leave it ready)
        // musicSource.Play();
    }

    public void ToggleSFX()
    {
        IsSFXEnabled = !IsSFXEnabled;
        PlayerPrefs.SetInt("Setting_SFX", IsSFXEnabled ? 1 : 0);
        PlayerPrefs.Save();
        
        foreach (var source in audioSourcePool)
        {
            source.mute = !IsSFXEnabled;
        }
    }

    public void ToggleMusic()
    {
        IsMusicEnabled = !IsMusicEnabled;
        PlayerPrefs.SetInt("Setting_Music", IsMusicEnabled ? 1 : 0);
        PlayerPrefs.Save();
        
        if (musicSource != null)
        {
            musicSource.mute = !IsMusicEnabled;
        }
    }

    public void ToggleHaptics()
    {
        IsHapticsEnabled = !IsHapticsEnabled;
        PlayerPrefs.SetInt("Setting_Haptics", IsHapticsEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void TriggerHaptic()
    {
        if (IsHapticsEnabled)
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }

    /// <summary>
    /// Pre-allocates AudioSource components as children of this GameObject.
    /// Each source is configured for one-shot SFX playback (no looping, no spatial blend).
    /// </summary>
    private void InitializeAudioSourcePool()
    {
        int poolSize = gameSettings != null ? gameSettings.initialAudioSourcePoolSize : 10;
        audioSourcePool = new AudioSource[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            // Create a child GameObject to hold each AudioSource (keeps hierarchy clean)
            GameObject sourceObj = new GameObject($"PooledAudioSource_{i}");
            sourceObj.transform.SetParent(transform);

            AudioSource source = sourceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.mute = !IsSFXEnabled; // Apply initial SFX setting
            source.loop = false;
            source.spatialBlend = 0f; // 2D sound (UI/board game)

            audioSourcePool[i] = source;
        }
    }

    /// <summary>
    /// Plays a sound effect clip using a pooled AudioSource with optional volume and pitch variation.
    /// Uses round-robin allocation — if all sources are busy, the oldest one is reused.
    /// Zero GC allocations during playback.
    /// </summary>
    /// <param name="clip">The AudioClip to play. Null clips are silently ignored.</param>
    /// <param name="volumeScale">Additional volume multiplier (0-1) on top of master/sfx volume.</param>
    /// <param name="pitchVariation">Random pitch offset range for richer audio feel (e.g., 0.05 = ±5%).</param>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f, float pitchVariation = 0.05f)
    {
        if (clip == null || gameSettings == null) return;

        AudioSource source = GetNextAudioSource();

        // Compute final volume: masterVolume * sfxVolume * per-call scale
        source.volume = gameSettings.masterVolume * gameSettings.sfxVolume * volumeScale;

        // Apply subtle pitch variation for organic feel
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

        source.clip = clip;
        source.Play();
    }

    /// <summary>
    /// Plays the piece movement sound effect.
    /// </summary>
    public void PlayMoveSound()
    {
        if (gameSettings != null)
        {
            PlaySFX(gameSettings.moveClip);
        }
    }

    /// <summary>
    /// Plays the tile removal sound effect.
    /// </summary>
    public void PlayTileRemoveSound()
    {
        if (gameSettings != null)
        {
            PlaySFX(gameSettings.tileRemoveClip);
        }
        TriggerHaptic();
    }

    /// <summary>
    /// Plays the UI click/selection sound effect.
    /// </summary>
    public void PlayUIClickSound()
    {
        if (gameSettings != null)
        {
            PlaySFX(gameSettings.uiClickClip);
        }
    }

    /// <summary>
    /// Plays the victory sound effect (no pitch variation for fanfares).
    /// </summary>
    public void PlayVictorySound()
    {
        if (gameSettings != null)
        {
            PlaySFX(gameSettings.victoryClip, 1f, 0f);
        }
        TriggerHaptic();
    }

    /// <summary>
    /// Plays the defeat sound effect (no pitch variation for fanfares).
    /// </summary>
    public void PlayDefeatSound()
    {
        if (gameSettings != null)
        {
            PlaySFX(gameSettings.defeatClip, 1f, 0f);
        }
    }

    /// <summary>
    /// Returns the next available AudioSource from the pool using round-robin allocation.
    /// If all sources are currently playing, the next in rotation is reused (interrupted).
    /// This guarantees zero allocation — no new AudioSource is ever created at runtime.
    /// </summary>
    private AudioSource GetNextAudioSource()
    {
        AudioSource source = audioSourcePool[nextSourceIndex];
        nextSourceIndex = (nextSourceIndex + 1) % audioSourcePool.Length;
        return source;
    }
}
