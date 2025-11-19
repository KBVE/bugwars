using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BugWars.Editor
{
    /// <summary>
    /// Unity Editor script to automatically generate prefabs from FBX models
    /// Scans Forest/Models folder and creates organized prefab variants
    /// </summary>
    public class EnvironmentPrefabGenerator : EditorWindow
    {
        private string sourceFolderPath = "Assets/BugWars/Prefabs/Forest/Models";
        private string targetPrefabFolder = "Assets/BugWars/Prefabs/Forest/Generated";
        private Material defaultMaterial;

        private int prefabsCreated = 0;
        private List<string> processedFiles = new List<string>();

        [MenuItem("KBVE/Tools/Environment Prefab Generator")]
        public static void ShowWindow()
        {
            GetWindow<EnvironmentPrefabGenerator>("Environment Prefab Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Environment Prefab Generator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This tool scans your FBX models and creates prefab variants organized by type (Trees, Bushes, Rocks, Grass).",
                MessageType.Info);

            GUILayout.Space(10);

            // Source folder
            EditorGUILayout.LabelField("Source Folder (FBX Models):", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            sourceFolderPath = EditorGUILayout.TextField(sourceFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFolderPanel("Select FBX Models Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    sourceFolderPath = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Target folder
            EditorGUILayout.LabelField("Target Prefab Folder:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            targetPrefabFolder = EditorGUILayout.TextField(targetPrefabFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Target Prefab Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    targetPrefabFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Material assignment
            EditorGUILayout.LabelField("Default Material (Optional):", EditorStyles.boldLabel);
            defaultMaterial = (Material)EditorGUILayout.ObjectField(defaultMaterial, typeof(Material), false);

            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Note: EnvironmentManager now loads prefabs automatically from Resources/Prefabs/Forest/ folders.\n" +
                "Make sure to copy generated prefabs to Resources folder for runtime loading.",
                MessageType.Info);

            GUILayout.Space(20);

            // Generate button
            if (GUILayout.Button("Generate Prefabs", GUILayout.Height(40)))
            {
                GeneratePrefabs();
            }

            // Status
            if (prefabsCreated > 0)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox($"Successfully created {prefabsCreated} prefabs!", MessageType.Info);
            }
        }

        private void GeneratePrefabs()
        {
            if (!AssetDatabase.IsValidFolder(sourceFolderPath))
            {
                EditorUtility.DisplayDialog("Error", $"Source folder not found: {sourceFolderPath}", "OK");
                return;
            }

            // Create target folders
            EnsureFoldersExist();

            prefabsCreated = 0;
            processedFiles.Clear();

            // Find all FBX files
            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { sourceFolderPath });
            int totalFiles = fbxGuids.Length;

            if (totalFiles == 0)
            {
                EditorUtility.DisplayDialog("No Models Found", $"No FBX models found in {sourceFolderPath}", "OK");
                return;
            }

            Debug.Log($"[EnvironmentPrefabGenerator] Found {totalFiles} FBX models to process");

            // Process each FBX
            for (int i = 0; i < fbxGuids.Length; i++)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[i]);
                float progress = (float)i / totalFiles;

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Generating Prefabs",
                    $"Processing {Path.GetFileName(fbxPath)} ({i + 1}/{totalFiles})",
                    progress))
                {
                    break;
                }

                ProcessFBXModel(fbxPath);
            }

            EditorUtility.ClearProgressBar();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Complete",
                $"Successfully created {prefabsCreated} prefabs!\n\nCheck: {targetPrefabFolder}",
                "OK");

            Debug.Log($"[EnvironmentPrefabGenerator] Created {prefabsCreated} prefabs in {targetPrefabFolder}");
        }

        private void ProcessFBXModel(string fbxPath)
        {
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxModel == null)
            {
                Debug.LogWarning($"[EnvironmentPrefabGenerator] Failed to load FBX: {fbxPath}");
                return;
            }

            string fileName = Path.GetFileNameWithoutExtension(fbxPath);

            // Determine object type from filename
            BugWars.Terrain.EnvironmentObjectType objectType = DetermineObjectType(fileName);

            // Get subfolder based on type
            string subfolder = GetSubfolderForType(objectType);
            string targetFolder = Path.Combine(targetPrefabFolder, subfolder);

            // Create prefab variant
            string prefabPath = Path.Combine(targetFolder, $"{fileName}.prefab");

            // Instantiate the FBX in the scene temporarily
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);

            // Apply default material if specified
            if (defaultMaterial != null)
            {
                ApplyMaterialToInstance(instance, defaultMaterial);
            }

            // Add cheap collision based on object type
            AddCollisionToInstance(instance, objectType);

            // Save as prefab variant
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

            // Clean up temporary instance
            DestroyImmediate(instance);

            if (prefab != null)
            {
                prefabsCreated++;
                processedFiles.Add(prefabPath);
                Debug.Log($"[EnvironmentPrefabGenerator] Created prefab: {prefabPath}");
            }
        }

        private void ApplyMaterialToInstance(GameObject instance, Material material)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                Material[] materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// Add cheap collision boxes based on object type
        /// Uses simple primitives (capsule for trees, box for rocks, etc.) instead of expensive mesh colliders
        /// </summary>
        private void AddCollisionToInstance(GameObject instance, BugWars.Terrain.EnvironmentObjectType objectType)
        {
            // Remove any existing colliders to avoid duplicates
            Collider[] existingColliders = instance.GetComponentsInChildren<Collider>();
            foreach (var collider in existingColliders)
            {
                DestroyImmediate(collider);
            }

            // Calculate bounds from renderers
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            // Convert to local space
            Vector3 localCenter = instance.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = bounds.size;

            switch (objectType)
            {
                case BugWars.Terrain.EnvironmentObjectType.Tree:
                    // Trees: Capsule collider (cheap, good for cylindrical trunks)
                    CapsuleCollider treeCapsule = instance.AddComponent<CapsuleCollider>();
                    treeCapsule.center = localCenter;
                    treeCapsule.radius = Mathf.Max(localSize.x, localSize.z) * 0.3f; // 30% of width for trunk
                    treeCapsule.height = localSize.y * 0.8f; // 80% of height (ignore top foliage)
                    treeCapsule.direction = 1; // Y-axis
                    Debug.Log($"[Collision] Added CapsuleCollider to tree: radius={treeCapsule.radius:F2}, height={treeCapsule.height:F2}");
                    break;

                case BugWars.Terrain.EnvironmentObjectType.Bush:
                    // Bushes: Sphere collider (cheap, good for round shapes)
                    SphereCollider bushSphere = instance.AddComponent<SphereCollider>();
                    bushSphere.center = localCenter;
                    bushSphere.radius = Mathf.Max(localSize.x, localSize.y, localSize.z) * 0.4f; // 40% of max dimension
                    Debug.Log($"[Collision] Added SphereCollider to bush: radius={bushSphere.radius:F2}");
                    break;

                case BugWars.Terrain.EnvironmentObjectType.Rock:
                    // Rocks: Box collider (cheap, good for angular shapes)
                    BoxCollider rockBox = instance.AddComponent<BoxCollider>();
                    rockBox.center = localCenter;
                    rockBox.size = localSize * 0.9f; // 90% of actual size for better fit
                    Debug.Log($"[Collision] Added BoxCollider to rock: size={rockBox.size}");
                    break;

                case BugWars.Terrain.EnvironmentObjectType.Grass:
                    // Grass: No collider (grass should not block movement)
                    Debug.Log($"[Collision] Skipping collider for grass (non-blocking)");
                    break;

                default:
                    // Default: Box collider
                    BoxCollider defaultBox = instance.AddComponent<BoxCollider>();
                    defaultBox.center = localCenter;
                    defaultBox.size = localSize * 0.9f;
                    Debug.Log($"[Collision] Added default BoxCollider: size={defaultBox.size}");
                    break;
            }
        }

        private BugWars.Terrain.EnvironmentObjectType DetermineObjectType(string fileName)
        {
            string lowerName = fileName.ToLower();

            if (lowerName.Contains("tree"))
                return BugWars.Terrain.EnvironmentObjectType.Tree;
            else if (lowerName.Contains("bush"))
                return BugWars.Terrain.EnvironmentObjectType.Bush;
            else if (lowerName.Contains("rock"))
                return BugWars.Terrain.EnvironmentObjectType.Rock;
            else if (lowerName.Contains("grass"))
                return BugWars.Terrain.EnvironmentObjectType.Grass;
            else
                return BugWars.Terrain.EnvironmentObjectType.Rock; // Default
        }

        private string GetSubfolderForType(BugWars.Terrain.EnvironmentObjectType type)
        {
            switch (type)
            {
                case BugWars.Terrain.EnvironmentObjectType.Tree:
                    return "Trees";
                case BugWars.Terrain.EnvironmentObjectType.Bush:
                    return "Bushes";
                case BugWars.Terrain.EnvironmentObjectType.Rock:
                    return "Rocks";
                case BugWars.Terrain.EnvironmentObjectType.Grass:
                    return "Grass";
                default:
                    return "Other";
            }
        }

        private void EnsureFoldersExist()
        {
            // Create target folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(targetPrefabFolder))
            {
                string parentFolder = Path.GetDirectoryName(targetPrefabFolder).Replace("\\", "/");
                string folderName = Path.GetFileName(targetPrefabFolder);

                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    AssetDatabase.CreateFolder("Assets/BugWars/Prefabs/Forest", "Generated");
                }
                else
                {
                    AssetDatabase.CreateFolder(parentFolder, folderName);
                }
            }

            // Create subfolders for each type
            string[] subfolders = { "Trees", "Bushes", "Rocks", "Grass", "Other" };
            foreach (var subfolder in subfolders)
            {
                string subfolderPath = Path.Combine(targetPrefabFolder, subfolder);
                if (!AssetDatabase.IsValidFolder(subfolderPath))
                {
                    AssetDatabase.CreateFolder(targetPrefabFolder, subfolder);
                }
            }

            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// Helper methods for prefab generation
    /// </summary>
    public static class PrefabGeneratorHelpers
    {

        /// <summary>
        /// Add collision to existing prefabs and assign to EnvironmentManager
        /// </summary>
        [MenuItem("KBVE/Tools/Update Prefabs with Collision + Assign to Manager")]
        public static void UpdateExistingPrefabs()
        {
            string targetPath = "Assets/BugWars/Prefabs/Forest/Generated";

            if (!AssetDatabase.IsValidFolder(targetPath))
            {
                EditorUtility.DisplayDialog("Error", $"Generated prefabs folder not found: {targetPath}", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Update Prefabs",
                "This will:\n1. Add collision boxes to all existing prefabs\n2. Assign them to EnvironmentManager\n\nContinue?",
                "Yes",
                "Cancel");

            if (!proceed)
                return;

            int updated = 0;

            // Process each subfolder
            string[] subfolders = { "Trees", "Bushes", "Rocks", "Grass" };
            foreach (var subfolder in subfolders)
            {
                string folderPath = Path.Combine(targetPath, subfolder);
                if (!AssetDatabase.IsValidFolder(folderPath))
                    continue;

                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
                BugWars.Terrain.EnvironmentObjectType objectType = GetTypeFromSubfolder(subfolder);

                foreach (var guid in prefabGuids)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                    if (prefab == null)
                        continue;

                    // Load prefab for editing
                    GameObject prefabInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                    // Add collision
                    AddCollisionToQuickInstance(prefabInstance, objectType);

                    // Save changes back to prefab
                    PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                    Object.DestroyImmediate(prefabInstance);

                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete",
                $"Updated {updated} prefabs with collision boxes!\n\n" +
                "Note: EnvironmentManager now loads prefabs automatically from Resources/Prefabs/Forest/ folders.\n" +
                "Make sure prefabs are in the correct Resources location for runtime loading.",
                "OK");
            Debug.Log($"[UpdatePrefabs] Updated {updated} prefabs. Ensure they are in Resources/Prefabs/Forest/ for auto-loading.");
        }

        private static BugWars.Terrain.EnvironmentObjectType GetTypeFromSubfolder(string subfolder)
        {
            return subfolder switch
            {
                "Trees" => BugWars.Terrain.EnvironmentObjectType.Tree,
                "Bushes" => BugWars.Terrain.EnvironmentObjectType.Bush,
                "Rocks" => BugWars.Terrain.EnvironmentObjectType.Rock,
                "Grass" => BugWars.Terrain.EnvironmentObjectType.Grass,
                _ => BugWars.Terrain.EnvironmentObjectType.Rock
            };
        }

        /// <summary>
        /// Add cheap collision to prefab instances (same logic as main generator)
        /// </summary>
        private static void AddCollisionToQuickInstance(GameObject instance, BugWars.Terrain.EnvironmentObjectType objectType)
        {
            // Remove existing colliders
            Collider[] existingColliders = instance.GetComponentsInChildren<Collider>();
            foreach (var collider in existingColliders)
            {
                Object.DestroyImmediate(collider);
            }

            // Calculate bounds
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            Vector3 localCenter = instance.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = bounds.size;

            switch (objectType)
            {
                case BugWars.Terrain.EnvironmentObjectType.Tree:
                    CapsuleCollider treeCapsule = instance.AddComponent<CapsuleCollider>();
                    treeCapsule.center = localCenter;
                    treeCapsule.radius = Mathf.Max(localSize.x, localSize.z) * 0.3f;
                    treeCapsule.height = localSize.y * 0.8f;
                    treeCapsule.direction = 1;
                    break;

                case BugWars.Terrain.EnvironmentObjectType.Bush:
                    SphereCollider bushSphere = instance.AddComponent<SphereCollider>();
                    bushSphere.center = localCenter;
                    bushSphere.radius = Mathf.Max(localSize.x, localSize.y, localSize.z) * 0.4f;
                    break;

                case BugWars.Terrain.EnvironmentObjectType.Rock:
                    BoxCollider rockBox = instance.AddComponent<BoxCollider>();
                    rockBox.center = localCenter;
                    rockBox.size = localSize * 0.9f;
                    break;

                case BugWars.Terrain.EnvironmentObjectType.Grass:
                    // No collider for grass
                    break;

                default:
                    BoxCollider defaultBox = instance.AddComponent<BoxCollider>();
                    defaultBox.center = localCenter;
                    defaultBox.size = localSize * 0.9f;
                    break;
            }

            // Add InteractableObject component for R3 interaction system
            AddInteractableComponent(instance, objectType);
        }

        /// <summary>
        /// Add InteractableObject component with appropriate settings
        /// </summary>
        private static void AddInteractableComponent(GameObject instance, BugWars.Terrain.EnvironmentObjectType objectType)
        {
            // Check if InteractableObject type exists (may not be compiled yet)
            var interactableType = System.Type.GetType("BugWars.Interaction.InteractableObject, Assembly-CSharp");
            if (interactableType == null)
            {
                Debug.LogWarning("[PrefabGenerator] InteractableObject class not found. Skipping interaction component.");
                return;
            }

            var interactable = instance.AddComponent(interactableType);

            // Use reflection to set properties since we can't reference the class directly in editor code
            var serializedObject = new UnityEditor.SerializedObject(interactable);

            switch (objectType)
            {
                case BugWars.Terrain.EnvironmentObjectType.Tree:
                    SetInteractableProperties(serializedObject, "Chop", 3f, "Chop", "Wood", 5, 2f);
                    break;

                case BugWars.Terrain.EnvironmentObjectType.Bush:
                    SetInteractableProperties(serializedObject, "Harvest", 2f, "Harvest", "Berries", 3, 1f);
                    break;

                case BugWars.Terrain.EnvironmentObjectType.Rock:
                    SetInteractableProperties(serializedObject, "Mine", 3f, "Mine", "Stone", 4, 3f);
                    break;

                case BugWars.Terrain.EnvironmentObjectType.Grass:
                    SetInteractableProperties(serializedObject, "Harvest", 1.5f, "Harvest", "Herbs", 2, 0.5f);
                    break;
            }

            serializedObject.ApplyModifiedProperties();

            // Set GameObject to Interactable layer for raycasting
            instance.layer = 8; // Interactable layer
            Debug.Log($"[PrefabGenerator] Set {instance.name} to Interactable layer (8)");
        }

        private static void SetInteractableProperties(UnityEditor.SerializedObject obj, string prompt, float distance,
            string interactionType, string resourceType, int amount, float harvestTime)
        {
            obj.FindProperty("interactionPrompt").stringValue = prompt;
            obj.FindProperty("interactionDistance").floatValue = distance;

            // Set enums by finding the index
            var interactionTypeProp = obj.FindProperty("interactionType");
            if (interactionTypeProp != null)
            {
                interactionTypeProp.enumValueIndex = System.Array.IndexOf(interactionTypeProp.enumNames, interactionType);
            }

            var resourceTypeProp = obj.FindProperty("resourceType");
            if (resourceTypeProp != null)
            {
                resourceTypeProp.enumValueIndex = System.Array.IndexOf(resourceTypeProp.enumNames, resourceType);
            }

            obj.FindProperty("resourceAmount").intValue = amount;
            obj.FindProperty("harvestTime").floatValue = harvestTime;
        }
    }
}
