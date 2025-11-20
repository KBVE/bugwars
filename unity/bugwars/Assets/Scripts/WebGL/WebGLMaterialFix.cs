using UnityEngine;

namespace BugWars.WebGL
{
    /// <summary>
    /// WebGL-specific fix for environment object materials
    /// URP/Lit shader can have compatibility issues in WebGL - this ensures objects are visible
    /// Automatically replaces shaders with WebGL-compatible versions at runtime
    /// </summary>
    public class WebGLMaterialFix : MonoBehaviour
    {
        [Header("WebGL Material Fix")]
        [SerializeField] private bool enableFix = true;
        [SerializeField] private bool logChanges = true;

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (enableFix)
            {
                ApplyWebGLMaterialFix();
            }
#else
            if (logChanges)
            {
                Debug.Log("[WebGLMaterialFix] Not running in WebGL build - fix disabled");
            }
#endif
        }

        private void ApplyWebGLMaterialFix()
        {
            Debug.Log("[WebGLMaterialFix] Scanning for environment objects with incompatible shaders...");

            // Find all renderers in the scene
            Renderer[] allRenderers = FindObjectsOfType<Renderer>(true);
            int fixedCount = 0;

            foreach (var renderer in allRenderers)
            {
                if (renderer == null) continue;

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;

                    // Check if material is using URP/Lit shader
                    if (material.shader != null && material.shader.name == "Universal Render Pipeline/Lit")
                    {
                        // Replace with WebGL-compatible shader
                        Shader webglShader = Shader.Find("Mobile/Diffuse");

                        if (webglShader != null)
                        {
                            if (logChanges)
                            {
                                Debug.Log($"[WebGLMaterialFix] Replacing shader on {renderer.gameObject.name}: {material.shader.name} → Mobile/Diffuse");
                            }

                            material.shader = webglShader;
                            fixedCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"[WebGLMaterialFix] Mobile/Diffuse shader not found! Trying fallback...");

                            // Fallback to Unlit/Color
                            Shader fallbackShader = Shader.Find("Unlit/Color");
                            if (fallbackShader != null)
                            {
                                material.shader = fallbackShader;
                                fixedCount++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"[WebGLMaterialFix] Fixed {fixedCount} materials for WebGL compatibility");
        }
    }
}
