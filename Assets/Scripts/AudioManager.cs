using UnityEngine;

public enum HapticType { Light, Medium, Heavy }

/// <summary>
/// Singleton AudioManager with zero-allocation AudioSource pooling.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Settings")]
    public GameSettings gameSettings;

    [Header("UI SFX")]
    public AudioClip buttonClickClip;
    public AudioClip toggleClip;
    public AudioClip modalOpenClip;

    [Header("Gameplay SFX")]
    public AudioClip pieceHopClip;
    public AudioClip tileCollapseClip;
    public AudioClip turnSwitchClip;

    [Header("Game Over SFX")]
    public AudioClip victoryJingleClip;
    public AudioClip defeatJingleClip;

    [Header("Background Music")]
    public AudioClip bgmClip;

    private AudioSource[] audioSourcePool;
    private AudioSource musicSource;
    private int nextSourceIndex;

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
        
        if (bgmClip != null)
        {
            musicSource.clip = bgmClip;
            musicSource.Play();
        }
    }

    private void InitializeAudioSourcePool()
    {
        int poolSize = 5; // Reduced to 5 as requested
        audioSourcePool = new AudioSource[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            GameObject sourceObj = new GameObject($"PooledAudioSource_{i}");
            sourceObj.transform.SetParent(transform);

            AudioSource source = sourceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            audioSourcePool[i] = source;
        }
    }

    public void ToggleSFX()
    {
        IsSFXEnabled = !IsSFXEnabled;
        PlayerPrefs.SetInt("Setting_SFX", IsSFXEnabled ? 1 : 0);
        PlayerPrefs.Save();
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

    public void TriggerHaptic(HapticType type)
    {
        if (IsHapticsEnabled)
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }

    // Fallback for previous calls
    public void TriggerHaptic()
    {
        TriggerHaptic(HapticType.Light);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f, bool randomizePitch = true)
    {
        if (clip == null || !IsSFXEnabled) return;

        AudioSource source = GetNextAudioSource();
        float masterVol = gameSettings != null ? gameSettings.masterVolume * gameSettings.sfxVolume : 1f;
        source.volume = masterVol * volume;

        if (randomizePitch)
        {
            source.pitch = Random.Range(0.92f, 1.08f);
        }
        else
        {
            source.pitch = 1f;
        }

        source.clip = clip;
        source.Play();
    }

    // Keep legacy methods that might be called elsewhere, routing them to the new clip references
    public void PlayMoveSound()
    {
        PlaySFX(pieceHopClip);
    }

    public void PlayTileRemoveSound()
    {
        PlaySFX(tileCollapseClip);
        TriggerHaptic(HapticType.Light);
    }

    public void PlayUIClickSound()
    {
        PlaySFX(buttonClickClip);
    }

    public void PlayVictorySound()
    {
        PlaySFX(victoryJingleClip, 1f, false);
        TriggerHaptic(HapticType.Medium);
    }

    public void PlayDefeatSound()
    {
        PlaySFX(defeatJingleClip, 1f, false);
        TriggerHaptic(HapticType.Medium);
    }

    private AudioSource GetNextAudioSource()
    {
        AudioSource source = audioSourcePool[nextSourceIndex];
        nextSourceIndex = (nextSourceIndex + 1) % audioSourcePool.Length;
        return source;
    }
}
