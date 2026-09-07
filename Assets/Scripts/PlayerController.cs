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

        if (currentState != lastObservedState)
        {
            OnPhaseChanged(lastObservedState, currentState);
            lastObservedState = currentState;
        }

        if (currentState != GameState.PlayerMovePhase &&
            currentState != GameState.PlayerRemovePhase &&
            currentState != GameState.Player2MovePhase &&
            currentState != GameState.Player2RemovePhase)
        {
            return;
        }

        bool inputDetected = false;
        Vector2 screenPosition = Vector2.zero;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            inputDetected = true;
            screenPosition = Mouse.current.position.ReadValue();
        }
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

    private void OnPhaseChanged(GameState previousState, GameState newState)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnStatus(newState);
        }

        switch (newState)
        {
            case GameState.PlayerMovePhase:
            case GameState.Player2MovePhase:
                ShowMoveHighlights(newState);
                break;

            case GameState.PlayerRemovePhase:
            case GameState.Player2RemovePhase:
                break;

            case GameState.AITurn:
            case GameState.GameOver:
            case GameState.MainMenu:
                GridManager.Instance.ClearAllHighlights();
                break;
        }
    }

    private void ShowMoveHighlights(GameState state)
    {
        Vector2Int pos = (state == GameState.PlayerMovePhase) ? GameManager.Instance.playerPos : GameManager.Instance.aiPos;
        List<Vector2Int> validMoves = GameManager.Instance.GetNeighbors(pos);
        GridManager.Instance.HighlightTiles(validMoves, true);
    }

    private void ShowRemoveHighlights()
    {
        GameSettings settings = GameManager.Instance.gameSettings;
        int size = settings.boardSize;
        List<Vector2Int> removable = new List<Vector2Int>();

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
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
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, tileLayer))
        {
            string[] nameParts = hit.collider.name.Split('_');
            if (nameParts.Length == 3)
            {
                int r = int.Parse(nameParts[1]);
                int c = int.Parse(nameParts[2]);
                Vector2Int clickedPos = new Vector2Int(r, c);

                GameState state = GameManager.Instance.currentState;
                if (state == GameState.PlayerMovePhase || state == GameState.Player2MovePhase)
                {
                    TryMovePiece(clickedPos, state);
                }
                else if (state == GameState.PlayerRemovePhase || state == GameState.Player2RemovePhase)
                {
                    TryRemoveTile(clickedPos, state);
                }
            }
        }
    }

    private void TryMovePiece(Vector2Int targetPos, GameState state)
    {
        int playerId = (state == GameState.PlayerMovePhase) ? 2 : 1;
        Vector2Int currentPos = (playerId == 2) ? GameManager.Instance.playerPos : GameManager.Instance.aiPos;
        List<Vector2Int> validMoves = GameManager.Instance.GetNeighbors(currentPos);

        if (validMoves.Contains(targetPos))
        {
            selectedMovePos = targetPos;
            GameObject targetTile = GridManager.Instance.GetTileAt(targetPos.x, targetPos.y);

            float moveDuration = GameManager.Instance.gameSettings.pieceMoveDuration;
            Vector3 targetWorldPos = targetTile.transform.position + Vector3.up * 0.5f;

            AudioManager.Instance?.PlayMoveSound();
            GameManager.Instance.isExecutingTurn = true; 
            
            Transform pieceToMove = (playerId == 2) ? GameManager.Instance.playerPieceTransform : GameManager.Instance.aiPieceTransform;

            pieceToMove.DOMove(targetWorldPos, moveDuration).SetEase(Ease.InOutQuad).OnComplete(() =>
            {
                GridManager.Instance.ClearAllHighlights();
                GameManager.Instance.currentState = (playerId == 2) ? GameState.PlayerRemovePhase : GameState.Player2RemovePhase;
                ShowRemoveHighlights();
                GameManager.Instance.isExecutingTurn = false;
            });
        }
    }

    private void TryRemoveTile(Vector2Int targetPos, GameState state)
    {
        bool isEmpty = GameManager.Instance[targetPos.x, targetPos.y] == 0;
        bool isNewPieceLocation = (targetPos == selectedMovePos);

        if (isEmpty && !isNewPieceLocation)
        {
            GridManager.Instance.ClearAllHighlights();
            int playerId = (state == GameState.PlayerRemovePhase) ? 2 : 1;
            GameManager.Instance.ExecuteTurn(playerId, selectedMovePos, targetPos);
        }
    }
}
