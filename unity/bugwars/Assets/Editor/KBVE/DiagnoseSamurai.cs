using UnityEngine;
using UnityEditor;
using BugWars.Character;

namespace BugWars.Editor
{
    /// <summary>
    /// Diagnostic tool to check Samurai setup and identify rendering issues
    /// </summary>
    public class DiagnoseSamurai : EditorWindow
    {
        [MenuItem("KBVE/Diagnose Samurai Setup")]
        public static void Diagnose()
        {
            Debug.Log("=== SAMURAI DIAGNOSTIC REPORT ===\n");

            // 1. Check Prefab
            string prefabPath = "Assets/BugWars/Prefabs/Character/Samurai/Samurai.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogError($"❌ Prefab not found at {prefabPath}");
                return;
            }
            Debug.Log($"✓ Prefab found: {prefabPath}");

            // 2. Check Samurai Component
            Samurai samurai = prefab.GetComponent<Samurai>();
            if (samurai == null)
            {
                Debug.LogError("❌ Samurai component not found on prefab");
                return;
            }
            Debug.Log("✓ Samurai component found");

            // 3. Check SpriteRenderer child
            Transform spriteRendererTransform = prefab.transform.Find("SpriteRenderer");
            if (spriteRendererTransform == null)
            {
                Debug.LogError("❌ SpriteRenderer child GameObject not found");
                return;
            }
            Debug.Log($"✓ SpriteRenderer GameObject found at: {spriteRendererTransform.localPosition}");

            SpriteRenderer spriteRenderer = spriteRendererTransform.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("❌ SpriteRenderer component not found");
                return;
            }
            Debug.Log("✓ SpriteRenderer component found");

            // 4. Check if sprite is assigned
            if (spriteRenderer.sprite == null)
            {
                Debug.LogWarning("⚠️  No sprite assigned to SpriteRenderer");
                Debug.LogWarning("   The SpriteRenderer needs a sprite for geometry,");
                Debug.LogWarning("   even when using a custom shader.");
                Debug.LogWarning("   Run 'KBVE/Fix Samurai Prefab' to assign a default sprite.");
            }
            else
            {
                Debug.Log($"✓ Sprite assigned: {spriteRenderer.sprite.name}");
            }

            // 5. Check Material
            if (spriteRenderer.sharedMaterial == null)
            {
                Debug.LogError("❌ No material assigned to SpriteRenderer");
            }
            else
            {
                Debug.Log($"✓ Material assigned: {spriteRenderer.sharedMaterial.name}");
                Debug.Log($"  Shader: {spriteRenderer.sharedMaterial.shader.name}");
            }

            // 6. Check Atlas JSON (via SerializedObject)
            SerializedObject so = new SerializedObject(samurai);
            SerializedProperty atlasJSONProp = so.FindProperty("atlasJSON");
            if (atlasJSONProp.objectReferenceValue == null)
            {
                Debug.LogError("❌ Atlas JSON not assigned to Samurai component");
            }
            else
            {
                Debug.Log($"✓ Atlas JSON assigned: {atlasJSONProp.objectReferenceValue.name}");
            }

            // 7. Check Sprite Material (via SerializedObject)
            SerializedProperty spriteMaterialProp = so.FindProperty("spriteMaterial");
            if (spriteMaterialProp.objectReferenceValue == null)
            {
                Debug.LogError("❌ Sprite Material not assigned to Samurai component");
            }
            else
            {
                Material mat = spriteMaterialProp.objectReferenceValue as Material;
                Debug.Log($"✓ Sprite Material assigned: {mat.name}");

                // Check if material has the atlas texture
                if (mat.mainTexture == null)
                {
                    Debug.LogWarning("⚠️  Material has no main texture (Sprite Sheet)");
                }
                else
                {
                    Debug.Log($"  Texture: {mat.mainTexture.name}");
                }
            }

            // 8. Check Entity Base Class Setup
            var entity = prefab.GetComponent<BugWars.Entity.Entity>();
            if (entity == null)
            {
                Debug.LogError("❌ Entity component not found (Samurai should extend Entity)");
            }
            else
            {
                Debug.Log("✓ Entity component found");

                SerializedObject entitySO = new SerializedObject(entity);
                SerializedProperty entitySpriteRenderer = entitySO.FindProperty("spriteRenderer");
                if (entitySpriteRenderer.objectReferenceValue == null)
                {
                    Debug.LogWarning("⚠️  SpriteRenderer not assigned in Entity component");
                    Debug.LogWarning("   This should be assigned in the prefab Inspector");
                }
                else
                {
                    Debug.Log($"✓ SpriteRenderer assigned in Entity component");
                }
            }

            // 9. Check Rigidbody and Collider
            var rb = prefab.GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogWarning("⚠️  Rigidbody not found (required by Entity)");
            }
            else
            {
                Debug.Log("✓ Rigidbody found");
            }

            var collider = prefab.GetComponent<CapsuleCollider>();
            if (collider == null)
            {
                Debug.LogWarning("⚠️  CapsuleCollider not found (required by Entity)");
            }
            else
            {
                Debug.Log("✓ CapsuleCollider found");
            }

            Debug.Log("\n=== END DIAGNOSTIC REPORT ===");
            Debug.Log("\nIf there are any ❌ or ⚠️ warnings above, address them to fix rendering issues.");
        }
    }
}
