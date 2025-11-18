using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Shader warmup script that forces Unity to include specific shaders in WebGL builds.
/// This script should be placed in a scene (like Credits) that is included in Build Settings.
/// By referencing materials and shaders here, Unity's shader stripping won't remove them.
/// </summary>
public class ShaderWarmup : MonoBehaviour
{
    [Header("Materials to Keep in Build")]
    [Tooltip("Add all materials that use custom shaders here")]
    public List<Material> materialsToWarmup = new List<Material>();

    [Header("Shaders to Keep in Build")]
    [Tooltip("Add all custom shaders here")]
    public List<Shader> shadersToWarmup = new List<Shader>();

    [Header("Warmup Settings")]
    [Tooltip("If true, will create temporary objects to warmup shaders on scene load")]
    public bool warmupOnStart = true;

    [Tooltip("If true, will log warmup information to console")]
    public bool debugLogging = true;

    private void Awake()
    {
        if (warmupOnStart)
        {
            WarmupShaders();
        }
    }

    /// <summary>
    /// Forces shader compilation by creating temporary objects with the materials
    /// </summary>
    public void WarmupShaders()
    {
        if (debugLogging)
        {
            Debug.Log($"[ShaderWarmup] Starting shader warmup - {materialsToWarmup.Count} materials, {shadersToWarmup.Count} shaders");
        }

        // Warmup materials by creating temporary invisible objects
        foreach (Material mat in materialsToWarmup)
        {
            if (mat != null)
            {
                WarmupMaterial(mat);
            }
        }

        // Warmup shaders by checking if they exist
        foreach (Shader shader in shadersToWarmup)
        {
            if (shader != null)
            {
                if (debugLogging)
                {
                    Debug.Log($"[ShaderWarmup] Shader '{shader.name}' is loaded and ready");
                }
            }
            else
            {
                Debug.LogWarning("[ShaderWarmup] Null shader in warmup list!");
            }
        }

        if (debugLogging)
        {
            Debug.Log("[ShaderWarmup] Shader warmup complete");
        }
    }

    /// <summary>
    /// Warms up a specific material by creating a temporary object
    /// </summary>
    private void WarmupMaterial(Material material)
    {
        // Create a temporary cube far from the camera
        GameObject tempObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tempObj.name = $"ShaderWarmup_{material.name}";
        tempObj.transform.position = new Vector3(10000, 10000, 10000); // Far away
        tempObj.transform.localScale = Vector3.one * 0.01f; // Very small

        // Assign the material
        Renderer renderer = tempObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = material;
            renderer.enabled = false; // Don't actually render it
        }

        // Keep the object alive (it will be cleaned up when the scene unloads)
        DontDestroyOnLoad(tempObj);

        if (debugLogging)
        {
            Debug.Log($"[ShaderWarmup] Warmed up material '{material.name}' using shader '{material.shader.name}'");
        }
    }

    /// <summary>
    /// Manually add a material to warmup at runtime
    /// </summary>
    public void AddMaterial(Material material)
    {
        if (material != null && !materialsToWarmup.Contains(material))
        {
            materialsToWarmup.Add(material);
            if (Application.isPlaying)
            {
                WarmupMaterial(material);
            }
        }
    }

    /// <summary>
    /// Manually add a shader to warmup at runtime
    /// </summary>
    public void AddShader(Shader shader)
    {
        if (shader != null && !shadersToWarmup.Contains(shader))
        {
            shadersToWarmup.Add(shader);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Load All Custom Shaders")]
    private void LoadAllCustomShaders()
    {
        // Helper function to automatically find and add custom shaders
        shadersToWarmup.Clear();

        // Add TerrainVertexColor shader
        Shader terrainShader = Shader.Find("BugWars/TerrainVertexColor");
        if (terrainShader != null)
        {
            shadersToWarmup.Add(terrainShader);
            Debug.Log($"Added shader: {terrainShader.name}");
        }

        // Load material from Resources
        Material terrainMat = Resources.Load<Material>("TerrainVertexColorMaterial");
        if (terrainMat != null)
        {
            materialsToWarmup.Clear();
            materialsToWarmup.Add(terrainMat);
            Debug.Log($"Added material: {terrainMat.name}");
        }
    }
#endif
}
