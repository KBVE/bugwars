using UnityEngine;
using UnityEditor;

namespace BugWars.Editor
{
    /// <summary>
    /// Verify the Samurai material setup
    /// </summary>
    public class VerifySamuraiMaterial : EditorWindow
    {
        [MenuItem("KBVE/Verify Samurai Material")]
        public static void Verify()
        {
            Debug.Log("=== SAMURAI MATERIAL VERIFICATION ===");

            // Load the material
            string materialPath = "Assets/BugWars/Prefabs/Character/Samurai/SamuraiMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
            {
                Debug.LogError($"❌ Could not find material at {materialPath}");
                return;
            }

            Debug.Log($"✓ Material found: {material.name}");
            Debug.Log($"✓ Shader: {material.shader.name}");

            // Check _BaseMap specifically
            Texture baseMap = material.GetTexture("_BaseMap");
            if (baseMap != null)
            {
                Debug.Log($"✓ _BaseMap texture: {baseMap.name}");
            }
            else
            {
                Debug.LogError("❌ _BaseMap is NULL!");
            }

            // Check mainTexture (legacy)
            if (material.mainTexture != null)
            {
                Debug.Log($"✓ mainTexture: {material.mainTexture.name}");
            }
            else
            {
                Debug.LogWarning("⚠️  mainTexture is NULL (this is OK for URP shaders)");
            }

            // Check all texture properties
            Debug.Log("\nAll texture properties:");
            var shader = material.shader;
            int propCount = shader.GetPropertyCount();
            for (int i = 0; i < propCount; i++)
            {
                if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    string propName = shader.GetPropertyName(i);
                    Texture tex = material.GetTexture(propName);
                    Debug.Log($"  {propName}: {(tex != null ? tex.name : "NULL")}");
                }
            }

            Debug.Log("=== END VERIFICATION ===");
        }
    }
}
