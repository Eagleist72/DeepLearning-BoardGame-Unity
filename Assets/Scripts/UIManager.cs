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

    [Header("UI Panels & CanvasGroups")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup gameHUDPanel;
    
    [Header("Settings Modal")]
    [SerializeField] private GameObject settingsModal;
    [SerializeField] private RectTransform settingsCard;
    [SerializeField] private CanvasGroup settingsModalCanvasGroup;
    [SerializeField] private Button settingsBtn;
    [SerializeField] private Button closeSettingsBtn;

    [Header("How To Play Modal")]
    [SerializeField] private GameObject howToPlayModal;
    [SerializeField] private RectTransform howToPlayCard;
    [SerializeField] private CanvasGroup howToPlayCanvasGroup;
    [SerializeField] private Button howToPlayBtn;
    [SerializeField] private Button closeHowToPlayBtn;

    [Header("Menu Buttons")]
    [Tooltip("Text element for difficulty badge on menu (e.g. 'MEDIUM')")]
    [SerializeField] private TextMeshProUGUI difficultyBadgeText;
    
    [Tooltip("Text to show what the pass and play mode is")]
    [SerializeField] private Button playVsBotBtn;
    [SerializeField] private Button playPassAndPlayBtn;
    [SerializeField] private Button difficultyToggleBtn;
    [SerializeField] private Button sfxToggleBtn;
    [SerializeField] private Button musicToggleBtn;
    [SerializeField] private Button hapticsToggleBtn;

    [Header("Score Header")]
    [Tooltip("TextMeshProUGUI for Player score")]
    [SerializeField] private TextMeshProUGUI playerScoreText;

    [Tooltip("TextMeshProUGUI for AI score")]
    [SerializeField] private TextMeshProUGUI aiScoreText;

    [Header("Theme & Dynamic Sky Colors")]
    [Tooltip("Main Camera to transition background color")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Sky color during Player 1's turn")]
    [SerializeField] private Color playerTurnBgColor = new Color(250f/255f, 177f/255f, 160f/255f, 1f); // #FAB1A0

    [Tooltip("Sky color during AI / Player 2's turn")]
    [SerializeField] private Color aiTurnBgColor = new Color(116f/255f, 185f/255f, 255f/255f, 1f); // #74B9FF

    [Range(0.1f, 1.0f)]
    [Tooltip("Duration of background color transition")]
    [SerializeField] private float bgTransitionDuration = 0.4f;

    [Header("Animation Settings")]
    [Tooltip("Duration for the turn banner punch scale animation")]
    [SerializeField] private float bannerPunchDuration = 0.35f;

    [Tooltip("Scale punch strength applied to the turn banner on text change")]
    [SerializeField] private float bannerPunchScale = 0.15f;

    [Tooltip("Duration for the game over popup fade and scale animation")]
    [SerializeField] private float gameOverAnimDuration = 0.5f;

    [Header("Tactile Juice Settings")]
    [Range(0.05f, 0.5f)] [Tooltip("Punch scale punch amount on button clicks")]
    [SerializeField] private float buttonPunchScale = 0.1f;
    
    [Range(0.05f, 0.5f)] [Tooltip("Duration of the tactile button press")]
    [SerializeField] private float buttonPunchDuration = 0.15f;

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

        // --- Auto-wire panels (fallback if Inspector refs are missing) ---
        AutoWireCanvasGroup(ref mainMenuPanel, "MainMenuPanel");
        AutoWireCanvasGroup(ref gameHUDPanel, "GameHUDPanel");

        // --- Auto-wire buttons ---
        AutoWireButton(ref playVsBotBtn, "PlayVsBotButton");
        AutoWireButton(ref playPassAndPlayBtn, "PassPlayButton");
        AutoWireButton(ref difficultyToggleBtn, "DifficultyBadge");
        
        // --- Auto-wire TMP text fields via hierarchy path ---
        AutoWireTMP(ref playerScoreText, "GameHUDPanel/ScoreHeaderCard/PlayerScoreText");
        AutoWireTMP(ref aiScoreText, "GameHUDPanel/ScoreHeaderCard/AIScoreText");
        AutoWireTMP(ref turnBannerText, "GameHUDPanel/TurnBannerCard/TurnBannerText");
        AutoWireTMP(ref difficultyBadgeText, "MainMenuPanel/PlayVsBotButton/DifficultyBadge");

        // --- Register button listeners ---
        if (playVsBotBtn != null) playVsBotBtn.onClick.AddListener(() => StartGame(0));
        if (playPassAndPlayBtn != null) playPassAndPlayBtn.onClick.AddListener(() => StartGame(1));
        if (difficultyToggleBtn != null) difficultyToggleBtn.onClick.AddListener(ToggleAIDifficulty);
        
        if (settingsBtn != null) settingsBtn.onClick.AddListener(OpenSettings);
        if (closeSettingsBtn != null) closeSettingsBtn.onClick.AddListener(CloseSettings);
        
        if (howToPlayBtn != null) howToPlayBtn.onClick.AddListener(OpenHowToPlay);
        if (closeHowToPlayBtn != null) closeHowToPlayBtn.onClick.AddListener(CloseHowToPlay);
        
        if (sfxToggleBtn != null) sfxToggleBtn.onClick.AddListener(ToggleSFX);
        if (musicToggleBtn != null) musicToggleBtn.onClick.AddListener(ToggleMusic);
        if (hapticsToggleBtn != null) hapticsToggleBtn.onClick.AddListener(ToggleHaptics);

        if (menuButton != null) menuButton.onClick.AddListener(OnMenuClicked);
        if (quickRestartButton != null) quickRestartButton.onClick.AddListener(OnQuickRestartClicked);
        if (restartButton != null) restartButton.onClick.AddListener(RestartGame);

        // Hide panels on start
        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 0f;
            gameOverCanvasGroup.interactable = false;
            gameOverCanvasGroup.blocksRaycasts = false;
            gameOverCanvasGroup.gameObject.SetActive(false);
        }
        
        if (settingsModal != null) settingsModal.SetActive(false);
        if (howToPlayModal != null) howToPlayModal.SetActive(false);
    }

    private void Start()
    {
        // Load difficulty from PlayerPrefs
        GameManager.ActiveDifficulty = (AIDifficulty)PlayerPrefs.GetInt("AIDifficulty", (int)AIDifficulty.Hard);

        // Force-populate all text fields so nothing is ever blank on screen
        ForceInitializeAllTexts();
        UpdateScoreDisplay();
        UpdateDifficultyUI();

        // Set initial turn banner text
        if (turnBannerText != null)
        {
            turnBannerText.text = StatusPlayerMove;
        }
    }

    /// <summary>
    /// Ensures every TextMeshProUGUI component under UICanvas has a valid font,
    /// bold styling, is enabled, and has mesh generated. This runs once at startup.
    /// </summary>
    private void ForceInitializeAllTexts()
    {
        var defaultFont = TMP_Settings.defaultFontAsset;
        var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (var txt in allTexts)
        {
            // Re-enable any accidentally disabled TMP components
            if (!txt.enabled)
            {
                txt.enabled = true;
            }

            // Fix any remaining broken font references
            if (txt.font == null || txt.font.atlasTexture == null)
            {
                txt.font = defaultFont;
            }

            // Ensure bold styling for readability
            if (txt.fontStyle == FontStyles.Normal)
            {
                txt.fontStyle = FontStyles.Bold;
            }

            // Force TMP to generate mesh geometry immediately
            txt.ForceMeshUpdate(true);
        }

        // Explicitly set default button label texts (in case scene-saved text is blank)
        SetChildText(playVsBotBtn, "PLAY VS BOT");
        SetChildText(playPassAndPlayBtn, "PASS & PLAY");

        // Force Canvas to process layout updates
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Sets the text on a TextMeshProUGUI child of a Button, if the child text is empty.
    /// </summary>
    private void SetChildText(Button btn, string text)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null && string.IsNullOrEmpty(tmp.text))
        {
            tmp.text = text;
        }
    }

    // --- Auto-wire helpers that search the entire Canvas hierarchy (including inactive) ---

    private void AutoWireCanvasGroup(ref CanvasGroup field, string objectName)
    {
        if (field != null) return;
        var found = FindInChildrenByName<CanvasGroup>(transform, objectName);
        if (found != null) field = found;
    }

    private void AutoWireButton(ref Button field, string objectName)
    {
        if (field != null) return;
        var found = FindInChildrenByName<Button>(transform, objectName);
        if (found != null) field = found;
    }

    private void AutoWireTMP(ref TextMeshProUGUI field, string hierarchyPath)
    {
        if (field != null) return;
        // hierarchyPath like "GameHUDPanel/ScoreHeaderCard/PlayerScoreText"
        Transform current = transform;
        string[] parts = hierarchyPath.Split('/');
        foreach (string part in parts)
        {
            current = current.Find(part);
            if (current == null) return;
        }
        field = current.GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// Recursively searches children (including inactive) for a component on a named object.
    /// </summary>
    private T FindInChildrenByName<T>(Transform parent, string objectName) where T : Component
    {
        foreach (Transform child in parent)
        {
            if (child.name == objectName)
            {
                var comp = child.GetComponent<T>();
                if (comp != null) return comp;
            }
            var result = FindInChildrenByName<T>(child, objectName);
            if (result != null) return result;
        }
        return null;
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
                statusText = GameManager.ActiveMode == GameMode.PassAndPlay ? "Player 1: Move" : StatusPlayerMove;
                dotColor = DotColorPlayer;
                targetBgColor = playerTurnBgColor;
                break;
            case GameState.PlayerRemovePhase:
                statusText = GameManager.ActiveMode == GameMode.PassAndPlay ? "Player 1: Collapse" : StatusPlayerRemove;
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

        if (newState == GameState.AITurn || newState == GameState.PlayerMovePhase || newState == GameState.Player2MovePhase)
        {
            if (AudioManager.Instance != null && AudioManager.Instance.turnSwitchClip != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.turnSwitchClip, 0.7f);
            }
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
            // For now, reload scene to reset board state
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        });
    }

    public void OnMenuClicked()
    {
        HandleButtonClick(menuButton, () => {
            // Fade out GameHUDPanel and GameOverModal
            if (gameOverCanvasGroup != null) gameOverCanvasGroup.DOFade(0f, 0.2f);
            if (gameHUDPanel != null)
            {
                gameHUDPanel.interactable = false;
                gameHUDPanel.blocksRaycasts = false;
                gameHUDPanel.DOFade(0f, 0.2f);
            }

            // Trigger CameraController.TransitionToMenu
            if (CameraController.Instance != null)
            {
                CameraController.Instance.TransitionToMenu(() => {
                    // Because we lack a soft ResetBoard() method, we'll reload the scene
                    // to reset the board. Since Awake() handles the boot states (MainMenu active, camera at MenuFraming),
                    // it will correctly appear seamlessly at the menu.
                    DOTween.KillAll();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                });
            }
            else
            {
                DOTween.KillAll();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
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
        btn.transform.DOPunchScale(Vector3.one * -buttonPunchScale, buttonPunchDuration, 1, 0.5f)
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
        bool isPassPlay = GameManager.ActiveMode == GameMode.PassAndPlay;

        if (playerScoreText != null)
        {
            playerScoreText.text = (isPassPlay ? "P1  " : "YOU  ") + SessionPlayerWins;
            playerScoreText.color = new Color(6f / 255f, 214f / 255f, 160f / 255f); // Jade
            playerScoreText.fontStyle = FontStyles.Bold;
            playerScoreText.fontSize = 24f;
        }
        if (aiScoreText != null)
        {
            aiScoreText.text = SessionAIWins + (isPassPlay ? "  P2" : "  BOT");
            aiScoreText.color = new Color(255f / 255f, 107f / 255f, 107f / 255f); // Rose
            aiScoreText.fontStyle = FontStyles.Bold;
            aiScoreText.fontSize = 24f;
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

        // 1. Immediately disable raycasts to prevent multi-clicks
        if (mainMenuPanel != null)
        {
            mainMenuPanel.blocksRaycasts = false;
            mainMenuPanel.interactable = false;
            
            // 2. Fade out MainMenuPanel
            mainMenuPanel.DOFade(0f, 0.25f);
        }

        // 3. Trigger Camera Transition
        if (CameraController.Instance != null)
        {
            CameraController.Instance.TransitionToGame(() =>
            {
                // 4. In callback: Fade in GameHUDPanel and start game
                if (mainMenuPanel != null) mainMenuPanel.gameObject.SetActive(false);
                
                if (gameHUDPanel != null)
                {
                    gameHUDPanel.gameObject.SetActive(true);
                    gameHUDPanel.DOFade(1f, 0.25f).OnComplete(() =>
                    {
                        gameHUDPanel.interactable = true;
                        gameHUDPanel.blocksRaycasts = true;
                    });
                }

                GameManager.Instance.StartGameSequence(mode, GameManager.ActiveDifficulty);
            });
        }
        else
        {
            // Fallback if camera controller is missing
            GameManager.Instance.StartGameSequence(mode, GameManager.ActiveDifficulty);
        }
    }

    public void ToggleAIDifficulty()
    {
        AudioManager.Instance?.PlayUIClickSound();
        if (difficultyBadgeText != null)
        {
            difficultyBadgeText.transform.DOPunchScale(Vector3.one * buttonPunchScale, buttonPunchDuration);
        }

        int diff = (int)GameManager.ActiveDifficulty + 1;
        if (diff > 2) diff = 0;
        GameManager.ActiveDifficulty = (AIDifficulty)diff;
        
        PlayerPrefs.SetInt("AIDifficulty", diff);
        PlayerPrefs.Save();
        
        UpdateDifficultyUI();
    }

    private void UpdateDifficultyUI()
    {
        if (difficultyBadgeText == null) return;
        
        switch (GameManager.ActiveDifficulty)
        {
            case AIDifficulty.Easy:
                difficultyBadgeText.text = "EASY";
                ColorUtility.TryParseHtmlString("#10B981", out Color easyCol);
                difficultyBadgeText.color = easyCol;
                break;
            case AIDifficulty.Medium:
                difficultyBadgeText.text = "MEDIUM";
                ColorUtility.TryParseHtmlString("#F59E0B", out Color medCol);
                difficultyBadgeText.color = medCol;
                break;
            case AIDifficulty.Hard:
                difficultyBadgeText.text = "HARD";
                ColorUtility.TryParseHtmlString("#EF4444", out Color hardCol);
                difficultyBadgeText.color = hardCol;
                break;
        }
    }

    public void OpenSettings()
    {
        if (settingsModal == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.modalOpenClip);
        settingsModal.SetActive(true);
        settingsModalCanvasGroup.alpha = 1;
        settingsModalCanvasGroup.blocksRaycasts = true;
        settingsCard.localScale = Vector3.zero;
        settingsCard.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
        UpdateSettingsUI();
    }

    public void CloseSettings()
    {
        if (settingsModal == null) return;
        AudioManager.Instance?.PlayUIClickSound();
        settingsCard.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() => {
            settingsModalCanvasGroup.blocksRaycasts = false;
            settingsModal.SetActive(false);
            settingsCard.localScale = Vector3.one;
        });
    }

    public void OpenHowToPlay()
    {
        if (howToPlayModal == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.modalOpenClip);
        howToPlayModal.SetActive(true);
        if (howToPlayCanvasGroup != null)
        {
            howToPlayCanvasGroup.alpha = 1;
            howToPlayCanvasGroup.blocksRaycasts = true;
        }
        howToPlayCard.localScale = Vector3.zero;
        howToPlayCard.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void CloseHowToPlay()
    {
        if (howToPlayModal == null) return;
        AudioManager.Instance?.PlayUIClickSound();
        howToPlayCard.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() => {
            if (howToPlayCanvasGroup != null)
            {
                howToPlayCanvasGroup.blocksRaycasts = false;
            }
            howToPlayModal.SetActive(false);
            howToPlayCard.localScale = Vector3.one;
        });
    }

    public void ToggleSFX()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.toggleClip);
        AudioManager.Instance?.ToggleSFX();
        PunchToggleUI("SfxToggleBtn");
        UpdateSettingsUI();
    }

    public void ToggleMusic()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.toggleClip);
        AudioManager.Instance?.ToggleMusic();
        PunchToggleUI("MusicToggleBtn");
        UpdateSettingsUI();
    }

    public void ToggleHaptics()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.toggleClip);
        AudioManager.Instance?.ToggleHaptics();
        PunchToggleUI("HapticsToggleBtn");
        UpdateSettingsUI();
    }

    private void PunchToggleUI(string name)
    {
        if (settingsCard == null) return;
        var btn = settingsCard.Find(name);
        if (btn != null)
        {
            btn.DOPunchScale(Vector3.one * buttonPunchScale, buttonPunchDuration);
        }
    }

    private void UpdateSettingsUI()
    {
        if (settingsModal == null) return;
        var am = AudioManager.Instance;
        if (am == null) return;

        UpdateToggleVisuals("SfxToggleBtn", "SFX: ON", "SFX: OFF", am.IsSFXEnabled);
        UpdateToggleVisuals("MusicToggleBtn", "MUSIC: ON", "MUSIC: OFF", am.IsMusicEnabled);
        UpdateToggleVisuals("HapticsToggleBtn", "VIBRATION: ON", "VIBRATION: OFF", am.IsHapticsEnabled);
    }

    private void UpdateToggleVisuals(string path, string textOn, string textOff, bool isOn)
    {
        if (settingsCard == null) return;
        var btn = settingsCard.Find(path);
        if (btn == null) return;

        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = isOn ? textOn : textOff;
            ColorUtility.TryParseHtmlString(isOn ? "#10B981" : "#94A3B8", out Color col); // Jade / Slate
            tmp.color = col;
        }
    }

    private void OnDestroy()
    {
        bannerPunchTween?.Kill();
        gameOverFadeTween?.Kill();
        gameOverScaleTween?.Kill();
        dotPulseTween?.Kill();
    }
}
