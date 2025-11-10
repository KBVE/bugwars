using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
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
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("GameManager");
                        _instance = go.AddComponent<GameManager>();
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

            // Subscribe to scene loaded event
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (_instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void Update()
        {
            // Handle global/system-level inputs only
            // Game-specific inputs should be handled in their respective controllers
            HandleGlobalInputs();
        }
        #endregion

        #region Input Handling
        /// <summary>
        /// Handles global system-level inputs (menu, pause, etc.)
        /// Game-specific inputs should be handled in their respective controllers
        /// </summary>
        private void HandleGlobalInputs()
        {
            if (Keyboard.current == null) return;

            // Toggle main menu with Escape key
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ToggleMainMenu();
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
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, loadMode);

            if (asyncLoad != null)
            {
                // Wait until the asynchronous scene fully loads
                while (!asyncLoad.isDone)
                {
                    await UniTask.Yield();
                }
            }

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
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneBuildIndex, loadMode);

            if (asyncLoad != null)
            {
                // Wait until the asynchronous scene fully loads
                while (!asyncLoad.isDone)
                {
                    await UniTask.Yield();
                }
            }

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
        #endregion
    }
}
