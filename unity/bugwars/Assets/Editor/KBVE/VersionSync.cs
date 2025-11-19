#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

namespace KBVE.Editor
{
    /// <summary>
    /// Synchronizes Unity version with Cargo.toml version (source of truth)
    /// Automatically runs before builds and can be manually triggered via KBVE menu
    /// </summary>
    public class VersionSync : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const string CARGO_TOML_PATH = "website/axum/Cargo.toml";

        /// <summary>
        /// Automatically sync version before build
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[VersionSync] Running automatic version sync before build...");
            SyncVersionFromCargo();
        }

        /// <summary>
        /// Manual version sync from KBVE menu
        /// </summary>
        [MenuItem("KBVE/Version/Sync Version from Cargo.toml", false, 300)]
        public static void SyncVersionManually()
        {
            if (SyncVersionFromCargo())
            {
                EditorUtility.DisplayDialog(
                    "Version Sync Complete",
                    $"Unity version successfully synced to: {PlayerSettings.bundleVersion}",
                    "OK");
            }
        }

        /// <summary>
        /// Show current version info
        /// </summary>
        [MenuItem("KBVE/Version/Show Current Version", false, 301)]
        public static void ShowCurrentVersion()
        {
            string cargoVersion = GetCargoVersion();
            string unityVersion = PlayerSettings.bundleVersion;

            bool inSync = cargoVersion == unityVersion;
            string syncStatus = inSync ? "✓ IN SYNC" : "✗ OUT OF SYNC";

            EditorUtility.DisplayDialog(
                "Version Information",
                $"Cargo.toml Version: {cargoVersion}\n" +
                $"Unity Version: {unityVersion}\n\n" +
                $"Status: {syncStatus}",
                "OK");
        }

        /// <summary>
        /// Core version sync logic
        /// </summary>
        public static bool SyncVersionFromCargo()
        {
            string version = GetCargoVersion();

            if (string.IsNullOrEmpty(version))
            {
                Debug.LogError("[VersionSync] Failed to extract version from Cargo.toml");
                return false;
            }

            // Update Unity's bundle version
            PlayerSettings.bundleVersion = version;

            // Parse version components for bundleVersionCode (Android) / buildNumber (iOS)
            // Format: major.minor.patch -> major * 10000 + minor * 100 + patch
            if (TryParseVersionCode(version, out int versionCode))
            {
                PlayerSettings.Android.bundleVersionCode = versionCode;
                PlayerSettings.iOS.buildNumber = version;
            }

            // Update WebGL product info to fix the warnings
            PlayerSettings.companyName = "KBVE";
            PlayerSettings.productName = "BugWars";

            Debug.Log($"[VersionSync] Version synced successfully: {version}");
            Debug.Log($"[VersionSync] Bundle version code: {versionCode}");

            // Save the changes
            AssetDatabase.SaveAssets();

            return true;
        }

        /// <summary>
        /// Extract version from Cargo.toml
        /// </summary>
        private static string GetCargoVersion()
        {
            // Build path to Cargo.toml (Unity project is in unity/bugwars/)
            // Application.dataPath = .../bugwars/unity/bugwars/Assets
            // Need to go up 3 levels: Assets -> bugwars -> unity -> bugwars (root)
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string cargoTomlPath = Path.Combine(projectRoot, CARGO_TOML_PATH);

            if (!File.Exists(cargoTomlPath))
            {
                Debug.LogError($"[VersionSync] Cargo.toml not found at: {cargoTomlPath}");
                return null;
            }

            try
            {
                string[] lines = File.ReadAllLines(cargoTomlPath);

                // Look for version = "x.y.z" pattern
                Regex versionRegex = new Regex(@"^version\s*=\s*""([^""]+)""");

                foreach (string line in lines)
                {
                    Match match = versionRegex.Match(line.Trim());
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                Debug.LogError("[VersionSync] Version field not found in Cargo.toml");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VersionSync] Error reading Cargo.toml: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse semantic version into integer code
        /// </summary>
        private static bool TryParseVersionCode(string version, out int versionCode)
        {
            versionCode = 0;

            Regex versionRegex = new Regex(@"^(\d+)\.(\d+)\.(\d+)");
            Match match = versionRegex.Match(version);

            if (!match.Success)
            {
                Debug.LogWarning($"[VersionSync] Could not parse version components from: {version}");
                return false;
            }

            int major = int.Parse(match.Groups[1].Value);
            int minor = int.Parse(match.Groups[2].Value);
            int patch = int.Parse(match.Groups[3].Value);

            // Convert to single integer: 1.2.3 -> 10203
            versionCode = major * 10000 + minor * 100 + patch;

            return true;
        }
    }
}
#endif
