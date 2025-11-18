using UnityEngine;
using VContainer;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace BugWars.Core
{

    /// <summary>
    /// Camera follow configuration for event-based camera control
    /// Supports cinematic camera parameters for professional feel
    /// </summary>
    public struct CameraFollowConfig
    {
        public Transform target;
        public string cameraName;
        public Vector3 shoulderOffset;
        public float verticalArmLength;
        public float cameraDistance;
        public bool immediate;

        // Cinematic parameters for smooth, professional camera feel
        public Vector3 positionDamping;      // XYZ damping for lag/smoothing
        public float screenX;                // Horizontal screen position (0-1, 0.5 = center)
        public float screenY;                // Vertical screen position (0-1, 0.5 = center)
        public float deadZoneWidth;          // No movement inside this (0-1)
        public float deadZoneHeight;         // No movement inside this (0-1)
        public float softZoneWidth;          // Gradual movement zone (0-1)
        public float softZoneHeight;         // Gradual movement zone (0-1)
        public float lookaheadTime;          // Anticipate motion (seconds)
        public float lookaheadSmoothing;     // Smooth lookahead (0-1)
        public Vector2 pitchClamp;           // Min/max pitch angles for billboard sprites

        /// <summary>
        /// Simple third-person follow preset: camera follows behind player and rotates with player
        /// Perfect for 3D low-poly characters
        /// Auto-rotates to stay behind player, follows player height, no mouse control needed
        /// </summary>
        public static CameraFollowConfig SimpleThirdPerson(Transform target, string cameraName = null, bool immediate = false)
        {
            return new CameraFollowConfig
            {
                target = target,
                cameraName = cameraName,
                shoulderOffset = new Vector3(0f, 4.4f, 0f), // Height offset: 4.4 units above player (~10% higher)
                verticalArmLength = 0f,
                cameraDistance = 8f, // Distance: 8 units behind player (not used, TargetOffset matters)
                immediate = immediate,

                // Damping: smooth follow (0.1 seconds for responsive but smooth following)
                // Y-axis damping increased (0.2) for smoother vertical following on slopes
                // Reduced from 0.15 to prevent camera drift
                positionDamping = new Vector3(0.1f, 0.2f, 0.1f),

                // Screen framing: centered
                screenX = 0.5f,
                screenY = 0.5f,

                // Dead zone: none needed for simple follow
                deadZoneWidth = 0f,
                deadZoneHeight = 0f,

                // Soft zone: none needed
                softZoneWidth = 0f,
                softZoneHeight = 0f,

                // Lookahead: none needed (camera follows directly)
                lookaheadTime = 0f,
                lookaheadSmoothing = 0f,

                // Pitch clamp: moderate downward angle for good visibility
                pitchClamp = new Vector2(15f, 50f)
            };
        }

        /// <summary>
        /// First-person camera preset: camera at player's eye level
        /// Camera is positioned at player's head/eye level, very close for immersive view
        /// </summary>
        public static CameraFollowConfig FirstPerson(Transform target, string cameraName = null, bool immediate = false)
        {
            return new CameraFollowConfig
            {
                target = target,
                cameraName = cameraName,
                shoulderOffset = new Vector3(0f, 1.6f, 0f), // Height offset: eye level (Y: 1.6)
                verticalArmLength = 0f,
                cameraDistance = 0.1f, // Very close distance for first-person (Z: -0.1)
                immediate = immediate,

                // Damping: very responsive for first-person (instant feel)
                positionDamping = new Vector3(0.05f, 0.05f, 0.05f),

                // Screen framing: centered
                screenX = 0.5f,
                screenY = 0.5f,

                // Dead zone: none needed
                deadZoneWidth = 0f,
                deadZoneHeight = 0f,

                // Soft zone: none needed
                softZoneWidth = 0f,
                softZoneHeight = 0f,

                // Lookahead: none needed
                lookaheadTime = 0f,
                lookaheadSmoothing = 0f,

                // Pitch clamp: allow full vertical rotation for first-person
                pitchClamp = new Vector2(-89f, 89f)
            };
        }
    }

    /// <summary>
    /// Camera Manager - Managed by VContainer
    /// Handles all camera operations including Cinemachine virtual cameras
    /// Automatically discovers and caches virtual cameras in the scene
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        private static CameraManager _instance;
        public static CameraManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CameraManager>();
                }
                return _instance;
            }
        }

        private EventManager _eventManager;
        private InputManager _inputManager;

        [Inject]
        public void Construct(EventManager eventManager, InputManager inputManager)
        {
            _eventManager = eventManager;
            _inputManager = inputManager;
            Debug.Log("[CameraManager] Dependencies injected via Construct()");
        }

        [Header("Settings")]
        [SerializeField] private bool debugMode = true; // Enabled for debugging camera setup
        [SerializeField] [Tooltip("Force camera settings from Inspector values on Awake (ensures consistency across team machines). Disable to allow manual camera modifications.")]
        private bool forceCameraSettings = true;
        [SerializeField] [Tooltip("Use orthographic projection (better readability) or perspective (depth)")]
        private bool preferOrthographic = false;

        [Header("Auto-Discovery")]
        [SerializeField] [Tooltip("Automatically find virtual cameras on Start")]
        private bool autoDiscoverCameras = true;

        [SerializeField] [Tooltip("Include inactive virtual cameras in discovery")]
        private bool includeInactiveCameras = false;

        private Camera _mainCamera;
        private CinemachineBrain _cinemachineBrain;

        // Cached virtual cameras
        private Dictionary<string, CinemachineCamera> _virtualCamerasByName = new Dictionary<string, CinemachineCamera>();
        private List<CinemachineCamera> _allVirtualCameras = new List<CinemachineCamera>();

        // Active camera tracking
        private CinemachineCamera _currentActiveCamera;
        private CinemachinePanTilt _activePanTilt;
        private CinemachinePositionComposer _positionComposer; // Reference for PositionComposer system
        private CinemachineOrbitalFollow _orbitalFollow; // Reference for OrbitalFollow system
        private int _defaultPriority = 10;
        private int _activePriority = 100;

        [Header("Camera Input Settings")]
        [SerializeField] [Tooltip("Default field of view for perspective cameras")]
        private float perspectiveFov = 60f;
        [SerializeField] [Tooltip("Minimum distance when zooming the third-person camera")]
        private float minZoomDistance = 4.0f;
        [SerializeField] [Tooltip("Maximum distance when zooming the third-person camera")]
        private float maxZoomDistance = 10.0f;
        [SerializeField] [Tooltip("Amount to change camera distance per zoom input unit")]
        private float zoomStep = 0.4f;
        [SerializeField] [Tooltip("Smoothing applied when adjusting zoom")]
        private float zoomSmoothing = 0.35f;
        [SerializeField] [Tooltip("Horizontal camera look sensitivity")]
        private float lookSensitivityX = 1.0f;
        [SerializeField] [Tooltip("Vertical camera look sensitivity")]
        private float lookSensitivityY = 1.0f;
        [SerializeField] [Tooltip("Invert the Y axis for camera look input")]
        private bool invertY = true;
        [SerializeField] [Tooltip("Default downward tilt (in degrees) applied when the camera is initialised")]
        private float defaultTiltAngle = 20f; // Slightly steeper to match higher camera position
        [SerializeField] [Tooltip("Default yaw offset relative to the target forward when camera initialises")]
        private float defaultPanOffset = 0f;

        [Header("Camera Mode")]
        [SerializeField] [Tooltip("Camera perspective mode: First Person (player view) or Third Person (behind player)")]
        private CameraMode cameraMode = CameraMode.ThirdPerson;
        
        [Header("Camera System")]
        [SerializeField] [Tooltip("Camera system: PositionComposer (manual control) or OrbitalFollow (built-in distance/zoom)")]
        private CameraSystemType cameraSystem = CameraSystemType.PositionComposer;
        
        public enum CameraSystemType
        {
            PositionComposer,  // Current system: PositionComposer + PanTilt (more control)
            OrbitalFollow      // Alternative: CinemachineOrbitalFollow (simpler, built-in zoom)
        }

        [Header("Third-Person Enhancements")]
        [SerializeField] [Tooltip("Camera will automatically rotate to follow player's movement direction")]
        private bool autoRotateToMovement = true;
        [SerializeField] [Tooltip("How fast camera rotates to follow movement (degrees/second)")]
        private float autoRotateSpeed = 120f;
        [SerializeField] [Tooltip("Minimum movement speed required to trigger auto-rotation")]
        private float movementThreshold = 0.1f;
        [SerializeField] [Tooltip("Enable smooth camera lag for cinematic feel")]
        private bool enableCameraLag = true;
        [SerializeField] [Tooltip("Camera lag amount (0 = instant, 1 = very laggy)")]
        [Range(0f, 1f)]
        private float cameraLagAmount = 0.15f;
        [SerializeField] [Tooltip("Camera will lean into turns for dynamic feel")]
        private bool enableCameraLean = false;
        [SerializeField] [Tooltip("Maximum lean angle when turning (degrees)")]
        [Range(0f, 15f)]
        private float maxLeanAngle = 5f;
        [SerializeField] [Tooltip("Add slight vertical offset based on player velocity")]
        private bool enableDynamicHeight = true;
        [SerializeField] [Tooltip("Maximum height adjustment based on vertical velocity")]
        [Range(0f, 2f)]
        private float dynamicHeightAmount = 0.5f;
        [SerializeField] [Tooltip("Camera FOV increases slightly when sprinting")]
        private bool enableSprintFOV = true;
        [SerializeField] [Tooltip("FOV increase when sprinting")]
        [Range(0f, 15f)]
        private float sprintFOVIncrease = 8f;
        [SerializeField] [Tooltip("Target speed to trigger sprint FOV (units/second)")]
        private float sprintSpeedThreshold = 6f;
        [SerializeField] [Tooltip("Enable camera recentering button")]
        private bool enableRecenterButton = true;
        [SerializeField] [Tooltip("Key to press to recenter camera behind player and reset zoom")]
        private KeyCode recenterKey = KeyCode.V;
        [SerializeField] [Tooltip("Enable zoom with scroll wheel")]
        private bool enableZoom = true;
        [SerializeField] [Tooltip("Default zoom distance (negative = behind player)")]
        private float defaultZoomDistance = -8.5f;

        private CameraFollowConfig _activeFollowConfig;
        private bool _hasActiveFollowConfig;
        private readonly Dictionary<string, CinemachinePanTilt> _panTiltCache = new Dictionary<string, CinemachinePanTilt>();

        // Track previous position for velocity calculation
        private Vector3 _previousPlayerPosition;
        private float _currentSprintFOV = 0f;
        private float _currentLeanAngle = 0f;
        
        // Store base camera offset to prevent drift
        private Vector3 _baseCameraOffset;
        
        // Zoom tracking (WoW-style: distance from player, not offset)
        private float _currentZoomDistance = 8.5f; // Default third-person distance (positive = units away from player)
        private float _targetZoomDistance = 8.5f; // Target zoom for smooth interpolation
        private bool _isResettingZoom = false; // Flag for smooth zoom reset
        private Vector3 _zoomDirection = Vector3.back; // Direction from player to camera (normalized)

        /// <summary>
        /// Gets the main Unity Camera component
        /// </summary>
        public Camera MainCamera
        {
            get
            {
                if (_mainCamera == null)
                {
                    _mainCamera = Camera.main;
                    if (_mainCamera == null)
                    {
                        Debug.LogWarning("[CameraManager] No main camera found in scene!");
                    }
                }
                return _mainCamera;
            }
        }

        /// <summary>
        /// Gets the Cinemachine Brain component attached to the main camera
        /// </summary>
        public CinemachineBrain CinemachineBrain
        {
            get
            {
                if (_cinemachineBrain == null && MainCamera != null)
                {
                    _cinemachineBrain = MainCamera.GetComponent<CinemachineBrain>();
                    if (_cinemachineBrain == null)
                    {
                        Debug.LogWarning("[CameraManager] No CinemachineBrain found on main camera!");
                    }
                }
                return _cinemachineBrain;
            }
        }

        /// <summary>
        /// Gets the currently active virtual camera
        /// </summary>
        public CinemachineCamera ActiveVirtualCamera => _currentActiveCamera;

        /// <summary>
        /// Gets all discovered virtual cameras (read-only)
        /// </summary>
        public IReadOnlyList<CinemachineCamera> AllVirtualCameras => _allVirtualCameras.AsReadOnly();

        /// <summary>
        /// Gets the total number of discovered virtual cameras
        /// </summary>
        public int VirtualCameraCount => _allVirtualCameras.Count;

        private void Awake()
        {
            // Initialize camera settings (ensures consistency across team project machines)
            InitializeCameraSettings();

            // Singleton setup
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[CameraManager] Multiple CameraManager instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (debugMode)
                Debug.Log("[CameraManager] Awake called");

            // Cache main camera reference early
            _mainCamera = Camera.main;

            if (_mainCamera != null && debugMode)
            {
                Debug.Log($"[CameraManager] Main camera found: {_mainCamera.name}");
            }
        }

        /// <summary>
        /// Initializes camera settings to ensure consistency across team project machines
        /// Called in Awake to ensure settings are applied before any camera operations
        /// Replaces the old ForceCameraDefaults() method - forces SerializeField values to be applied
        /// Can be disabled via forceCameraSettings to allow manual camera modifications
        /// </summary>
        private void InitializeCameraSettings()
        {
            // Skip forcing if disabled (allows manual camera modifications)
            if (!forceCameraSettings)
            {
                if (debugMode)
                    Debug.Log("[CameraManager] Force camera settings is disabled - using current camera values");
                return;
            }

            // Access Camera.main directly (MainCamera property may not be initialized yet)
            Camera mainCam = Camera.main;
            
            // Force main camera to use Inspector SerializeField values
            // This ensures consistency across team machines (main camera settings can vary)
            if (mainCam != null)
            {
                // Force orthographic mode from Inspector
                mainCam.orthographic = preferOrthographic;
                
                // Force FOV from Inspector
                if (!preferOrthographic)
                {
                    mainCam.fieldOfView = perspectiveFov;
                }
                
                if (debugMode)
                {
                    Debug.Log($"[CameraManager] Force applied camera settings: Orthographic={preferOrthographic}, FOV={perspectiveFov}°");
                }
            }
            else if (debugMode)
            {
                Debug.LogWarning("[CameraManager] Camera.main not found during initialization - settings will be applied when camera is available");
            }
            
            // Validate and clamp defaultTiltAngle to valid range
            // This prevents issues if someone accidentally sets it to an invalid value
            if (defaultTiltAngle < 0f || defaultTiltAngle > 90f)
            {
                defaultTiltAngle = 27.5f; // Reset to optimal value
                if (debugMode)
                    Debug.LogWarning($"[CameraManager] defaultTiltAngle was out of range, reset to: {defaultTiltAngle}°");
            }
            
            // Note: Active camera configs will be reapplied when they're set up
            // The SerializeField values (defaultTiltAngle, perspectiveFov, etc.) are used directly
            // in ConfigureSimpleThirdPerson, so they will be applied automatically
        }


        private void ReapplyActiveFollowConfig()
        {
            if (!_hasActiveFollowConfig || _currentActiveCamera == null)
            {
                return;
            }

            var config = _activeFollowConfig;
            
            // Apply mode-based settings
            if (cameraMode == CameraMode.FirstPerson)
            {
                config.shoulderOffset = new Vector3(0f, 1.6f, 0f);
                config.cameraDistance = 0.1f;
                config.positionDamping = new Vector3(0.05f, 0.05f, 0.05f);
                config.pitchClamp = new Vector2(-89f, 89f);
            }
            else // ThirdPerson
            {
                config.shoulderOffset = new Vector3(0f, 2f, 0f);
                config.cameraDistance = 5f;
                config.positionDamping = new Vector3(0.15f, 0.15f, 0.15f);
                config.pitchClamp = new Vector2(15f, 50f);
            }
            _activeFollowConfig = config;

            ConfigureThirdPersonFollow(_currentActiveCamera, config);
        }

        /// <summary>
        /// Called when Inspector values change - validates values and updates camera if needed
        /// Also ensures Inspector values match code defaults when forceCameraSettings is enabled
        /// </summary>
        private void OnValidate()
        {
            // Validate and clamp values to ensure they're within acceptable ranges
            ValidateCameraSettings();

            // Reconfigure camera if mode changed and we have an active follow config
            if (Application.isPlaying && _hasActiveFollowConfig && _currentActiveCamera != null)
            {
                ReapplyActiveFollowConfig();
            }
        }

        /// <summary>
        /// Validates and clamps camera settings to ensure they're within acceptable ranges
        /// Called from OnValidate() to keep Inspector values in sync with code expectations
        /// </summary>
        private void ValidateCameraSettings()
        {
            // Clamp FOV to reasonable range
            if (perspectiveFov < 30f || perspectiveFov > 120f)
            {
                perspectiveFov = Mathf.Clamp(perspectiveFov, 30f, 120f);
                if (debugMode)
                    Debug.LogWarning($"[CameraManager] perspectiveFov clamped to: {perspectiveFov}°");
            }

            // Clamp zoom distances
            if (minZoomDistance < 1f)
                minZoomDistance = 1f;
            if (maxZoomDistance < minZoomDistance)
                maxZoomDistance = minZoomDistance + 1f;

            // Clamp tilt angle to valid range
            if (defaultTiltAngle < 0f || defaultTiltAngle > 90f)
            {
                defaultTiltAngle = Mathf.Clamp(defaultTiltAngle, 0f, 90f);
                if (debugMode)
                    Debug.LogWarning($"[CameraManager] defaultTiltAngle clamped to: {defaultTiltAngle}°");
            }

            // Ensure zoom step is positive
            if (zoomStep <= 0f)
                zoomStep = 0.1f;
        }

        /// <summary>
        /// Resets camera settings to optimal default values
        /// Call this method to restore defaults (useful for syncing Inspector with code defaults)
        /// </summary>
        [ContextMenu("Reset Camera Settings to Defaults")]
        public void ResetCameraSettingsToDefaults()
        {
            perspectiveFov = 60f;
            minZoomDistance = 4.0f;
            maxZoomDistance = 10.0f;
            zoomStep = 0.4f;
            zoomSmoothing = 0.35f;
            lookSensitivityX = 1.0f;
            lookSensitivityY = 1.0f;
            invertY = true;
            defaultTiltAngle = 20f;
            defaultPanOffset = 0f;
            cameraMode = CameraMode.ThirdPerson;
            preferOrthographic = false;

            if (debugMode)
                Debug.Log("[CameraManager] Camera settings reset to defaults");

            // Reapply settings if camera is active
            if (Application.isPlaying)
            {
                InitializeCameraSettings();
                if (_hasActiveFollowConfig && _currentActiveCamera != null)
                {
                    ReapplyActiveFollowConfig();
                }
            }
        }

        private void Start()
        {
            if (debugMode)
            {
                Debug.Log($"[CameraManager] Start called");
                Debug.Log($"[CameraManager] EventManager: {(_eventManager != null ? "✓ available" : "✗ NULL")}");
                Debug.Log($"[CameraManager] InputManager: {(_inputManager != null ? "✓ available" : "✗ NULL")}");
                Debug.Log($"[CameraManager] MainCamera: {(_mainCamera != null ? "✓ found" : "✗ NULL")}");
            }

            // Validate dependencies
            if (_eventManager == null)
            {
                Debug.LogError("[CameraManager] EventManager was not injected! Camera events will not work.");
            }
            
            if (_inputManager == null)
            {
                Debug.LogWarning("[CameraManager] InputManager was not injected! Camera recenter button may not work.");
            }

            // Subscribe to events
            if (_eventManager != null)
            {
                SubscribeToEvents();
            }

            // Auto-discover virtual cameras if enabled
            if (autoDiscoverCameras)
            {
                DiscoverVirtualCameras();
            }

            // Validate Cinemachine setup
            if (CinemachineBrain == null)
            {
                Debug.LogWarning("[CameraManager] No CinemachineBrain found! Virtual cameras will not function. " +
                    "Add CinemachineBrain component to your main camera.");
            }
            else if (debugMode)
            {
                Debug.Log($"[CameraManager] CinemachineBrain found: {CinemachineBrain.name}");
            }

            // Initialize previous position for velocity calculation
            if (_activeFollowConfig.target != null)
            {
                _previousPlayerPosition = _activeFollowConfig.target.position;
            }
            _currentSprintFOV = perspectiveFov;
        }


        private void Update()
        {
            // Handle camera recentering and zoom reset button (using InputManager for consistency)
            if (enableRecenterButton && _activeFollowConfig.target != null && _inputManager != null)
            {
                // Convert KeyCode to new Input System key
                Key key = Key.None;
                switch (recenterKey)
                {
                    case KeyCode.V: key = Key.V; break;
                    case KeyCode.C: key = Key.C; break;
                    case KeyCode.R: key = Key.R; break;
                    case KeyCode.Space: key = Key.Space; break;
                    default: key = Key.V; break; // Default to V
                }
                
                // Use InputManager's utility method instead of directly accessing Keyboard
                if (_inputManager.IsKeyPressedThisFrame(key))
                {
                    RecenterCameraBehindPlayer();
                    ResetZoom(); // Also reset zoom when recentering
                }
            }
            
            // Smooth zoom interpolation (WoW-style)
            if (_isResettingZoom || Mathf.Abs(_currentZoomDistance - _targetZoomDistance) > 0.01f)
            {
                float zoomSpeed = _isResettingZoom ? 5f : 8f; // Faster when resetting
                _currentZoomDistance = Mathf.Lerp(_currentZoomDistance, _targetZoomDistance, Time.deltaTime * zoomSpeed);
                
                // Clamp current zoom to min/max bounds during interpolation (positive values)
                _currentZoomDistance = Mathf.Clamp(_currentZoomDistance, minZoomDistance, maxZoomDistance);
                
                // Apply WoW-style zoom during interpolation
                if (_hasActiveFollowConfig && _currentActiveCamera != null && cameraMode == CameraMode.ThirdPerson)
                {
                    ApplyWoWStyleZoom();
                }
                
                // Check if reset is complete
                if (_isResettingZoom && Mathf.Abs(_currentZoomDistance - _targetZoomDistance) < 0.05f)
                {
                    _isResettingZoom = false;
                }
            }
        }

        private void LateUpdate()
        {
            // CRITICAL: Ensure Follow target is always set (prevents drift from target being cleared)
            if (_hasActiveFollowConfig && _currentActiveCamera != null && _activeFollowConfig.target != null)
            {
                if (_currentActiveCamera.Follow != _activeFollowConfig.target)
                {
                    _currentActiveCamera.Follow = _activeFollowConfig.target;
                    if (debugMode)
                        Debug.LogWarning($"[CameraManager] Restored camera Follow target (was null or wrong)");
                }
                
                // CRITICAL FIX: Ensure camera offset is maintained correctly
                // Handle both PositionComposer and OrbitalFollow systems
                if (cameraMode == CameraMode.ThirdPerson)
                {
                    if (cameraSystem == CameraSystemType.OrbitalFollow && _orbitalFollow != null)
                    {
                        // OrbitalFollow: Update radius based on zoom (Cinemachine 3.x)
                        _orbitalFollow.Radius = _currentZoomDistance;

                        // Maintain height ratio in TargetOffset
                        float baseHeight = _baseCameraOffset.y;
                        float baseDistance = Mathf.Abs(_baseCameraOffset.z);
                        float heightRatio = baseHeight / baseDistance;
                        float zoomHeight = _currentZoomDistance * heightRatio;
                        _orbitalFollow.TargetOffset = new Vector3(0f, zoomHeight, 0f);
                    }
                    else if (_positionComposer != null)
                    {
                        // PositionComposer: Update TargetOffset
                        UpdatePositionComposerOffset();
                    }
                }
            }
            
            // Handle auto-rotation for simple third-person cameras
            UpdateSimpleThirdPersonRotation();
        }
        
        /// <summary>
        /// Updates PositionComposer TargetOffset (used in LateUpdate)
        /// </summary>
        private void UpdatePositionComposerOffset()
        {
            if (_positionComposer == null)
                return;
                
            // PositionComposer uses TargetOffset in target's LOCAL space
            // When player rotates, the offset rotates with them, which is correct for "behind player"
            var positionComposer = _positionComposer;
            if (positionComposer != null && cameraMode == CameraMode.ThirdPerson)
                {
                    // Verify offset is correct (should be 0, 4.4, -8.5 for third-person)
                    // Start with base offset but use current zoom distance (WoW-style)
                    // Calculate offset maintaining the same angle but with current zoom distance
                    float baseHeight = _baseCameraOffset.y;
                    float baseDistance = Mathf.Abs(_baseCameraOffset.z);
                    float heightRatio = baseHeight / baseDistance;
                    float zoomHeight = _currentZoomDistance * heightRatio;
                    Vector3 expectedOffset = new Vector3(_baseCameraOffset.x, zoomHeight, -_currentZoomDistance);
                    if (enableDynamicHeight)
                    {
                        // Calculate dynamic height adjustment with smoothing to prevent bouncing on slopes
                        Vector3 velocity = (_activeFollowConfig.target.position - _previousPlayerPosition) / Time.deltaTime;
                        float verticalSpeed = velocity.y;
                        
                        // Smooth the vertical speed calculation to reduce jitter on slopes
                        // Use a smaller multiplier and tighter clamping for smoother transitions
                        float heightOffset = Mathf.Clamp(verticalSpeed * dynamicHeightAmount * 0.5f, -0.3f, 0.3f);
                        
                    // Smooth interpolation to target offset to prevent sudden jumps
                    // Preserve current zoom distance (Z value) when adjusting height
                    // Calculate zoom offset maintaining the same angle
                    float dynamicBaseHeight = _baseCameraOffset.y;
                    float dynamicBaseDistance = Mathf.Abs(_baseCameraOffset.z);
                    float dynamicHeightRatio = dynamicBaseHeight / dynamicBaseDistance;
                    float dynamicZoomHeight = _currentZoomDistance * dynamicHeightRatio;
                    Vector3 dynamicBaseOffset = new Vector3(_baseCameraOffset.x, dynamicZoomHeight, -_currentZoomDistance);
                    Vector3 targetOffset = dynamicBaseOffset + new Vector3(0f, heightOffset, 0f);
                    expectedOffset = Vector3.Lerp(positionComposer.TargetOffset, targetOffset, Time.deltaTime * 5f);
                    }
                    else
                    {
                        // Preserve zoom distance even when dynamic height is disabled
                        // expectedOffset already has correct Z from line 642 (which is -_currentZoomDistance)
                    }
                    
                    // Only update if significantly different (prevents micro-adjustments)
                    // Reduced threshold for smoother updates
                    // IMPORTANT: expectedOffset already has correct zoom distance from calculation above
                    if (Vector3.Distance(positionComposer.TargetOffset, expectedOffset) > 0.05f)
                    {
                        positionComposer.TargetOffset = expectedOffset;
                    }
                    else
                    {
                        // Even if not updating, ensure zoom is preserved
                        Vector3 currentOffset = positionComposer.TargetOffset;
                        // Compare with negative zoom distance (Z should be negative for behind player)
                        float expectedZ = -_currentZoomDistance;
                        if (Mathf.Abs(currentOffset.z - expectedZ) > 0.01f)
                        {
                            currentOffset.z = expectedZ;
                            positionComposer.TargetOffset = currentOffset;
                        }
                    }
                }

            // Handle auto-rotation for simple third-person cameras
            UpdateSimpleThirdPersonRotation();
        }

        private void OnDestroy()
        {
            if (debugMode)
                Debug.Log("[CameraManager] OnDestroy called");

            UnsubscribeFromEvents();
            ClearCameraCache();
        }

        private void SubscribeToEvents()
        {
            // Subscribe to camera control events from EventManager
            if (_eventManager != null)
            {
                _eventManager.OnCameraFollowRequested.AddListener(HandleCameraFollowRequest);
                _eventManager.OnCameraStopFollowRequested.AddListener(HandleCameraStopFollowRequest);
                _eventManager.OnCameraLookInput.AddListener(OnCameraLookInput);
                _eventManager.OnCameraZoomInput.AddListener(OnCameraZoomInput);

                if (debugMode)
                {
                    Debug.Log("[CameraManager] Subscribed to EventManager camera events");
                    Debug.Log($"[CameraManager] OnCameraZoomInput listener count: {_eventManager.OnCameraZoomInput.GetPersistentEventCount()}");
                }
            }
            else
            {
                Debug.LogWarning("[CameraManager] EventManager is null, cannot subscribe to camera events");
            }
        }

        private void UnsubscribeFromEvents()
        {
            // Unsubscribe from camera control events from EventManager
            if (_eventManager != null)
            {
                _eventManager.OnCameraFollowRequested.RemoveListener(HandleCameraFollowRequest);
                _eventManager.OnCameraStopFollowRequested.RemoveListener(HandleCameraStopFollowRequest);
                _eventManager.OnCameraLookInput.RemoveListener(OnCameraLookInput);
                _eventManager.OnCameraZoomInput.RemoveListener(OnCameraZoomInput);

                if (debugMode)
                    Debug.Log("[CameraManager] Unsubscribed from EventManager camera events");
            }
        }

        /// <summary>
        /// Handles camera follow request from event system
        /// </summary>
        private void HandleCameraFollowRequest(CameraFollowConfig config)
        {
            if (config.target == null)
            {
                Debug.LogWarning("[CameraManager] Cannot setup camera follow - target is null");
                return;
            }

            // Override config based on Inspector camera mode setting
            if (cameraMode == CameraMode.FirstPerson)
            {
                // Apply first-person settings
                config.shoulderOffset = new Vector3(0f, 1.6f, 0f); // Eye level
                config.cameraDistance = 0.1f; // Very close
                config.positionDamping = new Vector3(0.05f, 0.05f, 0.05f); // Responsive
                config.pitchClamp = new Vector2(-89f, 89f); // Full vertical rotation
            }
            else // ThirdPerson
            {
                // Apply third-person settings (offset: 0, 4.4, -8.5)
                config.shoulderOffset = new Vector3(0f, 4.4f, 0f); // 4.4 units above (~10% higher)
                config.cameraDistance = 8f; // 8 units behind (not used, TargetOffset matters)
                // Y-axis damping increased for smoother vertical following on slopes
                config.positionDamping = new Vector3(0.1f, 0.2f, 0.1f); // Smoother Y-axis for slopes
                config.pitchClamp = new Vector2(15f, 50f); // Moderate downward angle
            }

            // Get the target camera (or use first available)
            string cameraName = string.IsNullOrEmpty(config.cameraName) ?
                (_allVirtualCameras.Count > 0 ? _allVirtualCameras[0].name : null) :
                config.cameraName;

            if (string.IsNullOrEmpty(cameraName))
            {
                Debug.LogWarning("[CameraManager] No cameras available for follow setup");
                return;
            }

            CinemachineCamera virtualCamera = GetVirtualCamera(cameraName);

            if (virtualCamera == null && _allVirtualCameras.Count > 0)
            {
                // Fallback to first available camera
                virtualCamera = _allVirtualCameras[0];
                Debug.LogWarning($"[CameraManager] Camera '{cameraName}' not found. Using '{virtualCamera.name}' instead.");
            }

            if (virtualCamera != null)
            {
                // CRITICAL: Set Follow target FIRST - PositionComposer requires this!
                // Follow must be set before configuring PositionComposer
                virtualCamera.Follow = config.target;
                virtualCamera.LookAt = null; // PanTilt handles rotation, not LookAt

                // Debug: Log camera and target positions BEFORE repositioning
                Debug.Log($"[CameraManager] BEFORE Reposition - Camera Position: {virtualCamera.transform.position} | Target Position: {config.target.position}");
                Debug.Log($"[CameraManager] Camera Rotation: {virtualCamera.transform.rotation.eulerAngles}");

                // Ensure virtual camera starts above the player, not below
                if (config.target != null && virtualCamera.transform.position.y < config.target.position.y)
                {
                    Vector3 startPos = config.target.position + Vector3.up * 5f + Vector3.back * 5f;
                    virtualCamera.transform.position = startPos;
                    Debug.Log($"[CameraManager] Repositioned virtual camera above player: {startPos}");
                }

                // Debug: Log camera position AFTER repositioning
                Debug.Log($"[CameraManager] AFTER Reposition - Camera Position: {virtualCamera.transform.position}");

                // List all components on the virtual camera
                var components = virtualCamera.GetComponents<UnityEngine.Component>();
                string componentList = string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name));
                Debug.Log($"[CameraManager] Virtual Camera Components: {componentList}");

                // Configure camera components based on character type
                ConfigureThirdPersonFollow(virtualCamera, config);

                // Activate this camera
                ActivateCamera(virtualCamera, true);

                config.cameraName = virtualCamera.name;
                _activeFollowConfig = config;
                _hasActiveFollowConfig = true;

                // Initialize previous position for velocity calculation
                _previousPlayerPosition = config.target.position;
                _currentSprintFOV = perspectiveFov;

                if (debugMode)
                    Debug.Log($"[CameraManager] Camera '{virtualCamera.name}' now following '{config.target.name}' (Mode: SimpleThirdPerson)");
            }
            else
            {
                Debug.LogWarning("[CameraManager] No virtual cameras found in scene");
            }
        }

        /// <summary>
        /// Handles camera stop follow request
        /// </summary>
        private void HandleCameraStopFollowRequest(string cameraName)
        {
            CinemachineCamera camera = string.IsNullOrEmpty(cameraName) ?
                _currentActiveCamera :
                GetVirtualCamera(cameraName);

            if (camera != null)
            {
                camera.Follow = null;
                camera.LookAt = null;
                _hasActiveFollowConfig = false;

                if (debugMode)
                    Debug.Log($"[CameraManager] Camera '{camera.name}' stopped following");
            }
        }

        /// <summary>
        /// Configures simple third-person camera follow
        /// Uses Framing Transposer (PositionComposer) for smooth following
        /// </summary>
        private void ConfigureThirdPersonFollow(CinemachineCamera camera, CameraFollowConfig config)
        {
            // Only support SimpleThirdPerson now
            ConfigureSimpleThirdPerson(camera, config);
        }

        /// <summary>
        /// Configures camera based on mode: First Person or Third Person
        /// Supports both immersive first-person view and third-person behind-player view
        /// Can use either PositionComposer or OrbitalFollow based on cameraSystem setting
        /// </summary>
        private void ConfigureSimpleThirdPerson(CinemachineCamera camera, CameraFollowConfig config)
        {
            if (camera == null || config.target == null)
                return;

            // Check camera mode from Inspector
            bool isFirstPerson = cameraMode == CameraMode.FirstPerson;
            string modeName = isFirstPerson ? "FIRST-PERSON" : "THIRD-PERSON";
            
            Debug.Log($"[CameraManager] ===== CONFIGURING {modeName} CAMERA =====");
            Debug.Log($"[CameraManager] Camera: {camera.name} | Target: {config.target.name} | Mode: {cameraMode}");

            // Ensure main camera is perspective (not orthographic)
            if (MainCamera != null && MainCamera.orthographic)
            {
                MainCamera.orthographic = false;
                MainCamera.fieldOfView = perspectiveFov;
                Debug.Log($"[CameraManager] Set Main Camera to perspective (FOV: {perspectiveFov}°)");
            }

            // === BODY COMPONENT: Choose between PositionComposer or OrbitalFollow ===
            // Remove old components first
            var oldThirdPersonFollow = camera.GetComponent<CinemachineThirdPersonFollow>();
            if (oldThirdPersonFollow != null)
            {
                Destroy(oldThirdPersonFollow);
                Debug.Log($"[CameraManager] Removed ThirdPersonFollow from '{camera.name}'");
            }
            
            var oldOrbitalFollow = camera.GetComponent<CinemachineOrbitalFollow>();
            var oldPositionComposer = camera.GetComponent<CinemachinePositionComposer>();
            
            // Configure based on selected camera system
            if (cameraSystem == CameraSystemType.OrbitalFollow && !isFirstPerson)
            {
                // Use OrbitalFollow for third-person
                ConfigureOrbitalFollow(camera, config);
            }
            else
            {
                // Use PositionComposer (default or first-person)
                if (oldOrbitalFollow != null)
                {
                    Destroy(oldOrbitalFollow);
                    Debug.Log($"[CameraManager] Removed OrbitalFollow from '{camera.name}'");
                }
                
                ConfigurePositionComposer(camera, config, isFirstPerson);
            }
        }
        
        /// <summary>
        /// Configures PositionComposer (Framing Transposer) system
        /// </summary>
        private void ConfigurePositionComposer(CinemachineCamera camera, CameraFollowConfig config, bool isFirstPerson)
        {
            var positionComposer = camera.GetComponent<CinemachinePositionComposer>();
            if (positionComposer == null)
            {
                positionComposer = camera.gameObject.AddComponent<CinemachinePositionComposer>();
                Debug.Log($"[CameraManager] Added PositionComposer (Framing Transposer) to '{camera.name}'");
            }

            // CRITICAL FIX: PositionComposer's TargetOffset with NEGATIVE Z positions camera BEHIND player
            // CameraDistance is for forward/backward from the composed position, not the offset!
            // TargetOffset is in target's local space: X=side, Y=height, Z=forward/back (negative = behind)
            Vector3 cameraOffset;
            if (isFirstPerson)
            {
                // First-person: camera at eye level, very close
                cameraOffset = new Vector3(0f, 1.6f, 0.1f); // (X, Y, Z) = (side, height, forward/back)
            }
            else
            {
                // Third-person: camera behind and above player
                // NEGATIVE Z = behind player (important!)
                // 8.5 units BEHIND (reduced to see more ahead than behind)
                // 4.4 units UP (higher = more ahead visible, less behind visible, ~10% increase)
                cameraOffset = new Vector3(0f, 4.4f, -8.5f); // 8.5 units BEHIND, 4.4 units UP
            }
            
            // Store base offset for dynamic height adjustments
            _baseCameraOffset = cameraOffset;
            
            // Initialize zoom distance from camera offset (WoW-style: positive distance)
            if (!isFirstPerson)
            {
                // Convert negative Z offset to positive distance
                _currentZoomDistance = Mathf.Abs(cameraOffset.z);
                _targetZoomDistance = Mathf.Abs(cameraOffset.z);
                defaultZoomDistance = Mathf.Abs(cameraOffset.z); // Store as default
                _zoomDirection = new Vector3(0f, cameraOffset.y, cameraOffset.z).normalized;
            }
            
            // Apply camera lag if enabled (enhances cinematic feel)
            // But keep damping reasonable - too much causes drift
            Vector3 damping = config.positionDamping;
            if (enableCameraLag && !isFirstPerson)
            {
                float lagMultiplier = 1f + (cameraLagAmount * 2f); // Reduced from 5f to 2f to prevent excessive lag
                damping = damping * lagMultiplier;
                // Preserve Y-axis damping (don't apply lag multiplier to Y for smoother slopes)
                // Y-axis already has higher damping (0.2) for smooth vertical following
                damping.y = config.positionDamping.y; // Keep original Y damping for smooth vertical following
                // Clamp damping to prevent camera from drifting too far
                damping = new Vector3(
                    Mathf.Clamp(damping.x, 0.05f, 0.5f),
                    Mathf.Clamp(damping.y, 0.05f, 0.5f),
                    Mathf.Clamp(damping.z, 0.05f, 0.5f)
                );
            }
            
            positionComposer.Damping = damping;
            positionComposer.CameraDistance = 0f; // Keep at 0 - we use TargetOffset for positioning
            positionComposer.TargetOffset = cameraOffset; // This positions the camera!
            
            // Configure composition (centered framing)
            var composition = ScreenComposerSettings.Default;
            composition.ScreenPosition = new Vector2(0.5f, 0.5f);
            positionComposer.Composition = composition;

            // === AIM COMPONENT: PanTilt with fixed tilt angle (downward view) ===
            ConfigurePanTilt(camera, isFirstPerson);
            
            // === COLLISION DETECTION: Deoccluder ===
            ConfigureDeoccluder(camera, isFirstPerson);

            // === SET TARGETS: Follow and LookAt the player directly ===
            // CRITICAL: Follow must ALWAYS be set to target for PositionComposer to work correctly
            // PositionComposer uses Follow target's position as the base for TargetOffset
            // If Follow is null or wrong, camera will drift!
            // Force set Follow every frame to ensure it never gets cleared
            camera.Follow = config.target; // Follow player position (includes height) - MUST be set!
            
            // PanTilt handles rotation, so we don't need LookAt for third-person
            // LookAt would conflict with PanTilt's rotation control
            camera.LookAt = null; // Let PanTilt handle all rotation
            
            // Verify PositionComposer is still configured correctly (defensive check)
            var verifyPositionComposer = camera.GetComponent<CinemachinePositionComposer>();
            if (verifyPositionComposer != null && verifyPositionComposer.TargetOffset != cameraOffset)
            {
                // Restore correct offset if it was changed
                verifyPositionComposer.TargetOffset = cameraOffset;
                if (debugMode)
                    Debug.LogWarning($"[CameraManager] Restored PositionComposer TargetOffset to: {cameraOffset}");
            }

            // Store reference for auto-rotation
            _activeFollowConfig = config;
            _hasActiveFollowConfig = true;

            if (debugMode)
            {
                string modeName = isFirstPerson ? "FIRST-PERSON" : "THIRD-PERSON";
                float tiltAngle = isFirstPerson ? 0f : defaultTiltAngle;
                Debug.Log($"[CameraManager] Configured {modeName} Camera (PositionComposer):");
                Debug.Log($"  - Mode: {cameraMode}");
                Debug.Log($"  - FOV: {perspectiveFov}°");
                Debug.Log($"  - Camera Offset: {cameraOffset} (X=side, Y=height, Z=forward/back)");
                Debug.Log($"  - Pitch Angle: {tiltAngle}°");
                Debug.Log($"  - Damping: {damping}");
                Debug.Log($"  - Following: {config.target.name}");
                if (!isFirstPerson)
                    Debug.Log($"  - Auto-rotation: Enabled");
            }
        }
        
        /// <summary>
        /// Configures OrbitalFollow system (alternative to PositionComposer)
        /// Uses similar settings but with built-in distance/zoom control
        /// </summary>
        private void ConfigureOrbitalFollow(CinemachineCamera camera, CameraFollowConfig config)
        {
            var orbitalFollow = camera.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow == null)
            {
                orbitalFollow = camera.gameObject.AddComponent<CinemachineOrbitalFollow>();
                Debug.Log($"[CameraManager] Added OrbitalFollow to '{camera.name}'");
            }
            
            // Calculate base offset (similar to PositionComposer)
            Vector3 cameraOffset = new Vector3(0f, 4.4f, -8.5f); // Same as PositionComposer default
            _baseCameraOffset = cameraOffset;
            
            // Calculate radius from base offset (similar to PositionComposer distance)
            float baseDistance = Mathf.Abs(cameraOffset.z);
            float baseHeight = cameraOffset.y;
            
            // Initialize zoom distance from base offset
            _currentZoomDistance = baseDistance;
            _targetZoomDistance = baseDistance;
            defaultZoomDistance = baseDistance;
            
            // Apply camera lag if enabled (similar to PositionComposer)
            Vector3 damping = config.positionDamping;
            if (enableCameraLag)
            {
                float lagMultiplier = 1f + (cameraLagAmount * 2f);
                damping = damping * lagMultiplier;
                damping.y = config.positionDamping.y; // Preserve Y damping
                damping = new Vector3(
                    Mathf.Clamp(damping.x, 0.05f, 0.5f),
                    Mathf.Clamp(damping.y, 0.05f, 0.5f),
                    Mathf.Clamp(damping.z, 0.05f, 0.5f)
                );
            }
            
            // Configure OrbitalFollow with Cinemachine 3.x API
            orbitalFollow.Radius = _currentZoomDistance; // Distance from target (zoom control)
            orbitalFollow.TargetOffset = new Vector3(0f, baseHeight, 0f); // Height offset in target-local space
            orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere; // Simple sphere mode

            // Configure TrackerSettings for damping (Cinemachine 3.x)
            var trackerSettings = orbitalFollow.TrackerSettings;
            trackerSettings.PositionDamping = damping; // Use TrackerSettings for damping
            trackerSettings.RotationDamping = Vector3.zero; // No rotation damping (PanTilt handles rotation)
            orbitalFollow.TrackerSettings = trackerSettings;

            // Configure horizontal axis - locked to behind player (0 degrees)
            var horizontalAxis = orbitalFollow.HorizontalAxis;
            horizontalAxis.Value = 0f; // Start at 0 (behind player)
            horizontalAxis.Center = 0f;
            horizontalAxis.Range = new Vector2(-180f, 180f); // Full range
            horizontalAxis.Wrap = true; // Wrap at 360 degrees
            horizontalAxis.Recentering = new InputAxis.RecenteringSettings { Enabled = false }; // No auto-recentering
            orbitalFollow.HorizontalAxis = horizontalAxis;

            // Configure vertical axis - fixed at default tilt angle
            var verticalAxis = orbitalFollow.VerticalAxis;
            verticalAxis.Value = defaultTiltAngle; // Use Inspector tilt angle
            verticalAxis.Center = defaultTiltAngle;
            verticalAxis.Range = new Vector2(defaultTiltAngle, defaultTiltAngle); // Fixed angle (no vertical rotation)
            verticalAxis.Wrap = false;
            verticalAxis.Recentering = new InputAxis.RecenteringSettings { Enabled = false };
            orbitalFollow.VerticalAxis = verticalAxis;

            // Configure radial axis - controls zoom distance scale
            var radialAxis = orbitalFollow.RadialAxis;
            radialAxis.Value = 1f; // Default scale (1.0 = no scaling)
            radialAxis.Center = 1f;
            radialAxis.Range = new Vector2(0.5f, 2f); // Allow 50% to 200% scale
            radialAxis.Wrap = false;
            radialAxis.Recentering = new InputAxis.RecenteringSettings { Enabled = false };
            orbitalFollow.RadialAxis = radialAxis;
            
            // Store reference for zoom
            _orbitalFollow = orbitalFollow;
            
            // Configure PanTilt for auto-rotation (OrbitalFollow handles position)
            ConfigurePanTilt(camera, false);
            
            // Configure collision detection
            ConfigureDeoccluder(camera, false);
            
            // Set targets
            camera.Follow = config.target;
            camera.LookAt = null;
            
            _activeFollowConfig = config;
            _hasActiveFollowConfig = true;
            
            if (debugMode)
            {
                Debug.Log($"[CameraManager] Configured THIRD-PERSON Camera (OrbitalFollow - Cinemachine 3.x):");
                Debug.Log($"  - Radius: {orbitalFollow.Radius} (zoom distance)");
                Debug.Log($"  - Target Offset: {orbitalFollow.TargetOffset}");
                Debug.Log($"  - Position Damping: {orbitalFollow.TrackerSettings.PositionDamping}");
                Debug.Log($"  - Orbit Style: {orbitalFollow.OrbitStyle}");
                Debug.Log($"  - Following: {config.target.name}");
            }
        }
        
        /// <summary>
        /// Configures PanTilt component for camera rotation
        /// </summary>
        private void ConfigurePanTilt(CinemachineCamera camera, bool isFirstPerson)
        {
            // Remove RotationComposer if it exists (we need PanTilt for fixed angle)
            var oldRotationComposer = camera.GetComponent<CinemachineRotationComposer>();
            if (oldRotationComposer != null)
            {
                Destroy(oldRotationComposer);
                Debug.Log($"[CameraManager] Removed RotationComposer from '{camera.name}'");
            }

            // Get or add PanTilt component (DO NOT remove it first!)
            var panTilt = camera.GetComponent<CinemachinePanTilt>();
            if (panTilt == null)
            {
                panTilt = camera.gameObject.AddComponent<CinemachinePanTilt>();
                Debug.Log($"[CameraManager] Added PanTilt to '{camera.name}' for fixed downward angle");
            }

            // Configure PanTilt for fixed downward angle (no mouse input, auto-rotates with player)
            panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.ParentObject;
            panTilt.RecenterTarget = CinemachinePanTilt.RecenterTargetModes.AxisCenter;

            // Pan axis: auto-rotates to stay behind player (no manual input)
            var panAxis = panTilt.PanAxis;
            panAxis.Range = new Vector2(-180f, 180f);
            panAxis.Wrap = true;
            panAxis.Recentering = new InputAxis.RecenteringSettings { Enabled = false };
            panAxis.Center = 0f; // Start at 0 (camera looks same direction as player)
            panAxis.Value = 0f; // Initial value: 0 degrees
            panTilt.PanAxis = panAxis;

            // Tilt axis: angle depends on camera mode
            float fixedTiltAngle;
            if (isFirstPerson)
            {
                fixedTiltAngle = 0f;
            }
            else
            {
                fixedTiltAngle = defaultTiltAngle; // Use Inspector value
            }
            
            // IMPORTANT: Set tilt angle correctly
            var tiltAxis = panTilt.TiltAxis;
            tiltAxis.Range = new Vector2(fixedTiltAngle, fixedTiltAngle);
            tiltAxis.Wrap = false;
            tiltAxis.Recentering = new InputAxis.RecenteringSettings { Enabled = false };
            tiltAxis.Center = fixedTiltAngle;
            tiltAxis.Value = fixedTiltAngle;
            panTilt.TiltAxis = tiltAxis;
            
            // Store reference
            _activePanTilt = panTilt;
        }
        
        /// <summary>
        /// Configures Deoccluder for collision detection (shared by both systems)
        /// </summary>
        private void ConfigureDeoccluder(CinemachineCamera camera, bool isFirstPerson)
        {
            var deoccluder = camera.GetComponent<CinemachineDeoccluder>();
            if (deoccluder == null)
            {
                deoccluder = camera.gameObject.AddComponent<CinemachineDeoccluder>();
                Debug.Log($"[CameraManager] Added Deoccluder for collision detection to '{camera.name}'");
            }

            deoccluder.CollideAgainst = LayerMask.GetMask("Default", "Environment", "Terrain");
            deoccluder.MinimumDistanceFromTarget = 0.8f;
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.DistanceLimit = isFirstPerson ? 1f : 6f;
            deoccluder.AvoidObstacles.CameraRadius = 0.2f;
        }

        /// <summary>
        /// Enhanced third-person camera rotation with movement-based auto-rotation, dynamic height, and sprint FOV
        /// Rotates pan axis to follow player's movement direction or facing direction
        /// </summary>
        private void UpdateSimpleThirdPersonRotation()
        {
            if (!_hasActiveFollowConfig || _currentActiveCamera == null || _activeFollowConfig.target == null)
                return;

            if (cameraMode == CameraMode.FirstPerson)
                return;

            var config = _activeFollowConfig;
            bool isSimpleThirdPerson = config.lookaheadTime == 0f && 
                                       config.shoulderOffset.x == 0f && 
                                       config.positionDamping.magnitude > 0.01f && 
                                       config.positionDamping.magnitude < 0.5f;

            if (!isSimpleThirdPerson)
                return;

            var panTilt = _currentActiveCamera.GetComponent<CinemachinePanTilt>();
            if (panTilt == null)
                return;

            Transform player = config.target;

            // === CALCULATE PLAYER VELOCITY ===
            Vector3 currentPosition = player.position;
            Vector3 velocity = (currentPosition - _previousPlayerPosition) / Time.deltaTime;
            _previousPlayerPosition = currentPosition;

            float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            float verticalSpeed = velocity.y;

            // === AUTO-ROTATE TO FOLLOW PLAYER ===
            // Always rotate camera to match player's FACING direction (not movement direction)
            // With standard WASD controls: A/D rotates character, W/S moves forward/backward
            // Camera should ONLY follow facing direction, never movement direction
            Vector3 playerForward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
            float targetYaw = 0f;
            
            // Sync camera rotation speed with player's rotation speed to prevent lag
            // Get player's rotation speed if available, otherwise use default
            float rotateSpeed = 180f; // Default rotation speed
            BugWars.Entity.Player.Player playerComponent = player.GetComponent<BugWars.Entity.Player.Player>();
            if (playerComponent != null && playerComponent.IsUsingStandardWASD())
            {
                // Match player's rotation speed so camera keeps up with character rotation
                rotateSpeed = playerComponent.GetRotationSpeed();
                // Add a small buffer (10%) to ensure camera stays ahead/keeps up
                rotateSpeed *= 1.1f;
            }

            if (playerForward.sqrMagnitude > 0.0001f)
            {
                playerForward.Normalize();
                // ALWAYS use player's facing direction (transform.forward)
                // Never use movement direction - this prevents camera from rotating when walking backwards
                targetYaw = Mathf.Atan2(playerForward.x, playerForward.z) * Mathf.Rad2Deg;

                // Disable auto-rotate to movement for standard WASD controls
                // With standard WASD, player rotates with A/D, so camera should only follow facing direction
                // Movement direction (W/S) should never affect camera rotation

                // Apply rotation to match player's facing direction
                var panAxis = panTilt.PanAxis;
                float currentPan = panAxis.Value;
                float newPan = Mathf.MoveTowardsAngle(currentPan, targetYaw, rotateSpeed * Time.deltaTime);
                panAxis.Value = panAxis.ClampValue(newPan);
                panAxis.Center = panAxis.Value;
                panTilt.PanAxis = panAxis;
            }

            // === DYNAMIC HEIGHT ADJUSTMENT ===
            // NOTE: Dynamic height is now handled in LateUpdate() to prevent conflicts
            // This ensures PositionComposer TargetOffset is only modified in one place

            // === CAMERA LEAN ON TURNS ===
            if (enableCameraLean)
            {
                // Calculate angular velocity (how fast player is turning)
                Vector3 playerForwardForLean = Vector3.ProjectOnPlane(player.forward, Vector3.up);
                if (playerForwardForLean.sqrMagnitude > 0.0001f)
                {
                    float turnSpeed = Vector3.SignedAngle(Vector3.forward, playerForwardForLean, Vector3.up);
                    float targetLean = Mathf.Clamp(turnSpeed * 0.1f, -maxLeanAngle, maxLeanAngle);

                    // Smooth lean transition
                    _currentLeanAngle = Mathf.Lerp(_currentLeanAngle, targetLean, Time.deltaTime * 5f);

                    // Note: Proper lean implementation would require Roll axis support in PanTilt
                    // This is a placeholder for future enhancement
                }
            }

            // === SPRINT FOV ADJUSTMENT ===
            if (enableSprintFOV && MainCamera != null)
            {
                float targetFOV = perspectiveFov;

                if (horizontalSpeed > sprintSpeedThreshold)
                {
                    // Player is sprinting - increase FOV
                    float sprintFactor = Mathf.Clamp01((horizontalSpeed - sprintSpeedThreshold) / 3f);
                    targetFOV += sprintFOVIncrease * sprintFactor;
                }

                // Smooth FOV transition
                _currentSprintFOV = Mathf.Lerp(_currentSprintFOV, targetFOV, Time.deltaTime * 5f);
                MainCamera.fieldOfView = _currentSprintFOV;
            }
        }

        /// <summary>
        /// Discovers all Cinemachine Virtual Cameras in the scene and caches them
        /// </summary>
        public void DiscoverVirtualCameras()
        {
            ClearCameraCache();

            // Find all CinemachineCamera components in the scene
            CinemachineCamera[] foundCameras = includeInactiveCameras
                ? FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                : FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var vcam in foundCameras)
            {
                // Add to list
                _allVirtualCameras.Add(vcam);

                // Add to dictionary by name
                string cameraName = vcam.gameObject.name;
                if (!_virtualCamerasByName.ContainsKey(cameraName))
                {
                    _virtualCamerasByName.Add(cameraName, vcam);
                }
                else
                {
                    Debug.LogWarning($"[CameraManager] Duplicate camera name found: '{cameraName}'. " +
                        "Consider using unique names for virtual cameras.");
                }

                // Track the currently active camera (highest priority)
                if (_currentActiveCamera == null || vcam.Priority > _currentActiveCamera.Priority)
                {
                    _currentActiveCamera = vcam;
                }

                if (debugMode)
                {
                    Debug.Log($"[CameraManager] Discovered virtual camera: '{cameraName}' " +
                        $"(Priority: {vcam.Priority}, Active: {vcam.gameObject.activeInHierarchy})");
                }
            }

            if (debugMode)
            {
                Debug.Log($"[CameraManager] Discovery complete. Found {_allVirtualCameras.Count} virtual camera(s).");
                if (_currentActiveCamera != null)
                {
                    Debug.Log($"[CameraManager] Current active camera: '{_currentActiveCamera.name}'");
                }
            }
        }

        /// <summary>
        /// Clears the camera cache
        /// </summary>
        public void ClearCameraCache()
        {
            _virtualCamerasByName.Clear();
            _allVirtualCameras.Clear();
            _currentActiveCamera = null;
            _activePanTilt = null;
            _panTiltCache.Clear();

            if (debugMode)
                Debug.Log("[CameraManager] Camera cache cleared");
        }

        /// <summary>
        /// Refreshes the camera cache (clears and re-discovers)
        /// </summary>
        public void RefreshCameraCache()
        {
            if (debugMode)
                Debug.Log("[CameraManager] Refreshing camera cache...");

            DiscoverVirtualCameras();
        }

        /// <summary>
        /// Gets a virtual camera by name
        /// </summary>
        /// <param name="cameraName">The name of the virtual camera GameObject</param>
        /// <returns>The virtual camera if found, null otherwise</returns>
        public CinemachineCamera GetVirtualCamera(string cameraName)
        {
            if (_virtualCamerasByName.TryGetValue(cameraName, out var camera))
            {
                return camera;
            }

            Debug.LogWarning($"[CameraManager] Virtual camera '{cameraName}' not found in cache. " +
                "Consider calling RefreshCameraCache() if cameras were added at runtime.");
            return null;
        }

        /// <summary>
        /// Gets a virtual camera by index
        /// </summary>
        /// <param name="index">The index in the camera list</param>
        /// <returns>The virtual camera if index is valid, null otherwise</returns>
        public CinemachineCamera GetVirtualCameraByIndex(int index)
        {
            if (index >= 0 && index < _allVirtualCameras.Count)
            {
                return _allVirtualCameras[index];
            }

            Debug.LogWarning($"[CameraManager] Index {index} out of range. Total cameras: {_allVirtualCameras.Count}");
            return null;
        }

        /// <summary>
        /// Checks if a virtual camera with the given name exists
        /// </summary>
        public bool HasVirtualCamera(string cameraName)
        {
            return _virtualCamerasByName.ContainsKey(cameraName);
        }

        /// <summary>
        /// Gets all camera names
        /// </summary>
        public string[] GetAllCameraNames()
        {
            return _virtualCamerasByName.Keys.ToArray();
        }

        /// <summary>
        /// Activates a virtual camera by name (sets its priority high)
        /// </summary>
        /// <param name="cameraName">The name of the camera to activate</param>
        /// <param name="resetOthers">If true, sets all other cameras to default priority</param>
        public void ActivateCamera(string cameraName, bool resetOthers = true)
        {
            var camera = GetVirtualCamera(cameraName);
            if (camera == null)
            {
                Debug.LogError($"[CameraManager] Cannot activate camera '{cameraName}' - not found!");
                return;
            }

            ActivateCamera(camera, resetOthers);
        }

        /// <summary>
        /// Activates a virtual camera (sets its priority high)
        /// </summary>
        /// <param name="camera">The camera to activate</param>
        /// <param name="resetOthers">If true, sets all other cameras to default priority</param>
        public void ActivateCamera(CinemachineCamera camera, bool resetOthers = true)
        {
            if (camera == null)
            {
                Debug.LogError("[CameraManager] Cannot activate null camera!");
                return;
            }

            // Reset all other cameras if requested
            if (resetOthers)
            {
                foreach (var vcam in _allVirtualCameras)
                {
                    if (vcam != camera)
                    {
                        vcam.Priority = _defaultPriority;
                    }
                }
            }

            // Set the target camera to active priority
            camera.Priority = _activePriority;
            _currentActiveCamera = camera;
            if (!_panTiltCache.TryGetValue(camera.name, out _activePanTilt) || _activePanTilt == null)
            {
                _activePanTilt = camera.GetComponent<CinemachinePanTilt>();
                if (_activePanTilt != null)
                {
                    _panTiltCache[camera.name] = _activePanTilt;
                }
            }

            if (debugMode)
            {
                Debug.Log($"[CameraManager] Activated camera: '{camera.name}' (Priority: {_activePriority})");
            }

            // Trigger camera changed event if EventManager has it
            // Example: _eventManager?.OnCameraChanged?.Invoke(camera);
        }

        /// <summary>
        /// Sets the priority of a specific camera
        /// </summary>
        public void SetCameraPriority(string cameraName, int priority)
        {
            var camera = GetVirtualCamera(cameraName);
            if (camera != null)
            {
                camera.Priority = priority;

                if (debugMode)
                    Debug.Log($"[CameraManager] Set camera '{cameraName}' priority to {priority}");
            }
        }

        /// <summary>
        /// Sets the follow target for a virtual camera
        /// </summary>
        public void SetCameraFollowTarget(string cameraName, Transform target)
        {
            var camera = GetVirtualCamera(cameraName);
            if (camera != null)
            {
                camera.Follow = target;

                if (debugMode)
                    Debug.Log($"[CameraManager] Set camera '{cameraName}' follow target to '{(target != null ? target.name : "null")}'");
            }
        }

        /// <summary>
        /// Sets the look-at target for a virtual camera
        /// </summary>
        public void SetCameraLookAtTarget(string cameraName, Transform target)
        {
            var camera = GetVirtualCamera(cameraName);
            if (camera != null)
            {
                camera.LookAt = target;

                if (debugMode)
                    Debug.Log($"[CameraManager] Set camera '{cameraName}' look-at target to '{(target != null ? target.name : "null")}'");
            }
        }

        /// <summary>
        /// Enables or disables a virtual camera
        /// </summary>
        public void SetCameraEnabled(string cameraName, bool enabled)
        {
            var camera = GetVirtualCamera(cameraName);
            if (camera != null)
            {
                camera.gameObject.SetActive(enabled);

                if (debugMode)
                    Debug.Log($"[CameraManager] Set camera '{cameraName}' enabled: {enabled}");
            }
        }

        /// <summary>
        /// Sets the default blend style for camera transitions
        /// </summary>
        public void SetDefaultBlend(CinemachineBlendDefinition.Styles style, float time)
        {
            if (CinemachineBrain != null)
            {
                CinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(style, time);

                if (debugMode)
                    Debug.Log($"[CameraManager] Set default blend: {style}, time: {time}s");
            }
        }


        /// <summary>
        /// Triggers a camera shake impulse
        /// Perfect for hits, explosions, dashes, landings, etc.
        /// </summary>
        /// <param name="source">Position where the impulse originates</param>
        /// <param name="velocity">Direction and strength of the shake (magnitude determines intensity)</param>
        /// <param name="useCustomSource">If true, requires a CinemachineImpulseSource in the scene</param>
        public void TriggerCameraShake(Vector3 source, Vector3 velocity, bool useCustomSource = false)
        {
            if (_currentActiveCamera == null)
            {
                Debug.LogWarning("[CameraManager] Cannot trigger camera shake - no active camera");
                return;
            }

            // Check if camera has impulse listener
            var impulseListener = _currentActiveCamera.GetComponent<CinemachineImpulseListener>();
            if (impulseListener == null)
            {
                Debug.LogWarning("[CameraManager] Camera does not have ImpulseListener component for shake effects");
                return;
            }

            if (!useCustomSource)
            {
                // Note: Cinemachine 3 impulse system requires a CinemachineImpulseSource component
                // For now, we'll log a warning that impulse sources need to be set up in the scene
                Debug.LogWarning("[CameraManager] Camera shake requires CinemachineImpulseSource components in the scene. " +
                    "Add CinemachineImpulseSource to objects that should trigger camera shake and call GenerateImpulse() on them.");
            }

            if (debugMode)
                Debug.Log($"[CameraManager] Triggered camera shake at {source} with velocity {velocity}");
        }

        /// <summary>
        /// Triggers a camera shake with predefined intensity presets
        /// </summary>
        /// <param name="source">Position where the shake originates</param>
        /// <param name="intensity">Shake intensity: Light (0.5), Medium (1.0), Heavy (2.0), Massive (4.0)</param>
        public void TriggerCameraShake(Vector3 source, CameraShakeIntensity intensity)
        {
            Vector3 velocity = intensity switch
            {
                CameraShakeIntensity.Light => new Vector3(0.5f, 0.5f, 0.5f),
                CameraShakeIntensity.Medium => new Vector3(1.0f, 1.0f, 1.0f),
                CameraShakeIntensity.Heavy => new Vector3(2.0f, 2.0f, 2.0f),
                CameraShakeIntensity.Massive => new Vector3(4.0f, 4.0f, 4.0f),
                _ => Vector3.one
            };

            TriggerCameraShake(source, velocity, false);
        }

        /// <summary>
        /// Camera shake intensity presets
        /// </summary>
        public enum CameraShakeIntensity
        {
            Light,   // Subtle shake (footsteps, light hits)
            Medium,  // Noticeable shake (normal hits, dashes)
            Heavy,   // Strong shake (heavy hits, explosions)
            Massive  // Extreme shake (massive explosions, boss attacks)
        }

        public enum CameraMode
        {
            FirstPerson,    // Camera at player's eye level (immersive view)
            ThirdPerson     // Camera behind player (shows character)
        }

        /// <summary>
        /// Triggers camera shake when player lands (called from player controller)
        /// </summary>
        /// <param name="impactVelocity">How fast the player was falling when they landed</param>
        public void OnPlayerLanded(float impactVelocity)
        {
            if (impactVelocity > 5f && _activeFollowConfig.target != null)
            {
                float intensity = Mathf.Clamp(impactVelocity / 10f, 0.1f, 1f);
                TriggerCameraShake(
                    _activeFollowConfig.target.position,
                    intensity >= 0.7f ? CameraShakeIntensity.Heavy : CameraShakeIntensity.Medium
                );
            }
        }

        /// <summary>
        /// Recenters camera behind player (useful for manual camera control)
        /// Press the recenter key (default: V) to snap camera behind player
        /// </summary>
        public void RecenterCameraBehindPlayer()
        {
            if (_currentActiveCamera == null || _activeFollowConfig.target == null)
                return;

            var panTilt = _currentActiveCamera.GetComponent<CinemachinePanTilt>();
            if (panTilt != null)
            {
                Transform player = _activeFollowConfig.target;
                Vector3 playerForward = Vector3.ProjectOnPlane(player.forward, Vector3.up);

                if (playerForward.sqrMagnitude > 0.0001f)
                {
                    float targetYaw = Mathf.Atan2(playerForward.x, playerForward.z) * Mathf.Rad2Deg;

                    var panAxis = panTilt.PanAxis;
                    panAxis.Value = targetYaw;
                    panAxis.Center = targetYaw;
                    panTilt.PanAxis = panAxis;

                    if (debugMode)
                        Debug.Log($"[CameraManager] Camera recentered behind player (Yaw: {targetYaw}°)");
                }
            }
        }


        /// <summary>
        /// Modifies camera FOV (useful for sprint/dash effects)
        /// </summary>
        /// <param name="targetFOV">Target field of view</param>
        /// <param name="duration">Time to transition to target FOV</param>
        public void SetCameraFOV(float targetFOV, float duration = 0.3f)
        {
            if (MainCamera == null)
            {
                Debug.LogWarning("[CameraManager] Cannot set FOV - no main camera");
                return;
            }

            // Start FOV transition coroutine
            StartCoroutine(TransitionFOV(targetFOV, duration));
        }

        /// <summary>
        /// Coroutine to smoothly transition FOV
        /// </summary>
        private System.Collections.IEnumerator TransitionFOV(float targetFOV, float duration)
        {
            float startFOV = MainCamera.fieldOfView;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Smooth step interpolation for natural feel
                t = t * t * (3f - 2f * t);

                MainCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t);
                yield return null;
            }

            MainCamera.fieldOfView = targetFOV;
        }

        /// <summary>
        /// Resets camera FOV to default value
        /// </summary>
        /// <param name="duration">Time to transition back</param>
        public void ResetCameraFOV(float duration = 0.3f)
        {
            SetCameraFOV(60f, duration); // Default FOV
        }


        // Old rig builder methods removed - we only use SimpleThirdPerson now

        /// <summary>
        /// Checks if a point is within the camera's view frustum
        /// </summary>
        /// <param name="point">World space point to check</param>
        /// <returns>True if the point is visible to the camera</returns>
        public bool IsPointInFrustum(Vector3 point)
        {
            if (MainCamera == null) return false;

            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(MainCamera);
            return GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(point, Vector3.one));
        }

        /// <summary>
        /// Checks if bounds are within the camera's view frustum
        /// </summary>
        /// <param name="bounds">Bounds to check</param>
        /// <returns>True if any part of the bounds is visible to the camera</returns>
        public bool IsBoundsInFrustum(Bounds bounds)
        {
            if (MainCamera == null) return false;

            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(MainCamera);
            return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }

        /// <summary>
        /// Gets the cached frustum planes for the current camera
        /// Useful for batch culling operations
        /// </summary>
        /// <returns>Array of frustum planes</returns>
        public Plane[] GetFrustumPlanes()
        {
            if (MainCamera == null) return null;
            return GeometryUtility.CalculateFrustumPlanes(MainCamera);
        }

        /// <summary>
        /// Calculate distance from camera to a point
        /// </summary>
        /// <param name="point">World space point</param>
        /// <returns>Distance in world units</returns>
        public float GetDistanceFromCamera(Vector3 point)
        {
            if (MainCamera == null) return float.MaxValue;
            return Vector3.Distance(MainCamera.transform.position, point);
        }

        /// <summary>
        /// Gets the camera's current position
        /// </summary>
        public Vector3 GetCameraPosition()
        {
            if (MainCamera == null) return Vector3.zero;
            return MainCamera.transform.position;
        }

        /// <summary>
        /// Gets the camera's forward direction
        /// </summary>
        public Vector3 GetCameraForward()
        {
            if (MainCamera == null) return Vector3.forward;
            return MainCamera.transform.forward;
        }

        /// <summary>
        /// Logs all discovered cameras and their current state
        /// </summary>
        public void LogCameraStatus()
        {
            Debug.Log($"[CameraManager] ===== Camera Status =====");
            Debug.Log($"[CameraManager] Main Camera: {(MainCamera != null ? MainCamera.name : "NULL")}");
            Debug.Log($"[CameraManager] Cinemachine Brain: {(CinemachineBrain != null ? "Found" : "NULL")}");
            Debug.Log($"[CameraManager] Total Virtual Cameras: {_allVirtualCameras.Count}");
            Debug.Log($"[CameraManager] Active Camera: {(_currentActiveCamera != null ? _currentActiveCamera.name : "NONE")}");

            foreach (var camera in _allVirtualCameras)
            {
                Debug.Log($"[CameraManager]   - '{camera.name}': Priority={camera.Priority}, Active={camera.gameObject.activeInHierarchy}, " +
                    $"Follow={camera.Follow?.name ?? "null"}, LookAt={camera.LookAt?.name ?? "null"}");
            }
            Debug.Log($"[CameraManager] ========================");
        }

        // Mouse input handlers removed - SimpleThirdPerson camera is auto-follow only (no mouse control)
        private void OnCameraLookInput(Vector2 delta)
        {
            // Not used for SimpleThirdPerson - camera auto-rotates with player
        }

        private void OnCameraZoomInput(float delta)
        {
            if (debugMode)
                Debug.Log($"[CameraManager] OnCameraZoomInput called: delta={delta}, enableZoom={enableZoom}, cameraMode={cameraMode}, hasConfig={_hasActiveFollowConfig}");
            
            if (!enableZoom)
            {
                if (debugMode)
                    Debug.Log("[CameraManager] Zoom is disabled in Inspector");
                return;
            }
            
            if (cameraMode == CameraMode.FirstPerson)
            {
                if (debugMode)
                    Debug.Log("[CameraManager] Zoom not available in first-person mode");
                return;
            }
            
            if (!_hasActiveFollowConfig || _currentActiveCamera == null || _activeFollowConfig.target == null)
            {
                if (debugMode)
                    Debug.LogWarning("[CameraManager] Cannot zoom - no active camera or follow config");
                return;
            }
            
            // Cancel any ongoing zoom reset
            _isResettingZoom = false;
            
            // WoW-style zoom: adjust distance from player
            // Scroll up (positive delta) = zoom in (decrease distance, move closer)
            // Scroll down (negative delta) = zoom out (increase distance, move further)
            float oldTarget = _targetZoomDistance;
            _targetZoomDistance -= delta * zoomStep; // Decrease distance when scrolling up
            
            // Clamp to min/max zoom distances (positive values now)
            _targetZoomDistance = Mathf.Clamp(_targetZoomDistance, minZoomDistance, maxZoomDistance);
            
            // Update current zoom immediately for responsive feel
            _currentZoomDistance = _targetZoomDistance;
            
            if (debugMode)
                Debug.Log($"[CameraManager] Zoom: {oldTarget} -> {_targetZoomDistance} (delta: {delta}, step: {zoomStep}, clamped: {minZoomDistance} to {maxZoomDistance})");
            
            // Update zoom direction based on current camera position relative to player
            UpdateZoomDirection();
            
            // Apply zoom to camera using WoW-style approach
            ApplyWoWStyleZoom();
        }
        
        /// <summary>
        /// Updates the zoom direction vector (from player to camera)
        /// This ensures zoom moves along the current camera-to-player line
        /// NOTE: Don't recalculate if camera is working correctly - only update when needed
        /// </summary>
        private void UpdateZoomDirection()
        {
            if (_currentActiveCamera == null || _activeFollowConfig.target == null)
                return;
            
            // Don't recalculate direction - use base offset direction to maintain camera orientation
            // Recalculating from camera position can cause rotation issues
            // Just use the base offset direction (behind and above player)
            Vector3 baseDir = new Vector3(0f, _baseCameraOffset.y, _baseCameraOffset.z);
            _zoomDirection = baseDir.normalized;
        }
        
        /// <summary>
        /// Applies WoW-style zoom by adjusting camera position along the player-to-camera line
        /// Maintains the same angle/height relative to player while changing distance
        /// IMPORTANT: Uses base offset direction to preserve camera rotation
        /// </summary>
        private void ApplyWoWStyleZoom()
        {
            if (_activeFollowConfig.target == null)
                return;
            
            if (cameraSystem == CameraSystemType.OrbitalFollow && _orbitalFollow != null)
            {
                // OrbitalFollow: Use Radius for zoom (Cinemachine 3.x)
                _orbitalFollow.Radius = _currentZoomDistance;

                // Maintain height ratio in TargetOffset
                float baseHeight = _baseCameraOffset.y;
                float baseDistance = Mathf.Abs(_baseCameraOffset.z);
                float heightRatio = baseHeight / baseDistance;
                float zoomHeight = _currentZoomDistance * heightRatio;
                _orbitalFollow.TargetOffset = new Vector3(0f, zoomHeight, 0f);

                if (debugMode)
                    Debug.Log($"[CameraManager] Applied WoW-style zoom (OrbitalFollow): radius={_currentZoomDistance}, height={zoomHeight}");
            }
            else if (_positionComposer != null)
            {
                // PositionComposer: Use TargetOffset for zoom
                float baseHeight = _baseCameraOffset.y;
                float baseDistance = Mathf.Abs(_baseCameraOffset.z);
                float heightRatio = baseHeight / baseDistance;
                
                float zoomHeight = _currentZoomDistance * heightRatio;
                float zoomBack = -_currentZoomDistance; // Negative Z = behind player
                
                Vector3 newOffset = new Vector3(0f, zoomHeight, zoomBack);
                _positionComposer.TargetOffset = newOffset;
                
                if (debugMode)
                    Debug.Log($"[CameraManager] Applied WoW-style zoom (PositionComposer): distance={_currentZoomDistance}, offset={newOffset}");
            }
        }
        
        /// <summary>
        /// Smoothly resets camera zoom to default distance (WoW-style)
        /// </summary>
        public void ResetZoom()
        {
            if (cameraMode == CameraMode.FirstPerson)
                return;
            
            _targetZoomDistance = defaultZoomDistance;
            _isResettingZoom = true;

            if (debugMode)
                Debug.Log($"[CameraManager] Resetting zoom to default: {defaultZoomDistance}");
        }
    }

    /// <summary>
    /// Interface for entities that want to specify their preferred camera configuration
    /// Allows characters to define custom camera behavior (3D follow, billboard, etc.)
    /// Implement this on player character classes (e.g., AdventurerCharacter, Samurai)
    /// </summary>
    public interface ICameraPreference
    {
        /// <summary>
        /// Get the preferred camera configuration for this character
        /// </summary>
        /// <param name="target">The transform to follow (usually the character itself)</param>
        /// <returns>Camera configuration that best suits this character type</returns>
        CameraFollowConfig GetPreferredCameraConfig(Transform target);

        /// <summary>
        /// Get the camera type tag for validation
        /// Should match Unity tags: "Camera3D" or "CameraBillboard"
        /// </summary>
        /// <returns>The expected camera tag for this character</returns>
        string GetExpectedCameraTag();

        /// <summary>
        /// Whether this character uses billboarding (2D sprite facing camera)
        /// </summary>
        bool UsesBillboarding { get; }
    }

    /// <summary>
    /// Camera type tags for validation and categorization
    /// Used by ICameraPreference and EntityManager for player detection
    /// </summary>
    public static class CameraTags
    {
        /// <summary>
        /// Tag for 3D characters (e.g., AdventurerCharacter)
        /// Uses perspective camera with FreeLookOrbit controls
        /// </summary>
        public const string Camera3D = "Camera3D";

        /// <summary>
        /// Tag for billboard sprite characters (e.g., Samurai, BlankPlayer)
        /// Uses orthographic camera with CinematicFollow and fixed angle
        /// </summary>
        public const string CameraBillboard = "CameraBillboard";

        /// <summary>
        /// Standard Unity player tag
        /// </summary>
        public const string Player = "Player";
    }
}
