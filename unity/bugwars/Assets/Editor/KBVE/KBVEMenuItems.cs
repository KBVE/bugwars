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
