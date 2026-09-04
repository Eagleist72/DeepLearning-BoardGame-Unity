using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("References")]
    public GameSettings gameSettings;

    // 2D Array to hold references to the instantiated physical tiles
    private GameObject[,] tileGrid;

    // Parallel 2D Array caching the TileVisual component on each tile (avoids GetComponent at runtime)
    private TileVisual[,] tileVisualGrid;

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
        tileVisualGrid = new TileVisual[size, size];

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

                    // Initialize the TileVisual component with shared settings and reset its visual state
                    TileVisual visual = tile.GetComponent<TileVisual>();
                    if (visual != null)
                    {
                        visual.Initialize(gameSettings);
                        tileVisualGrid[r, c] = visual;
                    }
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

    /// <summary>
    /// Returns the cached TileVisual component at the specified grid coordinates.
    /// </summary>
    public TileVisual GetTileVisualAt(int row, int col)
    {
        if (row >= 0 && row < gameSettings.boardSize && col >= 0 && col < gameSettings.boardSize)
        {
            return tileVisualGrid[row, col];
        }

        Debug.LogWarning($"[GridManager] Attempted to access out-of-bounds TileVisual at ({row}, {col}).");
        return null;
    }

    /// <summary>
    /// Sets the highlight state for a list of tile positions.
    /// </summary>
    /// <param name="positions">Board coordinates to toggle.</param>
    /// <param name="highlight">True to highlight, false to unhighlight.</param>
    public void HighlightTiles(List<Vector2Int> positions, bool highlight)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            TileVisual visual = GetTileVisualAt(positions[i].x, positions[i].y);
            if (visual != null)
            {
                visual.SetHighlight(highlight);
            }
        }
    }

    /// <summary>
    /// Clears all highlights across the entire board by resetting every active tile.
    /// </summary>
    public void ClearAllHighlights()
    {
        int size = gameSettings.boardSize;
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                TileVisual visual = tileVisualGrid[r, c];
                if (visual != null)
                {
                    visual.SetHighlight(false);
                }
            }
        }
    }
}
