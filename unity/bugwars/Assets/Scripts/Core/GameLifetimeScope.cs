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

            // Register MainMenuManager with special handling for UIDocument
            builder.RegisterComponentOnNewGameObject<MainMenuManager>(Lifetime.Singleton)
                .DontDestroyOnLoad()
                .UnderTransform(transform);

            // Register callback to configure UIDocument after MainMenuManager is built
            builder.RegisterBuildCallback(container =>
            {
                var mainMenuManager = container.Resolve<MainMenuManager>();
                if (mainMenuManager != null)
                {
                    var uiDocument = mainMenuManager.GetComponent<UIDocument>();
                    if (uiDocument != null && mainMenuVisualTree != null && panelSettings != null)
                    {
                        uiDocument.visualTreeAsset = mainMenuVisualTree;
                        uiDocument.panelSettings = panelSettings;
                        uiDocument.sortingOrder = 100;
                        Debug.Log("[GameLifetimeScope] MainMenuManager UIDocument configured");
                    }
                    else if (mainMenuVisualTree == null || panelSettings == null)
                    {
                        Debug.LogError("[GameLifetimeScope] MainMenu VisualTreeAsset or PanelSettings not assigned!");
                    }
                }
            });

            Debug.Log("[GameLifetimeScope] DI Container configured successfully");
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
