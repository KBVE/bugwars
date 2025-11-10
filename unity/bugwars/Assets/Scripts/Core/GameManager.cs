using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace BugWars.Core
{
    /// <summary>
    /// Universal Game Manager - Singleton pattern with DontDestroyOnLoad
    /// Handles core game functionality including scene management
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

        #region Event Handlers
        /// <summary>
        /// Called when a new scene is loaded
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CurrentScene = scene;
            Debug.Log($"[GameManager] Scene loaded: {scene.name} (Build Index: {scene.buildIndex})");
        }
        #endregion
    }
}
