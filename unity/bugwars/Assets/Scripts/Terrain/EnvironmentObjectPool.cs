using System.Collections.Generic;
using UnityEngine;

namespace BugWars.Terrain
{
    /// <summary>
    /// Represents a potential environment object spawn point (data-only, not yet instantiated)
    /// </summary>
    public class EnvironmentSpawnData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public EnvironmentAsset asset;
        public Vector2Int chunkCoord;
        public GameObject activeInstance; // null if not currently spawned
        public bool isHarvested; // true if object was harvested/destroyed - don't respawn
        public bool isActive => activeInstance != null && !isHarvested;
    }

    /// <summary>
    /// Object pool for environment objects
    /// Manages reusable GameObject instances to avoid expensive Instantiate/Destroy calls
    /// Optimized for WebGL where object creation is particularly expensive
    /// </summary>
    public class EnvironmentObjectPool
    {
        // Pool storage: Dictionary by asset name -> Queue of available instances
        private Dictionary<string, Queue<GameObject>> _availablePools = new Dictionary<string, Queue<GameObject>>();

        // Track all pooled objects (both active and inactive)
        private HashSet<GameObject> _allPooledObjects = new HashSet<GameObject>();

        // Container for all pooled objects
        private Transform _poolContainer;

        // Configuration
        private readonly int _initialPoolSize = 50; // Pre-warm pool with this many objects per type
        private readonly int _maxPoolSize = 500; // Maximum objects in pool per type
        private readonly bool _allowGrowth = true; // Allow pool to grow beyond initial size

        // Stats
        private int _totalSpawns = 0;
        private int _totalReturns = 0;
        private int _totalCreations = 0;

        public EnvironmentObjectPool(Transform poolContainer, int initialSize = 50, int maxSize = 500)
        {
            _poolContainer = poolContainer;
            _initialPoolSize = initialSize;
            _maxPoolSize = maxSize;
        }

        /// <summary>
        /// Pre-warm the pool with instances of a specific asset type
        /// </summary>
        public void PrewarmPool(EnvironmentAsset asset, int count)
        {
            if (asset == null || asset.prefab == null) return;

            string key = asset.assetName;
            if (!_availablePools.ContainsKey(key))
            {
                _availablePools[key] = new Queue<GameObject>();
            }

            for (int i = 0; i < count; i++)
            {
                GameObject obj = CreateNewInstance(asset);
                obj.SetActive(false);
                _availablePools[key].Enqueue(obj);
            }

            Debug.Log($"[EnvironmentObjectPool] Pre-warmed pool for {key}: {count} instances");
        }

        /// <summary>
        /// Spawn an object from the pool (or create new if pool is empty)
        /// </summary>
        public GameObject Spawn(EnvironmentAsset asset, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (asset == null || asset.prefab == null)
            {
                Debug.LogError("[EnvironmentObjectPool] Cannot spawn - asset or prefab is null");
                return null;
            }

            string key = asset.assetName;
            GameObject obj = null;

            // Try to get from pool
            if (_availablePools.ContainsKey(key) && _availablePools[key].Count > 0)
            {
                obj = _availablePools[key].Dequeue();

                // Validate object wasn't destroyed
                if (obj == null)
                {
                    // Object was destroyed, try again
                    return Spawn(asset, position, rotation, scale);
                }
            }
            else
            {
                // Pool empty or doesn't exist - create new instance
                if (!_availablePools.ContainsKey(key))
                {
                    _availablePools[key] = new Queue<GameObject>();
                }

                if (_allowGrowth && GetTotalPooledCount(key) < _maxPoolSize)
                {
                    obj = CreateNewInstance(asset);
                }
                else
                {
                    Debug.LogWarning($"[EnvironmentObjectPool] Pool for {key} at max capacity ({_maxPoolSize})");
                    return null;
                }
            }

            // Configure and activate
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.localScale = scale;
            obj.SetActive(true);

            _totalSpawns++;
            return obj;
        }

        /// <summary>
        /// Return an object to the pool for reuse
        /// </summary>
        public void Return(GameObject obj, string assetName)
        {
            if (obj == null) return;

            // Only return if this is actually a pooled object
            if (!_allPooledObjects.Contains(obj))
            {
                Debug.LogWarning($"[EnvironmentObjectPool] Attempted to return non-pooled object: {obj.name}");
                return;
            }

            // Deactivate and reset
            obj.SetActive(false);
            obj.transform.SetParent(_poolContainer);

            // Return to appropriate pool
            if (!_availablePools.ContainsKey(assetName))
            {
                _availablePools[assetName] = new Queue<GameObject>();
            }

            _availablePools[assetName].Enqueue(obj);
            _totalReturns++;
        }

        /// <summary>
        /// Create a new instance and register it with the pool
        /// </summary>
        private GameObject CreateNewInstance(EnvironmentAsset asset)
        {
            GameObject obj = Object.Instantiate(asset.prefab, _poolContainer);
            obj.name = $"{asset.assetName}_Pooled";
            _allPooledObjects.Add(obj);
            _totalCreations++;
            return obj;
        }

        /// <summary>
        /// Get total number of objects (active + inactive) for a specific asset type
        /// </summary>
        private int GetTotalPooledCount(string assetName)
        {
            int count = 0;
            if (_availablePools.ContainsKey(assetName))
            {
                count = _availablePools[assetName].Count;
            }
            // Would need to track active objects separately to get true total
            return count;
        }

        /// <summary>
        /// Clear all pools and destroy objects
        /// </summary>
        public void Clear()
        {
            foreach (var pool in _availablePools.Values)
            {
                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj != null)
                    {
                        Object.Destroy(obj);
                    }
                }
            }

            _availablePools.Clear();
            _allPooledObjects.Clear();

            Debug.Log($"[EnvironmentObjectPool] Cleared all pools. Stats - Spawns: {_totalSpawns}, Returns: {_totalReturns}, Created: {_totalCreations}");
        }

        /// <summary>
        /// Get pool statistics for debugging
        /// </summary>
        public string GetStats()
        {
            return $"Pool Stats - Spawns: {_totalSpawns}, Returns: {_totalReturns}, Created: {_totalCreations}, Types: {_availablePools.Count}";
        }
    }
}
