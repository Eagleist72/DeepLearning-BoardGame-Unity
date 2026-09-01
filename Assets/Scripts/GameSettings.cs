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

    [Header("Colors & Visuals")]
    public Color playerColor = new Color(0.9f, 0.2f, 0.2f); // Player (Red)
    public Color aiColor = new Color(0.2f, 0.5f, 0.9f); // AI (Blue)
    public Color highlightColor = new Color(0.9f, 0.8f, 0.2f); // Valid move indicator
    public Color removedTileColor = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Removed tile state
}
