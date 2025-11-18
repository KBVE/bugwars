using UnityEngine;
using UnityEditor;
using BugWars.Core;

namespace BugWars.Editor
{
    /// <summary>
    /// Utility to assign InteractionPromptUIToolkit prefab to GameLifetimeScope
    /// </summary>
    public static class AssignInteractionUIToPrefab
    {
        [MenuItem("KBVE/Tools/Assign Interaction UI to GameLifetimeScope")]
        public static void AssignInteractionUI()
        {
            // Load the GameLifetimeScope prefab
            string prefabPath = "Assets/Scripts/Core/GameLifetimeScope.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Error", $"GameLifetimeScope prefab not found at:\n{prefabPath}", "OK");
                return;
            }

            // Load the InteractionPromptUIToolkit prefab
            string uiPrefabPath = "Assets/BugWars/UI/HUD/InteractionPromptUIToolkit.prefab";
            GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(uiPrefabPath);

            if (uiPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", $"InteractionPromptUIToolkit prefab not found at:\n{uiPrefabPath}", "OK");
                return;
            }

            // Open prefab in edit mode
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

            // Get the GameLifetimeScope component
            GameLifetimeScope lifetimeScope = prefabRoot.GetComponent<GameLifetimeScope>();

            if (lifetimeScope == null)
            {
                EditorUtility.DisplayDialog("Error", "GameLifetimeScope component not found on prefab!", "OK");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            // Assign the UI prefab using SerializedObject
            SerializedObject serializedScope = new SerializedObject(lifetimeScope);
            SerializedProperty uiPrefabProperty = serializedScope.FindProperty("interactionPromptUIPrefab");

            if (uiPrefabProperty == null)
            {
                EditorUtility.DisplayDialog("Error", "interactionPromptUIPrefab field not found on GameLifetimeScope!", "OK");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            uiPrefabProperty.objectReferenceValue = uiPrefab;
            serializedScope.ApplyModifiedProperties();

            // Save the prefab
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success",
                $"Assigned InteractionPromptUIToolkit prefab to GameLifetimeScope!\n\n" +
                $"Please restart Play Mode for changes to take effect.",
                "OK");

            Debug.Log("[AssignInteractionUI] Successfully assigned InteractionPromptUIToolkit to GameLifetimeScope prefab");
        }
    }
}
