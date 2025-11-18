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
        private float cameraZoomSensitivity = 1.0f; // Increased from 0.1f for more responsive zoom
        [SerializeField] [Tooltip("Invert vertical camera look input")]
        private bool invertCameraY = true;
        [SerializeField] [Tooltip("When enabled, mouse/gamepad look input and scroll wheel zoom will drive the camera")]
        private bool cameraInputCaptured = true; // Enabled for 3D character camera control (required for zoom)
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
            HandlePlayerRotationInputs();
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
            if (_eventManager == null)
            {
                if (debugMode)
                    Debug.LogWarning("[InputManager] EventManager is null in HandleCameraInputs");
                return;
            }
            
            if (Mouse.current == null)
            {
                if (debugMode)
                    Debug.LogWarning("[InputManager] Mouse.current is null in HandleCameraInputs");
                return;
            }

            // Always allow zoom input even if camera look is disabled
            // Only check cameraInputCaptured for look input, not zoom
            bool allowLookInput = cameraInputCaptured;

            // Only process look input if cameraInputCaptured is enabled
            if (allowLookInput)
            {
                Vector2 lookDelta = Mouse.current.delta.ReadValue();
                if (lookDelta.sqrMagnitude > Mathf.Epsilon)
                {
                    float yMultiplier = invertCameraY ? 1f : -1f;
                    Vector2 scaledDelta = new Vector2(
                        lookDelta.x * cameraLookSensitivity,
                        lookDelta.y * cameraLookSensitivity * yMultiplier);
                    _eventManager.TriggerCameraLook(scaledDelta);
                }
            }

            float scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (debugMode && Mathf.Abs(scrollDelta) > Mathf.Epsilon)
            {
                Debug.Log($"[InputManager] Scroll wheel detected: raw={scrollDelta}");
            }
            
            if (Mathf.Abs(scrollDelta) > Mathf.Epsilon)
            {
                float scaledDelta = scrollDelta * cameraZoomSensitivity;
                if (debugMode)
                    Debug.Log($"[InputManager] Scroll wheel: raw={scrollDelta}, scaled={scaledDelta}, sensitivity={cameraZoomSensitivity}, EventManager={_eventManager != null}");
                
                if (_eventManager != null)
                {
                    _eventManager.TriggerCameraZoom(scaledDelta);
                    if (debugMode)
                        Debug.Log($"[InputManager] TriggerCameraZoom called with: {scaledDelta}");
                }
                else if (debugMode)
                {
                    Debug.LogError("[InputManager] EventManager is NULL! Cannot trigger zoom event!");
                }
            }
        }
        #endregion

        #region Player Movement Input
        /// <summary>
        /// Handles player movement inputs (W/S for forward/backward) and broadcasts to EventManager
        /// Standard WASD: W/S moves forward/backward relative to character facing
        /// </summary>
        private void HandlePlayerMovementInputs()
        {
            if (_eventManager == null || Keyboard.current == null)
            {
                return;
            }

            // Get forward/backward movement input (W/S)
            float vertical = 0f;

            // Vertical: W/S or Up/Down (forward/backward movement)
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                vertical = -1f; // Backward
            else if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                vertical = 1f; // Forward

            // Broadcast movement input (forward/backward only, relative to character facing)
            Vector2 movement = new Vector2(0f, vertical);
            _eventManager.TriggerPlayerMovementInput(movement);
        }
        #endregion

        #region Player Rotation Input
        /// <summary>
        /// Handles player rotation inputs (A/D for left/right rotation) and broadcasts to EventManager
        /// Standard WASD: A/D rotates character left/right
        /// </summary>
        private void HandlePlayerRotationInputs()
        {
            if (_eventManager == null || Keyboard.current == null)
            {
                return;
            }

            // Get rotation input (A/D)
            float rotation = 0f;

            // Horizontal: A/D or Left/Right (rotate left/right)
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                rotation = -1f; // Rotate left
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                rotation = 1f; // Rotate right

            // Broadcast rotation input (always, even if zero for consistent updates)
            _eventManager.TriggerPlayerRotationInput(rotation);
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
