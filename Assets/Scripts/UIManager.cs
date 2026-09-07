using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// Singleton UI Manager responsible for turn status display and game over popup.
/// Features glassmorphism-styled HUD card with status dot indicator and
/// animated modal game-over panel with themed victory/defeat messages.
/// All UI animations use DOTween with cached tween references for zero GC.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Turn Status")]
    [Tooltip("TextMeshProUGUI element displaying the current turn/phase status")]
    public TextMeshProUGUI turnBannerText;

    [Tooltip("TextMeshProUGUI element displaying the subtext for the current turn/phase")]
    public TextMeshProUGUI turnBannerSubtext;

    [Tooltip("Image used as the status dot color indicator on the HUD card")]
    public Image statusDotImage;

    [Header("Game Over Panel")]
    [Tooltip("CanvasGroup wrapping the game over modal card (controls alpha for fade)")]
    public CanvasGroup gameOverCanvasGroup;

    [Tooltip("TextMeshProUGUI showing the win/lose result header")]
    public TextMeshProUGUI gameOverText;

    [Tooltip("TextMeshProUGUI showing the flavor subtext below the result")]
    public TextMeshProUGUI gameOverSubtext;

    [Tooltip("Restart button RectTransform for press punch animation")]
    public Button restartButton;

    [Header("Animation Settings")]
    [Tooltip("Duration for the turn banner punch scale animation")]
    public float bannerPunchDuration = 0.35f;

    [Tooltip("Scale punch strength applied to the turn banner on text change")]
    public float bannerPunchScale = 0.15f;

    [Tooltip("Duration for the game over popup fade and scale animation")]
    public float gameOverAnimDuration = 0.5f;

    // Pre-allocated strings to avoid GC allocations on every phase change
    private const string StatusAIThinking = "AI THINKING";
    private const string SubAIThinking = "Calculating next move...";
    private const string StatusPlayerMove = "YOUR TURN";
    private const string SubPlayerMove = "Select an adjacent tile to move";
    private const string StatusPlayerRemove = "COLLAPSE TILE";
    private const string SubPlayerRemove = "Choose a tile to collapse";
    private const string StatusGameOver = "GAME OVER";
    private const string SubGameOver = "";
    
    private const string ResultVictoryHeader = "VICTORY";
    private const string ResultVictorySub = "Island Sanctuary Cleared";
    private const string ResultDefeatHeader = "DEFEAT";
    private const string ResultDefeatSub = "Trapped in the Void";

    // Theme colors for status dot and game-over accents
    private static readonly Color DotColorPlayer = new Color(6f / 255f, 214f / 255f, 160f / 255f, 1f);   // Jade Green
    private static readonly Color DotColorAI = new Color(255f / 255f, 107f / 255f, 107f / 255f, 1f);      // Rose Coral
    private static readonly Color DotColorNeutral = new Color(0.55f, 0.6f, 0.7f, 1f);                     // Slate grey

    private static readonly Color VictoryHeaderColor = new Color(6f / 255f, 214f / 255f, 160f / 255f, 1f); // Jade
    private static readonly Color DefeatHeaderColor = new Color(230f / 255f, 57f / 255f, 70f / 255f, 1f);  // Crimson Rose

    // Cached tween references to prevent stacking
    private Tween bannerPunchTween;
    private Tween gameOverFadeTween;
    private Tween gameOverScaleTween;
    private Tween buttonPressTween;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure game over panel starts hidden
        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 0f;
            gameOverCanvasGroup.interactable = false;
            gameOverCanvasGroup.blocksRaycasts = false;
            gameOverCanvasGroup.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Updates the turn status banner text, status dot color, and plays a punch animation.
    /// Called by GameManager/PlayerController on every game state transition.
    /// </summary>
    public void UpdateTurnStatus(GameState newState)
    {
        if (turnBannerText == null) return;

        string statusText;
        string subText;
        Color dotColor;

        switch (newState)
        {
            case GameState.AITurn:
                statusText = StatusAIThinking;
                subText = SubAIThinking;
                dotColor = DotColorAI;
                break;
            case GameState.PlayerMovePhase:
                statusText = StatusPlayerMove;
                subText = SubPlayerMove;
                dotColor = DotColorPlayer;
                break;
            case GameState.PlayerRemovePhase:
                statusText = StatusPlayerRemove;
                subText = SubPlayerRemove;
                dotColor = DotColorPlayer;
                break;
            case GameState.GameOver:
                statusText = StatusGameOver;
                subText = SubGameOver;
                dotColor = DotColorNeutral;
                break;
            default:
                statusText = string.Empty;
                subText = string.Empty;
                dotColor = DotColorNeutral;
                break;
        }

        turnBannerText.text = statusText;
        if (turnBannerSubtext != null)
        {
            turnBannerSubtext.text = subText;
        }

        if (statusDotImage != null)
        {
            statusDotImage.color = dotColor;
        }

        PlayBannerPunch();
    }

    /// <summary>
    /// Shows the game over popup with themed colors and smooth scale-up/fade-in animation.
    /// </summary>
    public void ShowGameOver(bool playerWon)
    {
        if (gameOverCanvasGroup == null) return;

        // Set themed result text and colors
        if (gameOverText != null)
        {
            gameOverText.text = playerWon ? ResultVictoryHeader : ResultDefeatHeader;
            gameOverText.color = playerWon ? VictoryHeaderColor : DefeatHeaderColor;
        }

        if (gameOverSubtext != null)
        {
            gameOverSubtext.text = playerWon ? ResultVictorySub : ResultDefeatSub;
        }

        if (playerWon)
        {
            AudioManager.Instance?.PlayVictorySound();
            VFXManager.Instance?.PlayVictoryConfetti(Vector3.up * 2f);
        }
        else
        {
            AudioManager.Instance?.PlayDefeatSound();
        }

        // Activate the panel and prepare for animation
        gameOverCanvasGroup.gameObject.SetActive(true);
        gameOverCanvasGroup.alpha = 0f;
        gameOverCanvasGroup.interactable = false;
        gameOverCanvasGroup.blocksRaycasts = false;

        Transform panelTransform = gameOverCanvasGroup.transform;
        panelTransform.localScale = Vector3.one * 0.5f;

        // Kill any leftover tweens
        gameOverFadeTween?.Kill();
        gameOverScaleTween?.Kill();

        // Fade in alpha
        gameOverFadeTween = DOTween.To(
            () => gameOverCanvasGroup.alpha,
            a => gameOverCanvasGroup.alpha = a,
            1f,
            gameOverAnimDuration
        ).SetEase(Ease.OutQuad)
         .SetTarget(gameOverCanvasGroup)
         .SetUpdate(true);

        // Scale up from 0.5 to 1.0
        gameOverScaleTween = panelTransform.DOScale(Vector3.one, gameOverAnimDuration)
            .SetEase(Ease.OutBack)
            .SetTarget(gameOverCanvasGroup)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                gameOverCanvasGroup.interactable = true;
                gameOverCanvasGroup.blocksRaycasts = true;
            });
    }

    /// <summary>
    /// Restarts the game with a subtle button press punch before reloading.
    /// Hooked to the Restart button via Inspector or code.
    /// </summary>
    public void RestartGame()
    {
        AudioManager.Instance?.PlayUIClickSound();

        if (restartButton != null)
        {
            buttonPressTween?.Kill();
            restartButton.interactable = false;

            buttonPressTween = restartButton.transform
                .DOPunchScale(Vector3.one * 0.12f, 0.25f, 8, 0.6f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    DOTween.KillAll();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                });
        }
        else
        {
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    /// <summary>
    /// Plays a punch scale effect on the turn banner card to draw attention on text changes.
    /// </summary>
    private void PlayBannerPunch()
    {
        if (turnBannerText == null) return;

        // Punch the parent card, not just the text
        Transform punchTarget = turnBannerText.transform.parent != null
            ? turnBannerText.transform.parent
            : turnBannerText.transform;

        bannerPunchTween?.Kill(true);
        punchTarget.localScale = Vector3.one;

        bannerPunchTween = punchTarget
            .DOPunchScale(Vector3.one * bannerPunchScale, bannerPunchDuration, 6, 0.7f)
            .SetTarget(punchTarget)
            .SetUpdate(true);
    }

    private void OnDestroy()
    {
        bannerPunchTween?.Kill();
        gameOverFadeTween?.Kill();
        gameOverScaleTween?.Kill();
        buttonPressTween?.Kill();
    }
}
