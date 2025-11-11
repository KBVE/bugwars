using UnityEngine;
using Cysharp.Threading.Tasks;

namespace BugWars.Terrain
{
    /// <summary>
    /// Represents a single terrain chunk with low-poly mesh generation
    /// Handles mesh creation and rendering for procedurally generated terrain
    /// </summary>
    public class TerrainChunk : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 20;
        [SerializeField] private int resolution = 20; // Vertices per side
        [SerializeField] private float heightMultiplier = 3f;
        [SerializeField] private Material terrainMaterial;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private Vector2Int chunkCoord;
        private float noiseScale;
        private int seed;

        public Vector2Int ChunkCoord => chunkCoord;
        public bool IsGenerated { get; private set; }

        /// <summary>
        /// Initialize the chunk with generation parameters
        /// </summary>
        public void Initialize(Vector2Int coord, int terrainSeed, float scale, Material material)
        {
            chunkCoord = coord;
            seed = terrainSeed;
            noiseScale = scale;
            terrainMaterial = material;

            // Setup mesh components
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshCollider = gameObject.AddComponent<MeshCollider>();

            if (terrainMaterial != null)
            {
                meshRenderer.material = terrainMaterial;
            }

            // Position chunk based on coordinates
            transform.position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
            gameObject.name = $"TerrainChunk_{coord.x}_{coord.y}";
        }

        /// <summary>
        /// Generate the terrain mesh asynchronously using UniTask
        /// </summary>
        public async UniTask GenerateMeshAsync()
        {
            if (IsGenerated) return;

            // Move mesh generation to background thread to avoid blocking main thread
            var meshData = await UniTask.RunOnThreadPool(() => GenerateMeshData());

            // Apply mesh on main thread (Unity requirement)
            ApplyMesh(meshData);

            IsGenerated = true;
        }

        /// <summary>
        /// Generates mesh data using Perlin noise for terrain height
        /// This runs on a background thread for performance
        /// </summary>
        private MeshData GenerateMeshData()
        {
            MeshData meshData = new MeshData(resolution);

            float halfSize = chunkSize * 0.5f;
            float stepSize = chunkSize / (float)(resolution - 1);

            int vertexIndex = 0;

            // Generate vertices with height from Perlin noise
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Calculate world position for noise sampling
                    float worldX = chunkCoord.x * chunkSize + x * stepSize;
                    float worldZ = chunkCoord.y * chunkSize + y * stepSize;

                    // Sample Perlin noise for height with seed offset
                    float seedOffsetX = seed * 0.1f;
                    float seedOffsetY = seed * 0.1f;
                    float height = Mathf.PerlinNoise(
                        (worldX + seedOffsetX) * noiseScale,
                        (worldZ + seedOffsetY) * noiseScale
                    ) * heightMultiplier;

                    // Create vertex position (centered around chunk origin)
                    Vector3 vertexPosition = new Vector3(
                        x * stepSize - halfSize,
                        height,
                        y * stepSize - halfSize
                    );

                    meshData.vertices[vertexIndex] = vertexPosition;

                    // Simple UV mapping
                    meshData.uvs[vertexIndex] = new Vector2(
                        x / (float)(resolution - 1),
                        y / (float)(resolution - 1)
                    );

                    // Generate triangles (2 triangles per quad, skip last row/column)
                    if (x < resolution - 1 && y < resolution - 1)
                    {
                        int triangleIndex = (y * (resolution - 1) + x) * 6;

                        // First triangle
                        meshData.triangles[triangleIndex] = vertexIndex;
                        meshData.triangles[triangleIndex + 1] = vertexIndex + resolution;
                        meshData.triangles[triangleIndex + 2] = vertexIndex + resolution + 1;

                        // Second triangle
                        meshData.triangles[triangleIndex + 3] = vertexIndex;
                        meshData.triangles[triangleIndex + 4] = vertexIndex + resolution + 1;
                        meshData.triangles[triangleIndex + 5] = vertexIndex + 1;
                    }

                    vertexIndex++;
                }
            }

            return meshData;
        }

        /// <summary>
        /// Apply generated mesh data to Unity mesh components
        /// Must be called on main thread
        /// </summary>
        private void ApplyMesh(MeshData meshData)
        {
            Mesh mesh = new Mesh();
            mesh.name = $"TerrainChunk_{chunkCoord.x}_{chunkCoord.y}";
            mesh.vertices = meshData.vertices;
            mesh.triangles = meshData.triangles;
            mesh.uv = meshData.uvs;

            // Calculate normals for proper lighting (low-poly flat shading)
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;
        }

        /// <summary>
        /// Unload chunk and cleanup resources
        /// </summary>
        public void Unload()
        {
            if (meshFilter.mesh != null)
            {
                Destroy(meshFilter.mesh);
            }

            IsGenerated = false;
        }

        /// <summary>
        /// Helper class to store mesh data for background generation
        /// </summary>
        private class MeshData
        {
            public Vector3[] vertices;
            public int[] triangles;
            public Vector2[] uvs;

            public MeshData(int resolution)
            {
                vertices = new Vector3[resolution * resolution];
                triangles = new int[(resolution - 1) * (resolution - 1) * 6];
                uvs = new Vector2[resolution * resolution];
            }
        }
    }
}
