#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KBVE.Editor
{
    /// <summary>
    /// KBVE Top-level menu items for common editor operations
    /// </summary>
    public static class KBVEMenuItems
    {
        [MenuItem("KBVE/Quick Actions/Clear Console _F1", false, 1)]
        public static void ClearConsole()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
        }

        [MenuItem("KBVE/Quick Actions/Refresh Assets _F5", false, 2)]
        public static void RefreshAssets()
        {
            AssetDatabase.Refresh();
        }

        [MenuItem("KBVE/Quick Actions/Reimport All Assets", false, 3)]
        public static void ReimportAllAssets()
        {
            if (EditorUtility.DisplayDialog(
                "Reimport All Assets",
                "This will reimport all assets in the project. This may take a while. Continue?",
                "Yes", "No"))
            {
                AssetDatabase.ImportAsset("Assets", ImportAssetOptions.ImportRecursive);
            }
        }

        [MenuItem("KBVE/Quick Actions/Reset Serialized Settings", false, 4)]
        public static void ResetSerializedSettings()
        {
            const string prefabPath = "Assets/BugWars/Prefabs/Core/GameManager.prefab";

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "KBVE – Reset Serialized Settings",
                    $"Could not load prefab at '{prefabPath}'.",
                    "OK");
                return;
            }

            bool anyChanges = false;
            try
            {
                var components = prefabRoot.GetComponentsInChildren<Component>(true);
                foreach (var component in components)
                {
                    if (component == null)
                        continue;

                    if (component is Transform || component is RectTransform)
                        continue;

                    var componentType = component.GetType();
                    var tempGameObject = new GameObject("KBVE_ResetTemp", componentType);
                    var templateComponent = tempGameObject.GetComponent(componentType);

                    if (UnityEditorInternal.ComponentUtility.CopyComponent(templateComponent))
                    {
                        UnityEditorInternal.ComponentUtility.PasteComponentValues(component);
                        EditorUtility.SetDirty(component);
                        anyChanges = true;
                    }

                    Object.DestroyImmediate(tempGameObject);
                }

                if (anyChanges)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog(
                        "KBVE – Reset Serialized Settings",
                        "All serialized component settings in the GameManager prefab have been reset to their script defaults.",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "KBVE – Reset Serialized Settings",
                        "No components were reset.",
                        "OK");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        [MenuItem("KBVE/Quick Actions/Delete PlayerPrefs", false, 20)]
        public static void DeletePlayerPrefs()
        {
            if (EditorUtility.DisplayDialog(
                "Delete PlayerPrefs",
                "This will delete all PlayerPrefs data. Continue?",
                "Yes", "No"))
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }
        }

        [MenuItem("KBVE/Scene Management/Save Scene _&s", false, 100)]
        public static void SaveScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        [MenuItem("KBVE/Scene Management/New Scene", false, 101)]
        public static void NewScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);
        }

        [MenuItem("KBVE/Debug/Log System Info", false, 200)]
        public static void LogSystemInfo()
        {
            // Debug logging removed - use defensive logging only
        }
    }
}
#endif
