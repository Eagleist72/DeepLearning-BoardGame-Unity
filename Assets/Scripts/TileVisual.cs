using UnityEngine;
using DG.Tweening;

/// <summary>
/// Handles dynamic visual states for board tiles: default, highlighted, and removed.
/// Attach to the Tile prefab. Uses MaterialPropertyBlock to avoid runtime GC allocations
/// from material instance creation, and DOTween for smooth color/scale transitions.
/// </summary>
public enum HighlightType { None, Move, Remove }

[RequireComponent(typeof(Renderer))]
public class TileVisual : MonoBehaviour
{
    // --- Cached References ---
    private Renderer cachedRenderer;
    private MaterialPropertyBlock propBlock;
    private GameSettings gameSettings;

    // --- Tween References ---
    private Tween colorTween;
    private Tween scaleTween;

    [Header("Collapse Animations")]
    [Tooltip("Shake and fall parameters when tile is destroyed")]
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeStrength = 0.05f;
    [SerializeField] private float fallDistance = 4.0f;
    [SerializeField] private float fallDuration = 0.4f;

    // --- State Tracking ---
    private Color currentColor;
    private HighlightType currentHighlight = HighlightType.None;
    private Vector3 initialScale;

    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        initialScale = transform.localScale;
    }

    public void Initialize(GameSettings settings)
    {
        gameSettings = settings;
        ResetVisual();
    }

    public void SetHighlight(HighlightType type)
    {
        if (gameSettings == null) return;
        if (currentHighlight == type) return;

        currentHighlight = type;

        colorTween?.Kill();
        scaleTween?.Kill();

        Color targetColor;
        
        switch (type)
        {
            case HighlightType.Move:
                targetColor = new Color(245f/255f, 158f/255f, 11f/255f); // Amber / Gold
                scaleTween = transform.DOScale(initialScale * 1.05f, 0.6f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetTarget(gameObject);
                break;
            case HighlightType.Remove:
                targetColor = new Color(239f/255f, 68f/255f, 68f/255f); // Red / Crimson
                transform.localScale = initialScale; // No pulsing for remove
                break;
            default:
                targetColor = gameSettings.defaultTileColor;
                transform.localScale = initialScale;
                break;
        }

        colorTween = DOTween.To(
            () => currentColor,
            color => SetColor(color),
            targetColor,
            gameSettings.tileFadeDuration
        ).SetEase(Ease.OutQuad)
         .SetTarget(gameObject);
    }

    public void PlayRemoveAnimation(System.Action onComplete)
    {
        if (gameSettings == null)
        {
            onComplete?.Invoke();
            return;
        }

        colorTween?.Kill();
        scaleTween?.Kill();

        // 1. Shake, 2. Drop + Fade out
        Sequence seq = DOTween.Sequence();
        
        seq.Append(transform.DOShakePosition(shakeDuration, shakeStrength));
        seq.Append(transform.DOMoveY(transform.position.y - fallDistance, fallDuration).SetEase(Ease.InCubic));
        
        colorTween = DOTween.To(
            () => currentColor,
            color => SetColor(color),
            new Color(currentColor.r, currentColor.g, currentColor.b, 0f),
            0.4f
        ).SetEase(Ease.InCubic).SetTarget(gameObject);

        seq.OnComplete(() =>
        {
            onComplete?.Invoke();
        });
        
        seq.SetTarget(gameObject);
        scaleTween = seq; // Assign sequence to scaleTween to track it for killing
    }

    public void ResetVisual()
    {
        colorTween?.Kill();
        scaleTween?.Kill();
        colorTween = null;
        scaleTween = null;

        currentHighlight = HighlightType.None;
        transform.localScale = initialScale;

        // Reset position Y in case it was dropped
        Vector3 pos = transform.position;
        pos.y = 0f;
        transform.position = pos;

        Color defaultColor = gameSettings != null ? gameSettings.defaultTileColor : Color.white;
        SetColor(defaultColor);
    }

    private void SetColor(Color color)
    {
        currentColor = color;
        cachedRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(ColorPropertyId, color);
        cachedRenderer.SetPropertyBlock(propBlock);
    }

    private void OnDisable()
    {
        colorTween?.Kill();
        scaleTween?.Kill();
        colorTween = null;
        scaleTween = null;
    }
}
