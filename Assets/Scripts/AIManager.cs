using System.Collections;
using System.Collections.Generic;
using Unity.InferenceEngine; // Updated library name
using UnityEngine;
using Random = UnityEngine.Random;

public class AIManager : MonoBehaviour
{
    [Header("AI Configuration")]
    [Tooltip("Drag and drop the ai_brain.onnx model here")]
    [SerializeField] private ModelAsset onnxModelAsset;

    [Header("AI Intelligence Tuning")]
    [Range(0f, 1f)] [Tooltip("Probability of random moves when in EASY mode (0 = smart, 1 = purely random)")]
    [SerializeField] private float easyRandomWeight = 0.5f;
    
    [Range(0.1f, 2.0f)] [Tooltip("Softmax temperature for MEDIUM mode (higher = more varied moves)")]
    [SerializeField] private float mediumTemperature = 0.5f;

    private Model runtimeModel;
    private Worker worker; // Using Worker instead of IWorker
    private bool isThinking = false;

    private void Start()
    {
        runtimeModel = ModelLoader.Load(onnxModelAsset);
        // Creating a Worker object directly instead of using WorkerFactory
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
    }

    private void Update()
    {
        if (GameManager.Instance.currentState != GameState.AITurn)
        {
            isThinking = false;
            return;
        }

        if (!isThinking)
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

        MoveData selectedMove = validMoves[0];
        AIDifficulty diff = GameManager.ActiveDifficulty;

        // EASY: chance of making a completely random valid move
        if (diff == AIDifficulty.Easy && Random.value < easyRandomWeight)
        {
            selectedMove = validMoves[Random.Range(0, validMoves.Count)];
        }
        else
        {
            List<KeyValuePair<MoveData, float>> scoredMoves = new List<KeyValuePair<MoveData, float>>();

            foreach (MoveData move in validMoves)
            {
                int[,] simulatedBoard = SimulateMove(GameManager.Instance.GetBoardCopy(), 1, move.movePos, move.removePos);
                float[] flatBoard = FlattenBoard(simulatedBoard);

                using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 49), flatBoard);
                worker.Schedule(inputTensor);

                using Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
                float[] outputData = outputTensor.DownloadToArray();
                float score = outputData[0];

                scoredMoves.Add(new KeyValuePair<MoveData, float>(move, score));
            }

            // Sort descending by score
            scoredMoves.Sort((a, b) => b.Value.CompareTo(a.Value));

            if (diff == AIDifficulty.Hard || diff == AIDifficulty.Easy)
            {
                // Hard mode (or the 50% 'smart' portion of Easy) picks the absolute best move.
                selectedMove = scoredMoves[0].Key;
            }
            else if (diff == AIDifficulty.Medium)
            {
                // Medium: Softmax temperature sampling over top moves to add variance without being completely dumb
                float temperature = mediumTemperature;
                float sumExp = 0f;
                
                // Limit to top 5 moves to ensure it's not completely random
                int poolSize = Mathf.Min(5, scoredMoves.Count);
                List<float> expScores = new List<float>();

                for (int i = 0; i < poolSize; i++)
                {
                    float exp = Mathf.Exp(scoredMoves[i].Value / temperature);
                    expScores.Add(exp);
                    sumExp += exp;
                }

                float rand = Random.value * sumExp;
                float cumulative = 0f;
                selectedMove = scoredMoves[0].Key; // fallback

                for (int i = 0; i < poolSize; i++)
                {
                    cumulative += expScores[i];
                    if (rand <= cumulative)
                    {
                        selectedMove = scoredMoves[i].Key;
                        break;
                    }
                }
            }
        }

        GameManager.Instance.ExecuteTurn(1, selectedMove.movePos, selectedMove.removePos);
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
