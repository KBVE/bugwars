using UnityEngine;
using BugWars.UI;

namespace BugWars.Core
{
    /// <summary>
    /// Diagnostic tool to check if UI managers are properly set up
    /// Attach this to any GameObject to run diagnostics in Play mode
    /// </summary>
    public class DiagnosticChecker : MonoBehaviour
    {
        [Header("Run Diagnostics")]
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
            {
                RunDiagnostics();
            }
        }

        [ContextMenu("Run UI Diagnostics")]
        public void RunDiagnostics()
        {
            Debug.Log("========== UI DIAGNOSTICS START ==========");

            // Check for GameLifetimeScope
            var lifetimeScope = FindFirstObjectByType<GameLifetimeScope>();
            if (lifetimeScope == null)
            {
                Debug.LogError("[Diagnostics] GameLifetimeScope NOT FOUND in scene!");
            }
            else
            {
                Debug.Log("[Diagnostics] ✓ GameLifetimeScope found");

                // Use reflection to check if UXML assets are assigned
                var mainMenuField = typeof(GameLifetimeScope).GetField("mainMenuVisualTree",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var settingsField = typeof(GameLifetimeScope).GetField("settingsPanelVisualTree",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var mainMenuAsset = mainMenuField?.GetValue(lifetimeScope);
                var settingsAsset = settingsField?.GetValue(lifetimeScope);

                if (mainMenuAsset == null)
                {
                    Debug.LogError("[Diagnostics] ✗ mainMenuVisualTree is NOT assigned in GameLifetimeScope Inspector!");
                }
                else
                {
                    Debug.Log($"[Diagnostics] ✓ mainMenuVisualTree assigned: {mainMenuAsset}");
                }

                if (settingsAsset == null)
                {
                    Debug.LogError("[Diagnostics] ✗ settingsPanelVisualTree is NOT assigned in GameLifetimeScope Inspector!");
                    Debug.LogError("[Diagnostics] → This is why Settings button doesn't work!");
                }
                else
                {
                    Debug.Log($"[Diagnostics] ✓ settingsPanelVisualTree assigned: {settingsAsset}");
                }
            }

            // Check for MainMenuManager
            var mainMenuManager = FindFirstObjectByType<MainMenuManager>();
            if (mainMenuManager == null)
            {
                Debug.LogError("[Diagnostics] MainMenuManager NOT FOUND in scene!");
            }
            else
            {
                Debug.Log("[Diagnostics] ✓ MainMenuManager found");

                // Check if SettingsPanelManager reference is set
                var settingsPanelField = typeof(MainMenuManager).GetField("_settingsPanelManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var settingsPanelRef = settingsPanelField?.GetValue(mainMenuManager);

                if (settingsPanelRef == null)
                {
                    Debug.LogError("[Diagnostics] ✗ _settingsPanelManager reference is NULL in MainMenuManager!");
                    Debug.LogError("[Diagnostics] → This is why you see 'SettingsPanelManager reference not set!' warning");
                }
                else
                {
                    Debug.Log($"[Diagnostics] ✓ _settingsPanelManager reference is set: {settingsPanelRef}");
                }
            }

            // Check for SettingsPanelManager
            var settingsPanelManager = FindFirstObjectByType<SettingsPanelManager>();
            if (settingsPanelManager == null)
            {
                Debug.LogError("[Diagnostics] SettingsPanelManager NOT FOUND in scene!");
                Debug.LogError("[Diagnostics] → It should be created by GameLifetimeScope.CreateSettingsPanelManager()");
            }
            else
            {
                Debug.Log("[Diagnostics] ✓ SettingsPanelManager found");
            }

            Debug.Log("========== UI DIAGNOSTICS END ==========");
            Debug.Log("");
            Debug.Log("SOLUTION:");
            Debug.Log("1. Select GameLifetimeScope GameObject in Hierarchy");
            Debug.Log("2. In Inspector, find 'UI Configuration' section");
            Debug.Log("3. Assign: settingsPanelVisualTree → Assets/BugWars/UI/Settings/settings_panel.uxml");
            Debug.Log("4. Assign: mainMenuVisualTree → Assets/BugWars/UI/MainMenu/main_menu.uxml");
        }
    }
}
