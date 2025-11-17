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

            public MeshData(int vertexCount, int triangleCount)
            {
                vertices = new Vector3[vertexCount];
                triangles = new int[triangleCount];
                uvs = new Vector2[vertexCount];
            }
        }
    }
}
