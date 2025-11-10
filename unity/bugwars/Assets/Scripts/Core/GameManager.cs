using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using BugWars.UI;

namespace BugWars.Core
{
    /// <summary>
    /// Universal Game Manager - Singleton pattern with DontDestroyOnLoad
    /// Handles core game functionality including scene management and main menu control
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Singleton
        private static GameManager _instance;

        /// <summary>
        /// Singleton instance - managed by VContainer
        /// </summary>
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GameManager>();
                    if (_instance == null)
                    {
                        Debug.LogWarning("[GameManager] Instance not found! Make sure GameLifetimeScope is in the scene.");
                    }
                }
                return _instance;
            }
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
        private EventManager _eventManager;

        /// <summary>
        /// Cached reference to the EventManager for performance
        /// </summary>
        public EventManager Events
        {
            get
            {
                if (_eventManager == null)
                {
                    _eventManager = EventManager.Instance;
                }
                return _eventManager;
            }
        }
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
                Debug.Log("[GameManager] Game paused");
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
                Debug.Log("[GameManager] Game resumed");
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
            // Ensure singleton pattern
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize current scene reference
            CurrentScene = SceneManager.GetActiveScene();

            // Cache main camera reference
            _mainCamera = Camera.main;

            // Initialize EventManager and InputManager (ensures they exist)
            _eventManager = EventManager.Instance;
            var inputManager = InputManager.Instance;

            // Subscribe to EventManager events
            SubscribeToEvents();

            // Subscribe to Unity scene loaded event
            SceneManager.sceneLoaded += OnSceneLoaded;

            Debug.Log("[GameManager] Initialized with Event System");
        }

        /// <summary>
        /// Subscribe to all relevant events from EventManager
        /// </summary>
        private void SubscribeToEvents()
        {
            // Input events
            Events.OnEscapePressed.AddListener(OnEscapePressed);
            Events.OnPausePressed.AddListener(OnPausePressed);

            // You can subscribe to more events here as needed
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
            if (_instance == this)
            {
                UnsubscribeFromEvents();
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
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
            if (MainMenuManager.Instance != null)
            {
                MainMenuManager.Instance.ToggleMenu();
                // Note: Trigger events in MainMenuManager instead, to know actual state
            }
            else
            {
                Debug.LogWarning("[GameManager] MainMenuManager instance not found!");
            }
        }

        /// <summary>
        /// Shows the main menu
        /// </summary>
        public void ShowMainMenu()
        {
            if (MainMenuManager.Instance != null)
            {
                MainMenuManager.Instance.ShowMenu();
                Events.TriggerMainMenuOpened();
            }
            else
            {
                Debug.LogWarning("[GameManager] MainMenuManager instance not found!");
            }
        }

        /// <summary>
        /// Hides the main menu
        /// </summary>
        public void HideMainMenu()
        {
            if (MainMenuManager.Instance != null)
            {
                MainMenuManager.Instance.HideMenu();
                Events.TriggerMainMenuClosed();
            }
            else
            {
                Debug.LogWarning("[GameManager] MainMenuManager instance not found!");
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

            Debug.Log($"[GameManager] Scene loaded: {scene.name} (Build Index: {scene.buildIndex})");
        }

        /// <summary>
        /// Event handler for Escape key press from EventManager
        /// </summary>
        private void OnEscapePressed()
        {
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
