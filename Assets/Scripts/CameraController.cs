using UnityEngine;
using System;
using DG.Tweening;

/// <summary>
/// Singleton Camera Controller responsible for cinematic framing transitions and screen shake.
/// Uses DOTween for smooth, non-allocating camera movements.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("References")]
    private Camera cam;

    [Header("Framing Presets")]
    [Tooltip("Camera position & rotation for Main Menu overview")]
    [SerializeField] private Vector3 menuPosition = new Vector3(0f, 13.5f, -11.5f);
    [SerializeField] private Vector3 menuRotation = new Vector3(54f, 0f, 0f);

    [Tooltip("Camera position & rotation for Active In-Game view")]
    [SerializeField] private Vector3 gamePosition = new Vector3(0f, 10.5f, -8f);
    [SerializeField] private Vector3 gameRotation = new Vector3(46f, 0f, 0f);

    [Header("Animation Durations")]
    [Range(0.1f, 2.0f)] [Tooltip("Transition time from menu to game")]
    [SerializeField] private float transitionToGameDuration = 0.65f;
    [Range(0.1f, 2.0f)] [Tooltip("Transition time from game to menu")]
    [SerializeField] private float transitionToMenuDuration = 0.5f;
    [Tooltip("Easing curve used for camera sweeps")]
    [SerializeField] private Ease transitionEase = Ease.InOutCubic;

    // Cache to prevent floating point drift after multiple shakes
    private Vector3 originalLocalPosition;
    private Tween shakeTween;
    private Tween moveTween;
    private Tween rotateTween;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        // Initial State on Boot: Camera starts at MenuFraming
        transform.position = menuPosition;
        transform.rotation = Quaternion.Euler(menuRotation);
        originalLocalPosition = transform.localPosition;
    }

    /// <summary>
    /// Smoothly transitions the camera from Menu framing to Game framing.
    /// </summary>
    public void TransitionToGame(Action onComplete = null)
    {
        moveTween?.Kill();
        rotateTween?.Kill();

        moveTween = transform.DOMove(gamePosition, transitionToGameDuration)
            .SetEase(transitionEase)
            .SetUpdate(true);
            
        rotateTween = transform.DORotate(gameRotation, transitionToGameDuration)
            .SetEase(transitionEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                originalLocalPosition = transform.localPosition;
                onComplete?.Invoke();
            });
    }

    /// <summary>
    /// Smoothly transitions the camera back to Menu framing.
    /// </summary>
    public void TransitionToMenu(Action onComplete = null)
    {
        moveTween?.Kill();
        rotateTween?.Kill();

        moveTween = transform.DOMove(menuPosition, transitionToMenuDuration)
            .SetEase(transitionEase)
            .SetUpdate(true);
            
        rotateTween = transform.DORotate(menuRotation, transitionToMenuDuration)
            .SetEase(transitionEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                originalLocalPosition = transform.localPosition;
                onComplete?.Invoke();
            });
    }

    private void OnDrawGizmosSelected()
    {
        // Draw Menu Framing
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(menuPosition, 0.5f);
        Gizmos.DrawRay(menuPosition, Quaternion.Euler(menuRotation) * Vector3.forward * 5f);

        // Draw Game Framing
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(gamePosition, 0.5f);
        Gizmos.DrawRay(gamePosition, Quaternion.Euler(gameRotation) * Vector3.forward * 5f);
    }

    /// <summary>
    /// Shakes the camera slightly to provide juice upon a tile being destroyed.
    /// </summary>
    public void ShakeTileDestroy(float duration = 0.2f, float strength = 0.15f, int vibrato = 10)
    {
        PlayShake(duration, strength, vibrato);
    }

    /// <summary>
    /// Shakes the camera to emphasize impactful moments (e.g., heavy piece impact).
    /// </summary>
    public void ShakeImpact(float duration = 0.3f, float strength = 0.25f, int vibrato = 14)
    {
        PlayShake(duration, strength, vibrato);
    }

    private void PlayShake(float duration, float strength, int vibrato)
    {
        // Kill existing shake and reset to original position to prevent drift
        if (shakeTween != null && shakeTween.IsActive())
        {
            shakeTween.Kill(true); // Complete=true returns it to start before next shake
        }
        
        transform.localPosition = originalLocalPosition;

        shakeTween = transform.DOShakePosition(duration, strength, vibrato, 90f, false, true)
            .SetUpdate(true);
    }

    private void OnDestroy()
    {
        shakeTween?.Kill();
        moveTween?.Kill();
        rotateTween?.Kill();
    }
}
