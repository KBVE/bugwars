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
        [SerializeField] [Tooltip("Scale applied to raw mouse delta before broadcasting camera look input")]
        private float cameraLookSensitivity = 0.1f;
        [SerializeField] [Tooltip("Scale applied to scroll wheel delta before broadcasting camera zoom input")]
        private float cameraZoomSensitivity = 0.1f;
        [SerializeField] [Tooltip("Invert vertical camera look input")]
        private bool invertCameraY = true;
        [SerializeField] [Tooltip("When enabled, mouse/gamepad look input will drive the camera")]
        private bool cameraInputCaptured = true; // Enabled for 3D character camera control
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Component initialized
        }

        private void Start()
        {
            if (_eventManager == null)
            {
                Debug.LogError("[InputManager] EventManager was not injected! Input events will not work!");
            }
        }

        private void Update()
        {
            HandleGlobalInputs();
            HandleCameraInputs();
            HandlePlayerMovementInputs();
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
                _eventManager.TriggerEscapePressed();
            }

            // P key or Pause - Pause toggle (optional, can be removed if not needed)
            if (Keyboard.current.pKey.wasPressedThisFrame ||
                Keyboard.current.pauseKey.wasPressedThisFrame)
            {
                _eventManager.TriggerPausePressed();
            }

            // Add more global inputs here as needed:
            // - Settings menu (F1, Options key, etc.)
            // - Screenshot (F12)
            // - Debug console (~, F3)
            // - Quit game (Alt+F4 override)
        }
        #endregion

        #region Camera Input
        private void HandleCameraInputs()
        {
            if (_eventManager == null || Mouse.current == null)
            {
                return;
            }

            if (!cameraInputCaptured)
            {
                return;
            }

            Vector2 lookDelta = Mouse.current.delta.ReadValue();
            if (lookDelta.sqrMagnitude > Mathf.Epsilon)
            {
                float yMultiplier = invertCameraY ? 1f : -1f;
                Vector2 scaledDelta = new Vector2(
                    lookDelta.x * cameraLookSensitivity,
                    lookDelta.y * cameraLookSensitivity * yMultiplier);
                _eventManager.TriggerCameraLook(scaledDelta);
            }

            float scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollDelta) > Mathf.Epsilon)
            {
                _eventManager.TriggerCameraZoom(scrollDelta * cameraZoomSensitivity);
            }
        }
        #endregion

        #region Player Movement Input
        /// <summary>
        /// Handles player movement inputs (WASD, arrow keys) and broadcasts to EventManager
        /// </summary>
        private void HandlePlayerMovementInputs()
        {
            if (_eventManager == null || Keyboard.current == null)
            {
                return;
            }

            // Get movement input
            float horizontal = 0f;
            float vertical = 0f;

            // Horizontal: A/D or Left/Right
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontal = -1f;
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontal = 1f;

            // Vertical: W/S or Up/Down
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                vertical = -1f;
            else if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                vertical = 1f;

            // Broadcast movement input (always, even if zero for consistent updates)
            Vector2 movement = new Vector2(horizontal, vertical);
            _eventManager.TriggerPlayerMovementInput(movement);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Enables or disables debug logging
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            debugMode = enabled;
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
