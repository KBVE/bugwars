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
        [SerializeField] private PanelSettings panelSettings;

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

            // Validate required assets
            if (mainMenuVisualTree == null || panelSettings == null)
            {
                Debug.LogError("[GameLifetimeScope] MainMenu VisualTreeAsset or PanelSettings not assigned!");
                return null;
            }

            // Create new GameObject under this transform
            var menuObject = new GameObject("MainMenuManager");
            menuObject.transform.SetParent(transform);
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
