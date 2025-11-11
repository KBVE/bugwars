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
                builder.RegisterComponent(webGLBridge);
            }
            else
            {
                RegisterOrCreateManager<WebGLBridge>(builder, "WebGLBridge");
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

            // Create and configure MainMenuManager with UIDocument properly set up
            var mainMenuManagerInstance = CreateMainMenuManager();
            if (mainMenuManagerInstance != null)
            {
                builder.RegisterComponent(mainMenuManagerInstance);
            }
            else
            {
                Debug.LogError("[GameLifetimeScope] Failed to create MainMenuManager - UI will not function!");
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
                builder.RegisterComponent(terrainManager).AsImplementedInterfaces();
            }
            else
            {
                var registration = builder.RegisterComponentOnNewGameObject<TerrainManager>(Lifetime.Singleton, "TerrainManager");
                registration.DontDestroyOnLoad().AsImplementedInterfaces();
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
        /// Creates and configures the MainMenuManager with UIDocument
        /// This ensures UIDocument is properly configured before Unity Inspector tries to validate it
        /// </summary>
        private MainMenuManager CreateMainMenuManager()
        {
            // Check if MainMenuManager already exists in scene
            var existingManager = FindFirstObjectByType<MainMenuManager>();
            if (existingManager != null)
            {
                return existingManager;
            }

            // Validate required UXML asset
            if (mainMenuVisualTree == null)
            {
                Debug.LogError("[GameLifetimeScope] MainMenu VisualTreeAsset not assigned!");
                return null;
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
            uiDocument.sortingOrder = 100;

            // Now add MainMenuManager component (UIDocument is already configured)
            var mainMenuManager = menuObject.AddComponent<MainMenuManager>();

            return mainMenuManager;
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
