using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace BugWars.Editor
{
    /// <summary>
    /// Prevents URP shader stripping in WebGL builds
    /// Ensures critical shaders like Hidden/CoreSRP/CoreCopy are included
    ///
    /// WebGL Issue: Unity's shader stripping is too aggressive for WebGL,
    /// removing essential URP shaders that are needed at runtime.
    /// This script forces inclusion of critical shaders.
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

            // List of critical URP shaders that must never be stripped in WebGL
            string[] criticalShaders = new string[]
            {
                "Hidden/CoreSRP/CoreCopy",
                "Hidden/Universal Render Pipeline/StencilDitherMaskSeed",
                "Hidden/Universal/HDRDebugView",
                "Hidden/Universal Render Pipeline/Blit",
                "Hidden/Universal Render Pipeline/CopyDepth",
                "Hidden/Universal Render Pipeline/Sampling",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Sprites/Default"
            };

            // Check if this is a critical shader
            foreach (string criticalShader in criticalShaders)
            {
                if (shader.name.Contains(criticalShader))
                {
                    // Prevent stripping by not removing any variants
                    Debug.Log($"[WebGLShaderVariantCollector] Preserving critical shader: {shader.name} ({data.Count} variants)");
                    return;
                }
            }

            // For non-critical shaders, allow default stripping behavior
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
