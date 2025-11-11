using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using BugWars.UI;
using VContainer;

namespace BugWars.Core
{
    /// <summary>
    /// Universal Game Manager - Managed by VContainer
    /// Handles core game functionality including scene management and main menu control
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Dependencies
        private EventManager _eventManager;
        private MainMenuManager _mainMenuManager;

        [Inject]
        public void Construct(EventManager eventManager, MainMenuManager mainMenuManager)
        {
            _eventManager = eventManager;
            _mainMenuManager = mainMenuManager;
        }
        #endregion

        #region Scene Management
        /// <summary>
        /// Reference to the currently active scene
        /// </summary>
        public Scene CurrentScene { get; private set; }

        /// <summary>
        /// Name of the currently active scene
        /// </summary>
        public string CurrentSceneName => CurrentScene.name;

        /// <summary>
        /// Build index of the currently active scene
        /// </summary>
        public int CurrentSceneBuildIndex => CurrentScene.buildIndex;
        #endregion

        #region Event Management
        /// <summary>
        /// Reference to the EventManager for event triggering
        /// </summary>
        public EventManager Events => _eventManager;
        #endregion

        #region Camera Management
        private Camera _mainCamera;

        /// <summary>
        /// Cached reference to the main camera for performance
        /// Avoids repeated Camera.main calls which use FindGameObjectWithTag internally
        /// </summary>
        public Camera MainCamera
        {
            get
            {
                // Refresh camera reference if it's null or destroyed
                if (_mainCamera == null)
                {
                    _mainCamera = Camera.main;
                    if (_mainCamera == null)
                    {
                        Debug.LogWarning("[GameManager] No main camera found in scene!");
                    }
                }
                return _mainCamera;
            }
        }
        #endregion

        #region Game State
        private bool _isPaused = false;

        /// <summary>
        /// Indicates whether the game is currently paused
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Pauses the game by setting timeScale to 0
        /// </summary>
        public void PauseGame()
        {
            if (!_isPaused)
            {
                _isPaused = true;
                Time.timeScale = 0f;
                Events.TriggerGamePaused();
            }
        }

        /// <summary>
        /// Resumes the game by setting timeScale to 1
        /// </summary>
        public void ResumeGame()
        {
            if (_isPaused)
            {
                _isPaused = false;
                Time.timeScale = 1f;
                Events.TriggerGameResumed();
            }
        }

        /// <summary>
        /// Toggles between paused and resumed states
        /// </summary>
        public void TogglePause()
        {
            if (_isPaused)
                ResumeGame();
            else
                PauseGame();
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            Debug.Log("[GameManager] Awake called");
            // Initialize current scene reference
            CurrentScene = SceneManager.GetActiveScene();

            // Cache main camera reference
            _mainCamera = Camera.main;
        }

        private void Start()
        {
            Debug.Log($"[GameManager] Start called - EventManager: {(_eventManager != null ? "available" : "NULL")}, MainMenuManager: {(_mainMenuManager != null ? "available" : "NULL")}");

            // Subscribe to EventManager events (after injection)
            if (_eventManager != null)
            {
                SubscribeToEvents();
            }
            else
            {
                Debug.LogError("[GameManager] EventManager reference is null, cannot subscribe to events!");
            }

            if (_mainMenuManager == null)
            {
                Debug.LogError("[GameManager] MainMenuManager reference is null!");
            }

            // Subscribe to Unity scene loaded event
            SceneManager.sceneLoaded += OnSceneLoaded;

            Debug.Log("[GameManager] Start complete");
        }

        /// <summary>
        /// Subscribe to all relevant events from EventManager
        /// </summary>
        private void SubscribeToEvents()
        {
            // Input events
            Debug.Log("[GameManager] Subscribing to OnEscapePressed event");
            Events.OnEscapePressed.AddListener(OnEscapePressed);
            Debug.Log("[GameManager] Subscribing to OnPausePressed event");
            Events.OnPausePressed.AddListener(OnPausePressed);

            // You can subscribe to more events here as needed
            Debug.Log("[GameManager] Event subscriptions complete");

            // Note: GetPersistentEventCount() only returns Inspector-assigned listeners, not runtime ones
            // We've added runtime listeners, so this will show 0 but that's expected
            Debug.Log($"[GameManager] Persistent listener count: {Events.OnEscapePressed.GetPersistentEventCount()} (runtime listeners not included in this count)");
        }

        /// <summary>
        /// Unsubscribe from all EventManager events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (_eventManager != null)
            {
                Events.OnEscapePressed.RemoveListener(OnEscapePressed);
                Events.OnPausePressed.RemoveListener(OnPausePressed);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            UnsubscribeFromEvents();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        #endregion

        #region Scene Loading
        /// <summary>
        /// Asynchronously loads a scene by name
        /// </summary>
        /// <param name="sceneName">Name of the scene to load</param>
        /// <param name="loadMode">Load mode (Single or Additive)</param>
        /// <returns>AsyncOperation for the scene load</returns>
        public async UniTask<AsyncOperation> LoadSceneAsync(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            Events.TriggerSceneLoadStarted(sceneName);

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, loadMode);

            if (asyncLoad != null)
            {
                // Wait until the asynchronous scene fully loads
                while (!asyncLoad.isDone)
                {
                    await UniTask.Yield();
                }
            }

            Events.TriggerSceneLoadCompleted(sceneName);
            return asyncLoad;
        }

        /// <summary>
        /// Asynchronously loads a scene by build index
        /// </summary>
        /// <param name="sceneBuildIndex">Build index of the scene to load</param>
        /// <param name="loadMode">Load mode (Single or Additive)</param>
        /// <returns>AsyncOperation for the scene load</returns>
        public async UniTask<AsyncOperation> LoadSceneAsync(int sceneBuildIndex, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            string sceneName = SceneManager.GetSceneByBuildIndex(sceneBuildIndex).name;
            Events.TriggerSceneLoadStarted(sceneName);

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneBuildIndex, loadMode);

            if (asyncLoad != null)
            {
                // Wait until the asynchronous scene fully loads
                while (!asyncLoad.isDone)
                {
                    await UniTask.Yield();
                }
            }

            Events.TriggerSceneLoadCompleted(sceneName);
            return asyncLoad;
        }

        /// <summary>
        /// Asynchronously unloads a scene by name
        /// </summary>
        /// <param name="sceneName">Name of the scene to unload</param>
        /// <returns>AsyncOperation for the scene unload</returns>
        public async UniTask<AsyncOperation> UnloadSceneAsync(string sceneName)
        {
            AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneName);

            if (asyncUnload != null)
            {
                while (!asyncUnload.isDone)
                {
                    await UniTask.Yield();
                }
            }

            return asyncUnload;
        }
        #endregion

        #region Main Menu Control
        /// <summary>
        /// Toggles the main menu visibility
        /// </summary>
        public void ToggleMainMenu()
        {
            Debug.Log($"[GameManager] ToggleMainMenu called - MainMenuManager is {(_mainMenuManager != null ? "available" : "NULL")}");
            if (_mainMenuManager != null)
            {
                Debug.Log($"[GameManager] Current menu state before toggle: {_mainMenuManager.IsMenuVisible}");
                _mainMenuManager.ToggleMenu();
                // Note: Trigger events in MainMenuManager instead, to know actual state
            }
            else
            {
                Debug.LogWarning("[GameManager] MainMenuManager reference not available!");
            }
        }

        /// <summary>
        /// Shows the main menu
        /// </summary>
        public void ShowMainMenu()
        {
            if (_mainMenuManager != null)
            {
                _mainMenuManager.ShowMenu();
                Events.TriggerMainMenuOpened();
            }
            else
            {
                Debug.LogWarning("[GameManager] MainMenuManager reference not available!");
            }
        }

        /// <summary>
        /// Hides the main menu
        /// </summary>
        public void HideMainMenu()
        {
            if (_mainMenuManager != null)
            {
                _mainMenuManager.HideMenu();
                Events.TriggerMainMenuClosed();
            }
            else
            {
                Debug.LogWarning("[GameManager] MainMenuManager reference not available!");
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Called when a new scene is loaded
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CurrentScene = scene;

            // Refresh camera reference when scene changes
            _mainCamera = Camera.main;
        }

        /// <summary>
        /// Event handler for Escape key press from EventManager
        /// </summary>
        private void OnEscapePressed()
        {
            Debug.Log("[GameManager] OnEscapePressed event received - calling ToggleMainMenu()");
            ToggleMainMenu();
        }

        /// <summary>
        /// Event handler for Pause key press from EventManager
        /// </summary>
        private void OnPausePressed()
        {
            TogglePause();
        }
        #endregion
    }
}
