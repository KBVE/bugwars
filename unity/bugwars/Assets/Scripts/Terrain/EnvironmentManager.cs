using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace BugWars.Terrain
{
    /// <summary>
    /// Type of environment object for categorization
    /// </summary>
    public enum EnvironmentObjectType
    {
        Tree,
        Bush,
        Rock,
        Grass
    }

    /// <summary>
    /// Asset reference for environment objects
    /// Can be expanded to use Unity Addressables in the future
    /// </summary>
    [System.Serializable]
    public class EnvironmentAsset
    {
        public string assetName;
        public GameObject prefab; // For now use direct prefab reference, later convert to Addressable
        public EnvironmentObjectType type;
        public float spawnWeight = 1f; // Weight for random selection (higher = more common)
        public float minScale = 0.8f;
        public float maxScale = 1.2f;
    }

    /// <summary>
    /// Spawn settings for each environment type
    /// </summary>
    [System.Serializable]
    public class EnvironmentSpawnSettings
    {
        [Header("Density")]
        public float objectsPerChunk = 20f; // Average number of objects per chunk
        public float spawnProbability = 0.8f; // Probability of spawning in a valid location (0-1)

        [Header("Placement")]
        public float minDistanceBetweenObjects = 5f; // Minimum distance between objects
        public float noiseScale = 0.1f; // Perlin noise scale for clustering
        public float noiseThreshold = 0.3f; // Noise value threshold for spawning (0-1)

        [Header("Terrain Height")]
        public float minHeight = 0f; // Minimum terrain height for spawning
        public float maxHeight = 10f; // Maximum terrain height for spawning
        public float maxSlope = 45f; // Maximum terrain slope (degrees) for spawning
    }

    /// <summary>
    /// Tracks spawned objects per chunk for efficient cleanup
    /// </summary>
    public class ChunkEnvironmentData
    {
        public Vector2Int chunkCoord;
        public List<GameObject> spawnedObjects = new List<GameObject>();
        public bool isLoaded = false;
    }

    /// <summary>
    /// Manages environmental objects (trees, bushes, rocks, grass) across terrain chunks
    /// Handles procedural placement, spawning, and despawning based on chunk visibility
    /// Integrates with TerrainManager for chunk-based culling
    /// </summary>
    public class EnvironmentManager : MonoBehaviour, IStartable
    {
        [Header("Asset References")]
        [SerializeField] private List<EnvironmentAsset> treeAssets = new List<EnvironmentAsset>();
        [SerializeField] private List<EnvironmentAsset> bushAssets = new List<EnvironmentAsset>();
        [SerializeField] private List<EnvironmentAsset> rockAssets = new List<EnvironmentAsset>();
        [SerializeField] private List<EnvironmentAsset> grassAssets = new List<EnvironmentAsset>();

        [Header("Spawn Settings")]
        [SerializeField] private EnvironmentSpawnSettings treeSettings = new EnvironmentSpawnSettings
        {
            objectsPerChunk = 15,
            spawnProbability = 0.7f,
            minDistanceBetweenObjects = 8f,
            noiseScale = 0.05f,
            noiseThreshold = 0.4f,
            minHeight = 0.5f,
            maxHeight = 8f,
            maxSlope = 35f
        };

        [SerializeField] private EnvironmentSpawnSettings bushSettings = new EnvironmentSpawnSettings
        {
            objectsPerChunk = 25,
            spawnProbability = 0.6f,
            minDistanceBetweenObjects = 3f,
            noiseScale = 0.08f,
            noiseThreshold = 0.3f,
            minHeight = 0.2f,
            maxHeight = 6f,
            maxSlope = 40f
        };

        [SerializeField] private EnvironmentSpawnSettings rockSettings = new EnvironmentSpawnSettings
        {
            objectsPerChunk = 10,
            spawnProbability = 0.5f,
            minDistanceBetweenObjects = 5f,
            noiseScale = 0.12f,
            noiseThreshold = 0.5f,
            minHeight = 0f,
            maxHeight = 10f,
            maxSlope = 60f
        };

        [Header("Performance")]
        [SerializeField] private bool useObjectPooling = false; // Future enhancement
        [SerializeField] private int maxObjectsPerFrame = 10; // Limit spawns per frame to prevent lag
        [SerializeField] private bool enableDynamicSpawning = true; // Spawn objects as chunks load

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool drawGizmos = false;

        // Dependencies
        private TerrainManager terrainManager;

        // Chunk tracking
        private Dictionary<Vector2Int, ChunkEnvironmentData> chunkEnvironmentData = new Dictionary<Vector2Int, ChunkEnvironmentData>();
        private GameObject environmentContainer;

        // Spawn state
        private int spawnSeed = 0;
        private bool isInitialized = false;

        /// <summary>
        /// VContainer dependency injection
        /// </summary>
        [Inject]
        public void Construct(TerrainManager terrainMgr)
        {
            terrainManager = terrainMgr;
        }

        /// <summary>
        /// VContainer startup callback
        /// </summary>
        public void Start()
        {
            InitializeEnvironmentSystem();
        }

        private void InitializeEnvironmentSystem()
        {
            Debug.Log("[EnvironmentManager] Initializing environment system");

            // Create container for all environment objects
            environmentContainer = new GameObject("EnvironmentObjects");
            DontDestroyOnLoad(environmentContainer);

            // Use same seed as terrain for consistency
            if (terrainManager != null)
            {
                spawnSeed = terrainManager.Seed;
            }

            isInitialized = true;
            Debug.Log("[EnvironmentManager] Environment system initialized");
        }

        private void Update()
        {
            if (!isInitialized || !enableDynamicSpawning || terrainManager == null)
                return;

            // Check for new chunks that need environment objects
            ProcessChunkEnvironmentLoading().Forget();
        }

        /// <summary>
        /// Process chunk environment loading/unloading based on terrain chunks
        /// </summary>
        private async UniTask ProcessChunkEnvironmentLoading()
        {
            if (terrainManager == null)
                return;

            // Get active terrain chunks
            var activeChunkCoords = terrainManager.GetActiveChunkCoords();

            // Load environment for new chunks
            foreach (var chunkCoord in activeChunkCoords)
            {
                if (!chunkEnvironmentData.ContainsKey(chunkCoord))
                {
                    await LoadChunkEnvironment(chunkCoord);
                }
            }

            // Unload environment for chunks that are no longer active
            var chunksToUnload = chunkEnvironmentData.Keys
                .Where(coord => !activeChunkCoords.Contains(coord))
                .ToList();

            foreach (var chunkCoord in chunksToUnload)
            {
                UnloadChunkEnvironment(chunkCoord);
            }
        }

        /// <summary>
        /// Load environment objects for a specific chunk
        /// </summary>
        public async UniTask LoadChunkEnvironment(Vector2Int chunkCoord)
        {
            if (chunkEnvironmentData.ContainsKey(chunkCoord))
                return;

            var chunkData = new ChunkEnvironmentData { chunkCoord = chunkCoord };
            chunkEnvironmentData[chunkCoord] = chunkData;

            // Get terrain chunk for height sampling
            var terrainChunk = terrainManager.GetChunk(chunkCoord);
            if (terrainChunk == null)
            {
                Debug.LogWarning($"[EnvironmentManager] Terrain chunk {chunkCoord} not found");
                return;
            }

            // Spawn environment objects by type
            await SpawnObjectsForChunk(chunkCoord, chunkData, EnvironmentObjectType.Tree, treeAssets, treeSettings);
            await SpawnObjectsForChunk(chunkCoord, chunkData, EnvironmentObjectType.Bush, bushAssets, bushSettings);
            await SpawnObjectsForChunk(chunkCoord, chunkData, EnvironmentObjectType.Rock, rockAssets, rockSettings);

            chunkData.isLoaded = true;

            if (showDebugInfo)
            {
                Debug.Log($"[EnvironmentManager] Loaded {chunkData.spawnedObjects.Count} objects for chunk {chunkCoord}");
            }
        }

        /// <summary>
        /// Spawn objects of a specific type for a chunk
        /// </summary>
        private async UniTask SpawnObjectsForChunk(
            Vector2Int chunkCoord,
            ChunkEnvironmentData chunkData,
            EnvironmentObjectType objectType,
            List<EnvironmentAsset> assets,
            EnvironmentSpawnSettings settings)
        {
            if (assets.Count == 0)
                return;

            int objectsSpawned = 0;
            int maxObjects = Mathf.RoundToInt(settings.objectsPerChunk);
            int attempts = 0;
            int maxAttempts = maxObjects * 3; // Try 3x to account for failed placements

            List<Vector3> spawnedPositions = new List<Vector3>();

            while (objectsSpawned < maxObjects && attempts < maxAttempts)
            {
                attempts++;

                // Generate random position within chunk
                Vector3 position = GetRandomPositionInChunk(chunkCoord);

                // Check noise threshold for clustering
                float noiseValue = GetNoiseValueForPosition(position, settings.noiseScale);
                if (noiseValue < settings.noiseThreshold)
                    continue;

                // Check spawn probability
                if (Random.value > settings.spawnProbability)
                    continue;

                // Check terrain height and slope
                if (!IsValidSpawnLocation(position, settings))
                    continue;

                // Check minimum distance from other objects
                if (!CheckMinimumDistance(position, spawnedPositions, settings.minDistanceBetweenObjects))
                    continue;

                // Select random asset
                var asset = SelectRandomAsset(assets);
                if (asset == null || asset.prefab == null)
                    continue;

                // Spawn object
                GameObject spawnedObject = await SpawnEnvironmentObject(asset, position, chunkCoord);
                if (spawnedObject != null)
                {
                    chunkData.spawnedObjects.Add(spawnedObject);
                    spawnedPositions.Add(position);
                    objectsSpawned++;

                    // Yield every N objects to prevent frame drops
                    if (objectsSpawned % maxObjectsPerFrame == 0)
                    {
                        await UniTask.Yield();
                    }
                }
            }
        }

        /// <summary>
        /// Spawn a single environment object
        /// </summary>
        private async UniTask<GameObject> SpawnEnvironmentObject(EnvironmentAsset asset, Vector3 position, Vector2Int chunkCoord)
        {
            // Adjust height to terrain surface
            float terrainHeight = GetTerrainHeightAtPosition(position);
            position.y = terrainHeight;

            // Instantiate object
            GameObject obj = Instantiate(asset.prefab, position, Quaternion.identity, environmentContainer.transform);

            // Random rotation (Y-axis only for most objects)
            float randomRotation = Random.Range(0f, 360f);
            obj.transform.rotation = Quaternion.Euler(0, randomRotation, 0);

            // Random scale
            float randomScale = Random.Range(asset.minScale, asset.maxScale);
            obj.transform.localScale = Vector3.one * randomScale;

            // Name for debugging
            obj.name = $"{asset.assetName}_Chunk{chunkCoord.x}_{chunkCoord.y}";

            await UniTask.Yield();
            return obj;
        }

        /// <summary>
        /// Unload environment objects for a specific chunk
        /// </summary>
        public void UnloadChunkEnvironment(Vector2Int chunkCoord)
        {
            if (!chunkEnvironmentData.TryGetValue(chunkCoord, out var chunkData))
                return;

            // Destroy all spawned objects
            foreach (var obj in chunkData.spawnedObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            chunkData.spawnedObjects.Clear();
            chunkEnvironmentData.Remove(chunkCoord);

            if (showDebugInfo)
            {
                Debug.Log($"[EnvironmentManager] Unloaded chunk environment {chunkCoord}");
            }
        }

        /// <summary>
        /// Get random position within a chunk
        /// </summary>
        private Vector3 GetRandomPositionInChunk(Vector2Int chunkCoord)
        {
            float chunkSize = 500f; // Match TerrainManager chunk size
            float randomX = Random.Range(0f, chunkSize);
            float randomZ = Random.Range(0f, chunkSize);

            return new Vector3(
                chunkCoord.x * chunkSize + randomX,
                0,
                chunkCoord.y * chunkSize + randomZ
            );
        }

        /// <summary>
        /// Get noise value for position to create clustering
        /// </summary>
        private float GetNoiseValueForPosition(Vector3 position, float noiseScale)
        {
            return Mathf.PerlinNoise(
                (position.x + spawnSeed) * noiseScale,
                (position.z + spawnSeed) * noiseScale
            );
        }

        /// <summary>
        /// Check if location is valid for spawning based on terrain
        /// </summary>
        private bool IsValidSpawnLocation(Vector3 position, EnvironmentSpawnSettings settings)
        {
            float terrainHeight = GetTerrainHeightAtPosition(position);

            // Check height range
            if (terrainHeight < settings.minHeight || terrainHeight > settings.maxHeight)
                return false;

            // TODO: Check slope (requires terrain normal calculation)
            // For now, skip slope check

            return true;
        }

        /// <summary>
        /// Get terrain height at world position
        /// </summary>
        private float GetTerrainHeightAtPosition(Vector3 position)
        {
            // Raycast down to find terrain
            if (Physics.Raycast(position + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 200f))
            {
                return hit.point.y;
            }

            // Fallback: use noise-based height (same as terrain generation)
            float noiseScale = 0.05f; // Match TerrainManager
            float heightMultiplier = 3f;
            return Mathf.PerlinNoise(
                (position.x + spawnSeed * 0.1f) * noiseScale,
                (position.z + spawnSeed * 0.1f) * noiseScale
            ) * heightMultiplier;
        }

        /// <summary>
        /// Check minimum distance from other objects
        /// </summary>
        private bool CheckMinimumDistance(Vector3 position, List<Vector3> existingPositions, float minDistance)
        {
            float minDistanceSqr = minDistance * minDistance;

            foreach (var existingPos in existingPositions)
            {
                float distanceSqr = (position - existingPos).sqrMagnitude;
                if (distanceSqr < minDistanceSqr)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Select random asset based on spawn weights
        /// </summary>
        private EnvironmentAsset SelectRandomAsset(List<EnvironmentAsset> assets)
        {
            if (assets.Count == 0)
                return null;

            float totalWeight = assets.Sum(a => a.spawnWeight);
            float randomValue = Random.Range(0f, totalWeight);

            float cumulativeWeight = 0f;
            foreach (var asset in assets)
            {
                cumulativeWeight += asset.spawnWeight;
                if (randomValue <= cumulativeWeight)
                {
                    return asset;
                }
            }

            return assets[assets.Count - 1];
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos || !isInitialized)
                return;

            // Draw spawned object positions
            Gizmos.color = Color.green;
            foreach (var chunkData in chunkEnvironmentData.Values)
            {
                foreach (var obj in chunkData.spawnedObjects)
                {
                    if (obj != null)
                    {
                        Gizmos.DrawWireSphere(obj.transform.position, 1f);
                    }
                }
            }
        }
        #endif

        /// <summary>
        /// Get total count of spawned objects
        /// </summary>
        public int GetTotalObjectCount()
        {
            return chunkEnvironmentData.Values.Sum(data => data.spawnedObjects.Count);
        }

        /// <summary>
        /// Force reload all chunk environments (for testing)
        /// </summary>
        [ContextMenu("Reload All Environments")]
        public void ReloadAllEnvironments()
        {
            var chunks = chunkEnvironmentData.Keys.ToList();
            foreach (var chunk in chunks)
            {
                UnloadChunkEnvironment(chunk);
            }
            Debug.Log("[EnvironmentManager] All environments unloaded. Will reload as chunks activate.");
        }
    }
}
