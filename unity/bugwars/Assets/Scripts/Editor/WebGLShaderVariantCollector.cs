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
            // WebGL doesn't need shadows for performance reasons
            if (snippet.passName.Contains("ShadowCaster") || snippet.passName.Contains("DepthOnly"))
            {
                data.Clear();
                return;
            }

            // Strip shadow-related keywords from ALL shaders
            for (int i = data.Count - 1; i >= 0; --i)
            {
                ShaderCompilerData variantData = data[i];

                // Strip ALL shadow-related keywords
                if (variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("_MAIN_LIGHT_SHADOWS_CASCADE")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("_ADDITIONAL_LIGHT_SHADOWS")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("LIGHTMAP_SHADOW_MIXING")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("SHADOWS_SHADOWMASK")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("_SHADOWS_SOFT")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("_LIGHT_LAYERS")))
                {
                    data.RemoveAt(i);
                    continue;
                }

                // Strip additional quality/features we don't need for WebGL
                if (variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("_REFLECTION_PROBE_BLENDING")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("_REFLECTION_PROBE_BOX_PROJECTION")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("LIGHTMAP_ON")) ||
                    variantData.shaderKeywordSet.IsEnabled(new ShaderKeyword("DYNAMICLIGHTMAP_ON")))
                {
                    data.RemoveAt(i);
                }
            }
        }
    }

    // NOTE: Shader inclusion is now handled by placing objects with the required materials
    // in the Credits scene (or any scene that's always included in build settings).
    // This is simpler and more reliable than programmatically adding shaders to GraphicsSettings.
}
