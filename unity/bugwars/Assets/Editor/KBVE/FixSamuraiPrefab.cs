using UnityEngine;
using UnityEditor;

namespace BugWars.Editor
{
    /// <summary>
    /// Editor utility to fix the Samurai prefab by assigning a default sprite to the SpriteRenderer.
    /// The SpriteRenderer needs a sprite assigned even when using a custom shader.
    /// </summary>
    public class FixSamuraiPrefab : EditorWindow
    {
        [MenuItem("KBVE/Fix Samurai Prefab")]
        public static void Fix()
        {
            // Load the Samurai prefab
            string prefabPath = "Assets/BugWars/Prefabs/Character/Samurai/Samurai.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[FixSamuraiPrefab] Could not find prefab at {prefabPath}");
                return;
            }

            // Find the SpriteRenderer child
            Transform spriteRendererTransform = prefab.transform.Find("SpriteRenderer");
            if (spriteRendererTransform == null)
            {
                Debug.LogError("[FixSamuraiPrefab] Could not find SpriteRenderer child in prefab");
                return;
            }

            SpriteRenderer spriteRenderer = spriteRendererTransform.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("[FixSamuraiPrefab] SpriteRenderer component not found");
                return;
            }

            // Check if sprite is already assigned
            if (spriteRenderer.sprite != null)
            {
                Debug.Log("[FixSamuraiPrefab] Sprite is already assigned. No changes needed.");
                return;
            }

            // Create a custom sprite with proper dimensions for the Samurai
            // The Samurai frames are 128x256 pixels (from SETUP.md: Pixels Per Unit: 128)
            Texture2D whiteTexture = Texture2D.whiteTexture;

            // Create sprite with 128 pixels per unit to match the atlas
            // Make it 128x256 (1x2 units) to match samurai proportions
            Sprite customSprite = Sprite.Create(
                whiteTexture,
                new Rect(0, 0, whiteTexture.width, whiteTexture.height),
                new Vector2(0.5f, 0.5f), // pivot at center
                128f // pixels per unit - IMPORTANT: must match atlas setting!
            );

            spriteRenderer.sprite = customSprite;

            // Fallback: Try to use Unity's built-in sprite if custom creation fails
            if (spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            }

            if (spriteRenderer.sprite == null)
            {
                // Last fallback
                spriteRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            }

            // Save the changes to the prefab
            PrefabUtility.SavePrefabAsset(prefab);

            Debug.Log("[FixSamuraiPrefab] Successfully assigned default sprite to Samurai prefab's SpriteRenderer");
            Debug.Log($"[FixSamuraiPrefab] Sprite assigned: {spriteRenderer.sprite?.name}");
        }
    }
}
