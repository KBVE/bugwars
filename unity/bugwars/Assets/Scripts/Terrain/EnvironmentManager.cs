using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using VContainer;
using VContainer.Unity;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using MessagePipe;
using BugWars.Interaction;

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

        [Header("Placement Algorithm")]
        public bool usePoissonDisk = true; // Use Poisson disk sampling for natural spacing
        public float minDistanceBetweenObjects = 5f; // Minimum distance between objects
        public int maxPlacementAttempts = 30; // Max attempts to find valid placement per object

        [Header("Clustering (Biome-like)")]
        public bool enableClustering = true; // Create natural clusters (forests, rock formations)
        public float clusterNoiseScale = 0.05f; // Large-scale noise for biome clustering
        public float clusterThreshold = 0.4f; // Threshold for cluster spawning (0-1)
        public float detailNoiseScale = 0.15f; // Fine-detail noise for variation within clusters
        public float clusterDensityMultiplier = 1.5f; // Density multiplier inside clusters

        [Header("Terrain Height")]
        public float minHeight = 0f; // Minimum terrain height for spawning
        public float maxHeight = 10f; // Maximum terrain height for spawning
        public float maxSlope = 45f; // Maximum terrain slope (degrees) for spawning

        [Header("Height-Based Variation")]
        public bool useHeightVariation = true; // Different densities at different heights
        public float lowlandBonus = 1.2f; // Density multiplier for low areas (0-3 units)
        public float highlandPenalty = 0.6f; // Density multiplier for high areas (6+ units)
    }

    /// <summary>
    /// Tracks environment object spawn points per chunk
    /// Objects are stored as data until player gets close, then spawned from pool
    /// </summary>
    public class ChunkEnvironmentData
    {
        public Vector2Int chunkCoord;
        public List<EnvironmentSpawnData> spawnPoints = new List<EnvironmentSpawnData>(); // All potential spawn points (data)
        public bool isLoaded = false;
        public bool spawnDataGenerated = false; // Whether spawn positions have been calculated
    }

    /// <summary>
    /// Manages environmental objects (trees, bushes, rocks, grass) across terrain chunks
    /// Handles procedural placement, spawning, and despawning based on chunk visibility
    /// Integrates with TerrainManager for chunk-based culling
    ///
    /// WebGL Optimizations:
    /// - Frame-time budgeting (8ms default) to prevent lag spikes
    /// - Throttled processing (minimum 2 frames between chunk updates)
    /// - Conservative object spawning (5 objects per yield, down from 50)
    /// - Concurrency protection to prevent collection modification
    /// - UniTask async/await for non-blocking spawning
    /// </summary>
    public class EnvironmentManager : IAsyncStartable, IDisposable
    {
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private CancellationTokenSource _cts;
        private List<EnvironmentAsset> treeAssets = new List<EnvironmentAsset>();
        private List<EnvironmentAsset> bushAssets = new List<EnvironmentAsset>();
        private List<EnvironmentAsset> rockAssets = new List<EnvironmentAsset>();
        private List<EnvironmentAsset> grassAssets = new List<EnvironmentAsset>();

        // Spawn Settings
        private EnvironmentSpawnSettings treeSettings = new EnvironmentSpawnSettings
        {
            // Production: Normal density
            objectsPerChunk = 350,             // Trees per chunk (500x500 area)
            spawnProbability = 0.7f,           // 70% chance to spawn

            // Natural spacing
            usePoissonDisk = true,
            minDistanceBetweenObjects = 8f,    // 8 units minimum spacing
            maxPlacementAttempts = 100,

            // Clustering for natural forest feel
            enableClustering = true,
            clusterNoiseScale = 0.08f,
            clusterThreshold = 0.5f,
            detailNoiseScale = 0.15f,
            clusterDensityMultiplier = 1.5f,

            minHeight = 0.5f,
            maxHeight = 8f,
            maxSlope = 35f,

            useHeightVariation = true,
            lowlandBonus = 1.2f,
            highlandPenalty = 0.8f
        };

        private readonly EnvironmentSpawnSettings bushSettings = new()
        {
            // Production: Normal density
            objectsPerChunk = 400,             // Bushes per chunk
            spawnProbability = 0.65f,          // 65% chance to spawn

            // Natural spacing
            usePoissonDisk = true,
            minDistanceBetweenObjects = 6f,    // 6 units minimum spacing
            maxPlacementAttempts = 100,

            // Clustering for natural undergrowth
            enableClustering = true,
            clusterNoiseScale = 0.1f,
            clusterThreshold = 0.5f,
            detailNoiseScale = 0.2f,
            clusterDensityMultiplier = 1.8f,

            minHeight = 0.2f,
            maxHeight = 6f,
            maxSlope = 40f,

            useHeightVariation = true,
            lowlandBonus = 1.3f,
            highlandPenalty = 0.7f
        };

        private readonly EnvironmentSpawnSettings rockSettings = new()
        {
            // Production: Normal density
            objectsPerChunk = 175,             // Rocks per chunk
            spawnProbability = 0.6f,           // 60% chance to spawn

            // Natural spacing
            usePoissonDisk = true,
            minDistanceBetweenObjects = 10f,   // 10 units minimum spacing
            maxPlacementAttempts = 100,

            // Clustering for natural rock formations
            enableClustering = true,
            clusterNoiseScale = 0.12f,
            clusterThreshold = 0.5f,
            detailNoiseScale = 0.18f,
            clusterDensityMultiplier = 2.0f,

            minHeight = 0f,
            maxHeight = 10f,
            maxSlope = 60f,

            useHeightVariation = true,
            lowlandBonus = 0.9f,
            highlandPenalty = 1.2f
        };

        private readonly EnvironmentSpawnSettings grassSettings = new()
        {
            // DENSE GRASS: 80 clumps per chunk - carpet of grass
            objectsPerChunk = 80,
            spawnProbability = 0.9f,

            usePoissonDisk = true,
            minDistanceBetweenObjects = 2f,  // Grass can be closer together
            maxPlacementAttempts = 20,

            // Grass covers most open areas
            enableClustering = false,        // Grass is everywhere (no clustering)
            clusterNoiseScale = 0.15f,
            clusterThreshold = 0.2f,
            detailNoiseScale = 0.25f,
            clusterDensityMultiplier = 1.0f,

            minHeight = 0f,
            maxHeight = 5f,
            maxSlope = 50f,

            useHeightVariation = true,
            lowlandBonus = 1.2f,
            highlandPenalty = 0.8f
        };

        // Performance - Object Pooling
        private readonly bool useObjectPooling = true; // ENABLED: Reuse objects for WebGL performance
        private readonly int poolPrewarmSize = 50; // Pre-create this many objects per type
        private readonly int poolMaxSize = 500; // Maximum pool size per type
        private readonly float objectSpawnDistance = 200f; // Distance at which objects spawn from pool (increased for better visibility)
        private readonly float objectDespawnDistance = 250f; // Distance at which objects return to pool (with 50m buffer)

        private readonly int maxObjectsPerFrame = 5; // WebGL: Very conservative - reduced from 50
        private readonly bool enableDynamicSpawning = true; // Spawn objects as chunks load

        // WebGL Performance
        private readonly float maxFrameTimeMs = 8.0f; // WebGL: Target 8ms budget (conservative for 60fps)
        private readonly int minFramesBetweenProcessing = 2; // WebGL: Minimum frames between chunk processing calls

        // LOD (Level of Detail) Performance Settings
        private readonly bool enableLOD = true; // Enable LOD system for environment objects
        private readonly float lodColliderCullingDistance = 80f; // Distance beyond which colliders are disabled (increased)
        private readonly float lodRendererCullingDistance = 200f; // Distance beyond which renderers are disabled (matches spawn distance)
        private readonly float lodUpdateInterval = 0.2f; // How often LOD state updates (seconds)

        // Debug
        private readonly bool showDebugInfo = false; // Reduced debug spam now that shaders are working
        private readonly bool drawGizmos = false;

        // Dependencies
        private TerrainManager terrainManager;
        private IPublisher<ObjectHarvestedMessage> harvestedPublisher;
        private ISubscriber<ObjectHarvestedMessage> harvestedSubscriber;
        private IDisposable harvestSubscription;

        // Chunk tracking
        private Dictionary<Vector2Int, ChunkEnvironmentData> chunkEnvironmentData = new Dictionary<Vector2Int, ChunkEnvironmentData>();
        private GameObject environmentContainer;

        // Object pooling
        private EnvironmentObjectPool objectPool;
        private Transform playerTransform; // Cached player position for distance checks

        // Spawn state
        private int spawnSeed = 0;
        private bool isInitialized = false;
        private bool isProcessingChunkLoading = false;
        private int lastProcessingFrame = -1;

        // Reusable collections to avoid LINQ allocations
        private List<Vector2Int> activeChunkCoordsCache = new List<Vector2Int>();
        private List<Vector2Int> chunksToUnloadCache = new List<Vector2Int>();

        // Frame-time tracking for WebGL optimization
        private System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// VContainer dependency injection
        /// </summary>
        [Inject]
        public void Construct(
            TerrainManager terrainMgr,
            IPublisher<ObjectHarvestedMessage> publisher,
            ISubscriber<ObjectHarvestedMessage> subscriber)
        {
            terrainManager = terrainMgr;
            harvestedPublisher = publisher;
            harvestedSubscriber = subscriber;
        }

        /// <summary>
        /// VContainer async startup callback
        /// </summary>
        public async UniTask StartAsync(CancellationToken cancellationToken)
        {
            _cts = new CancellationTokenSource();
            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken).Token;

            try
            {
                await InitializeEnvironmentSystemAsync(linkedToken);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[EnvironmentManager] Initialization canceled.");
            }
        }

        private async UniTask InitializeEnvironmentSystemAsync(CancellationToken cancellationToken)
        {
            await InitializeEnvironmentContainerAndAssets(cancellationToken);

            // Subscribe to object harvested messages for pooling
            harvestSubscription = harvestedSubscriber.Subscribe(OnObjectHarvested);

            // Yield to prevent blocking
            await UniTask.Yield(cancellationToken);

            isInitialized = true;

            Debug.Log("[EnvironmentManager] Environment system fully initialized and ready to spawn objects");

            // Start the update loop using UniTask PlayerLoop
            UpdateLoop(cancellationToken).Forget();
        }

        /// <summary>
        /// Update loop using UniTask PlayerLoop instead of MonoBehaviour Update
        /// </summary>
        private async UniTaskVoid UpdateLoop(CancellationToken cancellationToken)
        {
            // WebGL Optimization: Use larger delay between updates instead of every frame
            const float updateInterval = 0.1f; // Update 10 times per second instead of 60+

            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for interval instead of every frame
                await UniTask.Delay(System.TimeSpan.FromSeconds(updateInterval), cancellationToken: cancellationToken);

                if (!isInitialized || !enableDynamicSpawning || terrainManager == null)
                    continue;

                // Check for new chunks that need environment objects (non-blocking)
                if (!isProcessingChunkLoading)
                {
                    ProcessChunkEnvironmentLoading().Forget();
                }

                // POOLING: Update object spawning based on player distance
                // Run less frequently to reduce overhead
                if (useObjectPooling && playerTransform != null)
                {
                    UpdatePooledObjectSpawning();
                }
            }
        }

        /// <summary>
        /// Update pooled object spawning based on player distance
        /// Objects spawn from pool when player gets close, return to pool when player moves away
        /// </summary>
        private void UpdatePooledObjectSpawning()
        {
            if (playerTransform == null || objectPool == null)
                return;

            Vector3 playerPos = playerTransform.position;
            int objectsProcessedThisUpdate = 0;
            const int maxUpdatesPerCall = 50; // Process up to 50 objects per update (10 times per second)

            // WebGL Optimization: Only check chunks near player using chunk-based culling
            // Calculate which chunks could possibly contain objects within spawn range
            int chunkSize = 500; // Assuming 500x500 chunk size
            float maxCheckDistance = objectDespawnDistance + chunkSize; // Add chunk size as buffer

            // Iterate through all loaded chunks
            foreach (var kvp in chunkEnvironmentData)
            {
                var chunkData = kvp.Value;
                if (!chunkData.spawnDataGenerated)
                    continue;

                // OPTIMIZATION: Skip entire chunk if it's too far from player
                Vector3 chunkCenter = new Vector3(
                    chunkData.chunkCoord.x * chunkSize + chunkSize * 0.5f,
                    0,
                    chunkData.chunkCoord.y * chunkSize + chunkSize * 0.5f
                );

                float chunkDistance = Vector3.Distance(new Vector3(playerPos.x, 0, playerPos.z), chunkCenter);
                if (chunkDistance > maxCheckDistance)
                    continue; // Chunk is too far, skip all objects in it

                // Check each spawn point in the chunk
                foreach (var spawnData in chunkData.spawnPoints)
                {
                    if (objectsProcessedThisUpdate >= maxUpdatesPerCall)
                        return; // Spread work across multiple update calls

                    // Skip harvested objects - they should never respawn
                    if (spawnData.isHarvested)
                        continue;

                    // OPTIMIZATION: Use squared distance to avoid sqrt
                    float sqrDistance = (playerPos - spawnData.position).sqrMagnitude;
                    float sqrSpawnDistance = objectSpawnDistance * objectSpawnDistance;
                    float sqrDespawnDistance = objectDespawnDistance * objectDespawnDistance;

                    // Spawn object if player is close enough and object isn't already active
                    if (sqrDistance <= sqrSpawnDistance && !spawnData.isActive)
                    {
                        SpawnObjectFromPool(spawnData);
                        objectsProcessedThisUpdate++;
                    }
                    // Despawn object if player moved too far away and object is active
                    else if (sqrDistance > sqrDespawnDistance && spawnData.isActive)
                    {
                        DespawnObjectToPool(spawnData);
                        objectsProcessedThisUpdate++;
                    }
                }
            }
        }

        /// <summary>
        /// Spawn an object from the pool for a specific spawn point
        /// </summary>
        private void SpawnObjectFromPool(EnvironmentSpawnData spawnData)
        {
            if (spawnData.isActive || spawnData.asset == null)
                return;

            GameObject obj = objectPool.Spawn(spawnData.asset, spawnData.position, spawnData.rotation, spawnData.scale);
            if (obj != null)
            {
                spawnData.activeInstance = obj;

                // Apply WebGL material fix
                FixWebGLMaterials(obj);

                // Add LOD component
                if (enableLOD)
                {
                    var lodComponent = obj.GetComponent<EnvironmentObjectLOD>();
                    if (lodComponent == null)
                    {
                        lodComponent = obj.AddComponent<EnvironmentObjectLOD>();
                        lodComponent.colliderCullingDistance = lodColliderCullingDistance;
                        lodComponent.rendererCullingDistance = lodRendererCullingDistance;
                        lodComponent.updateInterval = lodUpdateInterval;
                        lodComponent.showDebugLogs = showDebugInfo;
                    }
                    lodComponent.ForceUpdate();
                }

                // Add tracker component to handle destruction/harvesting
                var tracker = obj.GetComponent<EnvironmentObjectTracker>();
                if (tracker == null)
                {
                    tracker = obj.AddComponent<EnvironmentObjectTracker>();
                }
                tracker.Initialize(spawnData);

                // Setup interactable component
                SetupInteractable(obj, spawnData.asset);

                // Set name for debugging
                obj.name = $"{spawnData.asset.assetName}_Chunk{spawnData.chunkCoord.x}_{spawnData.chunkCoord.y}_Pooled";
            }
        }

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        private void DespawnObjectToPool(EnvironmentSpawnData spawnData)
        {
            if (!spawnData.isActive || spawnData.activeInstance == null)
                return;

            objectPool.Return(spawnData.activeInstance, spawnData.asset.assetName);
            spawnData.activeInstance = null;
        }

        /// <summary>
        /// Async initialization - CRITICAL: Must wait for Addressables to load before spawning
        /// </summary>
        private async UniTask InitializeEnvironmentContainerAndAssets(CancellationToken cancellationToken)
        {
            // Check if we already have a container reference (from previous init)
            if (environmentContainer == null)
            {
                // Check if EnvironmentObjects container already exists (from previous initialization or scene load)
                var existingContainer = GameObject.Find("EnvironmentObjects");
                if (existingContainer != null)
                {
                    environmentContainer = existingContainer;
                }
                else
                {
                    // Create new container for all environment objects
                    // Note: No need for DontDestroyOnLoad - the LifetimeScope handles persistence
                    environmentContainer = new GameObject("EnvironmentObjects");
                }
            }

            // Initialize object pool
            if (useObjectPooling && objectPool == null)
            {
                objectPool = new EnvironmentObjectPool(environmentContainer.transform, poolPrewarmSize, poolMaxSize);
                Debug.Log($"[EnvironmentManager] Object pooling ENABLED (prewarm: {poolPrewarmSize}, max: {poolMaxSize})");
            }

            // Cache player transform for distance checks
            if (playerTransform == null)
            {
                // Try multiple tags - Player, Camera3D, MainCamera
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    player = GameObject.FindGameObjectWithTag("Camera3D");
                }
                if (player == null)
                {
                    player = GameObject.FindGameObjectWithTag("MainCamera");
                }

                if (player != null)
                {
                    playerTransform = player.transform;
                    Debug.Log($"[EnvironmentManager] Player transform cached for distance-based spawning (tag: {player.tag})");
                }
                else
                {
                    Debug.LogWarning("[EnvironmentManager] Could not find player with tags: Player, Camera3D, or MainCamera");
                }
            }

            // Use same seed as terrain for consistency
            if (terrainManager != null)
            {
                spawnSeed = terrainManager.Seed;
            }

            // Load prefabs programmatically if arrays are empty (for dynamic instantiation via VContainer)
            // CRITICAL: AWAIT this to ensure prefabs are loaded before spawning starts
            if (treeAssets.Count == 0)
            {
                Debug.Log("[EnvironmentManager] Loading environment prefabs - WAITING for completion before spawning");
                await LoadEnvironmentPrefabsAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Load environment prefabs from Addressables
        /// Called automatically when EnvironmentManager is created dynamically
        /// Loads prefabs asynchronously using Addressable labels: "Trees", "Bushes", "Rocks"
        /// CRITICAL: This returns UniTask and MUST be awaited to ensure prefabs load before spawning
        /// </summary>
        private async UniTask LoadEnvironmentPrefabsAsync(CancellationToken cancellationToken)
        {
            Debug.Log("[EnvironmentManager] Loading environment prefabs from Addressables...");

            try
            {
                // Load all tree prefabs with "Trees" label
                var treesHandle = Addressables.LoadAssetsAsync<GameObject>("Trees", null);
                var treePrefabs = await treesHandle.ToUniTask(cancellationToken: cancellationToken);

                foreach (var prefab in treePrefabs)
                {
                    if (prefab != null)
                    {
                        treeAssets.Add(new EnvironmentAsset
                        {
                            assetName = prefab.name,
                            prefab = prefab,
                            type = EnvironmentObjectType.Tree,
                            spawnWeight = 1f,
                            minScale = 0.8f,
                            maxScale = 1.2f
                        });
                    }
                }
                Debug.Log($"[EnvironmentManager] Loaded {treeAssets.Count} tree prefabs from Addressables");

                // Load all bush prefabs with "Bushes" label
                var bushesHandle = Addressables.LoadAssetsAsync<GameObject>("Bushes", null);
                var bushPrefabs = await bushesHandle.ToUniTask(cancellationToken: cancellationToken);

                foreach (var prefab in bushPrefabs)
                {
                    if (prefab != null)
                    {
                        bushAssets.Add(new EnvironmentAsset
                        {
                            assetName = prefab.name,
                            prefab = prefab,
                            type = EnvironmentObjectType.Bush,
                            spawnWeight = 1f,
                            minScale = 0.7f,
                            maxScale = 1.1f
                        });
                    }
                }
                Debug.Log($"[EnvironmentManager] Loaded {bushAssets.Count} bush prefabs from Addressables");

                // Load all rock prefabs with "Rocks" label
                var rocksHandle = Addressables.LoadAssetsAsync<GameObject>("Rocks", null);
                var rockPrefabs = await rocksHandle.ToUniTask(cancellationToken: cancellationToken);

                foreach (var prefab in rockPrefabs)
                {
                    if (prefab != null)
                    {
                        rockAssets.Add(new EnvironmentAsset
                        {
                            assetName = prefab.name,
                            prefab = prefab,
                            type = EnvironmentObjectType.Rock,
                            spawnWeight = 1f,
                            minScale = 0.9f,
                            maxScale = 1.3f
                        });
                    }
                }
                Debug.Log($"[EnvironmentManager] Loaded {rockAssets.Count} rock prefabs from Addressables");

                if (treeAssets.Count == 0 || bushAssets.Count == 0 || rockAssets.Count == 0)
                {
                    Debug.LogWarning("[EnvironmentManager] Some prefab types failed to load! Make sure prefabs are marked as Addressable with labels: 'Trees', 'Bushes', 'Rocks'");
                }
                else
                {
                    Debug.Log($"[EnvironmentManager] Successfully loaded all environment prefabs: {treeAssets.Count} trees, {bushAssets.Count} bushes, {rockAssets.Count} rocks");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EnvironmentManager] Failed to load environment prefabs from Addressables: {ex.Message}");
                Debug.LogError("[EnvironmentManager] Falling back to Resources folder...");
                LoadEnvironmentPrefabsFromResources();
            }
        }

        /// <summary>
        /// Fallback method to load from Resources folder if Addressables fails
        /// This ensures the game still works in the editor even if Addressables aren't configured
        /// WARNING: Resources folder doesn't work in WebGL builds! This is EDITOR ONLY.
        /// </summary>
        private void LoadEnvironmentPrefabsFromResources()
        {
            Debug.LogWarning("[EnvironmentManager] ⚠️ FALLING BACK TO RESOURCES FOLDER - THIS WON'T WORK IN WEBGL!");
            Debug.LogWarning("[EnvironmentManager] If you see this in WebGL, Addressables failed to load!");

            // Load all tree prefabs
            GameObject[] treePrefabs = Resources.LoadAll<GameObject>("Prefabs/Forest/Trees");
            Debug.Log($"[EnvironmentManager] Resources.LoadAll found {treePrefabs?.Length ?? 0} tree prefabs");
            foreach (var prefab in treePrefabs)
            {
                treeAssets.Add(new EnvironmentAsset
                {
                    assetName = prefab.name,
                    prefab = prefab,
                    type = EnvironmentObjectType.Tree,
                    spawnWeight = 1f,
                    minScale = 0.8f,
                    maxScale = 1.2f
                });
            }

            // Load all bush prefabs
            GameObject[] bushPrefabs = Resources.LoadAll<GameObject>("Prefabs/Forest/Bushes");
            foreach (var prefab in bushPrefabs)
            {
                bushAssets.Add(new EnvironmentAsset
                {
                    assetName = prefab.name,
                    prefab = prefab,
                    type = EnvironmentObjectType.Bush,
                    spawnWeight = 1f,
                    minScale = 0.7f,
                    maxScale = 1.1f
                });
            }

            // Load all rock prefabs
            GameObject[] rockPrefabs = Resources.LoadAll<GameObject>("Prefabs/Forest/Rocks");
            foreach (var prefab in rockPrefabs)
            {
                rockAssets.Add(new EnvironmentAsset
                {
                    assetName = prefab.name,
                    prefab = prefab,
                    type = EnvironmentObjectType.Rock,
                    spawnWeight = 1f,
                    minScale = 0.9f,
                    maxScale = 1.3f
                });
            }

            Debug.Log($"[EnvironmentManager] Loaded from Resources: {treeAssets.Count} trees, {bushAssets.Count} bushes, {rockAssets.Count} rocks");
        }

        /// <summary>
        /// Process chunk environment loading/unloading based on terrain chunks
        /// </summary>
        private async UniTask ProcessChunkEnvironmentLoading()
        {
            if (terrainManager == null || isProcessingChunkLoading)
                return;

            isProcessingChunkLoading = true;
            try
            {
                // Get active terrain chunks and populate cache (OPTIMIZED: Zero-allocation)
                activeChunkCoordsCache.Clear();
                var activeChunks = terrainManager.GetActiveChunkCoords();
                foreach (var coord in activeChunks)
                {
                    activeChunkCoordsCache.Add(coord);
                }

                // Load environment for new chunks
                foreach (var chunkCoord in activeChunkCoordsCache)
                {
                    if (!chunkEnvironmentData.ContainsKey(chunkCoord))
                    {
                        await LoadChunkEnvironment(chunkCoord);
                    }
                }

                // Unload environment for chunks that are no longer active (OPTIMIZED: Manual loop instead of LINQ)
                chunksToUnloadCache.Clear();
                foreach (var coord in chunkEnvironmentData.Keys)
                {
                    if (!activeChunkCoordsCache.Contains(coord))
                    {
                        chunksToUnloadCache.Add(coord);
                    }
                }

                foreach (var chunkCoord in chunksToUnloadCache)
                {
                    UnloadChunkEnvironment(chunkCoord);
                }
            }
            finally
            {
                isProcessingChunkLoading = false;
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

            // Generate spawn data for all environment object types
            await SpawnObjectsForChunk(chunkCoord, chunkData, EnvironmentObjectType.Tree, treeAssets, treeSettings);
            await SpawnObjectsForChunk(chunkCoord, chunkData, EnvironmentObjectType.Bush, bushAssets, bushSettings);
            await SpawnObjectsForChunk(chunkCoord, chunkData, EnvironmentObjectType.Rock, rockAssets, rockSettings);

            chunkData.spawnDataGenerated = true;
            chunkData.isLoaded = true;

            Debug.Log($"[EnvironmentManager] Generated {chunkData.spawnPoints.Count} spawn points for chunk {chunkCoord}");

            // POOLING: Immediately spawn objects within range of player
            if (useObjectPooling)
            {
                if (playerTransform != null)
                {
                    Vector3 playerPos = playerTransform.position;
                    int immediateSpawns = 0;

                    foreach (var spawnData in chunkData.spawnPoints)
                    {
                        // Skip harvested objects
                        if (spawnData.isHarvested)
                            continue;

                        float distance = Vector3.Distance(playerPos, spawnData.position);
                        if (distance <= objectSpawnDistance)
                        {
                            SpawnObjectFromPool(spawnData);
                            immediateSpawns++;
                        }
                    }

                    if (immediateSpawns > 0)
                    {
                        Debug.Log($"[EnvironmentManager] Immediately spawned {immediateSpawns} objects near player in chunk {chunkCoord}");
                    }
                }
                else
                {
                    // Player not found yet - try to find it now with multiple tags
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player == null)
                    {
                        player = GameObject.FindGameObjectWithTag("Camera3D");
                    }
                    if (player == null)
                    {
                        player = GameObject.FindGameObjectWithTag("MainCamera");
                    }

                    if (player != null)
                    {
                        playerTransform = player.transform;
                        Debug.Log($"[EnvironmentManager] Player transform found during chunk load (tag: {player.tag})");

                        // Recursive call now that we have player reference
                        Vector3 playerPos = playerTransform.position;
                        int immediateSpawns = 0;

                        foreach (var spawnData in chunkData.spawnPoints)
                        {
                            // Skip harvested objects
                            if (spawnData.isHarvested)
                                continue;

                            float distance = Vector3.Distance(playerPos, spawnData.position);
                            if (distance <= objectSpawnDistance)
                            {
                                SpawnObjectFromPool(spawnData);
                                immediateSpawns++;
                            }
                        }

                        if (immediateSpawns > 0)
                        {
                            Debug.Log($"[EnvironmentManager] Immediately spawned {immediateSpawns} objects near player in chunk {chunkCoord}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[EnvironmentManager] Player not found - objects in chunk {chunkCoord} will spawn when player gets close");
                    }
                }
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

            // WebGL: Start frame timer for this spawn batch
            frameTimer.Restart();

            // Calculate adjusted object count based on height variation
            float densityMultiplier = 1.0f;
            if (settings.useHeightVariation)
            {
                // Sample chunk center height to determine if lowland or highland
                Vector3 chunkCenter = GetChunkCenterPosition(chunkCoord);
                float centerHeight = GetTerrainHeightAtPosition(chunkCenter);

                if (centerHeight < 3f)
                {
                    // Lowland - apply bonus
                    densityMultiplier = settings.lowlandBonus;
                }
                else if (centerHeight > 6f)
                {
                    // Highland - apply penalty (or bonus for rocks)
                    densityMultiplier = settings.highlandPenalty;
                }
                // Else mid-range - use base density (1.0f)
            }

            int objectsSpawned = 0;
            int maxObjects = Mathf.RoundToInt(settings.objectsPerChunk * densityMultiplier);
            int attempts = 0;
            int maxAttempts = maxObjects * settings.maxPlacementAttempts; // Use configurable attempts

            List<Vector3> spawnedPositions = new List<Vector3>();

            while (objectsSpawned < maxObjects && attempts < maxAttempts)
            {
                attempts++;

                // Generate random position within chunk
                Vector3 position = GetRandomPositionInChunk(chunkCoord);

                // Check clustering (biome-based spawning)
                if (settings.enableClustering)
                {
                    float clusterNoise = GetNoiseValueForPosition(position, settings.clusterNoiseScale);
                    float detailNoise = GetNoiseValueForPosition(position, settings.detailNoiseScale);
                    float combinedNoise = clusterNoise * 0.7f + detailNoise * 0.3f;

                    // Skip if outside cluster zones
                    if (combinedNoise < settings.clusterThreshold)
                        continue;

                    // Increase density in cluster zones
                    if (combinedNoise > settings.clusterThreshold + 0.2f)
                    {
                        // In dense cluster - boost spawn probability
                        float clusterBoost = settings.clusterDensityMultiplier;
                        if (UnityEngine.Random.value > settings.spawnProbability * clusterBoost)
                            continue;
                    }
                    else
                    {
                        // Normal spawn probability
                        if (UnityEngine.Random.value > settings.spawnProbability)
                            continue;
                    }
                }
                else
                {
                    // No clustering - just use base probability
                    if (UnityEngine.Random.value > settings.spawnProbability)
                        continue;
                }

                // Check terrain height and slope
                if (!IsValidSpawnLocation(position, settings))
                    continue;

                // Check minimum distance from other objects (Poisson disk sampling)
                if (settings.usePoissonDisk)
                {
                    if (!CheckMinimumDistance(position, spawnedPositions, settings.minDistanceBetweenObjects))
                        continue;
                }

                // Select random asset
                var asset = SelectRandomAsset(assets);
                if (asset == null || asset.prefab == null)
                    continue;

                // Create spawn data
                Quaternion rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
                Vector3 scale = Vector3.one * UnityEngine.Random.Range(asset.minScale, asset.maxScale);

                var spawnData = new EnvironmentSpawnData
                {
                    position = position,
                    rotation = rotation,
                    scale = scale,
                    asset = asset,
                    chunkCoord = chunkCoord,
                    activeInstance = null
                };

                // If pooling disabled, spawn immediately (old behavior)
                if (!useObjectPooling)
                {
                    GameObject obj = SpawnEnvironmentObject(asset, position, chunkCoord);
                    if (obj != null)
                    {
                        obj.transform.rotation = rotation;
                        obj.transform.localScale = scale;
                        spawnData.activeInstance = obj;
                    }
                }

                chunkData.spawnPoints.Add(spawnData);
                spawnedPositions.Add(position);
                objectsSpawned++;

                // WebGL: Yield based on BOTH object count AND frame time budget
                if (objectsSpawned % maxObjectsPerFrame == 0)
                {
                    await UniTask.Yield();
                    frameTimer.Restart(); // Reset timer after yielding
                }
                else if (frameTimer.Elapsed.TotalMilliseconds > maxFrameTimeMs)
                {
                    // Exceeded frame time budget - yield to prevent lag
                    await UniTask.Yield();
                    frameTimer.Restart();
                }
            }
        }

        /// <summary>
        /// Spawn a single environment object (synchronous - batching handled by caller)
        /// </summary>
        private GameObject SpawnEnvironmentObject(EnvironmentAsset asset, Vector3 position, Vector2Int chunkCoord)
        {
            // Check if container was destroyed (scene transition/domain reload)
            if (environmentContainer == null)
            {
                Debug.LogWarning("[EnvironmentManager] Environment container was destroyed, reinitializing...");
                InitializeEnvironmentContainerAndAssets(CancellationToken.None);
                if (environmentContainer == null)
                {
                    Debug.LogError("[EnvironmentManager] Failed to reinitialize environment container!");
                    return null;
                }
            }

            // Adjust height to terrain surface
            float terrainHeight = GetTerrainHeightAtPosition(position);
            position.y = terrainHeight;

            // Instantiate object
            GameObject obj = UnityEngine.Object.Instantiate(asset.prefab, position, Quaternion.identity, environmentContainer.transform);

            // CRITICAL FIX FOR WEBGL: Replace URP/Lit shader with WebGL-compatible shader
            // URP/Lit has known compatibility issues in WebGL builds - materials can become invisible
            FixWebGLMaterials(obj);

            // Add LOD component for performance optimization (WebGL)
            // This will disable colliders/renderers based on distance from player
            if (enableLOD)
            {
                var lodComponent = obj.AddComponent<EnvironmentObjectLOD>();
                lodComponent.colliderCullingDistance = lodColliderCullingDistance;
                lodComponent.rendererCullingDistance = lodRendererCullingDistance;
                lodComponent.updateInterval = lodUpdateInterval;
                lodComponent.showDebugLogs = showDebugInfo;
                lodComponent.ForceUpdate(); // Initial update to set correct state

                if (showDebugInfo)
                {
                    Debug.Log($"[EnvironmentManager] Added LOD component to {asset.assetName} (Collider: {lodColliderCullingDistance}m, Renderer: {lodRendererCullingDistance}m)");
                }
            }

            // Random rotation (Y-axis only for most objects)
            float randomRotation = UnityEngine.Random.Range(0f, 360f);
            obj.transform.rotation = Quaternion.Euler(0, randomRotation, 0);

            // Random scale
            float randomScale = UnityEngine.Random.Range(asset.minScale, asset.maxScale);
            obj.transform.localScale = Vector3.one * randomScale;

            // Name for debugging
            obj.name = $"{asset.assetName}_Chunk{chunkCoord.x}_{chunkCoord.y}";

            // Add interactable component if not already present
            SetupInteractable(obj, asset);

            return obj;
        }

        /// <summary>
        /// Setup InteractableObject component on spawned environment objects
        /// </summary>
        private void SetupInteractable(GameObject obj, EnvironmentAsset asset)
        {
            // CRITICAL: Set object to Interactable layer (layer 8) for raycast detection
            obj.layer = LayerMask.NameToLayer("Interactable");
            if (obj.layer == -1)
            {
                Debug.LogWarning($"[EnvironmentManager] 'Interactable' layer not found! Object {obj.name} may not be detectable by InteractionManager.");
                obj.layer = 8; // Fallback to layer 8
            }

            // Ensure object has a collider for raycasting
            if (obj.GetComponent<Collider>() == null)
            {
                var collider = obj.AddComponent<BoxCollider>();
                // Auto-size the collider based on renderers
                var renderers = obj.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    collider.center = bounds.center - obj.transform.position;
                    collider.size = bounds.size;
                }
            }

            // Check if prefab already has InteractableObject component
            var interactable = obj.GetComponent<BugWars.Interaction.InteractableObject>();
            if (interactable != null)
                return; // Already configured on prefab

            // Add InteractableObject component dynamically
            interactable = obj.AddComponent<BugWars.Interaction.InteractableObject>();

            // Configure based on asset type
            BugWars.Interaction.InteractionType interactionType;
            BugWars.Interaction.ResourceType resourceType;
            int resourceAmount = 5;
            float harvestTime = 2f;
            string prompt = "Press E or Click";

            switch (asset.type)
            {
                case EnvironmentObjectType.Tree:
                    interactionType = BugWars.Interaction.InteractionType.Chop;
                    resourceType = BugWars.Interaction.ResourceType.Wood;
                    resourceAmount = UnityEngine.Random.Range(3, 8);
                    harvestTime = 3f;
                    break;
                case EnvironmentObjectType.Rock:
                    interactionType = BugWars.Interaction.InteractionType.Mine;
                    resourceType = BugWars.Interaction.ResourceType.Stone;
                    resourceAmount = UnityEngine.Random.Range(2, 6);
                    harvestTime = 4f;
                    break;
                case EnvironmentObjectType.Bush:
                    interactionType = BugWars.Interaction.InteractionType.Harvest;
                    resourceType = BugWars.Interaction.ResourceType.Berries;
                    resourceAmount = UnityEngine.Random.Range(1, 4);
                    harvestTime = 1.5f;
                    break;
                case EnvironmentObjectType.Grass:
                    interactionType = BugWars.Interaction.InteractionType.Harvest;
                    resourceType = BugWars.Interaction.ResourceType.Herbs;
                    resourceAmount = 1;
                    harvestTime = 0.5f;
                    break;
                default:
                    interactionType = BugWars.Interaction.InteractionType.Pickup;
                    resourceType = BugWars.Interaction.ResourceType.None;
                    break;
            }

            interactable.Configure(interactionType, resourceType, resourceAmount, harvestTime, prompt, asset.assetName);
            interactable.SetMessagePublisher(harvestedPublisher);
        }

        /// <summary>
        /// Handle object harvested messages and return to pool
        /// </summary>
        private void OnObjectHarvested(ObjectHarvestedMessage message)
        {
            if (message.GameObject == null)
            {
                Debug.LogWarning("[EnvironmentManager] Received harvest message with null GameObject");
                return;
            }

            if (useObjectPooling && objectPool != null)
            {
                objectPool.Return(message.GameObject, message.AssetName);
            }
            else
            {
                UnityEngine.Object.Destroy(message.GameObject);
            }
        }

        /// <summary>
        /// Unload environment objects for a specific chunk
        /// </summary>
        public void UnloadChunkEnvironment(Vector2Int chunkCoord)
        {
            if (!chunkEnvironmentData.TryGetValue(chunkCoord, out var chunkData))
                return;

            if (useObjectPooling)
            {
                // Return all active objects to pool
                foreach (var spawnData in chunkData.spawnPoints)
                {
                    if (spawnData.isActive)
                    {
                        DespawnObjectToPool(spawnData);
                    }
                }

                // Keep spawn data for re-use when chunk loads again
                // Only remove from dictionary
                chunkEnvironmentData.Remove(chunkCoord);
            }
            else
            {
                // Old behavior: Destroy all spawned objects
                foreach (var spawnData in chunkData.spawnPoints)
                {
                    if (spawnData.activeInstance != null)
                    {
                        UnityEngine.Object.Destroy(spawnData.activeInstance);
                    }
                }

                chunkData.spawnPoints.Clear();
                chunkEnvironmentData.Remove(chunkCoord);
            }
        }

        /// <summary>
        /// Get random position within a chunk
        /// </summary>
        private Vector3 GetRandomPositionInChunk(Vector2Int chunkCoord)
        {
            float chunkSize = 500f; // Match TerrainManager chunk size
            float randomX = UnityEngine.Random.Range(0f, chunkSize);
            float randomZ = UnityEngine.Random.Range(0f, chunkSize);

            return new Vector3(
                chunkCoord.x * chunkSize + randomX,
                0,
                chunkCoord.y * chunkSize + randomZ
            );
        }

        /// <summary>
        /// Get center position of a chunk
        /// </summary>
        private Vector3 GetChunkCenterPosition(Vector2Int chunkCoord)
        {
            float chunkSize = 500f; // Match TerrainManager chunk size
            return new Vector3(
                chunkCoord.x * chunkSize + chunkSize * 0.5f,
                0,
                chunkCoord.y * chunkSize + chunkSize * 0.5f
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
        /// Get terrain height at world position (OPTIMIZED: Use noise-based calculation to avoid raycasts)
        /// </summary>
        private float GetTerrainHeightAtPosition(Vector3 position)
        {
            // OPTIMIZED: Use noise-based height calculation instead of raycast
            // This matches the terrain generation and avoids expensive Physics.Raycast calls during spawning
            // TerrainManager uses the same noise algorithm, so this is accurate
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
        /// Recursively set layer on GameObject and all its children
        /// Critical for ensuring objects render properly (e.g., forcing Layer 0 for camera visibility)
        /// </summary>
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;

            obj.layer = layer;

            foreach (Transform child in obj.transform)
            {
                if (child != null)
                {
                    SetLayerRecursively(child.gameObject, layer);
                }
            }
        }

        /// <summary>
        /// Fix WebGL material compatibility issues
        /// Replace URP/Lit with custom BugWars/ForestEnvironment shader (uses same approach as character shaders)
        /// </summary>
        private void FixWebGLMaterials(GameObject obj)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Only run this fix in actual WebGL builds (not in editor)
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;

                    // Check if using URP/Lit shader
                    if (material.shader != null && material.shader.name == "Universal Render Pipeline/Lit")
                    {
                        // Store original properties
                        Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                        Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;

                        // Use the same shader approach as characters (BugWars/ForestEnvironment)
                        // This shader is proven to work in WebGL builds (based on PixelArtCharacter shader)
                        Shader webglShader = Shader.Find("BugWars/ForestEnvironment");

                        if (webglShader != null)
                        {
                            Debug.Log($"[EnvironmentManager] [WebGL Fix] Replacing shader: {material.shader.name} → {webglShader.name} on {renderer.gameObject.name}");

                            material.shader = webglShader;

                            // Restore color and texture (same property names)
                            if (material.HasProperty("_BaseMap") && baseMap != null)
                            {
                                material.SetTexture("_BaseMap", baseMap);
                            }
                            if (material.HasProperty("_BaseColor"))
                            {
                                material.SetColor("_BaseColor", baseColor);
                            }
                        }
                        else
                        {
                            Debug.LogError($"[EnvironmentManager] [WebGL Fix] BugWars/ForestEnvironment shader not found!");
                        }
                    }
                }
            }
#else
            // Not in WebGL build - no fix needed
            // Debug.Log($"[EnvironmentManager] Skipping WebGL material fix (not in WebGL build)");
#endif
        }

        /// <summary>
        /// Select random asset based on spawn weights (OPTIMIZED: Manual sum instead of LINQ)
        /// </summary>
        private EnvironmentAsset SelectRandomAsset(List<EnvironmentAsset> assets)
        {
            if (assets.Count == 0)
                return null;

            float totalWeight = 0f;
            for (int i = 0; i < assets.Count; i++)
            {
                totalWeight += assets[i].spawnWeight;
            }

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);

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
            Gizmos.color = Color.green;
            foreach (var chunkData in chunkEnvironmentData.Values)
            {
                foreach (var spawnData in chunkData.spawnPoints)
                {
                    // Draw active objects in green
                    if (spawnData.isActive && spawnData.activeInstance != null)
                    {
                        Gizmos.DrawWireSphere(spawnData.activeInstance.transform.position, 1f);
                    }
                    // Draw inactive spawn points in yellow
                    else
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(spawnData.position, 0.5f);
                        Gizmos.color = Color.green;
                    }
                }
            }
        }
        #endif

        /// <summary>
        /// Get total count of active spawned objects (OPTIMIZED: Manual count instead of LINQ)
        /// </summary>
        public int GetTotalObjectCount()
        {
            int total = 0;
            foreach (var chunkData in chunkEnvironmentData.Values)
            {
                foreach (var spawnData in chunkData.spawnPoints)
                {
                    if (spawnData.isActive)
                    {
                        total++;
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// Get total count of spawn points (both active and inactive)
        /// </summary>
        public int GetTotalSpawnPointCount()
        {
            int total = 0;
            foreach (var chunkData in chunkEnvironmentData.Values)
            {
                total += chunkData.spawnPoints.Count;
            }
            return total;
        }

        /// <summary>
        /// Force reload all chunk environments (for testing) (OPTIMIZED: Manual copy instead of LINQ)
        /// </summary>
        [ContextMenu("Reload All Environments")]
        public void ReloadAllEnvironments()
        {
            chunksToUnloadCache.Clear();
            foreach (var coord in chunkEnvironmentData.Keys)
            {
                chunksToUnloadCache.Add(coord);
            }

            foreach (var chunk in chunksToUnloadCache)
            {
                UnloadChunkEnvironment(chunk);
            }
        }

        /// <summary>
        /// Dispose pattern implementation for proper cleanup
        /// </summary>
        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _disposables?.Dispose();
            harvestSubscription?.Dispose();

            // Clean up object pool
            if (objectPool != null)
            {
                objectPool.Clear();
                objectPool = null;
            }

            if (environmentContainer != null)
            {
                UnityEngine.Object.Destroy(environmentContainer);
                environmentContainer = null;
            }

            chunkEnvironmentData?.Clear();
            treeAssets?.Clear();
            bushAssets?.Clear();
            rockAssets?.Clear();

            isInitialized = false;
        }
    }
}
