using System.Collections;
using System.Collections.Generic;
using Unity.InferenceEngine; // Güncellenmiþ kütüphane adý
using UnityEngine;

public class AIManager : MonoBehaviour
{
    [Header("AI Configuration")]
    [Tooltip("Drag and drop the ai_brain.onnx model here")]
    public ModelAsset onnxModelAsset;

    private Model runtimeModel;
    private Worker worker; // IWorker yerine Worker sýnýfý
    private bool isThinking = false;

    private void Start()
    {
        runtimeModel = ModelLoader.Load(onnxModelAsset);
        // WorkerFactory yerine doðrudan Worker objesi oluþturuyoruz
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
    }

    private void Update()
    {
        if (GameManager.Instance.currentState == GameState.AITurn && !isThinking)
        {
            StartCoroutine(ThinkAndPlay());
        }
    }

    private IEnumerator ThinkAndPlay()
    {
        isThinking = true;
        yield return new WaitForSeconds(GameManager.Instance.gameSettings.aiThinkingDelay);

        List<MoveData> validMoves = GetAllValidCombinations(1);

        if (validMoves.Count == 0)
        {
            isThinking = false;
            yield break;
        }

        float bestScore = -Mathf.Infinity;
        MoveData bestMove = validMoves[0];

        foreach (MoveData move in validMoves)
        {
            int[,] simulatedBoard = SimulateMove(GameManager.Instance.GetBoardCopy(), 1, move.movePos, move.removePos);
            float[] flatBoard = FlattenBoard(simulatedBoard);

            // TensorFloat yerine Tensor<float> kullanýyoruz
            using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 49), flatBoard);

            // Execute yerine Schedule
            worker.Schedule(inputTensor);

            using Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;

            // Veriyi okumak için DownloadToArray kullanýlýyor
            float[] outputData = outputTensor.DownloadToArray();
            float score = outputData[0];

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        GameManager.Instance.ExecuteTurn(1, bestMove.movePos, bestMove.removePos);
        isThinking = false;
    }

    private struct MoveData
    {
        public Vector2Int movePos;
        public Vector2Int removePos;
    }

    private List<MoveData> GetAllValidCombinations(int playerId)
    {
        List<MoveData> combinations = new List<MoveData>();
        Vector2Int currentPos = (playerId == 1) ? GameManager.Instance.aiPos : GameManager.Instance.playerPos;

        List<Vector2Int> possibleMoves = GameManager.Instance.GetNeighbors(currentPos);
        int size = GameManager.Instance.gameSettings.boardSize;

        foreach (Vector2Int movePos in possibleMoves)
        {
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    bool isEmpty = GameManager.Instance[r, c] == 0;
                    bool isOldSpot = (r == currentPos.x && c == currentPos.y);
                    bool isNewSpot = (r == movePos.x && c == movePos.y);

                    if ((isEmpty || isOldSpot) && !isNewSpot)
                    {
                        combinations.Add(new MoveData { movePos = movePos, removePos = new Vector2Int(r, c) });
                    }
                }
            }
        }
        return combinations;
    }

    private int[,] SimulateMove(int[,] originalBoard, int playerId, Vector2Int movePos, Vector2Int removePos)
    {
        int size = GameManager.Instance.gameSettings.boardSize;
        int[,] clone = new int[size, size];
        Vector2Int oldPos = (playerId == 1) ? GameManager.Instance.aiPos : GameManager.Instance.playerPos;

        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                clone[r, c] = originalBoard[r, c];

        clone[oldPos.x, oldPos.y] = 0;
        clone[movePos.x, movePos.y] = playerId;
        clone[removePos.x, removePos.y] = -1;

        return clone;
    }

    private float[] FlattenBoard(int[,] board)
    {
        int size = GameManager.Instance.gameSettings.boardSize;
        float[] flat = new float[size * size];
        int index = 0;

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                flat[index] = board[r, c];
                index++;
            }
        }
        return flat;
    }

    private void OnDestroy()
    {
        worker?.Dispose();
    }
}