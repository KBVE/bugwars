using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace BugWars.Editor
{
    /// <summary>
    /// Generates the InteractionPrompt UI Canvas prefab
    /// Creates a world-space canvas with interaction prompt and progress bar
    /// </summary>
    public static class InteractionUIGenerator
    {
        [MenuItem("KBVE/Tools/Create Interaction Prompt UI")]
        public static void CreateInteractionPromptUI()
        {
            // Create main Canvas
            GameObject canvasGO = new GameObject("InteractionPromptCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Set canvas size
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200, 100);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f); // Scale down for world space

            // Create Prompt Panel (background)
            GameObject panelGO = new GameObject("PromptPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);

            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(180, 80);

            Image panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f); // Semi-transparent black

            // Create Prompt Text
            GameObject textGO = new GameObject("PromptText");
            textGO.transform.SetParent(panelGO.transform, false);

            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.3f);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();
            tmpText.text = "Tree\nPress E to chop";
            tmpText.fontSize = 14;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;
            tmpText.enableWordWrapping = true;

            // Create Progress Bar Background
            GameObject progressBGGO = new GameObject("ProgressBarBackground");
            progressBGGO.transform.SetParent(panelGO.transform, false);

            RectTransform progressBGRect = progressBGGO.AddComponent<RectTransform>();
            progressBGRect.anchorMin = new Vector2(0.1f, 0.1f);
            progressBGRect.anchorMax = new Vector2(0.9f, 0.25f);
            progressBGRect.offsetMin = Vector2.zero;
            progressBGRect.offsetMax = Vector2.zero;

            Image progressBGImage = progressBGGO.AddComponent<Image>();
            progressBGImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Create Progress Bar Fill
            GameObject progressFillGO = new GameObject("ProgressBarFill");
            progressFillGO.transform.SetParent(progressBGGO.transform, false);

            RectTransform progressFillRect = progressFillGO.AddComponent<RectTransform>();
            progressFillRect.anchorMin = new Vector2(0, 0);
            progressFillRect.anchorMax = new Vector2(1, 1);
            progressFillRect.offsetMin = new Vector2(2, 2);
            progressFillRect.offsetMax = new Vector2(-2, -2);

            Image progressFillImage = progressFillGO.AddComponent<Image>();
            progressFillImage.color = new Color(0.2f, 0.8f, 0.3f, 1f); // Green
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = 0;
            progressFillImage.fillAmount = 0f;

            // Add InteractionPromptUI component
            var interactionUI = canvasGO.AddComponent<BugWars.Interaction.InteractionPromptUI>();

            // Use SerializedObject to set private fields
            SerializedObject serializedUI = new SerializedObject(interactionUI);
            serializedUI.FindProperty("promptPanel").objectReferenceValue = panelGO;
            serializedUI.FindProperty("promptText").objectReferenceValue = tmpText;
            serializedUI.FindProperty("progressBar").objectReferenceValue = progressFillImage;
            serializedUI.FindProperty("useWorldSpace").boolValue = true;
            serializedUI.FindProperty("worldOffset").vector3Value = new Vector3(0, 2.5f, 0);
            serializedUI.ApplyModifiedProperties();

            // Hide the progress bar initially
            progressBGGO.SetActive(false);

            // Save as prefab
            string prefabPath = "Assets/BugWars/UI/HUD/InteractionPromptCanvas.prefab";

            // Create the prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasGO, prefabPath);

            // Clean up scene object
            Object.DestroyImmediate(canvasGO);

            // Select the prefab in the project window
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            EditorUtility.DisplayDialog("Success",
                $"Created InteractionPromptCanvas prefab at:\n{prefabPath}\n\nAdd this to your game scene and ensure InteractionManager is registered in GameLifetimeScope.",
                "OK");

            Debug.Log($"[InteractionUIGenerator] Created InteractionPromptCanvas prefab at {prefabPath}");
        }

        [MenuItem("KBVE/Tools/Create Screen-Space Interaction Prompt UI")]
        public static void CreateScreenSpaceInteractionPromptUI()
        {
            // Create main Canvas (Screen Space Overlay)
            GameObject canvasGO = new GameObject("InteractionPromptCanvasScreenSpace");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Render on top

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Create Prompt Panel (centered bottom)
            GameObject panelGO = new GameObject("PromptPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);

            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0);
            panelRect.anchorMax = new Vector2(0.5f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 150); // 150px from bottom
            panelRect.sizeDelta = new Vector2(400, 120);

            Image panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);

            // Create Prompt Text
            GameObject textGO = new GameObject("PromptText");
            textGO.transform.SetParent(panelGO.transform, false);

            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.35f);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(20, 10);
            textRect.offsetMax = new Vector2(-20, -10);

            TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();
            tmpText.text = "Tree\nPress E to chop";
            tmpText.fontSize = 24;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;
            tmpText.enableWordWrapping = true;
            tmpText.fontStyle = FontStyles.Bold;

            // Create Progress Bar Background
            GameObject progressBGGO = new GameObject("ProgressBarBackground");
            progressBGGO.transform.SetParent(panelGO.transform, false);

            RectTransform progressBGRect = progressBGGO.AddComponent<RectTransform>();
            progressBGRect.anchorMin = new Vector2(0.1f, 0.1f);
            progressBGRect.anchorMax = new Vector2(0.9f, 0.3f);
            progressBGRect.offsetMin = Vector2.zero;
            progressBGRect.offsetMax = Vector2.zero;

            Image progressBGImage = progressBGGO.AddComponent<Image>();
            progressBGImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Create Progress Bar Fill
            GameObject progressFillGO = new GameObject("ProgressBarFill");
            progressFillGO.transform.SetParent(progressBGGO.transform, false);

            RectTransform progressFillRect = progressFillGO.AddComponent<RectTransform>();
            progressFillRect.anchorMin = new Vector2(0, 0);
            progressFillRect.anchorMax = new Vector2(1, 1);
            progressFillRect.offsetMin = new Vector2(4, 4);
            progressFillRect.offsetMax = new Vector2(-4, -4);

            Image progressFillImage = progressFillGO.AddComponent<Image>();
            progressFillImage.color = new Color(0.3f, 0.9f, 0.4f, 1f);
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = 0;
            progressFillImage.fillAmount = 0f;

            // Add InteractionPromptUI component
            var interactionUI = canvasGO.AddComponent<BugWars.Interaction.InteractionPromptUI>();

            // Use SerializedObject to set private fields
            SerializedObject serializedUI = new SerializedObject(interactionUI);
            serializedUI.FindProperty("promptPanel").objectReferenceValue = panelGO;
            serializedUI.FindProperty("promptText").objectReferenceValue = tmpText;
            serializedUI.FindProperty("progressBar").objectReferenceValue = progressFillImage;
            serializedUI.FindProperty("useWorldSpace").boolValue = false;
            serializedUI.ApplyModifiedProperties();

            // Hide the progress bar initially
            progressBGGO.SetActive(false);

            // Save as prefab
            string prefabPath = "Assets/BugWars/UI/HUD/InteractionPromptCanvasScreenSpace.prefab";

            // Create the prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasGO, prefabPath);

            // Clean up scene object
            Object.DestroyImmediate(canvasGO);

            // Select the prefab in the project window
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            EditorUtility.DisplayDialog("Success",
                $"Created Screen-Space InteractionPromptCanvas prefab at:\n{prefabPath}\n\nAdd this to your game scene and ensure InteractionManager is registered in GameLifetimeScope.",
                "OK");

            Debug.Log($"[InteractionUIGenerator] Created Screen-Space InteractionPromptCanvas prefab at {prefabPath}");
        }
    }
}
