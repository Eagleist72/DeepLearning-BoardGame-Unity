using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public enum GameState { PlayerMovePhase, PlayerRemovePhase, AITurn, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public GameSettings gameSettings;

    [Header("Physical Pieces")]
    public Transform playerPieceTransform;
    public Transform aiPieceTransform;

    [Header("Game State")]
    public GameState currentState;
    public bool isExecutingTurn;

    // Made private to fix UAC1009 serialization warning
    private int[,] board;

    public Vector2Int aiPos = new Vector2Int(0, 3);
    public Vector2Int playerPos = new Vector2Int(6, 3);

    // C# Indexer to allow safe external read/write access without serialization issues
    public int this[int r, int c]
    {
        get => board[r, c];
        set => board[r, c] = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private IEnumerator Start()
    {
        InitializeBoard();
        yield return null;
        SpawnPhysicalPieces();
        currentState = GameState.AITurn;

        // Notify the UI of the initial game state
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnStatus(currentState);
        }
    }

    private void InitializeBoard()
    {
        int size = gameSettings.boardSize;
        board = new int[size, size];

        board[aiPos.x, aiPos.y] = 1;
        board[playerPos.x, playerPos.y] = 2;
    }

    public int[,] GetBoardCopy()
    {
        int size = gameSettings.boardSize;
        int[,] clone = new int[size, size];
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                clone[r, c] = board[r, c];
        return clone;
    }

    private void SpawnPhysicalPieces()
    {
        float yOffset = 0.5f;

        Vector3 pSpawnPos = GridManager.Instance.GetTileAt(playerPos.x, playerPos.y).transform.position + Vector3.up * yOffset;
        GameObject pObj = ObjectPooler.Instance.GetObject("Piece", pSpawnPos, Quaternion.identity);
        pObj.GetComponent<Renderer>().material.color = gameSettings.playerColor;
        playerPieceTransform = pObj.transform;

        Vector3 aSpawnPos = GridManager.Instance.GetTileAt(aiPos.x, aiPos.y).transform.position + Vector3.up * yOffset;
        GameObject aObj = ObjectPooler.Instance.GetObject("Piece", aSpawnPos, Quaternion.identity);
        aObj.GetComponent<Renderer>().material.color = gameSettings.aiColor;
        aiPieceTransform = aObj.transform;
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
        isExecutingTurn = true;
        Vector2Int currentPos = (playerId == 1) ? aiPos : playerPos;

        board[currentPos.x, currentPos.y] = 0;
        board[movePos.x, movePos.y] = playerId;
        if (playerId == 1) aiPos = movePos;
        else playerPos = movePos;
        board[removePos.x, removePos.y] = -1;

        if (playerId == 1)
        {
            // Clear any leftover highlights before AI animation starts
            GridManager.Instance.ClearAllHighlights();

            GameObject targetTile = GridManager.Instance.GetTileAt(movePos.x, movePos.y);
            Vector3 targetWorldPos = targetTile.transform.position + Vector3.up * 0.5f;

            AudioManager.Instance?.PlayMoveSound();
            aiPieceTransform.DOMove(targetWorldPos, gameSettings.pieceMoveDuration).SetEase(Ease.InOutQuad).OnComplete(() =>
            {
                AnimateTileRemovalAndFinishTurn(removePos);
            });
        }
        else
        {
            // For the player, the piece movement is animated during PlayerMovePhase.
            // Here we only animate the tile removal to finish the turn.
            AnimateTileRemovalAndFinishTurn(removePos);
        }
    }

    private void AnimateTileRemovalAndFinishTurn(Vector2Int removePos)
    {
        GameObject tileToRemove = GridManager.Instance.GetTileAt(removePos.x, removePos.y);
        TileVisual removeVisual = GridManager.Instance.GetTileVisualAt(removePos.x, removePos.y);

        AudioManager.Instance?.PlayTileRemoveSound();
        VFXManager.Instance?.PlayTileDestroyEffect(tileToRemove.transform.position);
        CameraController.Instance?.ShakeTileDestroy();

        if (removeVisual != null)
        {
            removeVisual.PlayRemoveAnimation(() =>
            {
                ObjectPooler.Instance.ReturnObject("Tile", tileToRemove);
                CheckWinConditions();
                isExecutingTurn = false;
            });
        }
        else
        {
            // Fallback if TileVisual is somehow missing
            ObjectPooler.Instance.ReturnObject("Tile", tileToRemove);
            CheckWinConditions();
            isExecutingTurn = false;
        }
    }

    private void CheckWinConditions()
    {
        bool playerCanMove = GetNeighbors(playerPos).Count > 0;
        bool aiCanMove = GetNeighbors(aiPos).Count > 0;

        if (!playerCanMove)
        {
            Debug.Log("[GameManager] AI Wins! Player is trapped.");
            currentState = GameState.GameOver;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateTurnStatus(currentState);
                UIManager.Instance.ShowGameOver(false); // Player lost
            }
        }
        else if (!aiCanMove)
        {
            Debug.Log("[GameManager] Player Wins! AI is trapped.");
            currentState = GameState.GameOver;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateTurnStatus(currentState);
                UIManager.Instance.ShowGameOver(true); // Player won
            }
        }
        else
        {
            currentState = (currentState == GameState.AITurn) ? GameState.PlayerMovePhase : GameState.AITurn;

            // Notify UI of the new turn/phase
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateTurnStatus(currentState);
            }
        }
    }
}
