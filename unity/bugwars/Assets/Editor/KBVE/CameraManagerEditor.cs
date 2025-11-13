using UnityEngine;
using UnityEditor;

namespace BugWars.Core.Editor
{
    /// <summary>
    /// Editor utilities for CameraManager and ICameraPreference system
    /// Automatically adds Camera3D and CameraBillboard tags on Unity Editor load
    /// Provides validation and auto-fix tools for player prefab tags
    /// </summary>
    [InitializeOnLoad]
    public static class CameraManagerEditor
    {
        static CameraManagerEditor()
        {
            // Run tag setup when Unity Editor loads
            EnsureCameraTags();
        }

        #region Tag Management

        /// <summary>
        /// Ensures Camera3D and CameraBillboard tags exist in the project
        /// </summary>
        [MenuItem("KBVE/Camera/Ensure Camera Tags")]
        public static void EnsureCameraTags()
        {
            // Get the TagManager asset
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            // Tags to add (from CameraManager.CameraTags)
            string[] requiredTags = new string[]
            {
                CameraTags.Camera3D,
                CameraTags.CameraBillboard
            };

            bool tagsAdded = false;

            foreach (string tag in requiredTags)
            {
                // Check if tag already exists
                bool found = false;
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                    if (t.stringValue.Equals(tag))
                    {
                        found = true;
                        break;
                    }
                }

                // Add tag if it doesn't exist
                if (!found)
                {
                    tagsProp.InsertArrayElementAtIndex(0);
                    SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(0);
                    newTag.stringValue = tag;
                    tagsAdded = true;
                    Debug.Log($"[CameraManager] Added tag: {tag}");
                }
            }

            if (tagsAdded)
            {
                tagManager.ApplyModifiedProperties();
                Debug.Log("[CameraManager] Camera tags setup complete!");
            }
            else
            {
                Debug.Log("[CameraManager] All required camera tags already exist.");
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Menu item to validate all player prefabs have correct tags
        /// </summary>
        [MenuItem("KBVE/Camera/Validate Player Tags")]
        public static void ValidatePlayerTags()
        {
            Debug.Log("[CameraManager] Validating player prefab tags...");

            // Find all prefabs in the project
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BugWars/Prefabs/Character" });

            int validatedCount = 0;
            int warningCount = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                // Check if prefab has an Entity component (player characters)
                var entity = prefab.GetComponent<BugWars.Entity.Entity>();
                if (entity == null) continue;

                validatedCount++;

                // Check if it implements ICameraPreference
                var cameraPreference = prefab.GetComponent<ICameraPreference>();

                if (cameraPreference != null)
                {
                    string expectedTag = cameraPreference.GetExpectedCameraTag();

                    if (!prefab.CompareTag(expectedTag))
                    {
                        Debug.LogWarning($"[CameraManager] {prefab.name} expects tag '{expectedTag}' but has '{prefab.tag}'", prefab);
                        warningCount++;
                    }
                    else
                    {
                        Debug.Log($"[CameraManager] ✓ {prefab.name} correctly tagged as '{expectedTag}'", prefab);
                    }
                }
                else
                {
                    // Character doesn't implement ICameraPreference
                    if (!prefab.CompareTag(CameraTags.Camera3D) &&
                        !prefab.CompareTag(CameraTags.CameraBillboard))
                    {
                        Debug.LogWarning($"[CameraManager] {prefab.name} doesn't implement ICameraPreference and has no camera tag. " +
                                       $"Current tag: '{prefab.tag}'", prefab);
                        warningCount++;
                    }
                }
            }

            Debug.Log($"[CameraManager] Validation complete: {validatedCount} player prefabs checked, {warningCount} warnings.");
        }

        #endregion

        #region Auto-Fix

        /// <summary>
        /// Menu item to auto-fix player prefab tags based on ICameraPreference
        /// </summary>
        [MenuItem("KBVE/Camera/Auto-Fix Player Tags")]
        public static void AutoFixPlayerTags()
        {
            Debug.Log("[CameraManager] Auto-fixing player prefab tags...");

            // Find all prefabs in the character folder
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BugWars/Prefabs/Character" });

            int fixedCount = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                // Check if prefab has an Entity component
                var entity = prefab.GetComponent<BugWars.Entity.Entity>();
                if (entity == null) continue;

                // Check if it implements ICameraPreference
                var cameraPreference = prefab.GetComponent<ICameraPreference>();

                if (cameraPreference != null)
                {
                    string expectedTag = cameraPreference.GetExpectedCameraTag();

                    if (!prefab.CompareTag(expectedTag))
                    {
                        // Load prefab for editing
                        GameObject prefabInstance = PrefabUtility.LoadPrefabContents(path);

                        try
                        {
                            prefabInstance.tag = expectedTag;
                            PrefabUtility.SaveAsPrefabAsset(prefabInstance, path);
                            Debug.Log($"[CameraManager] Fixed: {prefab.name} tagged as '{expectedTag}'", prefab);
                            fixedCount++;
                        }
                        finally
                        {
                            PrefabUtility.UnloadPrefabContents(prefabInstance);
                        }
                    }
                }
            }

            if (fixedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[CameraManager] Auto-fix complete: {fixedCount} prefab(s) updated.");
            }
            else
            {
                Debug.Log("[CameraManager] No prefabs needed fixing.");
            }
        }

        #endregion
    }
}
