using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;
using BugWars.UI;
using BugWars.Terrain;
using BugWars.Entity;
using BugWars.JavaScriptBridge;

namespace BugWars.Core
{
    /// <summary>
    /// Main DI Container Orchestrator using VContainer
    /// Manages dependency injection and initialization of all core managers
    /// Singleton pattern ensures only one instance exists across scene transitions
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        private static GameLifetimeScope _instance;

        [Header("UI Configuration")]
        [SerializeField] private VisualTreeAsset mainMenuVisualTree;
        [SerializeField] private VisualTreeAsset hudVisualTree;
        [SerializeField] private VisualTreeAsset settingsPanelVisualTree;
        [SerializeField] [Tooltip("Optional - Will create default runtime PanelSettings if not assigned")]
        private PanelSettings panelSettings;

        [Header("Manager References")]
        [SerializeField] [Tooltip("Optional - EventManager component to register. Will create if not assigned.")]
        private EventManager eventManager;
        [SerializeField] [Tooltip("Optional - WebGLBridge component to register. Will create if not assigned.")]
        private WebGLBridge webGLBridge;
        [SerializeField] [Tooltip("Optional - InputManager component to register. Will create if not assigned.")]
        private InputManager inputManager;
        [SerializeField] [Tooltip("Optional - GameManager component to register. Will create if not assigned.")]
        private GameManager gameManager;
        [SerializeField] [Tooltip("Optional - TerrainManager component to register. Will create if not assigned.")]
        private TerrainManager terrainManager;
        [SerializeField] [Tooltip("Optional - CameraManager component to register. Will create if not assigned.")]
        private CameraManager cameraManager;
        [SerializeField] [Tooltip("Optional - EntityManager component to register. Will create if not assigned.")]
        private EntityManager entityManager;

        protected override void Configure(IContainerBuilder builder)
        {
            // Register core managers in dependency order
            // EventManager has no dependencies - register first
            if (eventManager != null)
            {
                builder.RegisterComponent(eventManager);
            }
            else
            {
                RegisterOrCreateManager<EventManager>(builder, "EventManager");
            }

            // WebGLBridge depends on EventManager - register after EventManager
            if (webGLBridge != null)
            {
                // Validate the GameObject name matches what React expects
                if (webGLBridge.gameObject.name != "WebGLBridge")
                {
                    Debug.LogWarning($"[GameLifetimeScope] WebGLBridge GameObject has incorrect name '{webGLBridge.gameObject.name}'. " +
                                    "Renaming to 'WebGLBridge' to ensure React Unity sendMessage works correctly.");
                    webGLBridge.gameObject.name = "WebGLBridge";
                }
                builder.RegisterComponent(webGLBridge);
                // Force resolution to ensure GameObject exists before React tries to send messages
                builder.RegisterBuildCallback(container => container.Resolve<WebGLBridge>());
            }
            else
            {
                // Create new WebGLBridge GameObject with correct name
                builder.RegisterComponentOnNewGameObject<WebGLBridge>(Lifetime.Singleton, "WebGLBridge")
                    .DontDestroyOnLoad();

                // CRITICAL: Force eager instantiation so GameObject exists before React sends messages
                // Without this, VContainer uses lazy instantiation and the GameObject won't exist
                // when React calls sendMessage('WebGLBridge', 'OnSessionUpdate', ...)
                builder.RegisterBuildCallback(container => container.Resolve<WebGLBridge>());
            }

            // InputManager depends on EventManager
            if (inputManager != null)
            {
                builder.RegisterComponent(inputManager);
            }
            else
            {
                RegisterOrCreateManager<InputManager>(builder, "InputManager");
            }

            // Create and configure SettingsPanelManager FIRST (it needs to exist before MainMenuManager references it)
            var settingsPanelManagerInstance = CreateSettingsPanelManager();
            if (settingsPanelManagerInstance != null)
            {
                builder.RegisterComponent(settingsPanelManagerInstance);
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] Failed to create SettingsPanelManager - Settings UI will not function!");
            }

            // Create and configure MainMenuManager with UIDocument properly set up
            var mainMenuManagerInstance = CreateMainMenuManager(settingsPanelManagerInstance);
            if (mainMenuManagerInstance != null)
            {
                builder.RegisterComponent(mainMenuManagerInstance);
            }
            else
            {
                Debug.LogError("[GameLifetimeScope] Failed to create MainMenuManager - UI will not function!");
            }

            // Create and configure HUDManager with UIDocument properly set up
            var hudManagerInstance = CreateHUDManager();
            if (hudManagerInstance != null)
            {
                builder.RegisterComponent(hudManagerInstance);
            }
            else
            {
                Debug.LogError("[GameLifetimeScope] Failed to create HUDManager - HUD will not function!");
            }

            // GameManager depends on EventManager and MainMenuManager - register last
            if (gameManager != null)
            {
                builder.RegisterComponent(gameManager);
            }
            else
            {
                RegisterOrCreateManager<GameManager>(builder, "GameManager");
            }

            // TerrainManager for procedural terrain generation
            if (terrainManager != null)
            {
                builder.RegisterComponent(terrainManager).AsImplementedInterfaces().AsSelf();
                builder.RegisterBuildCallback(container => container.Resolve<TerrainManager>());
            }
            else
            {
                var registration = builder.RegisterComponentOnNewGameObject<TerrainManager>(Lifetime.Singleton, "TerrainManager");
                registration.DontDestroyOnLoad().AsImplementedInterfaces().AsSelf();
                builder.RegisterBuildCallback(container => container.Resolve<TerrainManager>());
            }

            // CameraManager for camera control using Cinemachine
            if (cameraManager != null)
            {
                builder.RegisterComponent(cameraManager);
            }
            else
            {
                RegisterOrCreateManager<CameraManager>(builder, "CameraManager");
            }

            // EntityManager for entity tracking and management
            if (entityManager != null)
            {
                builder.RegisterComponent(entityManager);
            }
            else
            {
                RegisterOrCreateManager<EntityManager>(builder, "EntityManager");
            }
        }

        /// <summary>
        /// Creates and configures the SettingsPanelManager with UIDocument
        /// This should be called BEFORE CreateMainMenuManager so MainMenu can reference it
        /// </summary>
        private SettingsPanelManager CreateSettingsPanelManager()
        {
            // Check if SettingsPanelManager already exists in scene
            var existingManager = FindFirstObjectByType<SettingsPanelManager>();
            if (existingManager != null)
            {
                return existingManager;
            }

            // Validate required UXML asset
            if (settingsPanelVisualTree == null)
            {
                Debug.LogWarning("[GameLifetimeScope] SettingsPanel VisualTreeAsset not assigned in Inspector, attempting to load from Resources...");

                // Try to load from Resources as a fallback
                settingsPanelVisualTree = Resources.Load<VisualTreeAsset>("BugWars/UI/Settings/settings_panel");

                if (settingsPanelVisualTree == null)
                {
                    // Try loading directly from Assets using UnityEditor in editor mode
                    #if UNITY_EDITOR
                    settingsPanelVisualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Assets/BugWars/UI/Settings/settings_panel.uxml");

                    if (settingsPanelVisualTree != null)
                    {
                        Debug.Log("[GameLifetimeScope] Successfully loaded settings_panel.uxml from Assets folder");
                    }
                    #endif
                }

                if (settingsPanelVisualTree == null)
                {
                    Debug.LogError("[GameLifetimeScope] Failed to load SettingsPanel VisualTreeAsset! Settings UI will not function.");
                    Debug.LogError("[GameLifetimeScope] Please assign it in the GameLifetimeScope Inspector.");
                    return null;
                }
            }

            // If PanelSettings not assigned, create a default one
            if (panelSettings == null)
            {
                Debug.LogWarning("[GameLifetimeScope] PanelSettings not assigned, creating default runtime PanelSettings");
                panelSettings = CreateDefaultPanelSettings();
            }

            // Create new GameObject as root (required for DontDestroyOnLoad)
            var settingsObject = new GameObject("SettingsPanelManager");
            DontDestroyOnLoad(settingsObject);

            // Add and configure UIDocument FIRST to avoid null reference errors
            var uiDocument = settingsObject.AddComponent<UIDocument>();
            uiDocument.visualTreeAsset = settingsPanelVisualTree;
            uiDocument.panelSettings = panelSettings;
            uiDocument.sortingOrder = 200; // Higher than MainMenu (100) so it appears on top

            // Now add SettingsPanelManager component (UIDocument is already configured)
            var settingsPanelManager = settingsObject.AddComponent<SettingsPanelManager>();

            Debug.Log("[GameLifetimeScope] SettingsPanelManager created with sort order 200");

            return settingsPanelManager;
        }

        /// <summary>
        /// Creates and configures the MainMenuManager with UIDocument
        /// This ensures UIDocument is properly configured before Unity Inspector tries to validate it
        /// </summary>
        private MainMenuManager CreateMainMenuManager(SettingsPanelManager settingsPanelManager)
        {
            // Check if MainMenuManager already exists in scene
            var existingManager = FindFirstObjectByType<MainMenuManager>();
            if (existingManager != null)
            {
                // Wire up the settings panel reference for existing manager
                SetSettingsPanelReference(existingManager, settingsPanelManager);
                Debug.Log("[GameLifetimeScope] Using existing MainMenuManager from scene");
                return existingManager;
            }

            // Validate required UXML asset
            if (mainMenuVisualTree == null)
            {
                Debug.LogWarning("[GameLifetimeScope] MainMenu VisualTreeAsset not assigned in Inspector, attempting to load from Resources...");

                // Try to load from Resources as a fallback
                mainMenuVisualTree = Resources.Load<VisualTreeAsset>("BugWars/UI/MainMenu/main_menu");

                if (mainMenuVisualTree == null)
                {
                    // Try loading directly from Assets using UnityEditor in editor mode
                    #if UNITY_EDITOR
                    mainMenuVisualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Assets/BugWars/UI/MainMenu/main_menu.uxml");

                    if (mainMenuVisualTree != null)
                    {
                        Debug.Log("[GameLifetimeScope] Successfully loaded main_menu.uxml from Assets folder");
                    }
                    #endif
                }

                if (mainMenuVisualTree == null)
                {
                    Debug.LogError("[GameLifetimeScope] Failed to load MainMenu VisualTreeAsset! Main menu UI will not function.");
                    Debug.LogError("[GameLifetimeScope] Please assign it in the GameLifetimeScope Inspector.");
                    return null;
                }
            }

            // If PanelSettings not assigned, create a default one
            if (panelSettings == null)
            {
                Debug.LogWarning("[GameLifetimeScope] PanelSettings not assigned, creating default runtime PanelSettings");
                panelSettings = CreateDefaultPanelSettings();
            }

            // Create new GameObject as root (required for DontDestroyOnLoad)
            var menuObject = new GameObject("MainMenuManager");
            DontDestroyOnLoad(menuObject);

            // Add and configure UIDocument FIRST to avoid null reference errors
            var uiDocument = menuObject.AddComponent<UIDocument>();
            uiDocument.visualTreeAsset = mainMenuVisualTree;
            uiDocument.panelSettings = panelSettings;
            uiDocument.sortingOrder = 100; // Lower than SettingsPanel (200)

            // Now add MainMenuManager component (UIDocument is already configured)
            var mainMenuManager = menuObject.AddComponent<MainMenuManager>();

            // Wire up the SettingsPanelManager reference
            SetSettingsPanelReference(mainMenuManager, settingsPanelManager);

            return mainMenuManager;
        }

        /// <summary>
        /// Sets the SettingsPanelManager reference in MainMenuManager using reflection
        /// </summary>
        private void SetSettingsPanelReference(MainMenuManager mainMenuManager, SettingsPanelManager settingsPanelManager)
        {
            if (mainMenuManager == null || settingsPanelManager == null)
            {
                Debug.LogWarning("[GameLifetimeScope] Cannot set SettingsPanelManager reference - one of the managers is null");
                return;
            }

            var fieldInfo = typeof(MainMenuManager).GetField("_settingsPanelManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(mainMenuManager, settingsPanelManager);
                    Debug.Log("[GameLifetimeScope] SettingsPanelManager reference assigned to MainMenuManager");
                }
                else
                {
                    Debug.LogError("[GameLifetimeScope] Could not find _settingsPanelManager field in MainMenuManager!");
                }
        }

        /// <summary>
        /// Creates and configures the HUDManager with UIDocument
        /// This ensures UIDocument is properly configured before Unity Inspector tries to validate it
        /// </summary>
        private HUDManager CreateHUDManager()
        {
            // Check if HUDManager already exists in scene
            var existingManager = FindFirstObjectByType<HUDManager>();
            if (existingManager != null)
            {
                return existingManager;
            }

            // Validate required UXML asset
            if (hudVisualTree == null)
            {
                Debug.LogError("[GameLifetimeScope] HUD VisualTreeAsset not assigned!");
                return null;
            }

            // If PanelSettings not assigned, create a default one
            if (panelSettings == null)
            {
                Debug.LogWarning("[GameLifetimeScope] PanelSettings not assigned, creating default runtime PanelSettings");
                panelSettings = CreateDefaultPanelSettings();
            }

            // Create new GameObject as root (required for DontDestroyOnLoad)
            var hudObject = new GameObject("HUDManager");
            DontDestroyOnLoad(hudObject);

            // Add and configure UIDocument FIRST to avoid null reference errors
            var uiDocument = hudObject.AddComponent<UIDocument>();
            uiDocument.visualTreeAsset = hudVisualTree;
            uiDocument.panelSettings = panelSettings;
            uiDocument.sortingOrder = 0; // HUD renders below menus

            // Now add HUDManager component (UIDocument is already configured)
            var hudManager = hudObject.AddComponent<HUDManager>();

            return hudManager;
        }

        /// <summary>
        /// Creates a default PanelSettings at runtime
        /// </summary>
        private PanelSettings CreateDefaultPanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.scale = 1f;
            settings.fallbackDpi = 96f;
            settings.referenceDpi = 96f;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0f;
            settings.sortingOrder = 0;
            settings.clearDepthStencil = true;
            settings.clearColor = false;

            // Load default Unity runtime theme to avoid "No Theme Style Sheet" warning
            var defaultTheme = Resources.Load<ThemeStyleSheet>("Runtime Theme");
            if (defaultTheme != null)
            {
                settings.themeStyleSheet = defaultTheme;
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] Could not load default Runtime Theme from Resources - UI may not render correctly");
            }

            return settings;
        }

        /// <summary>
        /// Helper method to register or create managers using VContainer
        /// Checks for existing instance first, otherwise creates new one
        /// </summary>
        private void RegisterOrCreateManager<T>(IContainerBuilder builder, string managerName) where T : Component
        {
            var existingManager = FindFirstObjectByType<T>();

            if (existingManager != null)
            {
                // Manager already exists in scene, register it for injection
                builder.RegisterComponent(existingManager);
            }
            else
            {
                // Create new manager GameObject and register component as root object
                // VContainer will handle injection into this component
                builder.RegisterComponentOnNewGameObject<T>(Lifetime.Singleton, managerName)
                    .DontDestroyOnLoad();
            }
        }

        protected override void Awake()
        {
            // Singleton pattern with defensive guards
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[GameLifetimeScope] Duplicate instance detected on '{gameObject.name}'. " +
                    $"Only one GameLifetimeScope should exist. Destroying duplicate to prevent DI container conflicts.");
                Destroy(gameObject);
                return;
            }

            // Set this as the singleton instance
            _instance = this;

            // Persist across scene transitions
            DontDestroyOnLoad(gameObject);

            base.Awake();

            // Note: IsRoot is automatically determined by parent relationship
            // This LifetimeScope will be root if it has no parent LifetimeScope
            autoRun = true;
        }

        protected override void OnDestroy()
        {
            // Clean up singleton reference when this instance is destroyed
            if (_instance == this)
            {
                _instance = null;
            }

            base.OnDestroy();
        }
    }
}
