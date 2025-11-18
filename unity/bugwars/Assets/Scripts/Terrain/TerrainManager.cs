using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;
using BugWars.Core;

namespace BugWars.Terrain
{
    /// <summary>
    /// Manages procedural terrain generation using a chunk-based system
    /// Generates a 3x3 grid of low-poly terrain chunks around the player
    /// Uses Perlin noise for height generation with configurable seed
    /// Supports async chunk loading/unloading and frustum culling for performance
    /// </summary>
    public class TerrainManager : MonoBehaviour, IAsyncStartable
    {
        [Header("Terrain Generation Settings")]
        [SerializeField] private int seed = 12345;
        [SerializeField] private float noiseScale = 0.05f;
        [SerializeField] private int chunkGridSize = 3; // 3x3 grid (9 initial chunks) - reduced for better performance
        [SerializeField] private Material defaultTerrainMaterial;

        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 120; // Increased from 80 to 120 for better coverage
        [SerializeField] private int chunkResolution = 15; // Reduced from 20 to 15 for WebGL performance (225 verts vs 400)

        [Header("Chunk Loading/Unloading")]
        [SerializeField] private int hotChunkRadius = 6; // "Hot" chunks - CRITICAL 360° safety zone (loaded synchronously) - 13x13 grid = 169 chunks
        [SerializeField] private int warmChunkRadius = 12; // "Warm" chunks - visible/important area (high priority async)
        [SerializeField] private int coldChunkRadius = 18; // "Cold" chunks - preload area (low priority async)
        [SerializeField] private int chunkUnloadDistance = 25; // Unload chunks beyond this distance
        [SerializeField] private bool enableDynamicLoading = true; // Enable async chunk streaming
        [SerializeField] private bool enableFrustumCulling = true; // Enable camera frustum culling
        [SerializeField] private float cullingUpdateInterval = 0.5f; // How often to update culling (seconds)
        [SerializeField] private float chunkUpdateInterval = 0.25f; // Reduced frequency for WebGL (4 times per second instead of 10)

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        // Dependencies
        private CameraManager _cameraManager;
        private Transform _playerTransform; // Track player position for chunk streaming

        // Chunk management
        private Dictionary<Vector2Int, TerrainChunk> activeChunks = new Dictionary<Vector2Int, TerrainChunk>();
        private GameObject chunksContainer;
        private Vector2Int currentPlayerChunkCoord = Vector2Int.zero;

        // Generation state
        private bool isGenerating = false;
        private bool isInitialized = false;

        // Culling state
        private float lastCullingUpdate = 0f;
        private float lastChunkUpdate = 0f;

        /// <summary>
        /// VContainer dependency injection
        /// </summary>
        [Inject]
        public void Construct(CameraManager cameraManager)
        {
            _cameraManager = cameraManager;
        }

        /// <summary>
        /// VContainer async startup callback
        /// </summary>
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            Debug.Log("[TerrainManager] StartAsync called - initializing terrain system");
            InitializeTerrainSystem();

            Debug.Log("[TerrainManager] Generating initial terrain chunks");
            // Generate initial terrain chunks
            await GenerateInitialChunks();
            Debug.Log($"[TerrainManager] Initial terrain generation complete. Active chunks: {activeChunks.Count}");
        }

        private void Update()
        {
            if (!isInitialized)
                return;

            // Find player if not yet cached
            if (_playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                    Debug.Log("[TerrainManager] Found player for chunk streaming");
                }
            }

            // Continuously update chunks around player position
            if (_playerTransform != null && enableDynamicLoading && Time.time - lastChunkUpdate > chunkUpdateInterval)
            {
                UpdateChunksAroundPosition(_playerTransform.position).Forget();

                // Also update LOD levels based on current player position
                Vector2Int playerChunk = WorldPositionToChunkCoord(_playerTransform.position);
                UpdateChunkLODs(playerChunk);

                lastChunkUpdate = Time.time;
            }

            // Update frustum culling at intervals
            if (enableFrustumCulling && _cameraManager != null && Time.time - lastCullingUpdate > cullingUpdateInterval)
            {
                UpdateChunkVisibility();
                lastCullingUpdate = Time.time;
            }
        }

        private void InitializeTerrainSystem()
        {
            Debug.Log("[TerrainManager] Initializing terrain system");

            // Create container for all chunks as a ROOT GameObject
            // DontDestroyOnLoad only works on root GameObjects, not children
            chunksContainer = new GameObject("TerrainChunks");
            Debug.Log($"[TerrainManager] Created TerrainChunks container");

            // DO NOT parent to TerrainManager - must remain a root GameObject for DontDestroyOnLoad
            // Instead, mark it as DontDestroyOnLoad so it persists across scene loads
            DontDestroyOnLoad(chunksContainer);
            Debug.Log($"[TerrainManager] Set TerrainChunks to DontDestroyOnLoad (root GameObject)");

            // Create default material if none assigned
            if (defaultTerrainMaterial == null)
            {
                defaultTerrainMaterial = CreateDefaultGrasslandMaterial();
                Debug.Log($"[TerrainManager] Created default grassland material");
            }

            isInitialized = true;
            Debug.Log("[TerrainManager] Terrain system initialized");
        }

        /// <summary>
        /// Generates the initial 3x3 grid of chunks centered at (0,0)
        /// Creates 9 terrain chunks for immediate player area
        /// Player starts at center chunk (0,0)
        /// Total world size: 360x360 units (3 chunks * 120 units each)
        /// Additional chunks load dynamically as player moves
        /// </summary>
        public async UniTask GenerateInitialChunks()
        {
            if (isGenerating)
            {
                Debug.LogWarning("[TerrainManager] Already generating chunks");
                return;
            }

            isGenerating = true;

            int halfGrid = chunkGridSize / 2;

            // Generate center chunk first (synchronously) so player has ground immediately
            await GenerateChunk(Vector2Int.zero);

            // Generate remaining chunks in batches to prevent FPS spike
            List<Vector2Int> remainingChunks = new List<Vector2Int>();
            for (int x = -halfGrid; x <= halfGrid; x++)
            {
                for (int z = -halfGrid; z <= halfGrid; z++)
                {
                    Vector2Int chunkCoord = new Vector2Int(x, z);
                    if (chunkCoord != Vector2Int.zero) // Skip center (already generated)
                    {
                        remainingChunks.Add(chunkCoord);
                    }
                }
            }

            // Generate in very small batches (2 at a time) for WebGL - smoother frame rate
            int batchSize = 2;
            for (int i = 0; i < remainingChunks.Count; i += batchSize)
            {
                List<UniTask> batchTasks = new List<UniTask>();
                for (int j = i; j < Mathf.Min(i + batchSize, remainingChunks.Count); j++)
                {
                    batchTasks.Add(GenerateChunk(remainingChunks[j]));
                }

                await UniTask.WhenAll(batchTasks);

                // Yield multiple frames for WebGL (smoother experience)
                await UniTask.DelayFrame(2);
            }

            // Set initial LOD levels
            Vector2Int playerChunk = Vector2Int.zero;
            UpdateChunkLODs(playerChunk);

            isGenerating = false;

            if (showDebugInfo)
            {
                LogChunkGrid();
                Debug.Log($"[TerrainManager] Initial {activeChunks.Count} chunks generated with batched loading");
            }
        }

        /// <summary>
        /// Generate a single terrain chunk at the specified coordinates
        /// </summary>
        private async UniTask GenerateChunk(Vector2Int chunkCoord)
        {
            // Check if chunk already exists
            if (activeChunks.ContainsKey(chunkCoord))
            {
                Debug.LogWarning($"[TerrainManager] Chunk {chunkCoord} already exists");
                return;
            }

            // Verify chunksContainer exists
            if (chunksContainer == null)
            {
                Debug.LogError("[TerrainManager] chunksContainer is NULL! Cannot create terrain chunks.");
                return;
            }

            // Create chunk GameObject
            GameObject chunkObj = new GameObject($"TerrainChunk_{chunkCoord.x}_{chunkCoord.y}");
            chunkObj.transform.SetParent(chunksContainer.transform);
            Debug.Log($"[TerrainManager] Created GameObject for chunk {chunkCoord}, parent: {(chunkObj.transform.parent != null ? chunkObj.transform.parent.name : "NULL")}");

            // Add and initialize TerrainChunk component
            TerrainChunk chunk = chunkObj.AddComponent<TerrainChunk>();
            chunk.Initialize(chunkCoord, chunkSize, chunkResolution, seed, noiseScale, defaultTerrainMaterial);

            // Generate mesh asynchronously
            await chunk.GenerateMeshAsync();

            // Add to active chunks dictionary
            activeChunks[chunkCoord] = chunk;
            Debug.Log($"[TerrainManager] Chunk {chunkCoord} generated and added to activeChunks. GameObject active: {chunkObj.activeSelf}, GameObject exists: {chunkObj != null}");
        }

        /// <summary>
        /// Unload a specific chunk
        /// </summary>
        public void UnloadChunk(Vector2Int chunkCoord)
        {
            if (activeChunks.TryGetValue(chunkCoord, out TerrainChunk chunk))
            {
                chunk.Unload();
                Destroy(chunk.gameObject);
                activeChunks.Remove(chunkCoord);
            }
        }

        /// <summary>
        /// Update chunks based on player position - priority-based async chunk streaming
        /// Hot chunks (radius 6): Load synchronously to prevent falling through terrain in ANY direction (360°)
        /// - 13x13 grid = 169 chunks loaded BEFORE player can reach edge
        /// - Covers 720 units in all directions (6 chunks * 120 units)
        /// Warm chunks (radius 12): Load with high priority async for visible area
        /// Cold chunks (radius 18): Load with low priority async for preload area
        /// </summary>
        public async UniTask UpdateChunksAroundPosition(Vector3 playerPosition)
        {
            if (!enableDynamicLoading || isGenerating)
                return;

            // Calculate which chunk the player is in
            Vector2Int playerChunk = WorldPositionToChunkCoord(playerPosition);

            // Only update if player moved to a different chunk
            if (playerChunk == currentPlayerChunkCoord)
                return;

            currentPlayerChunkCoord = playerChunk;

            isGenerating = true;

            // === STEP 1: Unload distant chunks first ===
            List<Vector2Int> chunksToUnload = new List<Vector2Int>();
            foreach (var kvp in activeChunks)
            {
                Vector2Int chunkCoord = kvp.Key;
                int distance = Mathf.Max(
                    Mathf.Abs(chunkCoord.x - playerChunk.x),
                    Mathf.Abs(chunkCoord.y - playerChunk.y)
                );

                if (distance > chunkUnloadDistance)
                {
                    chunksToUnload.Add(chunkCoord);
                }
            }

            foreach (var chunkCoord in chunksToUnload)
            {
                UnloadChunk(chunkCoord);
            }

            // === STEP 2: Load HOT chunks synchronously (immediate vicinity) ===
            // These chunks MUST be loaded immediately to prevent falling through terrain
            // Radius 6 = 13x13 grid = 169 chunks = 720 units in ALL directions (360° coverage)
            int hotChunksLoaded = 0;
            for (int x = -hotChunkRadius; x <= hotChunkRadius; x++)
            {
                for (int z = -hotChunkRadius; z <= hotChunkRadius; z++)
                {
                    Vector2Int chunkCoord = playerChunk + new Vector2Int(x, z);
                    if (!activeChunks.ContainsKey(chunkCoord))
                    {
                        await GenerateChunk(chunkCoord); // Synchronous await - waits for completion
                        hotChunksLoaded++;
                    }
                }
            }

            if (hotChunksLoaded > 0 && showDebugInfo)
            {
                Debug.Log($"[TerrainManager] Loaded {hotChunksLoaded} hot chunks for 360° safety at player chunk {playerChunk}");
            }

            // Update LOD levels for all hot chunks to High
            UpdateChunkLODs(playerChunk);

            // === STEP 3: Load WARM chunks with high priority (parallel async) ===
            // These are chunks in the visible/important area around the player
            List<UniTask> warmTasks = new List<UniTask>();
            for (int x = -warmChunkRadius; x <= warmChunkRadius; x++)
            {
                for (int z = -warmChunkRadius; z <= warmChunkRadius; z++)
                {
                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));

                    // Skip if already covered by hot chunks
                    if (distance <= hotChunkRadius)
                        continue;

                    Vector2Int chunkCoord = playerChunk + new Vector2Int(x, z);
                    if (!activeChunks.ContainsKey(chunkCoord))
                    {
                        warmTasks.Add(GenerateChunk(chunkCoord));
                    }
                }
            }

            // Wait for all warm chunks to load in parallel
            if (warmTasks.Count > 0)
            {
                await UniTask.WhenAll(warmTasks);
            }

            // === STEP 4: Load COLD chunks with low priority (fire and forget) ===
            // These are preload chunks in the outer ring - load in background without blocking
            List<UniTask> coldTasks = new List<UniTask>();
            for (int x = -coldChunkRadius; x <= coldChunkRadius; x++)
            {
                for (int z = -coldChunkRadius; z <= coldChunkRadius; z++)
                {
                    int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));

                    // Skip if already covered by hot or warm chunks
                    if (distance <= warmChunkRadius)
                        continue;

                    Vector2Int chunkCoord = playerChunk + new Vector2Int(x, z);
                    if (!activeChunks.ContainsKey(chunkCoord))
                    {
                        coldTasks.Add(GenerateChunk(chunkCoord));
                    }
                }
            }

            // Start cold chunk loading but don't wait for completion (fire and forget)
            // They'll load in the background while player continues moving
            if (coldTasks.Count > 0)
            {
                UniTask.WhenAll(coldTasks).Forget();
            }

            isGenerating = false;
        }

        /// <summary>
        /// Update LOD levels for all chunks based on distance from player
        /// Hot zone (0-6): High LOD (full detail)
        /// Warm zone (7-12): Medium LOD (half resolution)
        /// Cold zone (13-18): Low LOD (quarter resolution)
        /// </summary>
        private void UpdateChunkLODs(Vector2Int playerChunk)
        {
            foreach (var kvp in activeChunks)
            {
                Vector2Int chunkCoord = kvp.Key;
                TerrainChunk chunk = kvp.Value;

                if (chunk == null || !chunk.IsGenerated)
                    continue;

                // Calculate Chebyshev distance (max of x and z distance)
                int distance = Mathf.Max(
                    Mathf.Abs(chunkCoord.x - playerChunk.x),
                    Mathf.Abs(chunkCoord.y - playerChunk.y)
                );

                // Assign LOD based on distance
                TerrainLOD targetLOD;
                if (distance <= hotChunkRadius)
                {
                    targetLOD = TerrainLOD.High; // Hot zone: full detail
                }
                else if (distance <= warmChunkRadius)
                {
                    targetLOD = TerrainLOD.Medium; // Warm zone: medium detail
                }
                else
                {
                    targetLOD = TerrainLOD.Low; // Cold zone: low detail
                }

                // Apply LOD change
                chunk.SetLOD(targetLOD);
            }
        }

        /// <summary>
        /// Update chunk visibility based on camera frustum culling
        /// Hides chunks outside the camera view for better performance
        /// </summary>
        private void UpdateChunkVisibility()
        {
            if (_cameraManager == null || _cameraManager.MainCamera == null)
                return;

            // Get frustum planes once for all chunks (optimization)
            Plane[] frustumPlanes = _cameraManager.GetFrustumPlanes();
            if (frustumPlanes == null)
                return;

            // Check each chunk's visibility
            foreach (var kvp in activeChunks)
            {
                TerrainChunk chunk = kvp.Value;
                if (chunk == null || !chunk.IsGenerated)
                    continue;

                // Check if chunk bounds are in camera frustum
                bool isVisible = _cameraManager.IsBoundsInFrustum(chunk.Bounds);

                // Update chunk visibility
                chunk.SetVisibility(isVisible);
            }
        }

        /// <summary>
        /// Convert world position to chunk coordinate
        /// </summary>
        private Vector2Int WorldPositionToChunkCoord(Vector3 worldPosition)
        {
            int chunkX = Mathf.FloorToInt(worldPosition.x / chunkSize);
            int chunkZ = Mathf.FloorToInt(worldPosition.z / chunkSize);
            return new Vector2Int(chunkX, chunkZ);
        }

        /// <summary>
        /// Get all active chunk coordinates
        /// </summary>
        public IReadOnlyCollection<Vector2Int> GetActiveChunkCoords()
        {
            return activeChunks.Keys;
        }

        /// <summary>
        /// Get chunk at specific coordinate
        /// </summary>
        public TerrainChunk GetChunk(Vector2Int coord)
        {
            activeChunks.TryGetValue(coord, out TerrainChunk chunk);
            return chunk;
        }

        /// <summary>
        /// Create a default grassland material if none is assigned
        /// Uses custom vertex color shader for terrain variation (dirt patches, rocky peaks)
        /// </summary>
        private Material CreateDefaultGrasslandMaterial()
        {
            // Try to use custom terrain shader first
            Shader terrainShader = Shader.Find("BugWars/TerrainVertexColor");

            if (terrainShader != null)
            {
                Material material = new Material(terrainShader);
                material.name = "Terrain Vertex Color Material";

                // Set shader properties for natural terrain look
                material.SetFloat("_ColorMultiplier", 1.0f);
                material.SetFloat("_Smoothness", 0.2f);
                material.SetFloat("_AmbientStrength", 0.5f); // Increased from 0.4 to reduce dark areas

                Debug.Log($"[TerrainManager] Successfully created material with custom shader: {terrainShader.name}");
                return material;
            }
            else
            {
                // Fallback to standard URP Lit shader if custom shader not found
                Debug.LogWarning("TerrainVertexColor shader not found. Using fallback URP/Lit shader. Terrain colors will not display correctly.");
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = "Default Grassland Material (Fallback)";
                material.color = new Color(0.3f, 0.6f, 0.2f, 1f);
                material.SetFloat("_Smoothness", 0.2f);
                material.SetFloat("_Metallic", 0f);

                return material;
            }
        }

        /// <summary>
        /// Regenerate all terrain with a new seed
        /// </summary>
        public async UniTask RegenerateWithNewSeed(int newSeed)
        {
            // Clear existing chunks
            foreach (var chunk in activeChunks.Values)
            {
                chunk.Unload();
                Destroy(chunk.gameObject);
            }
            activeChunks.Clear();

            // Update seed and regenerate
            seed = newSeed;
            await GenerateInitialChunks();
        }

        /// <summary>
        /// Debug method to visualize chunk grid in console
        /// </summary>
        private void LogChunkGrid()
        {
            // Debug logging removed - use defensive logging only
        }

        /// <summary>
        /// Public getter for seed (useful for debugging/saving)
        /// </summary>
        public int Seed => seed;

        /// <summary>
        /// Public getter for chunk count
        /// </summary>
        public int ActiveChunkCount => activeChunks.Count;

        /// <summary>
        /// Check if terrain is ready
        /// </summary>
        public bool IsReady => isInitialized && !isGenerating && activeChunks.Count > 0;

        /// <summary>
        /// Get count of visible chunks (for debugging/stats)
        /// </summary>
        public int VisibleChunkCount
        {
            get
            {
                int count = 0;
                foreach (var chunk in activeChunks.Values)
                {
                    if (chunk.IsVisible) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Manually trigger chunk visibility update (useful for camera changes)
        /// </summary>
        public void ForceUpdateChunkVisibility()
        {
            UpdateChunkVisibility();
        }

        /// <summary>
        /// Get the center position of the terrain (center of all active chunks)
        /// </summary>
        public Vector3 GetTerrainCenter()
        {
            if (activeChunks.Count == 0)
            {
                return Vector3.zero;
            }

            // Calculate bounds of all active chunks
            Vector3 min = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, 0, float.MinValue);

            foreach (var kvp in activeChunks)
            {
                Vector2Int coord = kvp.Key;
                Vector3 chunkMin = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
                Vector3 chunkMax = new Vector3((coord.x + 1) * chunkSize, 0, (coord.y + 1) * chunkSize);

                min = Vector3.Min(min, chunkMin);
                max = Vector3.Max(max, chunkMax);
            }

            return (min + max) * 0.5f;
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showDebugInfo || !isInitialized) return;

            // Draw chunk boundaries
            Gizmos.color = Color.yellow;
            foreach (var kvp in activeChunks)
            {
                Vector2Int coord = kvp.Key;
                Vector3 center = new Vector3(
                    coord.x * chunkSize + chunkSize * 0.5f,
                    0,
                    coord.y * chunkSize + chunkSize * 0.5f
                );

                Gizmos.DrawWireCube(center, new Vector3(chunkSize, 0.1f, chunkSize));
            }

            // Draw player chunk in different color
            Gizmos.color = Color.green;
            Vector3 playerChunkCenter = new Vector3(
                currentPlayerChunkCoord.x * chunkSize + chunkSize * 0.5f,
                0,
                currentPlayerChunkCoord.y * chunkSize + chunkSize * 0.5f
            );
            Gizmos.DrawWireCube(playerChunkCenter, new Vector3(chunkSize, 1f, chunkSize));
        }
        #endif
    }
}
