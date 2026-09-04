using UnityEngine;

[CreateAssetMenu(fileName = "NewGameSettings", menuName = "NeuralGrid/GameSettings")]
public class GameSettings : ScriptableObject
{
    [Header("Board Configuration")]
    [Tooltip("Size of the game board (Default: 7)")]
    public int boardSize = 7;

    [Tooltip("World space offset distance between tiles")]
    public float tileOffset = 1.1f;

    [Header("Animation & Timing (DOTween)")]
    public float pieceMoveDuration = 0.4f;
    public float tileFadeDuration = 0.3f;

    [Tooltip("Artificial delay for AI decision making to prevent abrupt turns")]
    public float aiThinkingDelay = 0.5f;

    [Header("Object Pooling Limits")]
    [Tooltip("Pre-allocated object counts at runtime start to prevent GC spikes")]
    public int initialTilePoolSize = 49;
    public int initialPiecePoolSize = 10;
    public int initialVfxPoolSize = 5;
    public int initialAudioSourcePoolSize = 10;

    [Header("Colors & Visuals")]
    public Color defaultTileColor = Color.white; // Default tile rest-state color
    public Color playerColor = new Color(0.9f, 0.2f, 0.2f); // Player (Red)
    public Color aiColor = new Color(0.2f, 0.5f, 0.9f); // AI (Blue)
    public Color highlightColor = new Color(0.9f, 0.8f, 0.2f); // Valid move indicator
    public Color removedTileColor = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Removed tile state

    [Header("Audio Configuration")]
    [Tooltip("Sound played when a piece moves to a new tile")]
    public AudioClip moveClip;

    [Tooltip("Sound played when a tile is removed from the board")]
    public AudioClip tileRemoveClip;

    [Tooltip("Sound played on UI button presses and valid selections")]
    public AudioClip uiClickClip;

    [Tooltip("Sound played when the player wins the game")]
    public AudioClip victoryClip;

    [Tooltip("Sound played when the player loses the game")]
    public AudioClip defeatClip;

    [Range(0f, 1f)]
    [Tooltip("Global master volume multiplier applied to all audio")]
    public float masterVolume = 1f;

    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for sound effects")]
    public float sfxVolume = 0.8f;
}
