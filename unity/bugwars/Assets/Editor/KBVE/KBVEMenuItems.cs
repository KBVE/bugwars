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

            Debug.Log("[KBVE] Console cleared");
        }

        [MenuItem("KBVE/Quick Actions/Refresh Assets _F5", false, 2)]
        public static void RefreshAssets()
        {
            AssetDatabase.Refresh();
            Debug.Log("[KBVE] Assets refreshed");
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
                Debug.Log("[KBVE] All assets reimported");
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
                Debug.Log("[KBVE] PlayerPrefs deleted");
            }
        }

        [MenuItem("KBVE/Scene Management/Save Scene _&s", false, 100)]
        public static void SaveScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[KBVE] Scene saved");
        }

        [MenuItem("KBVE/Scene Management/New Scene", false, 101)]
        public static void NewScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            Debug.Log("[KBVE] New scene created");
        }

        [MenuItem("KBVE/Debug/Log System Info", false, 200)]
        public static void LogSystemInfo()
        {
            Debug.Log("=== KBVE System Info ===");
            Debug.Log($"Unity Version: {Application.unityVersion}");
            Debug.Log($"Platform: {Application.platform}");
            Debug.Log($"Graphics Device: {SystemInfo.graphicsDeviceName}");
            Debug.Log($"Processor: {SystemInfo.processorType} ({SystemInfo.processorCount} cores)");
            Debug.Log($"RAM: {SystemInfo.systemMemorySize}MB");
            Debug.Log($"Screen Resolution: {Screen.currentResolution}");
        }
    }
}
#endif
