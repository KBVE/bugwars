using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using System.Linq;

namespace BugWars.Editor
{
    /// <summary>
    /// Ensures the TerrainVertexColor shader is always included in builds
    /// This prevents the shader from being stripped out in WebGL builds
    /// </summary>
    [InitializeOnLoad]
    public static class EnsureTerrainShaderIncluded
    {
        private const string TERRAIN_SHADER_NAME = "BugWars/TerrainVertexColor";

        static EnsureTerrainShaderIncluded()
        {
            // Run on editor load to ensure shader is included
            EnsureShaderIncluded();
        }

        [MenuItem("KBVE/Tools/Ensure Terrain Shader Included")]
        public static void EnsureShaderIncludedMenuItem()
        {
            if (EnsureShaderIncluded())
            {
                EditorUtility.DisplayDialog(
                    "Shader Included",
                    $"The shader '{TERRAIN_SHADER_NAME}' has been added to Always Included Shaders.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Shader Already Included",
                    $"The shader '{TERRAIN_SHADER_NAME}' is already in Always Included Shaders.",
                    "OK");
            }
        }

        private static bool EnsureShaderIncluded()
        {
            // Find the shader
            Shader terrainShader = Shader.Find(TERRAIN_SHADER_NAME);
            if (terrainShader == null)
            {
                Debug.LogError($"[EnsureTerrainShaderIncluded] Shader not found: {TERRAIN_SHADER_NAME}");
                return false;
            }

            // Access GraphicsSettings using the correct API
            var graphicsSettingsObj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            if (graphicsSettingsObj == null)
            {
                Debug.LogError("[EnsureTerrainShaderIncluded] Could not load GraphicsSettings.asset");
                return false;
            }

            // Access the always included shaders using SerializedObject
            SerializedObject serializedSettings = new SerializedObject(graphicsSettingsObj);
            SerializedProperty alwaysIncludedShadersProperty = serializedSettings.FindProperty("m_AlwaysIncludedShaders");

            if (alwaysIncludedShadersProperty == null)
            {
                Debug.LogError("[EnsureTerrainShaderIncluded] Could not find m_AlwaysIncludedShaders property");
                return false;
            }

            // Check if shader is already included
            for (int i = 0; i < alwaysIncludedShadersProperty.arraySize; i++)
            {
                SerializedProperty shaderProperty = alwaysIncludedShadersProperty.GetArrayElementAtIndex(i);
                if (shaderProperty.objectReferenceValue == terrainShader)
                {
                    Debug.Log($"[EnsureTerrainShaderIncluded] Shader '{TERRAIN_SHADER_NAME}' is already included in Always Included Shaders.");
                    return false;
                }
            }

            // Add shader to the list
            alwaysIncludedShadersProperty.InsertArrayElementAtIndex(alwaysIncludedShadersProperty.arraySize);
            SerializedProperty newShaderProperty = alwaysIncludedShadersProperty.GetArrayElementAtIndex(alwaysIncludedShadersProperty.arraySize - 1);
            newShaderProperty.objectReferenceValue = terrainShader;

            // Apply changes
            serializedSettings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log($"[EnsureTerrainShaderIncluded] Successfully added '{TERRAIN_SHADER_NAME}' to Always Included Shaders.");
            return true;
        }
    }
}
