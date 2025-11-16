using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using BugWars.UI;
using BugWars.Terrain;
using BugWars.Entity;
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
        private HUDManager _hudManager;
        private TerrainManager _terrainManager;
        private EntityManager _entityManager;

        [Inject]
        public void Construct(EventManager eventManager, MainMenuManager mainMenuManager, HUDManager hudManager, TerrainManager terrainManager, EntityManager entityManager)
        {
            _eventManager = eventManager;
            _mainMenuManager = mainMenuManager;
            _hudManager = hudManager;
            _terrainManager = terrainManager;
            _entityManager = entityManager;
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

        #region Terrain Management
        /// <summary>
        /// Reference to the TerrainManager for terrain operations
        /// </summary>
        public TerrainManager Terrain => _terrainManager;
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
            // Initialize current scene reference
            CurrentScene = SceneManager.GetActiveScene();

            // Cache main camera reference
            _mainCamera = Camera.main;
        }

        private void Start()
        {
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

            if (_hudManager == null)
            {
                Debug.LogError("[GameManager] HUDManager reference is null!");
            }

            if (_terrainManager == null)
            {
                Debug.LogError("[GameManager] TerrainManager reference is null!");
            }

            // Subscribe to Unity scene loaded event
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Initialize terrain generation
            InitializeWorld().Forget();
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
            if (_mainMenuManager != null)
            {
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

        #region HUD Control
        /// <summary>
        /// Shows the HUD
        /// </summary>
        public void ShowHUD()
        {
            if (_hudManager != null)
            {
                _hudManager.ShowHUD();
            }
            else
            {
                Debug.LogWarning("[GameManager] HUDManager reference not available!");
            }
        }

        /// <summary>
        /// Hides the HUD
        /// </summary>
        public void HideHUD()
        {
            if (_hudManager != null)
            {
                _hudManager.HideHUD();
            }
            else
            {
                Debug.LogWarning("[GameManager] HUDManager reference not available!");
            }
        }

        /// <summary>
        /// Toggles the HUD visibility
        /// </summary>
        public void ToggleHUD()
        {
            if (_hudManager != null)
            {
                _hudManager.ToggleHUD();
            }
            else
            {
                Debug.LogWarning("[GameManager] HUDManager reference not available!");
            }
        }

        /// <summary>
        /// Updates player health in the HUD
        /// </summary>
        public void UpdateHUDHealth(float current, float max)
        {
            if (_hudManager != null)
            {
                _hudManager.UpdateHealth(current, max);
            }
        }

        /// <summary>
        /// Updates wave number in the HUD
        /// </summary>
        public void UpdateHUDWave(int wave)
        {
            if (_hudManager != null)
            {
                _hudManager.UpdateWave(wave);
            }
        }

        /// <summary>
        /// Updates score in the HUD
        /// </summary>
        public void UpdateHUDScore(int score)
        {
            if (_hudManager != null)
            {
                _hudManager.UpdateScore(score);
            }
        }

        /// <summary>
        /// Updates player speed stat in the HUD
        /// </summary>
        public void UpdateHUDSpeed(float speed)
        {
            if (_hudManager != null)
            {
                _hudManager.UpdateSpeed(speed);
            }
        }

        /// <summary>
        /// Updates player damage stat in the HUD
        /// </summary>
        public void UpdateHUDDamage(int damage)
        {
            if (_hudManager != null)
            {
                _hudManager.UpdateDamage(damage);
            }
        }

        /// <summary>
        /// Provides direct access to the HUDManager for advanced usage
        /// </summary>
        public HUDManager HUD => _hudManager;
        #endregion

        #region World Initialization
        /// <summary>
        /// Initialize the game world including terrain generation
        /// Called after all managers are injected and ready
        /// </summary>
        private async UniTask InitializeWorld()
        {
            Debug.Log("[GameManager] ===== InitializeWorld() STARTED =====");

            if (_terrainManager != null)
            {
                // Wait for terrain to be ready (TerrainManager's StartAsync handles generation)
                await UniTask.WaitUntil(() => _terrainManager.IsReady);

                // Spawn player at terrain center after terrain is ready
                if (_entityManager != null && !_entityManager.IsPlayerSpawned())
                {
                    Vector3 spawnPos = _terrainManager.GetTerrainCenter();
                    spawnPos.y = 10f; // Spawn well above terrain (max terrain height ~3f, so 10f is safe)

                    // Raycast down to find actual terrain surface
                    if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 20f))
                    {
                        spawnPos.y = hit.point.y + 1.5f; // Spawn 1.5 units above surface
                    }
                    else
                    {
                        Debug.LogWarning($"[GameManager] No terrain found below spawn position, using Y=10");
                    }

                    _entityManager.SpawnPlayerAt(spawnPos);
                }
            }
            else
            {
                Debug.LogError("[GameManager] Cannot initialize terrain - TerrainManager is null!");
            }
        }

        /// <summary>
        /// Create a simple ground plane - IMMEDIATE FIX for camera seeing void
        /// This creates a large, solid ground that prevents camera from seeing "under" the world
        /// </summary>
        private void CreateGroundPlane()
        {
            Debug.Log("[GameManager] Creating ground plane");

            // Create a large plane (Unity plane is 10x10 units by default)
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GroundPlane";
            ground.transform.position = new Vector3(0, 0, 0);
            ground.transform.localScale = new Vector3(100, 1, 100); // 1000x1000 units ground

            // Try URP shader first, fallback to Standard
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                Debug.Log("[GameManager] Using Standard shader for ground plane");
            }
            else
            {
                Debug.Log("[GameManager] Using URP/Lit shader for ground plane");
            }

            if (shader != null)
            {
                Material groundMat = new Material(shader);
                groundMat.color = new Color(0.4f, 0.7f, 0.3f); // Grass green

                var renderer = ground.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = groundMat;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            }
            else
            {
                Debug.LogError("[GameManager] No shader found! Ground will show purple/pink");
            }

            Debug.Log("[GameManager] Ground plane created at (0,0,0) with 1000x1000 size");
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
