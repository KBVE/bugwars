using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;

namespace BugWars.Editor
{
    /// <summary>
    /// Generates the InteractionPrompt UI prefab using UI Toolkit (UXML/USS)
    /// Supports both screen-space and world-space rendering
    /// </summary>
    public static class InteractionUIGenerator
    {
        [MenuItem("KBVE/UI Toolkit/Create Interaction Prompt UI")]
        public static void CreateInteractionPromptUI()
        {
            // Ask user which mode they want
            int choice = EditorUtility.DisplayDialogComplex(
                "Create Interaction Prompt UI",
                "Choose the rendering mode for the interaction prompt:",
                "Screen-Space",  // 0
                "World-Space",   // 1
                "Cancel"         // 2
            );

            if (choice == 2) // Cancel
                return;

            bool isWorldSpace = (choice == 1);

            CreateInteractionPromptUIInternal(isWorldSpace);
        }

        private static void CreateInteractionPromptUIInternal(bool isWorldSpace)
        {
            // Load UXML and USS assets
            string uxmlPath = "Assets/BugWars/UI/HUD/interaction_prompt.uxml";
            string ussPath = "Assets/BugWars/UI/HUD/interaction_prompt.uss";

            VisualTreeAsset uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            StyleSheet ussAsset = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);

            if (uxmlAsset == null)
            {
                EditorUtility.DisplayDialog("Error", $"UXML file not found at:\n{uxmlPath}\n\nPlease ensure the UXML file exists.", "OK");
                return;
            }

            string prefabName = isWorldSpace ? "InteractionPromptUIToolkitWorldSpace" : "InteractionPromptUIToolkit";
            string prefabPath = $"Assets/BugWars/UI/HUD/{prefabName}.prefab";

            // Create UIDocument GameObject
            GameObject uiDocGO = new GameObject(prefabName);
            UIDocument uiDocument = uiDocGO.AddComponent<UIDocument>();
            uiDocument.visualTreeAsset = uxmlAsset;

            // World-space specific setup
            string panelSettingsPath = null;
            if (isWorldSpace)
            {
                // Create PanelSettings for world-space rendering
                PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
                panelSettings.scale = 1f;
                panelSettings.targetDisplay = 0;
                panelSettings.clearDepthStencil = false;
                panelSettings.clearColor = false;
                panelSettings.sortingOrder = 0;

                // Save PanelSettings as asset
                panelSettingsPath = "Assets/BugWars/UI/HUD/InteractionPromptWorldSpacePanelSettings.asset";
                AssetDatabase.CreateAsset(panelSettings, panelSettingsPath);

                uiDocument.panelSettings = panelSettings;
            }

            // Apply USS if available
            if (ussAsset != null)
            {
                uiDocument.rootVisualElement.styleSheets.Add(ussAsset);
            }

            // Add InteractionPromptUIToolkit component
            var interactionUI = uiDocGO.AddComponent<BugWars.Interaction.InteractionPromptUIToolkit>();

            // Set world-space flag using SerializedObject
            if (isWorldSpace)
            {
                SerializedObject serializedUI = new SerializedObject(interactionUI);
                serializedUI.FindProperty("useWorldSpace").boolValue = true;
                serializedUI.FindProperty("worldOffset").vector3Value = new Vector3(0, 2.5f, 0);
                serializedUI.ApplyModifiedProperties();
            }

            // Create the prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(uiDocGO, prefabPath);

            // Clean up scene object
            Object.DestroyImmediate(uiDocGO);

            // Select the prefab in the project window
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Success message
            string modeText = isWorldSpace ? "World-Space" : "Screen-Space";
            string additionalInfo = isWorldSpace ? $"\n\nPanel Settings saved at:\n{panelSettingsPath}" : "";

            EditorUtility.DisplayDialog("Success",
                $"Created UI Toolkit {modeText} InteractionPrompt prefab at:\n{prefabPath}{additionalInfo}\n\nAdd this to your game scene and ensure InteractionManager is registered in GameLifetimeScope.",
                "OK");

            Debug.Log($"[InteractionUIGenerator] Created UI Toolkit {modeText} InteractionPrompt prefab at {prefabPath}");
        }
    }
}
