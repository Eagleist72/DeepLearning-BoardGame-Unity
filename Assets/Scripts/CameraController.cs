using UnityEngine;
using DG.Tweening;

/// <summary>
/// Singleton Camera Controller responsible for responsive mobile framing and screen shake.
/// Adjusts the camera position and FOV based on the board size and current screen aspect ratio.
/// Uses DOTween for non-allocating screen shakes.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("References")]
    public GameSettings gameSettings;
    private Camera cam;

    [Header("Framing Settings")]
    [Tooltip("Extra padding around the board (in Unity units or FOV adjustment)")]
    public float boardPadding = 2f;
    [Tooltip("Angle of the camera looking down at the board")]
    public float cameraAngleX = 50f;
    [Tooltip("Base distance from the board center")]
    public float baseDistance = 8f;

    // Cache to prevent floating point drift after multiple shakes
    private Vector3 originalLocalPosition;
    private Tween shakeTween;
    private float lastAspect;

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
        FrameBoard();
        originalLocalPosition = transform.localPosition;
        lastAspect = (float)Screen.width / Screen.height;
    }

    private void Update()
    {
        float currentAspect = (float)Screen.width / Screen.height;
        // Dynamically re-frame if the screen is resized or rotated (e.g., orientation change)
        if (Mathf.Abs(currentAspect - lastAspect) > 0.01f)
        {
            lastAspect = currentAspect;
            // Kill any active shakes before reframing to avoid capturing wrong local position
            shakeTween?.Kill(true);
            FrameBoard();
            originalLocalPosition = transform.localPosition;
        }
    }

    /// <summary>
    /// Calculates the board's center and adjusts the camera's position and field of view 
    /// to ensure the entire grid fits comfortably on screen, regardless of portrait or landscape.
    /// </summary>
    private void FrameBoard()
    {
        if (gameSettings == null || cam == null) return;

        int size = gameSettings.boardSize;
        float offset = gameSettings.tileOffset;

        // The GridManager perfectly centers the board at (0, 0, 0)
        Vector3 boardCenter = Vector3.zero;

        // Position the camera
        // We set a fixed rotation for an isometric/top-down feel (e.g., 50 degrees down)
        transform.rotation = Quaternion.Euler(cameraAngleX, 0f, 0f);

        // Move the camera back and up based on the angle
        float radAngle = cameraAngleX * Mathf.Deg2Rad;
        float yPos = Mathf.Sin(radAngle) * baseDistance;
        float zOffset = Mathf.Cos(radAngle) * baseDistance;
        
        Vector3 desiredPosition = boardCenter + new Vector3(0f, yPos, -zOffset);
        transform.position = desiredPosition;

        // Calculate required size to fit the board width and depth
        float boardPhysicalWidth = size * offset;
        
        // Dynamic FOV calculation based on aspect ratio
        float aspect = (float)Screen.width / Screen.height;
        float requiredSize = (boardPhysicalWidth / 2f) + boardPadding;

        if (aspect < 1f)
        {
            // Portrait mode: Width is the limiting factor
            requiredSize /= aspect;
        }
        // Landscape mode: Height is typically the limiting factor, requiredSize remains as is

        // Use trigonometry to find the right FOV to fit 'requiredSize' at 'baseDistance'
        float desiredFOV = 2f * Mathf.Atan(requiredSize / baseDistance) * Mathf.Rad2Deg;
        cam.fieldOfView = Mathf.Clamp(desiredFOV, 30f, 90f);
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
        
        // Reset to original local position manually just in case
        transform.localPosition = originalLocalPosition;

        shakeTween = transform.DOShakePosition(duration, strength, vibrato, 90f, false, true)
            .SetUpdate(true); // Ignore timeScale for UI/Juice
    }

    private void OnDestroy()
    {
        shakeTween?.Kill();
    }
}
