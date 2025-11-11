using UnityEngine;
using UnityEditor;

namespace BugWars.Editor
{
    /// <summary>
    /// Editor utility to fix the Samurai material by assigning the atlas texture
    /// </summary>
    public class FixSamuraiMaterial : EditorWindow
    {
        [MenuItem("KBVE/Fix Samurai Material Texture")]
        public static void Fix()
        {
            Debug.Log("[FixSamuraiMaterial] Starting material fix...");

            // Load the material
            string materialPath = "Assets/BugWars/Prefabs/Character/Samurai/SamuraiMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
            {
                Debug.LogError($"[FixSamuraiMaterial] Could not find material at {materialPath}");
                return;
            }

            Debug.Log($"[FixSamuraiMaterial] ✓ Material found: {material.name}");
            Debug.Log($"[FixSamuraiMaterial] Current shader: {material.shader.name}");

            // Load the atlas texture
            string texturePath = "Assets/BugWars/Prefabs/Character/Samurai/SamuraiAtlas.png";
            Texture2D atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            if (atlasTexture == null)
            {
                Debug.LogError($"[FixSamuraiMaterial] Could not find texture at {texturePath}");
                return;
            }

            Debug.Log($"[FixSamuraiMaterial] ✓ Texture found: {atlasTexture.name}");

            // Assign the texture to the material
            material.SetTexture("_BaseMap", atlasTexture);

            // Also set default UV frame to full texture
            material.SetVector("_FrameUVMin", new Vector4(0, 0, 0, 0));
            material.SetVector("_FrameUVMax", new Vector4(1, 1, 0, 0));

            // Save the material
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            Debug.Log("[FixSamuraiMaterial] ✓ Successfully assigned SamuraiAtlas texture to material!");
            Debug.Log($"[FixSamuraiMaterial] Main texture: {material.mainTexture?.name ?? "NULL"}");
        }
    }
}
