using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // Yeni Input System kütüphanesi eklendi
using DG.Tweening;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public LayerMask tileLayer;
    private Vector2Int selectedMovePos;

    private void Update()
    {
        if (GameManager.Instance.currentState != GameState.PlayerMovePhase &&
            GameManager.Instance.currentState != GameState.PlayerRemovePhase)
        {
            return;
        }

        bool inputDetected = false;
        Vector2 screenPosition = Vector2.zero;

        // 1. Fare (Mouse) kontrolü
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            inputDetected = true;
            screenPosition = Mouse.current.position.ReadValue();
        }
        // 2. Mobil Dokunmatik (Touch) kontrolü
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            inputDetected = true;
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (inputDetected)
        {
            HandleInteraction(screenPosition);
        }
    }

    private void HandleInteraction(Vector2 screenPos)
    {
        // Iþýný artýk doðrudan ekrandan aldýðýmýz Vector2 koordinatýndan gönderiyoruz
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

            // Changed to read from GameManager dynamically
            GameManager.Instance.playerPieceTransform.DOMove(targetWorldPos, moveDuration).SetEase(Ease.InOutQuad);

            GameManager.Instance.currentState = GameState.PlayerRemovePhase;
        }
    }

    private void TryRemoveTile(Vector2Int targetPos)
    {
        bool isEmpty = GameManager.Instance[targetPos.x, targetPos.y] == 0;
        bool isNewPieceLocation = (targetPos == selectedMovePos);

        if (isEmpty && !isNewPieceLocation)
        {
            GameObject tileToRemove = GridManager.Instance.GetTileAt(targetPos.x, targetPos.y);
            float fadeDuration = GameManager.Instance.gameSettings.tileFadeDuration;

            tileToRemove.transform.DOScale(Vector3.zero, fadeDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    ObjectPooler.Instance.ReturnObject("Tile", tileToRemove);
                });

            GameManager.Instance.ExecuteTurn(2, selectedMovePos, targetPos);
        }
    }
}
