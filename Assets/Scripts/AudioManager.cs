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

    // Round-robin index to distribute playback across the pool evenly
    private int nextSourceIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeAudioSourcePool();
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
