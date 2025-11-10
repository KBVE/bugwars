using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BugWars.UI.Editor
{
    /// <summary>
    /// Editor utility to create PanelSettings asset
    /// </summary>
    public static class CreatePanelSettings
    {
        [MenuItem("Assets/Create/BugWars/Main Menu Panel Settings")]
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

            // Save asset
            string path = "Assets/BugWars/UI/MainMenu/MainMenuPanelSettings.asset";
            AssetDatabase.CreateAsset(panelSettings, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the created asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = panelSettings;

            Debug.Log($"[CreatePanelSettings] Created PanelSettings at {path}");
        }
    }
}
