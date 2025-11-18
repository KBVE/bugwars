using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BugWars.Editor
{
    /// <summary>
    /// Auto-populates EnvironmentManager with generated prefabs
    /// Scans the Generated folder and creates EnvironmentAsset entries
    /// </summary>
    public static class PopulateEnvironmentAssets
    {
        [MenuItem("KBVE/Tools/Auto-Populate Environment Assets")]
        public static void PopulateAssets()
        {
            // Find EnvironmentManager in the scene
            BugWars.Terrain.EnvironmentManager envManager = Object.FindFirstObjectByType<BugWars.Terrain.EnvironmentManager>();

            if (envManager == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "EnvironmentManager not found in scene. Make sure GameLifetimeScope is in the scene.",
                    "OK");
                return;
            }

            string generatedPath = "Assets/BugWars/Prefabs/Forest/Generated";

            if (!AssetDatabase.IsValidFolder(generatedPath))
            {
                EditorUtility.DisplayDialog("Error",
                    $"Generated prefabs folder not found: {generatedPath}\nRun 'Quick Generate Forest Prefabs' first.",
                    "OK");
                return;
            }

            // Use SerializedObject to modify the EnvironmentManager
            SerializedObject serializedManager = new SerializedObject(envManager);

            int treesAdded = PopulatePrefabList(serializedManager, "treeAssets", Path.Combine(generatedPath, "Trees"), BugWars.Terrain.EnvironmentObjectType.Tree);
            int bushesAdded = PopulatePrefabList(serializedManager, "bushAssets", Path.Combine(generatedPath, "Bushes"), BugWars.Terrain.EnvironmentObjectType.Bush);
            int rocksAdded = PopulatePrefabList(serializedManager, "rockAssets", Path.Combine(generatedPath, "Rocks"), BugWars.Terrain.EnvironmentObjectType.Rock);
            int grassAdded = PopulatePrefabList(serializedManager, "grassAssets", Path.Combine(generatedPath, "Grass"), BugWars.Terrain.EnvironmentObjectType.Grass);

            // Apply changes
            serializedManager.ApplyModifiedProperties();
            EditorUtility.SetDirty(envManager);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Success",
                $"Populated EnvironmentManager with:\n" +
                $"• {treesAdded} Trees\n" +
                $"• {bushesAdded} Bushes\n" +
                $"• {rocksAdded} Rocks\n" +
                $"• {grassAdded} Grass",
                "OK");

            Debug.Log($"[PopulateEnvironmentAssets] Added {treesAdded + bushesAdded + rocksAdded + grassAdded} prefabs to EnvironmentManager");
        }

        private static int PopulatePrefabList(SerializedObject serializedManager, string propertyName, string folderPath, BugWars.Terrain.EnvironmentObjectType type)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[PopulateEnvironmentAssets] Folder not found: {folderPath}");
                return 0;
            }

            // Find all prefabs in the folder
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

            if (prefabGuids.Length == 0)
            {
                Debug.LogWarning($"[PopulateEnvironmentAssets] No prefabs found in {folderPath}");
                return 0;
            }

            // Get the array property
            SerializedProperty arrayProperty = serializedManager.FindProperty(propertyName);

            if (arrayProperty == null)
            {
                Debug.LogError($"[PopulateEnvironmentAssets] Property '{propertyName}' not found on EnvironmentManager");
                return 0;
            }

            // Clear existing array
            arrayProperty.ClearArray();

            int added = 0;

            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                    continue;

                // Add new element to array
                arrayProperty.InsertArrayElementAtIndex(arrayProperty.arraySize);
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(arrayProperty.arraySize - 1);

                // Set properties on EnvironmentAsset
                element.FindPropertyRelative("assetName").stringValue = prefab.name;
                element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                element.FindPropertyRelative("type").enumValueIndex = (int)type;
                element.FindPropertyRelative("spawnWeight").floatValue = 1f;

                // Set scale variation based on type
                switch (type)
                {
                    case BugWars.Terrain.EnvironmentObjectType.Tree:
                        element.FindPropertyRelative("minScale").floatValue = 0.9f;
                        element.FindPropertyRelative("maxScale").floatValue = 1.3f;
                        break;
                    case BugWars.Terrain.EnvironmentObjectType.Bush:
                        element.FindPropertyRelative("minScale").floatValue = 0.8f;
                        element.FindPropertyRelative("maxScale").floatValue = 1.2f;
                        break;
                    case BugWars.Terrain.EnvironmentObjectType.Rock:
                        element.FindPropertyRelative("minScale").floatValue = 0.7f;
                        element.FindPropertyRelative("maxScale").floatValue = 1.4f;
                        break;
                    case BugWars.Terrain.EnvironmentObjectType.Grass:
                        element.FindPropertyRelative("minScale").floatValue = 0.9f;
                        element.FindPropertyRelative("maxScale").floatValue = 1.1f;
                        break;
                }

                added++;
            }

            Debug.Log($"[PopulateEnvironmentAssets] Added {added} {type} prefabs from {folderPath}");
            return added;
        }
    }
}
