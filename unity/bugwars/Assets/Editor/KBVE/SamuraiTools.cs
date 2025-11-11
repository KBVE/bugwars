using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using BugWars.Debugging;

namespace BugWars.Editor
{
    /// <summary>
    /// Unified Samurai development tools
    /// - Sync Samurai: Fixes shader, material, and texture setup
    /// - Debug Samurai: Toggle runtime debugging component
    /// </summary>
    public class SamuraiTools : EditorWindow
    {
        private const string SHADER_NAME = "BugWars/SamuraiAnimatedSprite_Unity6";
        private const string MATERIAL_PATH = "Assets/BugWars/Prefabs/Character/Samurai/SamuraiMaterial.mat";
        private const string TEXTURE_PATH = "Assets/BugWars/Prefabs/Character/Samurai/SamuraiAtlas.png";
        private const string PREFAB_PATH = "Assets/BugWars/Prefabs/Character/Samurai/Samurai.prefab";

        [MenuItem("KBVE/Sync Samurai")]
        public static void SyncSamurai()
        {
            Debug.Log("=== SYNC SAMURAI START ===");

            bool success = true;

            // Step 1: Ensure shader is in Always Included Shaders
            success &= EnsureShaderIncluded();

            // Step 2: Fix material texture assignment
            success &= FixMaterialTexture();

            // Step 3: Verify setup
            success &= VerifySetup();

            if (success)
            {
                Debug.Log("✓ SYNC COMPLETE - Samurai is ready!");
                EditorUtility.DisplayDialog("Sync Samurai",
                    "Successfully synced Samurai!\n\n• Shader added to Always Included list\n• Material texture assigned\n• Setup verified",
                    "OK");
            }
            else
            {
                Debug.LogError("❌ SYNC FAILED - Check console for details");
                EditorUtility.DisplayDialog("Sync Samurai",
                    "Sync encountered errors. Check the Console for details.",
                    "OK");
            }

            Debug.Log("=== SYNC SAMURAI END ===\n");
        }

        [MenuItem("KBVE/Debug Samurai")]
        public static void DebugSamurai()
        {
            // Find Samurai in scene
            GameObject samurai = FindSamuraiInScene();

            if (samurai == null)
            {
                EditorUtility.DisplayDialog("Debug Samurai",
                    "Could not find Samurai GameObject in scene.\n\nMake sure the Samurai is spawned or present in the scene.",
                    "OK");
                return;
            }

            // Toggle CheckSamuraiBillboard component
            CheckSamuraiBillboard debugComponent = samurai.GetComponent<CheckSamuraiBillboard>();

            if (debugComponent == null)
            {
                // Add the component
                debugComponent = samurai.AddComponent<CheckSamuraiBillboard>();
                Debug.Log($"[SamuraiTools] ✓ Enabled debug component on {samurai.name}");
                EditorUtility.DisplayDialog("Debug Samurai",
                    "Debug mode ENABLED\n\nCheckSamuraiBillboard component added to Samurai.\nCheck Console for debug output.",
                    "OK");
            }
            else
            {
                // Remove the component
                DestroyImmediate(debugComponent);
                Debug.Log($"[SamuraiTools] ✓ Disabled debug component on {samurai.name}");
                EditorUtility.DisplayDialog("Debug Samurai",
                    "Debug mode DISABLED\n\nCheckSamuraiBillboard component removed from Samurai.",
                    "OK");
            }

            // Mark scene as dirty so changes are saved
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(samurai.scene);
            }
        }

        #region Helper Methods

        private static bool EnsureShaderIncluded()
        {
            Debug.Log("[SamuraiTools] Checking shader inclusion...");

            Shader samuraiShader = Shader.Find(SHADER_NAME);

            if (samuraiShader == null)
            {
                Debug.LogError($"[SamuraiTools] ❌ Could not find shader '{SHADER_NAME}'");
                return false;
            }

            var graphicsSettings = GraphicsSettings.GetGraphicsSettings();
            var serializedObject = new SerializedObject(graphicsSettings);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

            // Check if already included
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var shader = arrayProp.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader == samuraiShader)
                {
                    Debug.Log("[SamuraiTools] ✓ Shader already in Always Included list");
                    return true;
                }
            }

            // Add shader
            arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
            var newElement = arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);
            newElement.objectReferenceValue = samuraiShader;
            serializedObject.ApplyModifiedProperties();

            Debug.Log($"[SamuraiTools] ✓ Added shader to Always Included list (total: {arrayProp.arraySize})");
            return true;
        }

        private static bool FixMaterialTexture()
        {
            Debug.Log("[SamuraiTools] Fixing material texture...");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (material == null)
            {
                Debug.LogError($"[SamuraiTools] ❌ Could not find material at {MATERIAL_PATH}");
                return false;
            }

            Texture2D atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_PATH);
            if (atlasTexture == null)
            {
                Debug.LogError($"[SamuraiTools] ❌ Could not find texture at {TEXTURE_PATH}");
                return false;
            }

            // Assign texture and default UV frame
            material.SetTexture("_BaseMap", atlasTexture);
            material.SetVector("_FrameUVMin", new Vector4(0, 0, 0, 0));
            material.SetVector("_FrameUVMax", new Vector4(1, 1, 0, 0));

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SamuraiTools] ✓ Assigned texture '{atlasTexture.name}' to material");
            return true;
        }

        private static bool VerifySetup()
        {
            Debug.Log("[SamuraiTools] Verifying setup...");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (material == null)
            {
                Debug.LogError($"[SamuraiTools] ❌ Verification failed: Material not found");
                return false;
            }

            // Check shader
            if (material.shader == null || material.shader.name != SHADER_NAME)
            {
                Debug.LogError($"[SamuraiTools] ❌ Material has wrong shader: {material.shader?.name ?? "NULL"}");
                return false;
            }

            // Check texture
            Texture baseMap = material.GetTexture("_BaseMap");
            if (baseMap == null)
            {
                Debug.LogError("[SamuraiTools] ❌ Material _BaseMap is NULL");
                return false;
            }

            // Check shader support
            if (!material.shader.isSupported)
            {
                Debug.LogError("[SamuraiTools] ❌ Shader is not supported on this platform!");
                return false;
            }

            Debug.Log($"[SamuraiTools] ✓ Material: {material.name}");
            Debug.Log($"[SamuraiTools] ✓ Shader: {material.shader.name} (supported: {material.shader.isSupported})");
            Debug.Log($"[SamuraiTools] ✓ Texture: {baseMap.name} ({baseMap.width}x{baseMap.height})");

            return true;
        }

        private static GameObject FindSamuraiInScene()
        {
            // Try exact name first
            GameObject samurai = GameObject.Find("Samurai");
            if (samurai != null)
                return samurai;

            // Try finding by name contains
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name.Contains("Samurai") || obj.name.Contains("samurai"))
                {
                    Debug.Log($"[SamuraiTools] Found Samurai as: {obj.name}");
                    return obj;
                }
            }

            // Try finding the prefab instance
            GameObject prefabInstance = PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH)
            ) as GameObject;

            if (prefabInstance != null)
            {
                Debug.Log($"[SamuraiTools] No Samurai in scene, instantiated from prefab");
                return prefabInstance;
            }

            return null;
        }

        #endregion
    }
}
