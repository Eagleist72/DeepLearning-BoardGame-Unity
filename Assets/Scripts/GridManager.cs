using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("References")]
    public GameSettings gameSettings;

    // 2D Array to hold references to the instantiated physical tiles
    private GameObject[,] tileGrid;

    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        int size = gameSettings.boardSize;
        float offset = gameSettings.tileOffset;

        tileGrid = new GameObject[size, size];

        // Calculate starting offsets to perfectly center the grid at world origin (0,0,0)
        float startX = -(size / 2f) * offset + (offset / 2f);
        float startZ = -(size / 2f) * offset + (offset / 2f);

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                // X represents columns (width), Z represents rows (depth)
                Vector3 spawnPos = new Vector3(startX + (c * offset), 0f, startZ + (r * offset));

                // Fetch tile from the centralized Object Pool instead of Instantiating
                GameObject tile = ObjectPooler.Instance.GetObject("Tile", spawnPos, Quaternion.identity);

                if (tile != null)
                {
                    tile.transform.SetParent(this.transform);
                    tile.name = $"Tile_{r}_{c}";
                    tileGrid[r, c] = tile;
                }
                else
                {
                    Debug.LogError("[GridManager] Failed to fetch 'Tile' from ObjectPooler. Check pool tags.");
                }
            }
        }
    }

    /// <summary>
    /// Returns the physical tile GameObject at the specified grid coordinates.
    /// </summary>
    public GameObject GetTileAt(int row, int col)
    {
        if (row >= 0 && row < gameSettings.boardSize && col >= 0 && col < gameSettings.boardSize)
        {
            return tileGrid[row, col];
        }

        Debug.LogWarning($"[GridManager] Attempted to access out-of-bounds tile at ({row}, {col}).");
        return null;
    }
}
