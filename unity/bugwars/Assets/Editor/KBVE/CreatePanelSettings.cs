#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KBVE.Editor
{
    /// <summary>
    /// KBVE Editor Utilities for creating UI Toolkit assets
    /// </summary>
    public static class KBVEEditorUtilities
    {
        [MenuItem("KBVE/UI Toolkit/Create Panel Settings")]
        public static void CreatePanelSettings()
        {
            // Create PanelSettings instance
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            // Configure settings
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.scale = 1f;
            panelSettings.fallbackDpi = 96f;
            panelSettings.referenceDpi = 96f;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0f;
            panelSettings.sortingOrder = 0;
            panelSettings.clearDepthStencil = true;
            panelSettings.clearColor = false;

            // Get the selected folder or use default
            string path = "Assets";

            // If something is selected, try to get its path
            if (Selection.activeObject != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // If it's a folder, use it; otherwise use its parent folder
                    if (System.IO.Directory.Exists(selectedPath))
                    {
                        path = selectedPath;
                    }
                    else
                    {
                        path = System.IO.Path.GetDirectoryName(selectedPath);
                    }
                }
            }

            // Create unique filename
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{path}/PanelSettings.asset");

            // Save asset
            AssetDatabase.CreateAsset(panelSettings, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the created asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = panelSettings;
        }

        [MenuItem("KBVE/UI Toolkit/Create Main Menu Panel Settings")]
        public static void CreateMainMenuPanelSettings()
        {
            // Create PanelSettings instance
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            // Configure settings
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.scale = 1f;
            panelSettings.fallbackDpi = 96f;
            panelSettings.referenceDpi = 96f;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0f;
            panelSettings.sortingOrder = 0;
            panelSettings.clearDepthStencil = true;
            panelSettings.clearColor = false;

            // Save asset to specific location
            string path = "Assets/BugWars/UI/MainMenu/MainMenuPanelSettings.asset";

            // Ensure directory exists
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(panelSettings, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the created asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = panelSettings;
        }
    }
}
#endif
