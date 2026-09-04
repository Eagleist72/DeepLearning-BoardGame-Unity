using UnityEngine;

/// <summary>
/// Singleton VFX Manager for handling particle effects across the game.
/// Utilizes the ObjectPooler to achieve zero runtime GC allocations.
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Fetches and plays a tile destruction particle effect from the ObjectPooler.
    /// Optionally tints the particles if a color is provided.
    /// </summary>
    /// <param name="position">World position to play the effect.</param>
    /// <param name="tileColor">Optional color to tint the particles.</param>
    public void PlayTileDestroyEffect(Vector3 position, Color? tileColor = null)
    {
        if (ObjectPooler.Instance == null) return;

        // "VFX" tag should match the pool tag set up in GameSettings / ObjectPooler
        GameObject vfxObj = ObjectPooler.Instance.GetObject("VFX", position, Quaternion.identity);

        if (vfxObj != null)
        {
            ParticleSystem ps = vfxObj.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                if (tileColor.HasValue)
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.startColor = tileColor.Value;
                }
                ps.Play();
            }
        }
    }

    /// <summary>
    /// Fetches and triggers a victory confetti effect at the specified position.
    /// Uses the "ConfettiVFX" pool tag.
    /// </summary>
    /// <param name="position">World position to play the effect.</param>
    public void PlayVictoryConfetti(Vector3 position)
    {
        if (ObjectPooler.Instance == null) return;

        GameObject vfxObj = ObjectPooler.Instance.GetObject("ConfettiVFX", position, Quaternion.identity);

        if (vfxObj != null)
        {
            ParticleSystem ps = vfxObj.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
        }
    }
}
