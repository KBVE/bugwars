using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace BugWars.Core
{
    /// <summary>
    /// Central event management system - Managed by VContainer
    /// Handles all game events using UnityEvents for decoupled communication
    /// Also provides a generic event system for dynamic event handling
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        #region Generic Event System
        // Dictionary to store generic event listeners
        private Dictionary<string, Delegate> _genericEvents = new Dictionary<string, Delegate>();

        /// <summary>
        /// Add a listener for a generic event with typed payload
        /// </summary>
        public void AddListener<T>(string eventName, Action<T> listener)
        {
            if (_genericEvents.ContainsKey(eventName))
            {
                _genericEvents[eventName] = Delegate.Combine(_genericEvents[eventName], listener);
            }
            else
            {
                _genericEvents[eventName] = listener;
            }
        }

        /// <summary>
        /// Remove a listener for a generic event with typed payload
        /// </summary>
        public void RemoveListener<T>(string eventName, Action<T> listener)
        {
            if (_genericEvents.ContainsKey(eventName))
            {
                _genericEvents[eventName] = Delegate.Remove(_genericEvents[eventName], listener);
                if (_genericEvents[eventName] == null)
                {
                    _genericEvents.Remove(eventName);
                }
            }
        }

        /// <summary>
        /// Trigger a generic event with typed payload
        /// </summary>
        public void TriggerEvent<T>(string eventName, T data)
        {
            if (_genericEvents.ContainsKey(eventName))
            {
                var eventDelegate = _genericEvents[eventName] as Action<T>;
                eventDelegate?.Invoke(data);
            }
        }
        #endregion

        #region Event Definitions
        // Input Events - System
        public UnityEvent OnEscapePressed = new UnityEvent();
        public UnityEvent OnPausePressed = new UnityEvent();

        // Input Events - Player Movement
        public UnityEvent<Vector2> OnPlayerMovementInput = new UnityEvent<Vector2>();
        public UnityEvent<float> OnPlayerRotationInput = new UnityEvent<float>(); // Rotation input: -1 (A/Left) to +1 (D/Right)

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

        // Camera Events
        public UnityEvent<CameraFollowConfig> OnCameraFollowRequested = new UnityEvent<CameraFollowConfig>();
        public UnityEvent<string> OnCameraStopFollowRequested = new UnityEvent<string>();
        public UnityEvent<Vector2> OnCameraLookInput = new UnityEvent<Vector2>();
        public UnityEvent<float> OnCameraZoomInput = new UnityEvent<float>();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Component initialized
        }

        private void Start()
        {
        }

        private void OnDestroy()
        {
            // Clean up all listeners
            OnEscapePressed.RemoveAllListeners();
            OnPausePressed.RemoveAllListeners();
            OnPlayerMovementInput.RemoveAllListeners();
            OnPlayerRotationInput.RemoveAllListeners();
            OnGamePaused.RemoveAllListeners();
            OnGameResumed.RemoveAllListeners();
            OnSceneLoadStarted.RemoveAllListeners();
            OnSceneLoadCompleted.RemoveAllListeners();
            OnPlayerHealthChanged.RemoveAllListeners();
            OnPlayerDied.RemoveAllListeners();
            OnMainMenuOpened.RemoveAllListeners();
            OnMainMenuClosed.RemoveAllListeners();
            OnCameraFollowRequested.RemoveAllListeners();
            OnCameraStopFollowRequested.RemoveAllListeners();
            OnCameraLookInput.RemoveAllListeners();
            OnCameraZoomInput.RemoveAllListeners();

            // Clean up generic events
            _genericEvents.Clear();
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

        /// <summary>
        /// Request camera to follow a target
        /// </summary>
        public void RequestCameraFollow(CameraFollowConfig config)
        {
            if (config.target == null)
            {
                Debug.LogWarning("[EventManager] Cannot request camera follow - target is null");
                return;
            }
            OnCameraFollowRequested?.Invoke(config);
        }

        /// <summary>
        /// Request camera to stop following
        /// </summary>
        public void RequestCameraStopFollow(string cameraName = null)
        {
            OnCameraStopFollowRequested?.Invoke(cameraName);
        }

        /// <summary>
        /// Broadcast camera look input (mouse/stick delta)
        /// </summary>
        public void TriggerCameraLook(Vector2 delta)
        {
            OnCameraLookInput?.Invoke(delta);
        }

        /// <summary>
        /// Broadcast camera zoom input (scroll wheel or gamepad trigger)
        /// </summary>
        public void TriggerCameraZoom(float delta)
        {
            Debug.Log($"[EventManager] TriggerCameraZoom called with delta={delta}, listenerCount={OnCameraZoomInput?.GetPersistentEventCount() ?? 0}");
            OnCameraZoomInput?.Invoke(delta);
        }

        /// <summary>
        /// Broadcast player movement input (WASD/arrow keys or gamepad stick)
        /// </summary>
        public void TriggerPlayerMovementInput(Vector2 movement)
        {
            OnPlayerMovementInput?.Invoke(movement);
        }

        /// <summary>
        /// Broadcast player rotation input (A/D or Left/Right arrow keys)
        /// </summary>
        public void TriggerPlayerRotationInput(float rotation)
        {
            OnPlayerRotationInput?.Invoke(rotation);
        }
        #endregion

        #region Debug Helpers
        /// <summary>
        /// Logs all active event listeners (for debugging)
        /// </summary>
        public void LogActiveListeners()
        {
            // Debug logging removed - use defensive logging only
        }
        #endregion
    }
}
