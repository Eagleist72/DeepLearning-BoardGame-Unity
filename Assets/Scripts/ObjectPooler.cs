using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        [Tooltip("Tag to identify the pool (e.g., 'Tile', 'Piece', 'VFX')")]
        public string poolTag;
        public GameObject prefab;
    }

    [Header("References")]
    public GameSettings gameSettings;
    public List<Pool> pools;

    private Dictionary<string, Queue<GameObject>> poolDictionary;

    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializePools();
    }

    private void InitializePools()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();

            // Fetch the pre-allocated size dynamically from GameSettings
            int poolSize = GetInitialSizeFromSettings(pool.poolTag);

            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(pool.prefab, transform);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }

            poolDictionary.Add(pool.poolTag, objectPool);
        }
    }

    private int GetInitialSizeFromSettings(string tag)
    {
        // Matches the tags defined in the Inspector to the ScriptableObject limits
        switch (tag)
        {
            case "Tile": return gameSettings.initialTilePoolSize;
            case "Piece": return gameSettings.initialPiecePoolSize;
            case "VFX": return gameSettings.initialVfxPoolSize;
            default: return 5; // Fallback capacity
        }
    }

    public GameObject GetObject(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"[ObjectPooler] Pool with tag '{tag}' does not exist.");
            return null;
        }

        GameObject obj;

        // Check if we have available objects, otherwise expand the pool (Fail-safe)
        if (poolDictionary[tag].Count > 0)
        {
            obj = poolDictionary[tag].Dequeue();
        }
        else
        {
            Debug.LogWarning($"[ObjectPooler] Pool '{tag}' exhausted. Instantiating a new one (Watch for GC spikes!).");
            Pool poolInfo = pools.Find(p => p.poolTag == tag);
            obj = Instantiate(poolInfo.prefab, transform);
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        return obj;
    }

    public void ReturnObject(string tag, GameObject obj)
    {
        obj.SetActive(false);

        if (poolDictionary.ContainsKey(tag))
        {
            poolDictionary[tag].Enqueue(obj);
        }
    }
}
