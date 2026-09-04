using UnityEngine;
using DG.Tweening;

/// <summary>
/// Handles dynamic visual states for board tiles: default, highlighted, and removed.
/// Attach to the Tile prefab. Uses MaterialPropertyBlock to avoid runtime GC allocations
/// from material instance creation, and DOTween for smooth color/scale transitions.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class TileVisual : MonoBehaviour
{
    // --- Cached References ---
    private Renderer cachedRenderer;
    private MaterialPropertyBlock propBlock;
    private GameSettings gameSettings;

    // --- Tween References (cached to allow kill-before-reassign) ---
    private Tween colorTween;
    private Tween scaleTween;

    // --- State Tracking ---
    private Color currentColor;
    private bool isHighlighted;

    // Shader property ID cached once to avoid string hashing every frame
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Initializes the component with a reference to the shared GameSettings asset.
    /// Must be called once after the tile is first spawned or retrieved from the pool.
    /// </summary>
    public void Initialize(GameSettings settings)
    {
        gameSettings = settings;
        ResetVisual();
    }

    /// <summary>
    /// Toggles the highlight state with a smooth color fade using DOTween.
    /// When highlighted, the tile fades to <see cref="GameSettings.highlightColor"/>.
    /// When unhighlighted, it fades back to <see cref="GameSettings.defaultTileColor"/>.
    /// </summary>
    /// <param name="highlighted">True to highlight, false to return to default.</param>
    public void SetHighlight(bool highlighted)
    {
        if (gameSettings == null) return;
        if (isHighlighted == highlighted) return; // No-op if already in desired state

        isHighlighted = highlighted;
        Color targetColor = highlighted ? gameSettings.highlightColor : gameSettings.defaultTileColor;

        // Kill any in-progress color tween before starting a new one
        colorTween?.Kill();

        colorTween = DOTween.To(
            () => currentColor,
            color => SetColor(color),
            targetColor,
            gameSettings.tileFadeDuration
        ).SetEase(Ease.OutQuad)
         .SetTarget(gameObject); // Link tween lifecycle to this GameObject
    }

    /// <summary>
    /// Plays the tile removal animation: shrinks scale to zero with Ease.InBack,
    /// then invokes the callback (typically to return to ObjectPooler).
    /// </summary>
    /// <param name="onComplete">Callback invoked after the shrink animation finishes.</param>
    public void PlayRemoveAnimation(System.Action onComplete)
    {
        if (gameSettings == null)
        {
            onComplete?.Invoke();
            return;
        }

        // Kill any existing tweens to prevent conflicts
        colorTween?.Kill();
        scaleTween?.Kill();

        scaleTween = transform.DOScale(Vector3.zero, gameSettings.tileFadeDuration)
            .SetEase(Ease.InBack)
            .SetTarget(gameObject)
            .OnComplete(() =>
            {
                onComplete?.Invoke();
            });
    }

    /// <summary>
    /// Resets the tile's visual state to default. Call when spawning from ObjectPooler
    /// to ensure a clean slate (full scale, default color, no active tweens).
    /// </summary>
    public void ResetVisual()
    {
        // Kill any lingering tweens from a previous lifecycle
        colorTween?.Kill();
        scaleTween?.Kill();
        colorTween = null;
        scaleTween = null;

        isHighlighted = false;
        transform.localScale = Vector3.one;

        // Apply default color immediately (no tween)
        Color defaultColor = gameSettings != null ? gameSettings.defaultTileColor : Color.white;
        SetColor(defaultColor);
    }

    /// <summary>
    /// Applies a color to the renderer via MaterialPropertyBlock (zero GC allocation).
    /// </summary>
    private void SetColor(Color color)
    {
        currentColor = color;
        cachedRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(ColorPropertyId, color);
        cachedRenderer.SetPropertyBlock(propBlock);
    }

    private void OnDisable()
    {
        // Ensure tweens are cleaned up when the object returns to the pool
        colorTween?.Kill();
        scaleTween?.Kill();
        colorTween = null;
        scaleTween = null;
    }
}
