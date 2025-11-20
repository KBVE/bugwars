using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using System.IO;

namespace BugWars.Editor
{
    /// <summary>
    /// Editor utility to automatically configure environment prefabs as Addressables
    /// One-click solution that:
    /// - Scans and marks all tree/bush/rock prefabs as Addressables
    /// - Assigns labels: "Trees", "Bushes", "Rocks" for runtime loading
    /// - Builds Addressables bundles for WebGL
    /// - Ensures EnvironmentManager.cs loads prefabs correctly
    /// Run via: KBVE > Tools > Sync Environment Addressables
    /// </summary>
    public class EnvironmentAddressableSetup : EditorWindow
    {
        [MenuItem("KBVE/Tools/Sync Environment Addressables")]
        public static void SyncEnvironmentAddressables()
        {
            Debug.Log("[EnvironmentAddressableSetup] Starting setup...");

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[EnvironmentAddressableSetup] Addressable Asset Settings not found! Please create Addressables settings first.");
                EditorUtility.DisplayDialog("Error", "Addressable Asset Settings not found!\n\nPlease go to:\nWindow > Asset Management > Addressables > Groups\n\nAnd create the settings.", "OK");
                return;
            }

            // CRITICAL FOR WEBGL: Enable auto-building Addressables with Player build
            // This ensures Addressables bundles are included in the WebGL build
            if (settings.BuildAddressablesWithPlayerBuild != AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer)
            {
                settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log("[EnvironmentAddressableSetup] ✓ Enabled 'Build Addressables with Player Build' for WebGL compatibility");
            }

            // Find or create the Environment group
            var environmentGroup = FindOrCreateGroup(settings, "Environment");

            int treesAdded = 0;
            int bushesAdded = 0;
            int rocksAdded = 0;

            // Setup Trees
            string treesPath = "Assets/Resources/Prefabs/Forest/Trees";
            if (Directory.Exists(treesPath))
            {
                treesAdded = SetupPrefabsInFolder(settings, environmentGroup, treesPath, "Trees");
                Debug.Log($"[EnvironmentAddressableSetup] Added {treesAdded} tree prefabs to Addressables");
            }
            else
            {
                Debug.LogWarning($"[EnvironmentAddressableSetup] Trees folder not found: {treesPath}");
            }

            // Setup Bushes
            string bushesPath = "Assets/Resources/Prefabs/Forest/Bushes";
            if (Directory.Exists(bushesPath))
            {
                bushesAdded = SetupPrefabsInFolder(settings, environmentGroup, bushesPath, "Bushes");
                Debug.Log($"[EnvironmentAddressableSetup] Added {bushesAdded} bush prefabs to Addressables");
            }
            else
            {
                Debug.LogWarning($"[EnvironmentAddressableSetup] Bushes folder not found: {bushesPath}");
            }

            // Setup Rocks
            string rocksPath = "Assets/Resources/Prefabs/Forest/Rocks";
            if (Directory.Exists(rocksPath))
            {
                rocksAdded = SetupPrefabsInFolder(settings, environmentGroup, rocksPath, "Rocks");
                Debug.Log($"[EnvironmentAddressableSetup] Added {rocksAdded} rock prefabs to Addressables");
            }
            else
            {
                Debug.LogWarning($"[EnvironmentAddressableSetup] Rocks folder not found: {rocksPath}");
            }

            // Save changes
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryAdded, null, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int totalAdded = treesAdded + bushesAdded + rocksAdded;

            Debug.Log($"[EnvironmentAddressableSetup] Added {totalAdded} prefabs. Starting Addressables build...");

            // Automatically build Addressables
            try
            {
                AddressableAssetSettings.BuildPlayerContent();
                Debug.Log("[EnvironmentAddressableSetup] Addressables build completed successfully!");

                // Also refresh shaders to ensure WebGL rendering works
                Debug.Log("[EnvironmentAddressableSetup] Refreshing shaders & materials for WebGL...");
                EnsureShadersAndMaterials.RefreshAllShaders();

                string message = $"Environment Addressables Synced Successfully!\n\n" +
                               $"Trees: {treesAdded}\n" +
                               $"Bushes: {bushesAdded}\n" +
                               $"Rocks: {rocksAdded}\n\n" +
                               $"Total: {totalAdded} prefabs added to Addressables group 'Environment'\n\n" +
                               $"✓ Addressables built\n" +
                               $"✓ Shaders & materials refreshed for WebGL\n" +
                               $"✓ Auto-build with Player enabled\n\n" +
                               $"Ready for WebGL build!";

                Debug.Log($"[EnvironmentAddressableSetup] {message}");
                EditorUtility.DisplayDialog("Success", message, "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EnvironmentAddressableSetup] Failed to build Addressables: {ex.Message}");
                EditorUtility.DisplayDialog("Warning",
                    $"Prefabs added successfully ({totalAdded} total), but Addressables build failed.\n\n" +
                    $"Please build manually via:\nWindow > Asset Management > Addressables > Groups\n" +
                    $"Then: Build > New Build > Default Build Script\n\n" +
                    $"Error: {ex.Message}", "OK");
            }
        }

        private static AddressableAssetGroup FindOrCreateGroup(AddressableAssetSettings settings, string groupName)
        {
            // Try to find existing group
            var group = settings.FindGroup(groupName);
            if (group != null)
            {
                Debug.Log($"[EnvironmentAddressableSetup] Using existing group: {groupName}");
                return group;
            }

            // Create new group
            Debug.Log($"[EnvironmentAddressableSetup] Creating new group: {groupName}");
            group = settings.CreateGroup(groupName, false, false, true, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema), typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));

            // Configure the group for remote loading (optional - you can change this to local if needed)
            var schema = group.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>();
            if (schema != null)
            {
                schema.BuildPath.SetVariableByName(settings, "LocalBuildPath");
                schema.LoadPath.SetVariableByName(settings, "LocalLoadPath");
                schema.BundleMode = UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            }

            return group;
        }

        private static int SetupPrefabsInFolder(AddressableAssetSettings settings, AddressableAssetGroup group, string folderPath, string label)
        {
            int count = 0;

            // Ensure label exists
            if (!settings.GetLabels().Contains(label))
            {
                settings.AddLabel(label);
                Debug.Log($"[EnvironmentAddressableSetup] Created new label: {label}");
            }

            // Find all prefabs in folder
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // Create or get addressable entry
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                if (entry != null)
                {
                    // Set the address to just the prefab name (without path)
                    string prefabName = Path.GetFileNameWithoutExtension(assetPath);
                    entry.address = prefabName;

                    // Add label
                    entry.SetLabel(label, true);

                    count++;
                    Debug.Log($"[EnvironmentAddressableSetup] Added: {prefabName} with label '{label}'");

                    // CRITICAL: Find and mark all dependencies (FBX models, materials, textures) as Addressable too
                    // This ensures materials embedded in FBX files are included in the bundle
                    string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);
                    foreach (string depPath in dependencies)
                    {
                        // Skip the prefab itself and script files
                        if (depPath == assetPath || depPath.EndsWith(".cs")) continue;

                        // Mark dependency as addressable in the same group (but don't give it a label)
                        string depGuid = AssetDatabase.AssetPathToGUID(depPath);
                        if (!string.IsNullOrEmpty(depGuid))
                        {
                            var depEntry = settings.CreateOrMoveEntry(depGuid, group, false, false);
                            if (depEntry != null)
                            {
                                Debug.Log($"[EnvironmentAddressableSetup]   → Included dependency: {depPath}");
                            }
                        }
                    }
                }
            }

            return count;
        }

    }
}
