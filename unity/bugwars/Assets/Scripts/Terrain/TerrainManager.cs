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
        [SerializeField] private int chunkGridSize = 7; // 7x7 grid (49 initial chunks)
        [SerializeField] private Material defaultTerrainMaterial;

        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 80; // Quadrupled from 20 to 80
        [SerializeField] private int chunkResolution = 20; // Vertices per chunk side

        [Header("Chunk Loading/Unloading")]
        [SerializeField] private int chunkLoadDistance = 4; // Load chunks within this distance (in chunk units)
        [SerializeField] private int chunkUnloadDistance = 6; // Unload chunks beyond this distance
        [SerializeField] private bool enableDynamicLoading = true; // Enable async chunk streaming
        [SerializeField] private bool enableFrustumCulling = true; // Enable camera frustum culling
        [SerializeField] private float cullingUpdateInterval = 0.5f; // How often to update culling (seconds)

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        // Dependencies
        private CameraManager _cameraManager;

        // Chunk management
        private Dictionary<Vector2Int, TerrainChunk> activeChunks = new Dictionary<Vector2Int, TerrainChunk>();
        private GameObject chunksContainer;
        private Vector2Int currentPlayerChunkCoord = Vector2Int.zero;

        // Generation state
        private bool isGenerating = false;
        private bool isInitialized = false;

        // Culling state
        private float lastCullingUpdate = 0f;

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
            if (!isInitialized || !enableFrustumCulling || _cameraManager == null)
                return;

            // Update frustum culling at intervals
            if (Time.time - lastCullingUpdate > cullingUpdateInterval)
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
        /// Generates the initial 7x7 grid of chunks centered at (0,0)
        /// Creates 49 terrain chunks for a large explorable world
        /// Player starts at center chunk (0,0)
        /// Total world size: 560x560 units (7 chunks * 80 units each)
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
            List<UniTask> generationTasks = new List<UniTask>();

            // Generate chunks in a grid centered around player position (0,0)
            for (int x = -halfGrid; x <= halfGrid; x++)
            {
                for (int z = -halfGrid; z <= halfGrid; z++)
                {
                    Vector2Int chunkCoord = new Vector2Int(x, z);
                    generationTasks.Add(GenerateChunk(chunkCoord));
                }
            }

            // Wait for all chunks to generate in parallel
            await UniTask.WhenAll(generationTasks);

            isGenerating = false;

            if (showDebugInfo)
            {
                LogChunkGrid();
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
        /// Update chunks based on player position - async chunk streaming
        /// Loads chunks within load distance and unloads chunks beyond unload distance
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

            // Determine which chunks should be loaded
            HashSet<Vector2Int> chunksToLoad = new HashSet<Vector2Int>();
            for (int x = -chunkLoadDistance; x <= chunkLoadDistance; x++)
            {
                for (int z = -chunkLoadDistance; z <= chunkLoadDistance; z++)
                {
                    Vector2Int chunkCoord = playerChunk + new Vector2Int(x, z);
                    chunksToLoad.Add(chunkCoord);
                }
            }

            // Unload chunks that are too far away
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

            // Unload distant chunks
            foreach (var chunkCoord in chunksToUnload)
            {
                UnloadChunk(chunkCoord);
            }

            // Load new chunks asynchronously
            List<UniTask> loadTasks = new List<UniTask>();
            foreach (var chunkCoord in chunksToLoad)
            {
                if (!activeChunks.ContainsKey(chunkCoord))
                {
                    loadTasks.Add(GenerateChunk(chunkCoord));
                }
            }

            // Wait for all new chunks to load
            if (loadTasks.Count > 0)
            {
                await UniTask.WhenAll(loadTasks);
            }

            isGenerating = false;
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
                material.SetFloat("_AmbientStrength", 0.4f);

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
