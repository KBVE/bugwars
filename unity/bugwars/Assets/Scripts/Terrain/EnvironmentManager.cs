using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
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
            // TIGHT CLUSTERING: Creates actual forest "zones" with 3-10 trees per cluster
            objectsPerChunk = 30,
            spawnProbability = 0.95f,  // High probability INSIDE clusters

            // Tight spacing within forest zones (trees close together like real forests)
            usePoissonDisk = true,
            minDistanceBetweenObjects = 5f,  // REDUCED: Trees closer together in forests
            maxPlacementAttempts = 40,        // More attempts to pack trees into clusters

            // REALISTIC BIOME CLUSTERING: Tight forest zones with 3-10 trees each
            enableClustering = true,
            clusterNoiseScale = 0.02f,        // LARGER-scale: Creates distinct forest "zones"
            clusterThreshold = 0.55f,         // HIGHER: Fewer but more concentrated clusters
            detailNoiseScale = 0.3f,          // HIGHER: More variation = smaller tight groups
            clusterDensityMultiplier = 4.0f,  // MUCH HIGHER: Pack 3-10 trees per zone

            minHeight = 0.5f,
            maxHeight = 8f,
            maxSlope = 35f,

            // More trees in valleys, fewer on peaks
            useHeightVariation = true,
            lowlandBonus = 1.3f,
            highlandPenalty = 0.5f
        };

        private readonly EnvironmentSpawnSettings bushSettings = new()
        {
            // TIGHT CLUSTERING: Creates understory "zones" with 3-8 bushes per cluster around forest areas
            objectsPerChunk = 40,
            spawnProbability = 0.9f,         // High probability INSIDE clusters

            // Tight spacing within undergrowth zones (bushes cluster like real forest understory)
            usePoissonDisk = true,
            minDistanceBetweenObjects = 2.5f, // REDUCED: Bushes very close together in undergrowth
            maxPlacementAttempts = 35,        // More attempts to pack bushes into clusters

            // REALISTIC BIOME CLUSTERING: Tight undergrowth zones with 3-8 bushes each
            enableClustering = true,
            clusterNoiseScale = 0.025f,       // LARGER-scale: Match tree zones (slightly offset)
            clusterThreshold = 0.50f,         // HIGHER: Fewer but more concentrated clusters
            detailNoiseScale = 0.35f,         // HIGHER: More variation = smaller tight groups
            clusterDensityMultiplier = 3.5f,  // HIGH: Pack 3-8 bushes per zone (understory layer)

            minHeight = 0.2f,
            maxHeight = 6f,
            maxSlope = 40f,

            useHeightVariation = true,
            lowlandBonus = 1.4f,
            highlandPenalty = 0.7f
        };

        private readonly EnvironmentSpawnSettings rockSettings = new()
        {
            // TIGHT CLUSTERING: Creates distinct rock "outcrops" with 2-6 rocks per formation
            objectsPerChunk = 18,
            spawnProbability = 0.85f,        // High probability INSIDE outcrops

            // Tight spacing within rock formations (rocks cluster like real geological outcrops)
            usePoissonDisk = true,
            minDistanceBetweenObjects = 3f,  // REDUCED: Rocks close together in formations
            maxPlacementAttempts = 30,       // More attempts to pack rocks into outcrops

            // REALISTIC BIOME CLUSTERING: Tight outcrop formations with 2-6 rocks each
            enableClustering = true,
            clusterNoiseScale = 0.04f,       // LARGER-scale: Creates distinct outcrop zones
            clusterThreshold = 0.60f,        // HIGHER: Fewer but more concentrated formations
            detailNoiseScale = 0.25f,        // HIGHER: More variation = smaller tight groups
            clusterDensityMultiplier = 3.0f, // HIGH: Pack 2-6 rocks per outcrop formation

            minHeight = 0f,
            maxHeight = 10f,
            maxSlope = 60f, // Rocks can spawn on steeper terrain

            // More rocks at higher elevations (rocky peaks)
            useHeightVariation = true,
            lowlandBonus = 0.8f,
            highlandPenalty = 1.5f  // BONUS on highlands (inverted from trees)
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

        // Performance
        private readonly bool useObjectPooling = false; // Future enhancement
        private readonly int maxObjectsPerFrame = 5; // WebGL: Very conservative - reduced from 50
        private readonly bool enableDynamicSpawning = true; // Spawn objects as chunks load

        // WebGL Performance
        private readonly float maxFrameTimeMs = 8.0f; // WebGL: Target 8ms budget (conservative for 60fps)
        private readonly int minFramesBetweenProcessing = 2; // WebGL: Minimum frames between chunk processing calls

        // Debug
        private readonly bool showDebugInfo = true;
        private readonly bool drawGizmos = false;

        // Dependencies
        private TerrainManager terrainManager;

        // Chunk tracking
        private Dictionary<Vector2Int, ChunkEnvironmentData> chunkEnvironmentData = new Dictionary<Vector2Int, ChunkEnvironmentData>();
        private GameObject environmentContainer;

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
        public void Construct(TerrainManager terrainMgr)
        {
            terrainManager = terrainMgr;
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
            InitializeEnvironmentContainerAndAssets();

            // Yield to prevent blocking
            await UniTask.Yield(cancellationToken);

            isInitialized = true;

            // Start the update loop using UniTask PlayerLoop
            UpdateLoop(cancellationToken).Forget();
        }

        /// <summary>
        /// Update loop using UniTask PlayerLoop instead of MonoBehaviour Update
        /// </summary>
        private async UniTaskVoid UpdateLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for next frame (equivalent to Update())
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                if (!isInitialized || !enableDynamicSpawning || terrainManager == null)
                    continue;

                // WebGL: Throttle processing to prevent every-frame execution
                int currentFrame = Time.frameCount;
                if (currentFrame - lastProcessingFrame < minFramesBetweenProcessing)
                    continue;

                // Check for new chunks that need environment objects (non-blocking)
                if (!isProcessingChunkLoading)
                {
                    lastProcessingFrame = currentFrame;
                    ProcessChunkEnvironmentLoading().Forget();
                }
            }
        }

        /// <summary>
        /// Synchronous initialization for recovery scenarios (when container is destroyed)
        /// </summary>
        private void InitializeEnvironmentContainerAndAssets()
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

            // Use same seed as terrain for consistency
            if (terrainManager != null)
            {
                spawnSeed = terrainManager.Seed;
            }

            // Load prefabs programmatically if arrays are empty (for dynamic instantiation via VContainer)
            if (treeAssets.Count == 0)
            {
                LoadEnvironmentPrefabs();
            }
        }

        /// <summary>
        /// Load environment prefabs from Resources folder
        /// Called automatically when EnvironmentManager is created dynamically
        /// </summary>
        private void LoadEnvironmentPrefabs()
        {
            // Load all tree prefabs
            GameObject[] treePrefabs = Resources.LoadAll<GameObject>("Prefabs/Forest/Trees");
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

            if (treeAssets.Count == 0 || bushAssets.Count == 0 || rockAssets.Count == 0)
            {
                Debug.LogWarning("[EnvironmentManager] Some prefab types failed to load! Make sure prefabs are in Resources/Prefabs/Forest/ folders.");
            }
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

            // Spawn environment objects by type
            await SpawnObjectsForChunk(chunkCoord, chunkData, EnvironmentObjectType.Tree, treeAssets, treeSettings);
            await SpawnObjectsForChunk(chunkCoord, chunkData, EnvironmentObjectType.Bush, bushAssets, bushSettings);
            await SpawnObjectsForChunk(chunkCoord, chunkData, EnvironmentObjectType.Rock, rockAssets, rockSettings);

            chunkData.isLoaded = true;
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

                // Spawn object (synchronous - batching happens below)
                GameObject spawnedObject = SpawnEnvironmentObject(asset, position, chunkCoord);
                if (spawnedObject != null)
                {
                    chunkData.spawnedObjects.Add(spawnedObject);
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
                InitializeEnvironmentContainerAndAssets();
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

            interactable.Configure(interactionType, resourceType, resourceAmount, harvestTime, prompt);
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
                    UnityEngine.Object.Destroy(obj);
                }
            }

            chunkData.spawnedObjects.Clear();
            chunkEnvironmentData.Remove(chunkCoord);
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
        /// Get total count of spawned objects (OPTIMIZED: Manual count instead of LINQ)
        /// </summary>
        public int GetTotalObjectCount()
        {
            int total = 0;
            foreach (var chunkData in chunkEnvironmentData.Values)
            {
                total += chunkData.spawnedObjects.Count;
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
