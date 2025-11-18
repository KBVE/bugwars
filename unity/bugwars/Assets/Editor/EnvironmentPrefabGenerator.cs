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
        private bool autoAssignToEnvironmentManager = true;
        private BugWars.Terrain.EnvironmentManager environmentManager;

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

            // Environment Manager assignment
            autoAssignToEnvironmentManager = EditorGUILayout.Toggle("Auto-assign to EnvironmentManager", autoAssignToEnvironmentManager);
            if (autoAssignToEnvironmentManager)
            {
                environmentManager = (BugWars.Terrain.EnvironmentManager)EditorGUILayout.ObjectField(
                    "Environment Manager",
                    environmentManager,
                    typeof(BugWars.Terrain.EnvironmentManager),
                    true);
            }

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
    /// Quick menu item to generate prefabs with default settings
    /// </summary>
    public static class QuickEnvironmentPrefabGenerator
    {
        [MenuItem("KBVE/Tools/Quick Generate Forest Prefabs")]
        public static void QuickGenerate()
        {
            string sourcePath = "Assets/BugWars/Prefabs/Forest/Models";
            string targetPath = "Assets/BugWars/Prefabs/Forest/Generated";

            if (!AssetDatabase.IsValidFolder(sourcePath))
            {
                EditorUtility.DisplayDialog("Error", $"Source folder not found: {sourcePath}", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Quick Generate Prefabs",
                $"This will generate prefabs from:\n{sourcePath}\n\nTo:\n{targetPath}\n\nContinue?",
                "Yes",
                "Cancel");

            if (!proceed)
                return;

            // Create target folders
            EnsureTargetFoldersExist(targetPath);

            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { sourcePath });
            int created = 0;

            foreach (var guid in fbxGuids)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

                if (fbxModel == null)
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(fbxPath);
                var objectType = DetermineType(fileName);
                string subfolder = GetSubfolder(objectType);
                string prefabPath = Path.Combine(targetPath, subfolder, $"{fileName}.prefab");

                // Skip if already exists
                if (File.Exists(prefabPath))
                    continue;

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);

                if (prefab != null)
                    created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Complete", $"Created {created} new prefabs!", "OK");
            Debug.Log($"[QuickPrefabGenerator] Created {created} prefabs");
        }

        private static void EnsureTargetFoldersExist(string targetPath)
        {
            if (!AssetDatabase.IsValidFolder(targetPath))
            {
                AssetDatabase.CreateFolder("Assets/BugWars/Prefabs/Forest", "Generated");
            }

            string[] subfolders = { "Trees", "Bushes", "Rocks", "Grass" };
            foreach (var sub in subfolders)
            {
                string path = Path.Combine(targetPath, sub);
                if (!AssetDatabase.IsValidFolder(path))
                {
                    AssetDatabase.CreateFolder(targetPath, sub);
                }
            }
        }

        private static BugWars.Terrain.EnvironmentObjectType DetermineType(string name)
        {
            string lower = name.ToLower();
            if (lower.Contains("tree")) return BugWars.Terrain.EnvironmentObjectType.Tree;
            if (lower.Contains("bush")) return BugWars.Terrain.EnvironmentObjectType.Bush;
            if (lower.Contains("rock")) return BugWars.Terrain.EnvironmentObjectType.Rock;
            if (lower.Contains("grass")) return BugWars.Terrain.EnvironmentObjectType.Grass;
            return BugWars.Terrain.EnvironmentObjectType.Rock;
        }

        private static string GetSubfolder(BugWars.Terrain.EnvironmentObjectType type)
        {
            return type switch
            {
                BugWars.Terrain.EnvironmentObjectType.Tree => "Trees",
                BugWars.Terrain.EnvironmentObjectType.Bush => "Bushes",
                BugWars.Terrain.EnvironmentObjectType.Rock => "Rocks",
                BugWars.Terrain.EnvironmentObjectType.Grass => "Grass",
                _ => "Other"
            };
        }
    }
}
