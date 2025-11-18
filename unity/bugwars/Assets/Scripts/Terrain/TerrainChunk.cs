using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace BugWars.Terrain
{
    /// <summary>
    /// Level of Detail for terrain chunks
    /// </summary>
    public enum TerrainLOD
    {
        High = 0,   // Full resolution (20x20 vertices) - Hot chunks
        Medium = 1, // Half resolution (10x10 vertices) - Warm chunks
        Low = 2     // Quarter resolution (5x5 vertices) - Cold chunks
    }

    /// <summary>
    /// Represents a single terrain chunk with low-poly mesh generation
    /// Handles mesh creation and rendering for procedurally generated terrain
    /// Supports multiple LOD levels for performance optimization
    /// </summary>
    public class TerrainChunk : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 120; // Increased from 80 to 120 for better coverage
        [SerializeField] private int resolution = 15; // Reduced from 20 to 15 for WebGL performance
        [SerializeField] private float heightMultiplier = 3f;
        [SerializeField] private Material terrainMaterial;

        [Header("Fade-In Settings")]
        [SerializeField] private bool enableFadeIn = false; // Disabled by default for WebGL performance
        [SerializeField] private float fadeInDuration = 0.3f; // Smooth fade-in over 0.3 seconds

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private Vector2Int chunkCoord;
        private float noiseScale;
        private int seed;

        // Fade-in state
        private float fadeTimer = 0f;
        private Material instanceMaterial; // Instance material for per-chunk fade
        private static readonly int ColorMultiplierProperty = Shader.PropertyToID("_ColorMultiplier");

        // LOD state
        private TerrainLOD currentLOD = TerrainLOD.Low; // Start at low LOD
        private Dictionary<TerrainLOD, Mesh> lodMeshes = new Dictionary<TerrainLOD, Mesh>(); // Cache meshes for each LOD

        public Vector2Int ChunkCoord => chunkCoord;
        public bool IsGenerated { get; private set; }
        public bool IsVisible { get; private set; } = true;
        public Bounds Bounds { get; private set; }
        public TerrainLOD CurrentLOD => currentLOD;

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
                // Create an instance of the material for per-chunk fade control
                instanceMaterial = new Material(terrainMaterial);
                meshRenderer.material = instanceMaterial;
            }

            // Position chunk based on coordinates
            transform.position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
            gameObject.name = $"TerrainChunk_{coord.x}_{coord.y}";
        }

        /// <summary>
        /// Update fade-in animation
        /// </summary>
        private void Update()
        {
            // Handle fade-in animation
            if (enableFadeIn && fadeTimer < fadeInDuration && IsGenerated)
            {
                fadeTimer += Time.deltaTime;
                float fadeProgress = Mathf.Clamp01(fadeTimer / fadeInDuration);

                // Smooth fade using easing function
                float easedProgress = Mathf.SmoothStep(0f, 1f, fadeProgress);

                // Update material color multiplier for fade effect
                if (instanceMaterial != null)
                {
                    instanceMaterial.SetFloat(ColorMultiplierProperty, easedProgress);
                }
            }
        }

        /// <summary>
        /// Generate the terrain mesh asynchronously using UniTask
        /// Only generates High LOD initially - other LODs generated on-demand
        /// </summary>
        public async UniTask GenerateMeshAsync()
        {
            if (IsGenerated) return;

            // Only generate High LOD mesh immediately - this is critical for collision
            var meshData = await UniTask.RunOnThreadPool(() => GenerateMeshData(TerrainLOD.High));

            // Create and cache the high LOD mesh
            Mesh highMesh = CreateMeshFromData(meshData, TerrainLOD.High);
            lodMeshes[TerrainLOD.High] = highMesh;

            // Set to high LOD initially (will be updated based on distance)
            currentLOD = TerrainLOD.High;
            meshFilter.mesh = highMesh;
            meshCollider.sharedMesh = highMesh;
            Bounds = meshRenderer.bounds;

            // Initialize fade-in
            if (enableFadeIn && instanceMaterial != null)
            {
                fadeTimer = 0f;
                instanceMaterial.SetFloat(ColorMultiplierProperty, 0f);
            }

            IsGenerated = true;
        }

        /// <summary>
        /// Generates mesh data using Perlin noise for terrain height
        /// This runs on a background thread for performance
        /// Creates a simple top surface terrain plane
        /// </summary>
        private MeshData GenerateMeshData(TerrainLOD lod)
        {
            // Calculate resolution based on LOD level
            int lodResolution = lod switch
            {
                TerrainLOD.High => resolution,         // 20x20 vertices
                TerrainLOD.Medium => resolution / 2,   // 10x10 vertices
                TerrainLOD.Low => resolution / 4,      // 5x5 vertices
                _ => resolution
            };

            // Ensure minimum resolution of 2x2
            lodResolution = Mathf.Max(2, lodResolution);

            // Calculate array sizes for just the top surface
            int vertCount = lodResolution * lodResolution;
            int triCount = (lodResolution - 1) * (lodResolution - 1) * 6;

            MeshData meshData = new MeshData(vertCount, triCount);

            float halfSize = chunkSize * 0.5f;
            float stepSize = chunkSize / (float)(lodResolution - 1);

            int vertexIndex = 0;
            int triIndex = 0;

            // === GENERATE TOP SURFACE ===
            for (int y = 0; y < lodResolution; y++)
            {
                for (int x = 0; x < lodResolution; x++)
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
                    meshData.uvs[vertexIndex] = new Vector2(x / (float)(lodResolution - 1), y / (float)(lodResolution - 1));

                    // Calculate and assign vertex color for terrain variation
                    meshData.colors[vertexIndex] = CalculateTerrainColor(worldX, worldZ, height);

                    // Generate triangles for the current quad
                    if (x < lodResolution - 1 && y < lodResolution - 1)
                    {
                        int i = vertexIndex;
                        // First triangle
                        meshData.triangles[triIndex++] = i;
                        meshData.triangles[triIndex++] = i + lodResolution;
                        meshData.triangles[triIndex++] = i + lodResolution + 1;
                        // Second triangle
                        meshData.triangles[triIndex++] = i;
                        meshData.triangles[triIndex++] = i + lodResolution + 1;
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
        /// Create flat normals pointing straight up for all vertices
        /// This eliminates lighting discontinuities between chunks entirely
        /// Results in consistent, uniform lighting across all terrain
        /// </summary>
        private void CreateFlatNormals(Mesh mesh)
        {
            Vector3[] normals = new Vector3[mesh.vertexCount];

            // Set all normals to point straight up
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
            }

            mesh.normals = normals;
        }

        /// <summary>
        /// Create a mesh from mesh data (called on main thread)
        /// </summary>
        private Mesh CreateMeshFromData(MeshData meshData, TerrainLOD lod)
        {
            Mesh mesh = new Mesh();
            mesh.name = $"TerrainChunk_{chunkCoord.x}_{chunkCoord.y}_LOD{lod}";
            mesh.vertices = meshData.vertices;
            mesh.triangles = meshData.triangles;
            mesh.uv = meshData.uvs;
            mesh.colors = meshData.colors;

            // Create flat normals manually for consistent lighting
            CreateFlatNormals(mesh);

            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Set the LOD level for this chunk
        /// Generates LOD mesh on-demand if not cached, then switches to it
        /// WebGL-optimized: generates async to prevent main thread blocking
        /// </summary>
        public async void SetLOD(TerrainLOD newLOD)
        {
            if (currentLOD == newLOD)
                return;

            // Generate the LOD mesh if it doesn't exist yet (on-demand generation)
            if (!lodMeshes.ContainsKey(newLOD))
            {
                // Generate mesh data async on thread pool (WebGL performance)
                var meshData = await UniTask.RunOnThreadPool(() => GenerateMeshData(newLOD));
                Mesh newMesh = CreateMeshFromData(meshData, newLOD);
                lodMeshes[newLOD] = newMesh;
            }

            currentLOD = newLOD;

            // Switch to the cached mesh for this LOD level
            if (lodMeshes.TryGetValue(newLOD, out Mesh lodMesh))
            {
                meshFilter.mesh = lodMesh;

                // ALWAYS update collider - player needs collision on ALL LODs to prevent falling through
                // We use the high LOD mesh for collision even on lower visual LODs for accuracy
                if (lodMeshes.TryGetValue(TerrainLOD.High, out Mesh highLodMesh))
                {
                    meshCollider.sharedMesh = highLodMesh;
                }

                // Update bounds
                Bounds = meshRenderer.bounds;

                // Reset fade-in when upgrading LOD
                if (enableFadeIn && instanceMaterial != null)
                {
                    fadeTimer = 0f;
                    instanceMaterial.SetFloat(ColorMultiplierProperty, 0f);
                }
            }
        }

        /// <summary>
        /// Unload chunk and cleanup resources
        /// </summary>
        public void Unload()
        {
            // Clean up all LOD meshes
            foreach (var lodMesh in lodMeshes.Values)
            {
                if (lodMesh != null)
                {
                    Destroy(lodMesh);
                }
            }
            lodMeshes.Clear();

            // Clean up instance material to prevent memory leaks
            if (instanceMaterial != null)
            {
                Destroy(instanceMaterial);
                instanceMaterial = null;
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
