using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;
using BugWars.UI;

namespace BugWars.Core
{
    /// <summary>
    /// Main DI Container Orchestrator using VContainer
    /// Manages dependency injection and initialization of all core managers
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [Header("UI Configuration")]
        [SerializeField] private VisualTreeAsset mainMenuVisualTree;
        [SerializeField] [Tooltip("Optional - Will create default runtime PanelSettings if not assigned")]
        private PanelSettings panelSettings;

        protected override void Configure(IContainerBuilder builder)
        {
            // Register or find EventManager
            RegisterManager<EventManager>(builder, "EventManager");

            // Register or find InputManager
            RegisterManager<InputManager>(builder, "InputManager");

            // Register or find GameManager
            RegisterManager<GameManager>(builder, "GameManager");

            // Create and configure MainMenuManager with UIDocument properly set up
            var mainMenuManagerInstance = CreateMainMenuManager();
            if (mainMenuManagerInstance != null)
            {
                builder.RegisterComponent(mainMenuManagerInstance);
                Debug.Log("[GameLifetimeScope] MainMenuManager created and registered");
            }

            Debug.Log("[GameLifetimeScope] DI Container configured successfully");
        }

        /// <summary>
        /// Creates and configures the MainMenuManager with UIDocument
        /// This ensures UIDocument is properly configured before Unity Inspector tries to validate it
        /// </summary>
        private MainMenuManager CreateMainMenuManager()
        {
            // Check if MainMenuManager already exists in scene
            var existingManager = FindObjectOfType<MainMenuManager>();
            if (existingManager != null)
            {
                Debug.Log("[GameLifetimeScope] Found existing MainMenuManager in scene");
                return existingManager;
            }

            // Validate required UXML asset
            if (mainMenuVisualTree == null)
            {
                Debug.LogError("[GameLifetimeScope] MainMenu VisualTreeAsset not assigned!");
                return null;
            }

            // If PanelSettings not assigned, try to find or create a default one
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

            Debug.Log("[GameLifetimeScope] Created new MainMenuManager with configured UIDocument");
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

            Debug.Log("[GameLifetimeScope] Created default runtime PanelSettings");
            return settings;
        }

        /// <summary>
        /// Helper method to register or find existing managers in the scene
        /// </summary>
        private void RegisterManager<T>(IContainerBuilder builder, string managerName) where T : Component
        {
            var existingManager = FindObjectOfType<T>();

            if (existingManager != null)
            {
                // Manager already exists in scene, register it
                builder.RegisterComponent(existingManager).AsSelf();
                Debug.Log($"[GameLifetimeScope] Found existing {managerName} in scene");
            }
            else
            {
                // Create new manager
                builder.RegisterComponentOnNewGameObject<T>(Lifetime.Singleton)
                    .DontDestroyOnLoad()
                    .UnderTransform(transform);
                Debug.Log($"[GameLifetimeScope] Created new {managerName}");
            }
        }

        protected override void Awake()
        {
            base.Awake();

            // Note: IsRoot is automatically determined by parent relationship
            // This LifetimeScope will be root if it has no parent LifetimeScope
            autoRun = true;

            Debug.Log("[GameLifetimeScope] Initializing DI Container");
        }
    }
}
