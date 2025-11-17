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
        [SerializeField] private int chunkSize = 80; // Quadrupled from 20 to 80
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
        public bool IsVisible { get; private set; } = true;
        public Bounds Bounds { get; private set; }

        /// <summary>
        /// Initialize the chunk with generation parameters
        /// </summary>
        public void Initialize(Vector2Int coord, int size, int res, int terrainSeed, float scale, Material material)
        {
            chunkCoord = coord;
            chunkSize = size;
            resolution = res;
            seed = terrainSeed;
            noiseScale = scale;
            terrainMaterial = material;

            // Setup mesh components
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshCollider = gameObject.AddComponent<MeshCollider>();

            // Configure mesh collider for terrain surface
            meshCollider.convex = false; // Non-convex for accurate terrain collision

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
        /// Creates a simple top surface terrain plane
        /// </summary>
        private MeshData GenerateMeshData()
        {
            // Calculate array sizes for just the top surface
            int vertCount = resolution * resolution;
            int triCount = (resolution - 1) * (resolution - 1) * 6;

            MeshData meshData = new MeshData(vertCount, triCount);

            float halfSize = chunkSize * 0.5f;
            float stepSize = chunkSize / (float)(resolution - 1);

            int vertexIndex = 0;
            int triIndex = 0;

            // === GENERATE TOP SURFACE ===
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Calculate world position for noise sampling
                    float worldX = chunkCoord.x * chunkSize + x * stepSize;
                    float worldZ = chunkCoord.y * chunkSize + y * stepSize;

                    float seedOffsetX = seed * 0.1f;
                    float seedOffsetY = seed * 0.1f;
                    float height = Mathf.PerlinNoise(
                        (worldX + seedOffsetX) * noiseScale,
                        (worldZ + seedOffsetY) * noiseScale
                    ) * heightMultiplier;

                    Vector3 position = new Vector3(
                        x * stepSize - halfSize,
                        height,
                        y * stepSize - halfSize
                    );

                    meshData.vertices[vertexIndex] = position;
                    meshData.uvs[vertexIndex] = new Vector2(x / (float)(resolution - 1), y / (float)(resolution - 1));

                    // Calculate and assign vertex color for terrain variation
                    meshData.colors[vertexIndex] = CalculateTerrainColor(worldX, worldZ, height);

                    // Generate triangles for the current quad
                    if (x < resolution - 1 && y < resolution - 1)
                    {
                        int i = vertexIndex;
                        // First triangle
                        meshData.triangles[triIndex++] = i;
                        meshData.triangles[triIndex++] = i + resolution;
                        meshData.triangles[triIndex++] = i + resolution + 1;
                        // Second triangle
                        meshData.triangles[triIndex++] = i;
                        meshData.triangles[triIndex++] = i + resolution + 1;
                        meshData.triangles[triIndex++] = i + 1;
                    }

                    vertexIndex++;
                }
            }

            return meshData;
        }

        /// <summary>
        /// Calculate terrain color based on height and noise for natural variation
        /// Creates dirt patches, rocky peaks, and grass valleys
        /// </summary>
        private Color CalculateTerrainColor(float worldX, float worldZ, float height)
        {
            // Define base terrain colors
            Color grassColor = new Color(0.3f, 0.6f, 0.2f, 1f);      // Grass green
            Color dirtColor = new Color(0.5f, 0.35f, 0.2f, 1f);       // Brown dirt
            Color darkDirtColor = new Color(0.35f, 0.25f, 0.15f, 1f); // Darker dirt for variation
            Color rockColor = new Color(0.4f, 0.4f, 0.4f, 1f);        // Gray rock

            // Use multiple noise layers for natural-looking dirt patches
            float dirtNoise = Mathf.PerlinNoise(worldX * 0.1f + seed, worldZ * 0.1f + seed);
            float detailNoise = Mathf.PerlinNoise(worldX * 0.3f - seed, worldZ * 0.3f - seed);

            // Combine noise layers for more organic patches
            float combinedNoise = dirtNoise * 0.7f + detailNoise * 0.3f;

            // Height-based variation (normalized 0-1)
            float heightFactor = Mathf.InverseLerp(0f, heightMultiplier, height);

            // Start with grass as base
            Color result = grassColor;

            // Add dirt patches where noise is high (creates irregular dirt areas)
            if (combinedNoise > 0.5f)
            {
                float dirtAmount = Mathf.InverseLerp(0.5f, 0.7f, combinedNoise);
                result = Color.Lerp(grassColor, dirtColor, dirtAmount);
            }

            // Add darker dirt variation for more visual interest
            if (combinedNoise > 0.7f)
            {
                float darkDirtAmount = Mathf.InverseLerp(0.7f, 0.85f, combinedNoise);
                result = Color.Lerp(result, darkDirtColor, darkDirtAmount * 0.5f);
            }

            // Add rocky color on peaks (exponential curve for realistic distribution)
            if (heightFactor > 0.6f)
            {
                float rockAmount = Mathf.Pow(Mathf.InverseLerp(0.6f, 1.0f, heightFactor), 2f);
                result = Color.Lerp(result, rockColor, rockAmount);
            }

            // Darken valleys slightly for depth perception (reduced to avoid black lines)
            if (heightFactor < 0.3f)
            {
                float valleyDarken = Mathf.InverseLerp(0.3f, 0f, heightFactor) * 0.08f; // Reduced from 0.15f
                result = Color.Lerp(result, Color.black, valleyDarken);
            }

            return result;
        }

        /// <summary>
        /// Smooth normals at chunk edges to reduce visible seams between chunks
        /// Uses grid-based detection to reliably identify edge vertices
        /// </summary>
        private void SmoothEdgeNormals(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;

            float stepSize = chunkSize / (float)(resolution - 1);

            // Process vertices in a grid pattern to identify edges
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = y * resolution + x;

                    // Check if this vertex is on any edge of the chunk
                    bool isOnEdgeX = (x == 0 || x == resolution - 1);
                    bool isOnEdgeZ = (y == 0 || y == resolution - 1);

                    if (isOnEdgeX || isOnEdgeZ)
                    {
                        // Force edge normals to point straight up
                        // This ensures consistent lighting across chunk boundaries
                        normals[index] = Vector3.up;
                    }
                    else if (x == 1 || x == resolution - 2 || y == 1 || y == resolution - 2)
                    {
                        // Smooth vertices adjacent to edges (one step in)
                        normals[index] = Vector3.Lerp(normals[index], Vector3.up, 0.5f).normalized;
                    }
                }
            }

            mesh.normals = normals;
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
            mesh.colors = meshData.colors;

            // Use smooth normals to prevent visible seams between chunks
            mesh.RecalculateNormals();

            // Optionally apply normal smoothing for better chunk edge blending
            // This helps reduce the greenish lines at chunk boundaries
            SmoothEdgeNormals(mesh);

            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;

            // Cache the bounds for frustum culling
            Bounds = meshRenderer.bounds;
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
        /// Show the chunk (enable renderer)
        /// </summary>
        public void Show()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
                IsVisible = true;
            }
        }

        /// <summary>
        /// Hide the chunk (disable renderer) - useful for frustum culling
        /// </summary>
        public void Hide()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
                IsVisible = false;
            }
        }

        /// <summary>
        /// Set chunk visibility based on frustum culling
        /// </summary>
        public void SetVisibility(bool visible)
        {
            if (visible)
                Show();
            else
                Hide();
        }

        /// <summary>
        /// Get the center position of this chunk in world space
        /// </summary>
        public Vector3 GetCenterPosition()
        {
            return transform.position + new Vector3(chunkSize * 0.5f, 0, chunkSize * 0.5f);
        }

        /// <summary>
        /// Calculate distance from a point to this chunk's center
        /// </summary>
        public float DistanceFromPoint(Vector3 point)
        {
            return Vector3.Distance(GetCenterPosition(), point);
        }

        /// <summary>
        /// Helper class to store mesh data for background generation
        /// </summary>
        private class MeshData
        {
            public Vector3[] vertices;
            public int[] triangles;
            public Vector2[] uvs;
            public Color[] colors;

            public MeshData(int vertexCount, int triangleCount)
            {
                vertices = new Vector3[vertexCount];
                triangles = new int[triangleCount];
                uvs = new Vector2[vertexCount];
                colors = new Color[vertexCount];
            }
        }
    }
}
