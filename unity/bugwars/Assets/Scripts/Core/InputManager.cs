using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace BugWars.Core
{
    /// <summary>
    /// Input Manager - Managed by VContainer
    /// Captures all input and fires events through EventManager
    /// Handles global/system-level inputs only (Escape, Pause, etc.)
    /// Game-specific inputs should be handled in their respective controllers
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        #region Dependencies
        private EventManager _eventManager;

        [Inject]
        public void Construct(EventManager eventManager)
        {
            _eventManager = eventManager;
        }
        #endregion

        #region Settings
        [Header("Input Settings")]
        [Tooltip("Enable debug logging for input events")]
        [SerializeField] private bool debugMode = true; // Enabled for debugging
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Component initialized
            Debug.Log("[InputManager] Awake called - InputManager initialized");
        }

        private void Start()
        {
            Debug.Log($"[InputManager] Start called - EventManager reference: {(_eventManager != null ? "available" : "NULL")}");
            if (_eventManager == null)
            {
                Debug.LogError("[InputManager] EventManager was not injected! Input events will not work!");
            }
        }

        private void Update()
        {
            // Only handle global/system-level inputs
            // Game-specific inputs should be in respective controllers
            HandleGlobalInputs();
        }
        #endregion

        #region Input Handling
        /// <summary>
        /// Handles global system-level inputs and fires events
        /// </summary>
        private void HandleGlobalInputs()
        {
            if (Keyboard.current == null)
            {
                if (debugMode)
                    Debug.LogWarning("[InputManager] Keyboard.current is NULL - Input System may not be initialized");
                return;
            }

            if (_eventManager == null)
            {
                if (debugMode)
                    Debug.LogWarning("[InputManager] EventManager is NULL - cannot fire events");
                return;
            }

            // Escape key - Main menu toggle
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (debugMode)
                    Debug.Log("[InputManager] Escape key pressed - firing event");

                _eventManager.TriggerEscapePressed();
            }

            // P key or Pause - Pause toggle (optional, can be removed if not needed)
            if (Keyboard.current.pKey.wasPressedThisFrame ||
                Keyboard.current.pauseKey.wasPressedThisFrame)
            {
                if (debugMode)
                    Debug.Log("[InputManager] Pause key pressed - firing event");

                _eventManager.TriggerPausePressed();
            }

            // Add more global inputs here as needed:
            // - Settings menu (F1, Options key, etc.)
            // - Screenshot (F12)
            // - Debug console (~, F3)
            // - Quit game (Alt+F4 override)
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Enables or disables debug logging
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            debugMode = enabled;
            Debug.Log($"[InputManager] Debug mode {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Check if a specific key is currently pressed
        /// Utility method for other scripts
        /// </summary>
        public bool IsKeyPressed(Key key)
        {
            if (Keyboard.current == null) return false;
            return Keyboard.current[key].isPressed;
        }

        /// <summary>
        /// Check if a specific key was pressed this frame
        /// Utility method for other scripts
        /// </summary>
        public bool IsKeyPressedThisFrame(Key key)
        {
            if (Keyboard.current == null) return false;
            return Keyboard.current[key].wasPressedThisFrame;
        }
        #endregion
    }
}
