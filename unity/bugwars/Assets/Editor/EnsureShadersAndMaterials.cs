using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using System.Collections.Generic;
using System.Linq;

namespace BugWars.Editor
{
    /// <summary>
    /// Unified shader management for WebGL builds
    /// Ensures that all critical shaders are included and not stripped:
    /// - Terrain vertex color shader (BugWars/TerrainVertexColor)
    /// - All shaders used by environment objects (trees, bushes, rocks)
    /// Can be manually triggered via Sync Environment Addressables menu
    /// </summary>
    public class EnsureShadersAndMaterials : IPreprocessShaders
    {
        private const string TERRAIN_SHADER_NAME = "BugWars/TerrainVertexColor";

        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            // This callback is called for each shader during build - we just need to ensure they're registered
        }

        /// <summary>
        /// Refreshes all shaders - called automatically on startup and by Sync Environment Addressables
        /// </summary>
        public static void RefreshAllShaders()
        {
            Debug.Log("[EnsureShadersAndMaterials] Scanning all shaders and materials...");

            HashSet<Shader> shadersToInclude = new HashSet<Shader>();

            // 1. Add terrain shader
            Shader terrainShader = Shader.Find(TERRAIN_SHADER_NAME);
            if (terrainShader != null)
            {
                shadersToInclude.Add(terrainShader);
                Debug.Log($"[EnsureShadersAndMaterials] Found terrain shader: {TERRAIN_SHADER_NAME}");
            }
            else
            {
                Debug.LogWarning($"[EnsureShadersAndMaterials] Terrain shader not found: {TERRAIN_SHADER_NAME}");
            }

            // 2. Add WebGL-compatible fallback shader
            // This custom shader uses the same approach as character shaders (proven to work in WebGL)
            Shader forestShader = Shader.Find("BugWars/ForestEnvironment");
            if (forestShader != null)
            {
                shadersToInclude.Add(forestShader);
                Debug.Log("[EnsureShadersAndMaterials] Added WebGL-compatible shader: BugWars/ForestEnvironment");
            }
            else
            {
                Debug.LogWarning("[EnsureShadersAndMaterials] WebGL shader not found: BugWars/ForestEnvironment");
            }

            // 3. Scan environment prefabs for shaders
            ScanPrefabsForShaders("Assets/Resources/Prefabs/Forest/Trees", shadersToInclude);
            ScanPrefabsForShaders("Assets/Resources/Prefabs/Forest/Bushes", shadersToInclude);
            ScanPrefabsForShaders("Assets/Resources/Prefabs/Forest/Rocks", shadersToInclude);

            if (shadersToInclude.Count > 0)
            {
                Debug.Log($"[EnsureShadersAndMaterials] Found {shadersToInclude.Count} unique shaders to include");
                AddShadersToAlwaysIncluded(shadersToInclude);
            }
            else
            {
                Debug.LogWarning("[EnsureShadersAndMaterials] No shaders found to include!");
            }
        }

        /// <summary>
        /// Scan prefabs in a folder for all shaders used by their materials
        /// </summary>
        private static void ScanPrefabsForShaders(string folderPath, HashSet<Shader> shadersToInclude)
        {
            if (!System.IO.Directory.Exists(folderPath))
            {
                Debug.LogWarning($"[EnsureShadersAndMaterials] Folder not found: {folderPath}");
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            int prefabCount = 0;
            int shaderCount = 0;

            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab != null)
                {
                    prefabCount++;

                    // Get all renderers in the prefab (including children)
                    Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);

                    foreach (var renderer in renderers)
                    {
                        if (renderer != null && renderer.sharedMaterials != null)
                        {
                            foreach (var material in renderer.sharedMaterials)
                            {
                                if (material != null && material.shader != null)
                                {
                                    if (shadersToInclude.Add(material.shader))
                                    {
                                        shaderCount++;
                                        Debug.Log($"[EnsureShadersAndMaterials] Found shader: {material.shader.name} in {prefab.name}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log($"[EnsureShadersAndMaterials] Scanned {folderPath}: {prefabCount} prefabs, {shaderCount} new shaders");
        }

        /// <summary>
        /// Add all shaders to the AlwaysIncludedShaders list in GraphicsSettings
        /// </summary>
        private static void AddShadersToAlwaysIncluded(HashSet<Shader> shadersToInclude)
        {
            // Access GraphicsSettings
            var graphicsSettingsObj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (graphicsSettingsObj == null)
            {
                Debug.LogError("[EnsureShadersAndMaterials] Could not load GraphicsSettings.asset");
                return;
            }

            // Access the always included shaders using SerializedObject
            SerializedObject serializedSettings = new SerializedObject(graphicsSettingsObj);
            SerializedProperty alwaysIncludedShaders = serializedSettings.FindProperty("m_AlwaysIncludedShaders");

            if (alwaysIncludedShaders == null)
            {
                Debug.LogError("[EnsureShadersAndMaterials] Could not find m_AlwaysIncludedShaders property");
                return;
            }

            // Get existing shaders
            List<Shader> existingShaders = new List<Shader>();
            for (int i = 0; i < alwaysIncludedShaders.arraySize; i++)
            {
                var shader = alwaysIncludedShaders.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null)
                {
                    existingShaders.Add(shader);
                }
            }

            // Add missing shaders
            int addedCount = 0;
            foreach (var shader in shadersToInclude)
            {
                if (shader != null && !existingShaders.Contains(shader))
                {
                    alwaysIncludedShaders.arraySize++;
                    alwaysIncludedShaders.GetArrayElementAtIndex(alwaysIncludedShaders.arraySize - 1).objectReferenceValue = shader;
                    addedCount++;
                    Debug.Log($"[EnsureShadersAndMaterials] ✓ Added to AlwaysIncludedShaders: {shader.name}");
                }
                else if (shader != null)
                {
                    Debug.Log($"[EnsureShadersAndMaterials] Already included: {shader.name}");
                }
            }

            if (addedCount > 0)
            {
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(graphicsSettingsObj);
                AssetDatabase.SaveAssets();
                Debug.Log($"[EnsureShadersAndMaterials] SUCCESS: Added {addedCount} new shaders to AlwaysIncludedShaders");
                EditorUtility.DisplayDialog(
                    "Shaders & Materials Refreshed",
                    $"Successfully added {addedCount} shaders to AlwaysIncludedShaders!\n\n" +
                    $"Total shaders protected: {shadersToInclude.Count}\n\n" +
                    $"Your WebGL build will now render environment objects correctly.",
                    "OK");
            }
            else
            {
                Debug.Log($"[EnsureShadersAndMaterials] All {shadersToInclude.Count} shaders already included");
                EditorUtility.DisplayDialog(
                    "Shaders Already Included",
                    $"All {shadersToInclude.Count} shaders are already in AlwaysIncludedShaders.\n\n" +
                    $"No changes needed!",
                    "OK");
            }
        }
    }
}
