using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using System;
using System.IO;

namespace BugWars.Core
{
    /// <summary>
    /// Version Manager - Reads and displays version from version.json
    /// Source of truth: Cargo.toml (website/axum/Cargo.toml)
    /// </summary>
    public class VersionManager : MonoBehaviour
    {
        #region Serialized Fields
        [SerializeField]
        [Tooltip("Display version in console on startup")]
        private bool _logVersionOnStart = true;

        [SerializeField]
        [Tooltip("Verify version matches expected pattern")]
        private bool _validateVersion = true;
        #endregion

        #region Version Data
        /// <summary>
        /// Version data structure matching version.json schema
        /// </summary>
        [Serializable]
        public class VersionData
        {
            public string version;
            public string package;
            public string source;
            public string timestamp;
            public BuildInfo build;

            [Serializable]
            public class BuildInfo
            {
                public string unity;
                public string node;
                public string rust;
            }
        }

        private VersionData _versionData;
        private bool _isVersionLoaded = false;
        #endregion

        #region Properties
        /// <summary>
        /// Current game version (e.g., "1.0.10")
        /// </summary>
        public string Version => _versionData?.version ?? "Unknown";

        /// <summary>
        /// Package name
        /// </summary>
        public string PackageName => _versionData?.package ?? "kbve-bugwars";

        /// <summary>
        /// Build timestamp (ISO 8601 format)
        /// </summary>
        public string BuildTimestamp => _versionData?.timestamp ?? "Unknown";

        /// <summary>
        /// Full version data
        /// </summary>
        public VersionData Data => _versionData;

        /// <summary>
        /// Whether version has been successfully loaded
        /// </summary>
        public bool IsVersionLoaded => _isVersionLoaded;
        #endregion

        #region Unity Lifecycle
        private async void Start()
        {
            await LoadVersionAsync();

            if (_logVersionOnStart && _isVersionLoaded)
            {
                LogVersionInfo();
            }
        }
        #endregion

        #region Version Loading
        /// <summary>
        /// Load version.json from StreamingAssets
        /// </summary>
        public async UniTask<bool> LoadVersionAsync()
        {
            try
            {
                string versionPath = Path.Combine(Application.streamingAssetsPath, "version.json");

                // Use UnityWebRequest for WebGL compatibility
                using (UnityWebRequest request = UnityWebRequest.Get(versionPath))
                {
                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string jsonContent = request.downloadHandler.text;
                        _versionData = JsonUtility.FromJson<VersionData>(jsonContent);

                        if (_validateVersion)
                        {
                            ValidateVersionData();
                        }

                        _isVersionLoaded = true;
                        Debug.Log($"[VersionManager] Loaded version: {Version}");
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"[VersionManager] Failed to load version.json: {request.error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VersionManager] Exception loading version: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validate version data format
        /// </summary>
        private void ValidateVersionData()
        {
            if (_versionData == null)
            {
                Debug.LogWarning("[VersionManager] Version data is null");
                return;
            }

            // Validate version format (e.g., "1.0.10")
            if (string.IsNullOrEmpty(_versionData.version))
            {
                Debug.LogWarning("[VersionManager] Version string is empty");
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(_versionData.version, @"^\d+\.\d+\.\d+"))
            {
                Debug.LogWarning($"[VersionManager] Version format unexpected: {_versionData.version}");
            }

            // Validate source
            if (_versionData.source != "cargo.toml")
            {
                Debug.LogWarning($"[VersionManager] Unexpected version source: {_versionData.source}");
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Get version string with package name (e.g., "kbve-bugwars v1.0.10")
        /// </summary>
        public string GetFullVersionString()
        {
            return $"{PackageName} v{Version}";
        }

        /// <summary>
        /// Get build info string
        /// </summary>
        public string GetBuildInfoString()
        {
            if (_versionData?.build == null)
            {
                return "Build info unavailable";
            }

            return $"Unity: {_versionData.build.unity} | Node: {_versionData.build.node} | Rust: {_versionData.build.rust}";
        }

        /// <summary>
        /// Log detailed version information to console
        /// </summary>
        public void LogVersionInfo()
        {
            if (!_isVersionLoaded)
            {
                Debug.LogWarning("[VersionManager] Version not loaded yet");
                return;
            }

            Debug.Log($"╔══════════════════════════════════════╗");
            Debug.Log($"║  {PackageName,-34}  ║");
            Debug.Log($"║  Version: {Version,-26}  ║");
            Debug.Log($"║  Build: {BuildTimestamp,-28}  ║");
            Debug.Log($"║  Source: {_versionData.source,-27}  ║");
            Debug.Log($"╚══════════════════════════════════════╝");

            if (_versionData.build != null)
            {
                Debug.Log($"Build Tools: {GetBuildInfoString()}");
            }
        }

        /// <summary>
        /// Check if version matches expected version
        /// </summary>
        public bool IsVersionMatch(string expectedVersion)
        {
            return Version == expectedVersion;
        }
        #endregion
    }
}
