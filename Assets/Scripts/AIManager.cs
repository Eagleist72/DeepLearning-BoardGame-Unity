using System.Collections;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;
using Random = UnityEngine.Random;

public class AIManager : MonoBehaviour
{
    [Header("AI Configuration")]
    [Tooltip("Drag and drop the ai_brain.onnx model here")]
    [SerializeField] private ModelAsset onnxModelAsset;

    private Model runtimeModel;
    private Worker worker;
    private bool isThinking = false;

    // ── Difficulty Constants ──────────────────────────────────
    // EASY: 45% chance of a completely random adjacent move
    private const float EasyRandomChance = 0.45f;
    // MEDIUM: Temperature for softmax sampling (higher = more exploratory)
    private const float MediumTemperature = 1.2f;
    // MEDIUM: Pool size for temperature sampling (top-N moves)
    private const int MediumPoolSize = 5;

    private void Start()
    {
        runtimeModel = ModelLoader.Load(onnxModelAsset);
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

        // ── 1. Trigger UI: "Bot thinking..." + pulsing dot ──
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnStatus(GameState.AITurn);
        }

        // ── 2. Humanized thinking delay (non-blocking coroutine yield) ──
        yield return new WaitForSeconds(Random.Range(0.45f, 0.75f));

        // ── 3. Enumerate all valid (move, remove) combinations ──
        List<MoveData> validMoves = GetAllValidCombinations(1);

        if (validMoves.Count == 0)
        {
            isThinking = false;
            yield break;
        }

        // ── 4. Score each move via Sentis model inference ──
        AIDifficulty diff = GameManager.ActiveDifficulty;
        List<KeyValuePair<MoveData, float>> scoredMoves = new List<KeyValuePair<MoveData, float>>();

        foreach (MoveData move in validMoves)
        {
            int[,] simulatedBoard = SimulateMove(GameManager.Instance.GetBoardCopy(), 1, move.movePos, move.removePos);
            float[] flatBoard = FlattenBoard(simulatedBoard);

            // GC-safe tensor lifecycle: explicit using blocks with scoped disposal
            using (var inputTensor = new Tensor<float>(new TensorShape(1, 49), flatBoard))
            {
                worker.Schedule(inputTensor);

                // DownloadToArray() synchronously copies output data to a managed float[]
                using (var outputTensor = worker.PeekOutput() as Tensor<float>)
                {
                    float[] outputData = outputTensor.DownloadToArray();
                    float score = outputData[0];
                    scoredMoves.Add(new KeyValuePair<MoveData, float>(move, score));
                }
            }
        }

        // Sort descending by model score
        scoredMoves.Sort((a, b) => b.Value.CompareTo(a.Value));

        // ── 5. Select move based on difficulty ──
        MoveData selectedMove = SelectMove(diff, validMoves, scoredMoves);

        // ── 6. Execute the move/hop animation ──
        GameManager.Instance.ExecuteAIMoveStep(selectedMove.movePos);
        
        // Wait for hop animation to complete
        yield return new WaitForSeconds(GameManager.Instance.gameSettings.jumpDuration);

        // ── 7. Tile Collapse Phase (Strategic Delay) ──
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetTurnTextOverride("Choosing tile...");
        }

        yield return new WaitForSeconds(Random.Range(0.35f, 0.55f));

        // ── 8. Execute tile collapse ──
        GameManager.Instance.ExecuteAIRemoveStep(selectedMove.removePos);
    }

    /// <summary>
    /// Selects a move based on the current difficulty tier.
    /// EASY:   45% random adjacent move, 55% pick from top-2 scored moves.
    /// MEDIUM: Temperature sampling (T=1.2) over top-5 scored moves.
    /// HARD:   Strict argmax — always picks the highest-scored move.
    /// </summary>
    private MoveData SelectMove(AIDifficulty diff, List<MoveData> validMoves, List<KeyValuePair<MoveData, float>> scoredMoves)
    {
        switch (diff)
        {
            case AIDifficulty.Easy:
                return SelectEasyMove(validMoves, scoredMoves);

            case AIDifficulty.Medium:
                return SelectMediumMove(scoredMoves);

            case AIDifficulty.Hard:
            default:
                // Argmax: the list is already sorted descending, index 0 is best
                return scoredMoves[0].Key;
        }
    }

    /// <summary>
    /// EASY: 45% chance of a completely random valid move (forgiving mistakes).
    ///       55% chance of picking randomly from the top-2 highest-probability moves.
    /// </summary>
    private MoveData SelectEasyMove(List<MoveData> validMoves, List<KeyValuePair<MoveData, float>> scoredMoves)
    {
        if (Random.value < EasyRandomChance)
        {
            // Pure random: any valid (move, remove) combination
            return validMoves[Random.Range(0, validMoves.Count)];
        }
        else
        {
            // Pick randomly from the top 2 scored moves
            int topN = Mathf.Min(2, scoredMoves.Count);
            return scoredMoves[Random.Range(0, topN)].Key;
        }
    }

    /// <summary>
    /// MEDIUM: Temperature-scaled softmax sampling over the top-5 moves.
    /// T=1.2 produces balanced, competitive yet beatable play.
    /// </summary>
    private MoveData SelectMediumMove(List<KeyValuePair<MoveData, float>> scoredMoves)
    {
        int poolSize = Mathf.Min(MediumPoolSize, scoredMoves.Count);
        float sumExp = 0f;
        float[] expScores = new float[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            float exp = Mathf.Exp(scoredMoves[i].Value / MediumTemperature);
            expScores[i] = exp;
            sumExp += exp;
        }

        float rand = Random.value * sumExp;
        float cumulative = 0f;

        for (int i = 0; i < poolSize; i++)
        {
            cumulative += expScores[i];
            if (rand <= cumulative)
            {
                return scoredMoves[i].Key;
            }
        }

        // Fallback (floating-point edge case)
        return scoredMoves[0].Key;
    }

    // ── Board Evaluation Helpers ──────────────────────────────

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
