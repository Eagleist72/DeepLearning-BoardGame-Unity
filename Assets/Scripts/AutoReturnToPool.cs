using UnityEngine;

/// <summary>
/// Helper component for pooled objects, typically ParticleSystems, to automatically 
/// return themselves to the ObjectPooler once they have finished playing.
/// Ensure the ParticleSystem's 'Stop Action' is set to 'Callback'.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class AutoReturnToPool : MonoBehaviour
{
    [Tooltip("The tag used by ObjectPooler to identify this object's pool.")]
    public string poolTag = "VFX";

    /// <summary>
    /// Called automatically by Unity when a ParticleSystem stops playing.
    /// Requires ParticleSystem's 'Stop Action' to be set to 'Callback'.
    /// </summary>
    private void OnParticleSystemStopped()
    {
        if (ObjectPooler.Instance != null)
        {
            ObjectPooler.Instance.ReturnObject(poolTag, gameObject);
        }
        else
        {
            // Fallback in case ObjectPooler is missing or destroyed
            gameObject.SetActive(false);
        }
    }
}
