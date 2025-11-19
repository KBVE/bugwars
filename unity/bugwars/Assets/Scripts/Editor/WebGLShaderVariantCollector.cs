using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace BugWars.Editor
{
    /// <summary>
    /// Aggressively strips unused shader variants for WebGL builds to reduce compile time
    /// Only keeps essential variants needed for the game
    /// </summary>
    public class WebGLShaderVariantCollector : IPreprocessShaders
    {
        // Lower numbers execute first
        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            // Only modify stripping for WebGL builds
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                return;

            // Strip all shadow variants for WebGL (massive reduction)
            if (snippet.passName.Contains("ShadowCaster") || snippet.passName.Contains("DepthOnly"))
            {
                data.Clear();
                return;
            }

            // Strip most graphics API variants - WebGL only needs WebGL2/WebGPU
            for (int i = data.Count - 1; i >= 0; --i)
            {
                // Keep only essential keyword combinations
                ShaderCompilerData variantData = data[i];

                // Strip unnecessary keywords
                if (variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("_MAIN_LIGHT_SHADOWS_CASCADE")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("_ADDITIONAL_LIGHT_SHADOWS")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("LIGHTMAP_SHADOW_MIXING")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("SHADOWS_SHADOWMASK")))
                {
                    data.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Custom shader preprocessor for WebGL builds
    /// Adds always-included shaders to GraphicsSettings before build
    /// </summary>
    public class WebGLShaderInclusionBuildProcessor : UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            Debug.Log("[WebGLShaderInclusionBuildProcessor] Adding critical shaders to always-included list for WebGL build");

            // Get current GraphicsSettings
            SerializedObject graphicsSettings = new SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty alwaysIncludedShaders = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");

            // List of shader names/GUIDs to force-include
            string[] requiredShaderNames = new string[]
            {
                "Hidden/CoreSRP/CoreCopy",
                "Hidden/Universal Render Pipeline/StencilDitherMaskSeed",
                "Hidden/Universal/HDRDebugView",
                "Universal Render Pipeline/Lit",
                "Sprites/Default"
            };

            int addedCount = 0;
            foreach (string shaderName in requiredShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    // Check if already in list
                    bool alreadyIncluded = false;
                    for (int i = 0; i < alwaysIncludedShaders.arraySize; i++)
                    {
                        if (alwaysIncludedShaders.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                        {
                            alreadyIncluded = true;
                            break;
                        }
                    }

                    if (!alreadyIncluded)
                    {
                        alwaysIncludedShaders.InsertArrayElementAtIndex(alwaysIncludedShaders.arraySize);
                        alwaysIncludedShaders.GetArrayElementAtIndex(alwaysIncludedShaders.arraySize - 1).objectReferenceValue = shader;
                        addedCount++;
                        Debug.Log($"[WebGLShaderInclusionBuildProcessor] Added shader to always-included: {shaderName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[WebGLShaderInclusionBuildProcessor] Could not find shader: {shaderName}");
                }
            }

            if (addedCount > 0)
            {
                graphicsSettings.ApplyModifiedProperties();
                Debug.Log($"[WebGLShaderInclusionBuildProcessor] Added {addedCount} critical shaders to GraphicsSettings");
            }
        }
    }
}
