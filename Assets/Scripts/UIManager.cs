using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// Singleton UI Manager responsible for turn status display and game over popup.
/// All UI animations use DOTween. Requires a Canvas with the following hierarchy:
///   - TurnBannerText (TextMeshProUGUI)
///   - GameOverPanel (CanvasGroup, starts inactive)
///     - GameOverText (TextMeshProUGUI)
///     - RestartButton (UnityEngine.UI.Button)
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Turn Status")]
    [Tooltip("TextMeshProUGUI element displaying the current turn/phase status")]
    public TextMeshProUGUI turnBannerText;

    [Header("Game Over Panel")]
    [Tooltip("CanvasGroup wrapping the game over popup (controls alpha for fade)")]
    public CanvasGroup gameOverCanvasGroup;

    [Tooltip("TextMeshProUGUI showing the win/lose result message")]
    public TextMeshProUGUI gameOverText;

    [Header("Animation Settings")]
    [Tooltip("Duration for the turn banner punch scale animation")]
    public float bannerPunchDuration = 0.35f;

    [Tooltip("Scale punch strength applied to the turn banner on text change")]
    public float bannerPunchScale = 0.15f;

    [Tooltip("Duration for the game over popup fade and scale animation")]
    public float gameOverAnimDuration = 0.5f;

    // Pre-allocated strings to avoid GC allocations on every phase change
    private const string StatusAIThinking = "AI is thinking...";
    private const string StatusPlayerMove = "Your Turn: Select Move";
    private const string StatusPlayerRemove = "Your Turn: Remove a Tile";
    private const string StatusGameOver = "Game Over";
    private const string ResultVictory = "Victory!\nAI is trapped.";
    private const string ResultDefeat = "Defeat!\nYou are trapped.";

    // Cached tween references to prevent stacking
    private Tween bannerPunchTween;
    private Tween gameOverFadeTween;
    private Tween gameOverScaleTween;

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
    /// Updates the turn status banner text and plays a punch scale animation.
    /// Called by GameManager/PlayerController on every game state transition.
    /// </summary>
    /// <param name="newState">The new game state to reflect in the UI.</param>
    public void UpdateTurnStatus(GameState newState)
    {
        if (turnBannerText == null) return;

        string statusText;
        switch (newState)
        {
            case GameState.AITurn:
                statusText = StatusAIThinking;
                break;
            case GameState.PlayerMovePhase:
                statusText = StatusPlayerMove;
                break;
            case GameState.PlayerRemovePhase:
                statusText = StatusPlayerRemove;
                break;
            case GameState.GameOver:
                statusText = StatusGameOver;
                break;
            default:
                statusText = string.Empty;
                break;
        }

        turnBannerText.text = statusText;
        PlayBannerPunch();
    }

    /// <summary>
    /// Shows the game over popup with a smooth scale-up and fade-in animation.
    /// </summary>
    /// <param name="playerWon">True if the player won (AI trapped), false if AI won.</param>
    public void ShowGameOver(bool playerWon)
    {
        if (gameOverCanvasGroup == null) return;

        // Set result text
        if (gameOverText != null)
        {
            gameOverText.text = playerWon ? ResultVictory : ResultDefeat;
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
         .SetUpdate(true); // Use unscaled time in case timeScale is 0

        // Scale up from 0.5 to 1.0
        gameOverScaleTween = panelTransform.DOScale(Vector3.one, gameOverAnimDuration)
            .SetEase(Ease.OutBack)
            .SetTarget(gameOverCanvasGroup)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                // Enable interaction after animation completes
                gameOverCanvasGroup.interactable = true;
                gameOverCanvasGroup.blocksRaycasts = true;
            });
    }

    /// <summary>
    /// Restarts the game by reloading the active scene.
    /// Hooked to the Restart button via Inspector or code.
    /// </summary>
    public void RestartGame()
    {
        AudioManager.Instance?.PlayUIClickSound();
        
        // Kill all DOTween tweens to prevent callbacks on destroyed objects
        DOTween.KillAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Plays a punch scale effect on the turn banner to draw attention on text changes.
    /// </summary>
    private void PlayBannerPunch()
    {
        if (turnBannerText == null) return;

        // Kill previous punch to prevent compounding scale drift
        bannerPunchTween?.Kill(true); // complete=true resets scale to original
        turnBannerText.transform.localScale = Vector3.one;

        bannerPunchTween = turnBannerText.transform
            .DOPunchScale(Vector3.one * bannerPunchScale, bannerPunchDuration, 6, 0.7f)
            .SetTarget(turnBannerText)
            .SetUpdate(true);
    }

    private void OnDestroy()
    {
        // Clean up tweens when the UIManager is destroyed (e.g., scene reload)
        bannerPunchTween?.Kill();
        gameOverFadeTween?.Kill();
        gameOverScaleTween?.Kill();
    }
}
