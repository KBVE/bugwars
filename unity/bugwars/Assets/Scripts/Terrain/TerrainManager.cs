using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace BugWars.Terrain
{
    /// <summary>
    /// Manages procedural terrain generation using a chunk-based system
    /// Generates a 3x3 grid of low-poly terrain chunks around the player
    /// Uses Perlin noise for height generation with configurable seed
    /// </summary>
    public class TerrainManager : MonoBehaviour, IAsyncStartable
    {
        [Header("Terrain Generation Settings")]
        [SerializeField] private int seed = 12345;
        [SerializeField] private float noiseScale = 0.05f;
        [SerializeField] private int chunkGridSize = 3; // 3x3 grid
        [SerializeField] private Material defaultTerrainMaterial;

        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 20;
        [SerializeField] private int chunkResolution = 20; // Vertices per chunk side

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        // Chunk management
        private Dictionary<Vector2Int, TerrainChunk> activeChunks = new Dictionary<Vector2Int, TerrainChunk>();
        private GameObject chunksContainer;
        private Vector2Int currentPlayerChunkCoord = Vector2Int.zero;

        // Generation state
        private bool isGenerating = false;
        private bool isInitialized = false;

        /// <summary>
        /// VContainer async startup callback
        /// </summary>
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            InitializeTerrainSystem();

            // Generate initial terrain chunks
            await GenerateInitialChunks();
        }

        private void InitializeTerrainSystem()
        {
            // Create container for all chunks
            chunksContainer = new GameObject("TerrainChunks");

            // Only set parent if this component has a valid transform
            if (this != null && gameObject != null)
            {
                chunksContainer.transform.SetParent(transform);
            }

            // Create default material if none assigned
            if (defaultTerrainMaterial == null)
            {
                defaultTerrainMaterial = CreateDefaultGrasslandMaterial();
            }

            isInitialized = true;
        }

        /// <summary>
        /// Generates the initial 3x3 grid of chunks centered at (0,0)
        /// Layout:
        /// A(-1,1)  B(0,1)  C(1,1)
        /// D(-1,0)  E(0,0)  F(1,0)  <- Player starts at E(0,0)
        /// G(-1,-1) H(0,-1) I(1,-1)
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

            // Create chunk GameObject
            GameObject chunkObj = new GameObject();
            chunkObj.transform.SetParent(chunksContainer.transform);

            // Add and initialize TerrainChunk component
            TerrainChunk chunk = chunkObj.AddComponent<TerrainChunk>();
            chunk.Initialize(chunkCoord, chunkSize, chunkResolution, seed, noiseScale, defaultTerrainMaterial);

            // Generate mesh asynchronously
            await chunk.GenerateMeshAsync();

            // Add to active chunks dictionary
            activeChunks[chunkCoord] = chunk;
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
        /// Update chunks based on player position (for future chunk streaming)
        /// This will be used when implementing dynamic chunk loading/unloading
        /// </summary>
        public async UniTask UpdateChunksAroundPosition(Vector3 playerPosition)
        {
            // Calculate which chunk the player is in
            Vector2Int playerChunk = WorldPositionToChunkCoord(playerPosition);

            // Only update if player moved to a different chunk
            if (playerChunk == currentPlayerChunkCoord)
                return;

            currentPlayerChunkCoord = playerChunk;

            // TODO: Implement chunk loading/unloading based on distance
            // For now, this is a placeholder for future expansion
            await UniTask.CompletedTask;
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
        /// Create a default grassland material if none is assigned
        /// </summary>
        private Material CreateDefaultGrasslandMaterial()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "Default Grassland Material";

            // Set grassland green color
            material.color = new Color(0.3f, 0.6f, 0.2f, 1f);

            // Enable smoothness for low-poly look
            material.SetFloat("_Smoothness", 0.2f);
            material.SetFloat("_Metallic", 0f);

            return material;
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
