using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace BugWars.Core
{
    /// <summary>
    /// Central event management system - Singleton pattern
    /// Handles all game events using UnityEvents for decoupled communication
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        #region Singleton
        private static EventManager _instance;
        public static EventManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<EventManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("EventManager");
                        _instance = go.AddComponent<EventManager>();
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Event Definitions
        // Input Events
        public UnityEvent OnEscapePressed = new UnityEvent();
        public UnityEvent OnPausePressed = new UnityEvent();

        // Game State Events
        public UnityEvent OnGamePaused = new UnityEvent();
        public UnityEvent OnGameResumed = new UnityEvent();

        // Scene Events
        public UnityEvent<string> OnSceneLoadStarted = new UnityEvent<string>();
        public UnityEvent<string> OnSceneLoadCompleted = new UnityEvent<string>();

        // Player Events (examples for future use)
        public UnityEvent<float> OnPlayerHealthChanged = new UnityEvent<float>();
        public UnityEvent OnPlayerDied = new UnityEvent();

        // UI Events
        public UnityEvent OnMainMenuOpened = new UnityEvent();
        public UnityEvent OnMainMenuClosed = new UnityEvent();
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

            Debug.Log("[EventManager] Initialized");
        }

        private void OnDestroy()
        {
            // Clean up all listeners
            if (_instance == this)
            {
                OnEscapePressed.RemoveAllListeners();
                OnPausePressed.RemoveAllListeners();
                OnGamePaused.RemoveAllListeners();
                OnGameResumed.RemoveAllListeners();
                OnSceneLoadStarted.RemoveAllListeners();
                OnSceneLoadCompleted.RemoveAllListeners();
                OnPlayerHealthChanged.RemoveAllListeners();
                OnPlayerDied.RemoveAllListeners();
                OnMainMenuOpened.RemoveAllListeners();
                OnMainMenuClosed.RemoveAllListeners();
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Triggers the Escape key pressed event
        /// </summary>
        public void TriggerEscapePressed()
        {
            OnEscapePressed?.Invoke();
        }

        /// <summary>
        /// Triggers the Pause key pressed event
        /// </summary>
        public void TriggerPausePressed()
        {
            OnPausePressed?.Invoke();
        }

        /// <summary>
        /// Triggers the Game Paused event
        /// </summary>
        public void TriggerGamePaused()
        {
            OnGamePaused?.Invoke();
        }

        /// <summary>
        /// Triggers the Game Resumed event
        /// </summary>
        public void TriggerGameResumed()
        {
            OnGameResumed?.Invoke();
        }

        /// <summary>
        /// Triggers the Scene Load Started event
        /// </summary>
        public void TriggerSceneLoadStarted(string sceneName)
        {
            OnSceneLoadStarted?.Invoke(sceneName);
        }

        /// <summary>
        /// Triggers the Scene Load Completed event
        /// </summary>
        public void TriggerSceneLoadCompleted(string sceneName)
        {
            OnSceneLoadCompleted?.Invoke(sceneName);
        }

        /// <summary>
        /// Triggers the Main Menu Opened event
        /// </summary>
        public void TriggerMainMenuOpened()
        {
            OnMainMenuOpened?.Invoke();
        }

        /// <summary>
        /// Triggers the Main Menu Closed event
        /// </summary>
        public void TriggerMainMenuClosed()
        {
            OnMainMenuClosed?.Invoke();
        }
        #endregion

        #region Debug Helpers
        /// <summary>
        /// Logs all active event listeners (for debugging)
        /// </summary>
        public void LogActiveListeners()
        {
            Debug.Log($"[EventManager] Active Listeners:");
            Debug.Log($"  OnEscapePressed: {OnEscapePressed.GetPersistentEventCount()}");
            Debug.Log($"  OnPausePressed: {OnPausePressed.GetPersistentEventCount()}");
            Debug.Log($"  OnGamePaused: {OnGamePaused.GetPersistentEventCount()}");
            Debug.Log($"  OnGameResumed: {OnGameResumed.GetPersistentEventCount()}");
        }
        #endregion
    }
}
