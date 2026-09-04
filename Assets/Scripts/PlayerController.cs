using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public LayerMask tileLayer;
    private Vector2Int selectedMovePos;

    // Tracks the previous game state to detect phase transitions
    private GameState lastObservedState;

    private void Update()
    {
        GameState currentState = GameManager.Instance.currentState;

        // Detect phase transitions and update highlights accordingly
        if (currentState != lastObservedState)
        {
            OnPhaseChanged(lastObservedState, currentState);
            lastObservedState = currentState;
        }

        if (currentState != GameState.PlayerMovePhase &&
            currentState != GameState.PlayerRemovePhase)
        {
            return;
        }

        bool inputDetected = false;
        Vector2 screenPosition = Vector2.zero;

        // 1. Mouse input check
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            inputDetected = true;
            screenPosition = Mouse.current.position.ReadValue();
        }
        // 2. Mobile touch input check
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            inputDetected = true;
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (inputDetected && !GameManager.Instance.isExecutingTurn)
        {
            HandleInteraction(screenPosition);
        }
    }

    /// <summary>
    /// Reacts to game phase transitions by updating tile highlights to guide the player.
    /// </summary>
    private void OnPhaseChanged(GameState previousState, GameState newState)
    {
        // Update the turn status banner on every phase transition
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnStatus(newState);
        }

        switch (newState)
        {
            case GameState.PlayerMovePhase:
                ShowMoveHighlights();
                break;

            case GameState.PlayerRemovePhase:
                // Remove phase highlights are applied in TryMovePiece after the move is committed
                break;

            case GameState.AITurn:
            case GameState.GameOver:
                // Clean up all player-facing highlights when control leaves the player
                GridManager.Instance.ClearAllHighlights();
                break;
        }
    }

    /// <summary>
    /// Highlights all valid neighbor tiles the player can move to.
    /// </summary>
    private void ShowMoveHighlights()
    {
        List<Vector2Int> validMoves = GameManager.Instance.GetNeighbors(GameManager.Instance.playerPos);
        GridManager.Instance.HighlightTiles(validMoves, true);
    }

    /// <summary>
    /// Highlights all empty tiles eligible for removal (excludes the newly occupied tile).
    /// </summary>
    private void ShowRemoveHighlights()
    {
        GameSettings settings = GameManager.Instance.gameSettings;
        int size = settings.boardSize;
        List<Vector2Int> removable = new List<Vector2Int>();

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                // Board value 0 = empty tile; exclude the tile the player just moved to
                if (GameManager.Instance[r, c] == 0)
                {
                    Vector2Int pos = new Vector2Int(r, c);
                    if (pos != selectedMovePos)
                    {
                        removable.Add(pos);
                    }
                }
            }
        }

        GridManager.Instance.HighlightTiles(removable, true);
    }

    private void HandleInteraction(Vector2 screenPos)
    {
        // Cast a ray from the screen position through the camera into the scene
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, tileLayer))
        {
            string[] nameParts = hit.collider.name.Split('_');
            if (nameParts.Length == 3)
            {
                int r = int.Parse(nameParts[1]);
                int c = int.Parse(nameParts[2]);
                Vector2Int clickedPos = new Vector2Int(r, c);

                if (GameManager.Instance.currentState == GameState.PlayerMovePhase)
                {
                    TryMovePiece(clickedPos);
                }
                else if (GameManager.Instance.currentState == GameState.PlayerRemovePhase)
                {
                    TryRemoveTile(clickedPos);
                }
            }
        }
    }

    private void TryMovePiece(Vector2Int targetPos)
    {
        List<Vector2Int> validMoves = GameManager.Instance.GetNeighbors(GameManager.Instance.playerPos);

        if (validMoves.Contains(targetPos))
        {
            selectedMovePos = targetPos;
            GameObject targetTile = GridManager.Instance.GetTileAt(targetPos.x, targetPos.y);

            float moveDuration = GameManager.Instance.gameSettings.pieceMoveDuration;
            Vector3 targetWorldPos = targetTile.transform.position + Vector3.up * 0.5f;

            AudioManager.Instance?.PlayMoveSound();
            GameManager.Instance.isExecutingTurn = true; // Block input during move
            
            GameManager.Instance.playerPieceTransform.DOMove(targetWorldPos, moveDuration).SetEase(Ease.InOutQuad).OnComplete(() =>
            {
                // Clear move-phase highlights, then show removal-phase highlights
                GridManager.Instance.ClearAllHighlights();
                GameManager.Instance.currentState = GameState.PlayerRemovePhase;
                ShowRemoveHighlights();
                GameManager.Instance.isExecutingTurn = false; // Allow input again
            });
        }
    }

    private void TryRemoveTile(Vector2Int targetPos)
    {
        bool isEmpty = GameManager.Instance[targetPos.x, targetPos.y] == 0;
        bool isNewPieceLocation = (targetPos == selectedMovePos);

        if (isEmpty && !isNewPieceLocation)
        {
            // Clear all removal highlights before delegating to GameManager
            GridManager.Instance.ClearAllHighlights();

            // Delegate to GameManager which now handles the animation and state change
            GameManager.Instance.ExecuteTurn(2, selectedMovePos, targetPos);
        }
    }
}
