using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Sentis;

public class AIManager : MonoBehaviour
{
    [Header("Sentis AI Configuration")]
    [Tooltip("Drag and drop the ai_brain.onnx model here")]
    public ModelAsset onnxModelAsset;

    private Model runtimeModel;
    private IWorker worker;
    private bool isThinking = false;

    private void Start()
    {
        // 1. Load the ONNX model into memory
        runtimeModel = ModelLoader.Load(onnxModelAsset);

        // 2. Initialize the Sentis worker. GPUCompute provides excellent mobile performance.
        worker = WorkerFactory.CreateWorker(BackendType.GPUCompute, runtimeModel);
    }

    private void Update()
    {
        // Trigger AI logic only when it's AI's turn and it is not already calculating
        if (GameManager.Instance.currentState == GameState.AITurn && !isThinking)
        {
            StartCoroutine(ThinkAndPlay());
        }
    }

    private IEnumerator ThinkAndPlay()
    {
        isThinking = true;

        // Artificial delay for better UX (prevent instant moves)
        yield return new WaitForSeconds(GameManager.Instance.gameSettings.aiThinkingDelay);

        List<MoveData> validMoves = GetAllValidCombinations(1); // 1 represents the AI ID

        if (validMoves.Count == 0)
        {
            isThinking = false;
            yield break; // Handled by GameManager (Loss condition)
        }

        float bestScore = -Mathf.Infinity;
        MoveData bestMove = validMoves[0];

        // Evaluate all possible moves using the Neural Network
        foreach (MoveData move in validMoves)
        {
            int[,] simulatedBoard = SimulateMove(GameManager.Instance.board, 1, move.movePos, move.removePos);
            float[] flatBoard = FlattenBoard(simulatedBoard);

            // 'using' block ensures Tensors are disposed immediately to prevent memory leaks
            using TensorFloat inputTensor = new TensorFloat(new TensorShape(1, 49), flatBoard);

            worker.Execute(inputTensor);

            using TensorFloat outputTensor = worker.PeekOutput() as TensorFloat;

            // Extract the prediction score from the Sentis tensor array
            float[] outputData = outputTensor.ToReadOnlyArray();
            float score = outputData[0];

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        // Execute the chosen optimal move
        GameManager.Instance.ExecuteTurn(1, bestMove.movePos, bestMove.removePos);

        isThinking = false;
    }

    // --- HELPER STRUCTS & METHODS ---

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
                    bool isEmpty = GameManager.Instance.board[r, c] == 0;
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
        // Sentis Requirement: Memory must be explicitly released when the script dies
        worker?.Dispose();
    }
}
