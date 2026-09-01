using System.Collections.Generic;
using UnityEngine;

public enum GameState { PlayerMovePhase, PlayerRemovePhase, AITurn, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public GameSettings gameSettings;

    [Header("Game State")]
    public GameState currentState;
    public int[,] board;
    public Vector2Int aiPos = new Vector2Int(0, 3);
    public Vector2Int playerPos = new Vector2Int(6, 3);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        InitializeBoard();
        // Python kodundaki gibi ilk hamleyi yapay zekaya veriyoruz
        currentState = GameState.AITurn;
    }

    private void InitializeBoard()
    {
        int size = gameSettings.boardSize;
        board = new int[size, size];

        // 0: Empty, 1: AI, 2: Player, -1: Removed
        board[aiPos.x, aiPos.y] = 1;
        board[playerPos.x, playerPos.y] = 2;
    }

    public bool IsWithinBounds(int r, int c)
    {
        return r >= 0 && r < gameSettings.boardSize && c >= 0 && c < gameSettings.boardSize;
    }

    public List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;

                int nr = pos.x + dr;
                int nc = pos.y + dc;

                if (IsWithinBounds(nr, nc) && board[nr, nc] == 0)
                {
                    neighbors.Add(new Vector2Int(nr, nc));
                }
            }
        }
        return neighbors;
    }

    public void ExecuteTurn(int playerId, Vector2Int movePos, Vector2Int removePos)
    {
        Vector2Int currentPos = (playerId == 1) ? aiPos : playerPos;

        // 1. Move Logic
        board[currentPos.x, currentPos.y] = 0;
        board[movePos.x, movePos.y] = playerId;

        if (playerId == 1) aiPos = movePos;
        else playerPos = movePos;

        // 2. Remove Logic
        board[removePos.x, removePos.y] = -1;

        // TODO: Trigger DOTween visual animations here in Phase 5

        CheckWinConditions();
    }

    private void CheckWinConditions()
    {
        bool playerCanMove = GetNeighbors(playerPos).Count > 0;
        bool aiCanMove = GetNeighbors(aiPos).Count > 0;

        if (!playerCanMove)
        {
            Debug.Log("[GameManager] AI Wins! Player is trapped.");
            currentState = GameState.GameOver;
        }
        else if (!aiCanMove)
        {
            Debug.Log("[GameManager] Player Wins! AI is trapped.");
            currentState = GameState.GameOver;
        }
        else
        {
            // Switch Turn
            currentState = (currentState == GameState.AITurn) ? GameState.PlayerMovePhase : GameState.AITurn;
        }
    }

}
