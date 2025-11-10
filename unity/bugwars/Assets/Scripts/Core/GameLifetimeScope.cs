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
            builder.Register<MainMenuManager>(Lifetime.Singleton)
                .FromComponentOnNewGameObject()
                .DontDestroyOnLoad()
                .OnBuild((container, mainMenuManager) =>
                {
                    // Set up UIDocument component
                    var uiDocument = mainMenuManager.gameObject.AddComponent<UIDocument>();

                    if (mainMenuVisualTree != null && panelSettings != null)
                    {
                        uiDocument.visualTreeAsset = mainMenuVisualTree;
                        uiDocument.panelSettings = panelSettings;
                        uiDocument.sortingOrder = 100; // Ensure menu renders on top
                    }
                    else
                    {
                        Debug.LogError("[GameLifetimeScope] MainMenu VisualTreeAsset or PanelSettings not assigned!");
                    }

                    mainMenuManager.gameObject.name = "MainMenuManager";
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
                builder.Register<T>(Lifetime.Singleton)
                    .FromComponentOnNewGameObject()
                    .DontDestroyOnLoad()
                    .OnBuild((container, component) =>
                    {
                        component.gameObject.name = managerName;
                    });
                Debug.Log($"[GameLifetimeScope] Created new {managerName}");
            }
        }

        protected override void Awake()
        {
            base.Awake();

            // Ensure this is the root lifetime scope
            IsRoot = true;
            autoRun = true;

            Debug.Log("[GameLifetimeScope] Initializing DI Container");
        }
    }
}
