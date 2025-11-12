using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace BugWars.Editor
{
    /// <summary>
    /// Unified BlankPlayer development tools
    /// - Sync BlankPlayer: Fixes shader, material, texture, and prefab setup
    /// - Creates proper 4-directional character setup with all references
    /// </summary>
    public class BlankPlayerTools : EditorWindow
    {
        private const string SHADER_NAME = "BugWars/BlankPlayerAnimatedSprite_URP3D_Billboard";
        private const string MATERIAL_PATH = "Assets/BugWars/Prefabs/Character/BlankPlayer/BlankPlayerMaterial.mat";
        private const string TEXTURE_PATH = "Assets/BugWars/Prefabs/Character/BlankPlayer/BlankPlayerAtlas.png";
        private const string JSON_PATH = "Assets/BugWars/Prefabs/Character/BlankPlayer/BlankPlayerAtlas.json";
        private const string PREFAB_PATH = "Assets/BugWars/Prefabs/Character/BlankPlayer/BlankPlayer.prefab";

        [MenuItem("KBVE/Character/Sync BlankPlayer")]
        public static void SyncBlankPlayer()
        {
            Debug.Log("=== SYNC BLANKPLAYER START ===");

            bool success = true;

            // Step 1: Ensure shader is in Always Included Shaders
            success &= EnsureShaderIncluded();

            // Step 2: Fix material texture assignment
            success &= FixMaterialTexture();

            // Step 3: Fix prefab setup (SpriteRenderer + BlankPlayer component)
            success &= FixPrefabSetup();

            // Step 4: Verify setup
            success &= VerifySetup();

            if (success)
            {
                Debug.Log("✓ SYNC COMPLETE - BlankPlayer is ready!");
                EditorUtility.DisplayDialog("Sync BlankPlayer",
                    "Successfully synced BlankPlayer!\n\n" +
                    "• Shader added to Always Included list\n" +
                    "• Material texture assigned\n" +
                    "• Prefab configured with:\n" +
                    "  - SpriteRenderer child\n" +
                    "  - BlankPlayer component\n" +
                    "  - Atlas JSON reference\n" +
                    "  - Material reference\n" +
                    "• Setup verified",
                    "OK");
            }
            else
            {
                Debug.LogError("❌ SYNC FAILED - Check console for details");
                EditorUtility.DisplayDialog("Sync BlankPlayer",
                    "Sync encountered errors. Check the Console for details.",
                    "OK");
            }

            Debug.Log("=== SYNC BLANKPLAYER END ===\n");
        }

        #region Helper Methods

        private static bool EnsureShaderIncluded()
        {
            Debug.Log("[BlankPlayerTools] Checking shader inclusion...");

            Shader blankPlayerShader = Shader.Find(SHADER_NAME);

            if (blankPlayerShader == null)
            {
                Debug.LogError($"[BlankPlayerTools] ❌ Could not find shader '{SHADER_NAME}'");
                return false;
            }

            var graphicsSettings = GraphicsSettings.GetGraphicsSettings();
            var serializedObject = new SerializedObject(graphicsSettings);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

            // Check if already included
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var shader = arrayProp.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader == blankPlayerShader)
                {
                    Debug.Log("[BlankPlayerTools] ✓ Shader already in Always Included list");
                    return true;
                }
            }

            // Add shader
            arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
            var newElement = arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);
            newElement.objectReferenceValue = blankPlayerShader;
            serializedObject.ApplyModifiedProperties();

            Debug.Log($"[BlankPlayerTools] ✓ Added shader to Always Included list (total: {arrayProp.arraySize})");
            return true;
        }

        private static bool FixMaterialTexture()
        {
            Debug.Log("[BlankPlayerTools] Fixing material texture...");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (material == null)
            {
                Debug.LogError($"[BlankPlayerTools] ❌ Could not find material at {MATERIAL_PATH}");
                return false;
            }

            Texture2D atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_PATH);
            if (atlasTexture == null)
            {
                Debug.LogError($"[BlankPlayerTools] ❌ Could not find texture at {TEXTURE_PATH}");
                return false;
            }

            // Assign shader if needed
            Shader shader = Shader.Find(SHADER_NAME);
            if (material.shader != shader)
            {
                material.shader = shader;
                Debug.Log($"[BlankPlayerTools] ✓ Assigned shader to material");
            }

            // Assign texture and default UV frame
            material.SetTexture("_BaseMap", atlasTexture);
            material.SetVector("_FrameUVMin", new Vector4(0, 0, 0, 0));
            material.SetVector("_FrameUVMax", new Vector4(1, 1, 0, 0));

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BlankPlayerTools] ✓ Assigned texture '{atlasTexture.name}' to material");
            return true;
        }

        private static bool FixPrefabSetup()
        {
            Debug.Log("[BlankPlayerTools] Fixing prefab setup...");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError($"[BlankPlayerTools] ❌ Could not find prefab at {PREFAB_PATH}");
                return false;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (material == null)
            {
                Debug.LogError($"[BlankPlayerTools] ❌ Could not find material at {MATERIAL_PATH}");
                return false;
            }

            TextAsset atlasJSON = AssetDatabase.LoadAssetAtPath<TextAsset>(JSON_PATH);
            if (atlasJSON == null)
            {
                Debug.LogError($"[BlankPlayerTools] ❌ Could not find JSON at {JSON_PATH}");
                return false;
            }

            // Find or create SpriteRenderer child
            SpriteRenderer spriteRenderer = prefab.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                // Create SpriteRenderer child
                Transform spriteChild = prefab.transform.Find("SpriteRenderer");
                GameObject spriteObject;

                if (spriteChild == null)
                {
                    spriteObject = new GameObject("SpriteRenderer");
                    spriteObject.transform.SetParent(prefab.transform);
                    spriteObject.transform.localPosition = Vector3.zero;
                    spriteObject.transform.localRotation = Quaternion.identity;
                    spriteObject.transform.localScale = Vector3.one;
                    Debug.Log("[BlankPlayerTools] ✓ Created SpriteRenderer child object");
                }
                else
                {
                    spriteObject = spriteChild.gameObject;
                }

                spriteRenderer = spriteObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
                    Debug.Log("[BlankPlayerTools] ✓ Added SpriteRenderer component");
                }
            }

            // Assign material to SpriteRenderer
            spriteRenderer.sharedMaterial = material;

            // Find or add BlankPlayer component
            var blankPlayerComponent = prefab.GetComponent<BugWars.Character.BlankPlayer>();
            if (blankPlayerComponent == null)
            {
                Debug.LogError("[BlankPlayerTools] ❌ BlankPlayer component not found on prefab");
                return false;
            }

            // Use SerializedObject to set private serialized fields
            SerializedObject serializedPrefab = new SerializedObject(blankPlayerComponent);

            // Set atlas JSON
            SerializedProperty atlasJSONProp = serializedPrefab.FindProperty("atlasJSON");
            if (atlasJSONProp != null)
            {
                atlasJSONProp.objectReferenceValue = atlasJSON;
                Debug.Log("[BlankPlayerTools] ✓ Assigned atlas JSON to BlankPlayer component");
            }

            // Set sprite material
            SerializedProperty spriteMaterialProp = serializedPrefab.FindProperty("spriteMaterial");
            if (spriteMaterialProp != null)
            {
                spriteMaterialProp.objectReferenceValue = material;
                Debug.Log("[BlankPlayerTools] ✓ Assigned sprite material to BlankPlayer component");
            }

            // Set sprite renderer reference
            SerializedProperty spriteRendererProp = serializedPrefab.FindProperty("spriteRenderer");
            if (spriteRendererProp != null)
            {
                spriteRendererProp.objectReferenceValue = spriteRenderer;
                Debug.Log("[BlankPlayerTools] ✓ Assigned sprite renderer reference to BlankPlayer component");
            }

            serializedPrefab.ApplyModifiedProperties();

            // Mark prefab as dirty and save
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);

            Debug.Log($"[BlankPlayerTools] ✓ Prefab setup complete");
            return true;
        }

        private static bool VerifySetup()
        {
            Debug.Log("[BlankPlayerTools] Verifying setup...");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (material == null)
            {
                Debug.LogError($"[BlankPlayerTools] ❌ Verification failed: Material not found");
                return false;
            }

            // Check shader
            if (material.shader == null || material.shader.name != SHADER_NAME)
            {
                Debug.LogError($"[BlankPlayerTools] ❌ Material has wrong shader: {material.shader?.name ?? "NULL"}");
                return false;
            }

            // Check texture
            Texture baseMap = material.GetTexture("_BaseMap");
            if (baseMap == null)
            {
                Debug.LogError("[BlankPlayerTools] ❌ Material _BaseMap is NULL");
                return false;
            }

            // Check shader support
            if (!material.shader.isSupported)
            {
                Debug.LogError("[BlankPlayerTools] ❌ Shader is not supported on this platform!");
                return false;
            }

            // Check JSON
            TextAsset atlasJSON = AssetDatabase.LoadAssetAtPath<TextAsset>(JSON_PATH);
            if (atlasJSON == null)
            {
                Debug.LogError("[BlankPlayerTools] ❌ Atlas JSON not found");
                return false;
            }

            // Check prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError("[BlankPlayerTools] ❌ Prefab not found");
                return false;
            }

            var blankPlayerComponent = prefab.GetComponent<BugWars.Character.BlankPlayer>();
            if (blankPlayerComponent == null)
            {
                Debug.LogError("[BlankPlayerTools] ❌ BlankPlayer component not found on prefab");
                return false;
            }

            Debug.Log($"[BlankPlayerTools] ✓ Material: {material.name}");
            Debug.Log($"[BlankPlayerTools] ✓ Shader: {material.shader.name} (supported: {material.shader.isSupported})");
            Debug.Log($"[BlankPlayerTools] ✓ Texture: {baseMap.name} ({baseMap.width}x{baseMap.height})");
            Debug.Log($"[BlankPlayerTools] ✓ JSON: {atlasJSON.name} ({atlasJSON.text.Length} chars)");
            Debug.Log($"[BlankPlayerTools] ✓ Prefab: {prefab.name}");
            Debug.Log($"[BlankPlayerTools] ✓ Component: BlankPlayer attached");

            return true;
        }

        #endregion
    }
}
