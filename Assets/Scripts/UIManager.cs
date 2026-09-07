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
    [Tooltip("Transform of the status dot to animate pulsing")]
    public Transform statusDotTransform;

    [Tooltip("TextMeshProUGUI element displaying the current turn/phase status")]
    public TextMeshProUGUI turnBannerText;

    [Tooltip("Image used as the status dot color indicator on the HUD card")]
    public Image statusDotImage;

    [Header("Game Over Panel")]
    [Tooltip("CanvasGroup wrapping the game over modal card (controls alpha for fade)")]
    public CanvasGroup gameOverCanvasGroup;

    [Tooltip("TextMeshProUGUI showing the win/lose result header")]
    public TextMeshProUGUI gameOverText;

    [Tooltip("Restart button RectTransform for press punch animation")]
    public Button restartButton;

    [Tooltip("Menu button RectTransform for press punch animation")]
    public Button menuButton;

    [Header("Quick Actions")]
    public Button quickRestartButton;
    public Button settingsButton;

    [Header("Main Menu & Panels")]
    public CanvasGroup mainMenuPanel;
    public CanvasGroup gameHUDPanel;
    public CanvasGroup settingsModal;
    
    [Tooltip("Text element for difficulty badge on menu (e.g. '😎 MEDIUM')")]
    public TextMeshProUGUI difficultyBadgeText;
    
    [Tooltip("Text to show what the pass and play mode is")]
    public Button playVsBotBtn;
    public Button playPassAndPlayBtn;
    public Button difficultyToggleBtn;
    public Button closeSettingsBtn;

    [Header("Score Header")]
    [Tooltip("TextMeshProUGUI for Player score")]
    public TextMeshProUGUI playerScoreText;

    [Tooltip("TextMeshProUGUI for AI score")]
    public TextMeshProUGUI aiScoreText;

    [Header("Dynamic Background")]
    [Tooltip("Main Camera to transition background color")]
    public Camera mainCamera;

    [Tooltip("Background color during Player turn (Warm Peach)")]
    public Color playerTurnBgColor = new Color(250f/255f, 177f/255f, 160f/255f, 1f); // #FAB1A0

    [Tooltip("Background color during AI turn (Soft Blue)")]
    public Color aiTurnBgColor = new Color(116f/255f, 185f/255f, 255f/255f, 1f); // #74B9FF

    [Tooltip("Duration of background color transition")]
    public float bgTransitionDuration = 0.4f;

    [Header("Animation Settings")]
    [Tooltip("Duration for the turn banner punch scale animation")]
    public float bannerPunchDuration = 0.35f;

    [Tooltip("Scale punch strength applied to the turn banner on text change")]
    public float bannerPunchScale = 0.15f;

    [Tooltip("Duration for the game over popup fade and scale animation")]
    public float gameOverAnimDuration = 0.5f;

    // Session Score Tracking
    public static int SessionPlayerWins = 0;
    public static int SessionAIWins = 0;
    private bool scoreCountedThisRound = false;

    // Pre-allocated strings to avoid GC allocations on every phase change
    private const string StatusAIThinking = "Bot thinking...";
    private const string StatusPlayerMove = "Your Turn: Move";
    private const string StatusPlayerRemove = "Your Turn: Collapse";
    private const string StatusGameOver = "Game Over";
    
    private const string ResultVictoryHeader = "YOU WON!";
    private const string ResultDefeatHeader = "BOT WON!";

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
    private Tween dotPulseTween;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Auto-wire UI components if not assigned
        if (mainMenuPanel == null)
        {
            var mm = GameObject.Find("MainMenuPanel");
            if (mm != null) mainMenuPanel = mm.GetComponent<CanvasGroup>();
        }
        if (gameHUDPanel == null)
        {
            var gh = GameObject.Find("GameHUDPanel");
            if (gh != null) gameHUDPanel = gh.GetComponent<CanvasGroup>();
        }
        if (settingsModal == null)
        {
            var sm = GameObject.Find("SettingsModal");
            if (sm != null) settingsModal = sm.GetComponent<CanvasGroup>();
        }

        // Auto-wire buttons
        if (playVsBotBtn == null)
        {
            var pvb = GameObject.Find("PlayVsBotButton");
            if (pvb != null) playVsBotBtn = pvb.GetComponent<Button>();
        }
        if (playPassAndPlayBtn == null)
        {
            var ppb = GameObject.Find("PassPlayButton");
            if (ppb != null) playPassAndPlayBtn = ppb.GetComponent<Button>();
        }
        if (difficultyToggleBtn == null)
        {
            var dtb = GameObject.Find("DifficultyBadge");
            if (dtb != null) difficultyToggleBtn = dtb.GetComponent<Button>();
        }
        if (closeSettingsBtn == null)
        {
            var csb = GameObject.Find("CloseBtn");
            if (csb != null) closeSettingsBtn = csb.GetComponent<Button>();
        }
        if (difficultyBadgeText == null)
        {
            var db = GameObject.Find("DifficultyBadge");
            if (db != null) difficultyBadgeText = db.GetComponent<TextMeshProUGUI>();
        }

        // Add Listeners dynamically
        if (playVsBotBtn != null) playVsBotBtn.onClick.AddListener(() => StartGame(0));
        if (playPassAndPlayBtn != null) playPassAndPlayBtn.onClick.AddListener(() => StartGame(1));
        if (difficultyToggleBtn != null) difficultyToggleBtn.onClick.AddListener(ToggleAIDifficulty);
        if (closeSettingsBtn != null) closeSettingsBtn.onClick.AddListener(CloseSettings);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (menuButton != null) menuButton.onClick.AddListener(OnMenuClicked);
        if (quickRestartButton != null) quickRestartButton.onClick.AddListener(OnQuickRestartClicked);
        if (restartButton != null) restartButton.onClick.AddListener(RestartGame);

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 0f;
            gameOverCanvasGroup.interactable = false;
            gameOverCanvasGroup.blocksRaycasts = false;
            gameOverCanvasGroup.gameObject.SetActive(false);
        }

        UpdateScoreDisplay();
    }

    /// <summary>
    /// Updates the turn status banner text, status dot color, and plays a punch animation.
    /// Called by GameManager/PlayerController on every game state transition.
    /// </summary>
    public void UpdateTurnStatus(GameState newState)
    {
        if (turnBannerText == null) return;

        string statusText;
        Color dotColor;
        Color targetBgColor = mainCamera != null ? mainCamera.backgroundColor : Color.black;

        // Reset dot animation
        if (statusDotTransform != null)
        {
            dotPulseTween?.Kill(true);
            statusDotTransform.localScale = Vector3.one;
        }

        switch (newState)
        {
            case GameState.AITurn:
                statusText = StatusAIThinking;
                dotColor = DotColorAI;
                targetBgColor = aiTurnBgColor;
                if (statusDotTransform != null)
                {
                    dotPulseTween = statusDotTransform.DOScale(1.3f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
                }
                break;
            case GameState.PlayerMovePhase:
                statusText = StatusPlayerMove;
                dotColor = DotColorPlayer;
                targetBgColor = playerTurnBgColor;
                break;
            case GameState.PlayerRemovePhase:
                statusText = StatusPlayerRemove;
                dotColor = DotColorPlayer;
                targetBgColor = playerTurnBgColor;
                break;
            case GameState.Player2MovePhase:
                statusText = "Player 2: Move";
                dotColor = DotColorAI;
                targetBgColor = aiTurnBgColor;
                break;
            case GameState.Player2RemovePhase:
                statusText = "Player 2: Collapse";
                dotColor = DotColorAI;
                targetBgColor = aiTurnBgColor;
                break;
            case GameState.GameOver:
                statusText = StatusGameOver;
                dotColor = DotColorNeutral;
                // Keep the current background color during game over
                break;
            case GameState.MainMenu:
                statusText = "Menu";
                dotColor = DotColorNeutral;
                break;
            default:
                statusText = string.Empty;
                dotColor = DotColorNeutral;
                break;
        }

        turnBannerText.text = statusText;

        if (statusDotImage != null)
        {
            statusDotImage.color = dotColor;
        }

        if (mainCamera != null && newState != GameState.GameOver)
        {
            mainCamera.DOColor(targetBgColor, bgTransitionDuration).SetUpdate(true);
        }

        PlayBannerPunch();
    }

    /// <summary>
    /// Shows the game over popup with themed colors and smooth scale-up/fade-in animation.
    /// </summary>
    public void ShowGameOver(bool playerWon)
    {
        if (!scoreCountedThisRound)
        {
            if (playerWon) SessionPlayerWins++;
            else SessionAIWins++;
            scoreCountedThisRound = true;
            UpdateScoreDisplay();
        }

        if (gameOverCanvasGroup == null) return;

        // Set themed result text and colors
        if (gameOverText != null)
        {
            gameOverText.text = playerWon ? ResultVictoryHeader : ResultDefeatHeader;
            gameOverText.color = playerWon ? VictoryHeaderColor : DefeatHeaderColor;
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

    public void RestartGame()
    {
        HandleButtonClick(restartButton, () => {
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });
    }

    public void OnMenuClicked()
    {
        HandleButtonClick(menuButton, () => {
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });
    }

    public void OnQuickRestartClicked()
    {
        HandleButtonClick(quickRestartButton, () => {
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });
    }

    public void OnSettingsClicked()
    {
        HandleButtonClick(settingsButton, () => {
            Debug.Log("Settings Button Clicked");
        });
    }

    private void HandleButtonClick(Button btn, System.Action onComplete)
    {
        AudioManager.Instance?.PlayUIClickSound();
        if (btn == null)
        {
            onComplete?.Invoke();
            return;
        }
        
        btn.interactable = false;
        btn.transform.DOPunchScale(Vector3.one * -0.1f, 0.15f, 1, 0.5f)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                btn.interactable = true;
                onComplete?.Invoke();
            });
    }

    /// <summary>
    /// Plays a punch scale effect on the turn banner card to draw attention on text changes.
    /// </summary>
    private void PlayBannerPunch()
    {
        if (turnBannerText == null) return;

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

    private void UpdateScoreDisplay()
    {
        if (playerScoreText != null)
        {
            playerScoreText.text = $"YOU <color=#06D6A0>{SessionPlayerWins}</color>";
        }
        if (aiScoreText != null)
        {
            aiScoreText.text = $"<color=#FF6B6B>{SessionAIWins}</color> BOT";
        }
    }

    public void OpenMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.gameObject.SetActive(true);
            mainMenuPanel.alpha = 1f;
            mainMenuPanel.interactable = true;
            mainMenuPanel.blocksRaycasts = true;
        }
        if (gameHUDPanel != null)
        {
            gameHUDPanel.alpha = 0f;
            gameHUDPanel.interactable = false;
            gameHUDPanel.blocksRaycasts = false;
        }
        if (settingsModal != null) settingsModal.gameObject.SetActive(false);
        if (gameOverCanvasGroup != null) gameOverCanvasGroup.gameObject.SetActive(false);

        UpdateDifficultyUI();
    }

    public void StartGame(int modeIndex) // 0 = VsAI, 1 = PassAndPlay
    {
        GameMode mode = (GameMode)modeIndex;
        AudioManager.Instance?.PlayUIClickSound();

        // Crossfade panels
        if (mainMenuPanel != null)
        {
            mainMenuPanel.interactable = false;
            mainMenuPanel.blocksRaycasts = false;
            mainMenuPanel.DOFade(0f, 0.4f).OnComplete(() => mainMenuPanel.gameObject.SetActive(false));
        }

        if (gameHUDPanel != null)
        {
            gameHUDPanel.DOFade(1f, 0.4f).OnComplete(() =>
            {
                gameHUDPanel.interactable = true;
                gameHUDPanel.blocksRaycasts = true;
            });
        }

        GameManager.Instance.StartGameSequence(mode, GameManager.CurrentDifficulty);
    }

    public void ToggleAIDifficulty()
    {
        AudioManager.Instance?.PlayUIClickSound();
        if (difficultyToggleBtn != null)
        {
            difficultyToggleBtn.transform.DOPunchScale(Vector3.one * -0.1f, 0.15f, 1, 0.5f);
        }

        int diff = (int)GameManager.CurrentDifficulty + 1;
        if (diff > 2) diff = 0;
        GameManager.CurrentDifficulty = (AIDifficulty)diff;
        UpdateDifficultyUI();
    }

    private void UpdateDifficultyUI()
    {
        if (difficultyBadgeText == null) return;
        
        switch (GameManager.CurrentDifficulty)
        {
            case AIDifficulty.Easy:
                difficultyBadgeText.text = "😊 EASY";
                difficultyBadgeText.color = new Color(6f/255f, 214f/255f, 160f/255f); // Jade
                break;
            case AIDifficulty.Medium:
                difficultyBadgeText.text = "😎 MEDIUM";
                difficultyBadgeText.color = new Color(251f/255f, 191f/255f, 36f/255f); // Amber
                break;
            case AIDifficulty.Hard:
                difficultyBadgeText.text = "😈 HARD";
                difficultyBadgeText.color = new Color(255f/255f, 107f/255f, 107f/255f); // Coral
                break;
        }
    }

    public void OpenSettings()
    {
        if (settingsModal == null) return;
        AudioManager.Instance?.PlayUIClickSound();
        settingsModal.gameObject.SetActive(true);
        settingsModal.alpha = 0f;
        settingsModal.interactable = true;
        settingsModal.blocksRaycasts = true;
        settingsModal.DOFade(1f, 0.25f);
        settingsModal.transform.localScale = Vector3.one * 0.9f;
        settingsModal.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
    }

    public void CloseSettings()
    {
        if (settingsModal == null) return;
        AudioManager.Instance?.PlayUIClickSound();
        settingsModal.interactable = false;
        settingsModal.blocksRaycasts = false;
        settingsModal.DOFade(0f, 0.2f).OnComplete(() => settingsModal.gameObject.SetActive(false));
    }

    private void OnDestroy()
    {
        bannerPunchTween?.Kill();
        gameOverFadeTween?.Kill();
        gameOverScaleTween?.Kill();
        dotPulseTween?.Kill();
    }
}
