using UnityEngine;
using VContainer;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Linq;

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

        public static CameraFollowConfig ThirdPerson(Transform target, string cameraName = null, bool immediate = false)
        {
            return new CameraFollowConfig
            {
                target = target,
                cameraName = cameraName,
                shoulderOffset = new Vector3(0, 1.5f, 0), // Look slightly above player center
                verticalArmLength = 0f,
                cameraDistance = 8f, // 8 units behind player
                immediate = immediate
            };
        }

        /// <summary>
        /// Cinematic follow preset: smooth, non-twitchy camera with professional feel
        /// Perfect for billboarded 2D sprites in 3D world
        /// Auto-follow with lookahead (no mouse control)
        /// </summary>
        public static CameraFollowConfig CinematicFollow(Transform target, string cameraName = null, bool immediate = false)
        {
            return new CameraFollowConfig
            {
                target = target,
                cameraName = cameraName,
                shoulderOffset = new Vector3(0, 1.2f, 0), // Head-room
                verticalArmLength = 0f,
                cameraDistance = 7f,
                immediate = immediate,

                // Damping: moderate lag for cinematic feel
                positionDamping = new Vector3(0.45f, 0.45f, 0.6f), // Z (depth) slightly higher

                // Screen framing: slightly below center
                screenX = 0.5f,
                screenY = 0.45f,

                // Dead zone: small (minimal drift)
                deadZoneWidth = 0.03f,
                deadZoneHeight = 0.03f,

                // Soft zone: medium (gradual transitions)
                softZoneWidth = 0.25f,
                softZoneHeight = 0.25f,

                // Lookahead: anticipate player movement
                lookaheadTime = 0.2f,
                lookaheadSmoothing = 0.5f,

                // Pitch clamp: prevent looking under/over billboard sprite
                pitchClamp = new Vector2(-10f, 25f) // Min -10°, Max +25°
            };
        }

        /// <summary>
        /// Free-look orbit preset: mouse-driven third-person camera
        /// Based on professional 2.5D games (pixel art in 3D)
        /// Mouse controls yaw/pitch, shoulder offset, soft follow with collision awareness
        /// </summary>
        public static CameraFollowConfig FreeLookOrbit(Transform target, string cameraName = null, bool immediate = false)
        {
            return new CameraFollowConfig
            {
                target = target,
                cameraName = cameraName,
                shoulderOffset = new Vector3(0.35f, 1.15f, 0f), // Slight right + head-room
                verticalArmLength = 0f,
                cameraDistance = 6.0f, // Fixed orbit radius
                immediate = immediate,

                // Damping: smooth orbit (X/Y lower, Z slightly higher for depth)
                positionDamping = new Vector3(0.45f, 0.45f, 0.65f),

                // Screen framing: slightly off-center for shoulder cam
                screenX = 0.52f,
                screenY = 0.46f,

                // Dead zone: tiny (mouse-driven, not auto-follow)
                deadZoneWidth = 0.03f,
                deadZoneHeight = 0.03f,

                // Soft zone: gentle transitions
                softZoneWidth = 0.3f,
                softZoneHeight = 0.3f,

                // Lookahead: small (camera is mouse-driven, not velocity-driven)
                lookaheadTime = 0.15f,
                lookaheadSmoothing = 0.5f,

                // Pitch clamp: prevent looking under/over billboard sprite
                pitchClamp = new Vector2(-10f, 25f) // Min -10°, Max +25°
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
        #region Singleton
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
        #endregion

        #region Dependencies
        private EventManager _eventManager;

        [Inject]
        public void Construct(EventManager eventManager)
        {
            _eventManager = eventManager;
            Debug.Log("[CameraManager] Dependencies injected via Construct()");
        }
        #endregion

        #region Settings
        [Header("Settings")]
        [SerializeField] private bool debugMode = false;

        [Header("Auto-Discovery")]
        [SerializeField] [Tooltip("Automatically find virtual cameras on Start")]
        private bool autoDiscoverCameras = true;

        [SerializeField] [Tooltip("Include inactive virtual cameras in discovery")]
        private bool includeInactiveCameras = false;
        #endregion

        #region Camera References
        private Camera _mainCamera;
        private CinemachineBrain _cinemachineBrain;

        // Cached virtual cameras
        private Dictionary<string, CinemachineCamera> _virtualCamerasByName = new Dictionary<string, CinemachineCamera>();
        private List<CinemachineCamera> _allVirtualCameras = new List<CinemachineCamera>();

        // Active camera tracking
        private CinemachineCamera _currentActiveCamera;
        private int _defaultPriority = 10;
        private int _activePriority = 100;

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
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
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

        private void Start()
        {
            if (debugMode)
            {
                Debug.Log($"[CameraManager] Start called");
                Debug.Log($"[CameraManager] EventManager: {(_eventManager != null ? "✓ available" : "✗ NULL")}");
                Debug.Log($"[CameraManager] MainCamera: {(_mainCamera != null ? "✓ found" : "✗ NULL")}");
            }

            // Validate dependencies
            if (_eventManager == null)
            {
                Debug.LogError("[CameraManager] EventManager was not injected! Camera events will not work.");
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
        }

        private void OnDestroy()
        {
            if (debugMode)
                Debug.Log("[CameraManager] OnDestroy called");

            UnsubscribeFromEvents();
            ClearCameraCache();
        }
        #endregion

        #region Event Management
        private void SubscribeToEvents()
        {
            // Subscribe to camera control events from EventManager
            if (_eventManager != null)
            {
                _eventManager.OnCameraFollowRequested.AddListener(HandleCameraFollowRequest);
                _eventManager.OnCameraStopFollowRequested.AddListener(HandleCameraStopFollowRequest);

                if (debugMode)
                    Debug.Log("[CameraManager] Subscribed to EventManager camera events");
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
                // Set Follow and LookAt targets
                virtualCamera.Follow = config.target;
                virtualCamera.LookAt = config.target;

                // Configure third-person follow component
                ConfigureThirdPersonFollow(virtualCamera, config);

                // Activate this camera
                ActivateCamera(virtualCamera, true);

                if (debugMode)
                    Debug.Log($"[CameraManager] Camera '{virtualCamera.name}' now following '{config.target.name}'");
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

                if (debugMode)
                    Debug.Log($"[CameraManager] Camera '{camera.name}' stopped following");
            }
        }

        /// <summary>
        /// Configures Cinemachine 3 camera components for professional cinematic follow
        /// Supports: ThirdPersonFollow, FramingTransposer (Body) + Composer (Aim)
        /// Automatically adds missing components at runtime
        /// </summary>
        private void ConfigureThirdPersonFollow(CinemachineCamera camera, CameraFollowConfig config)
        {
            // Check if we have cinematic parameters configured (non-zero damping indicates cinematic mode)
            bool useCinematicMode = config.positionDamping.magnitude > 0.01f;

            if (useCinematicMode)
            {
                // CINEMATIC MODE: Use FramingTransposer (Body) + Composer (Aim) for professional camera feel
                ConfigureCinematicCamera(camera, config);
            }
            else
            {
                // LEGACY MODE: Use ThirdPersonFollow or basic Follow component
                ConfigureLegacyCamera(camera, config);
            }
        }

        /// <summary>
        /// Configures cinematic camera using ThirdPersonFollow + POV/Composer
        /// Professional camera feel with damping, dead zones, soft zones, lookahead
        /// Supports both auto-follow (Composer) and mouse-driven (POV) modes
        /// </summary>
        private void ConfigureCinematicCamera(CinemachineCamera camera, CameraFollowConfig config)
        {
            // Determine if this is a mouse-driven orbit camera or auto-follow camera
            // FreeLookOrbit has shoulder offset X > 0.2 (right shoulder bias)
            bool isFreeLookOrbit = config.shoulderOffset.x > 0.2f;

            if (isFreeLookOrbit)
            {
                // === FREE-LOOK ORBIT MODE: ThirdPersonFollow + POV ===
                ConfigureFreeLookOrbit(camera, config);
            }
            else
            {
                // === AUTO-FOLLOW MODE: PositionComposer (FramingTransposer) + RotationComposer ===
                ConfigureAutoFollow(camera, config);
            }
        }

        /// <summary>
        /// Configure free-look orbit camera: mouse-driven yaw/pitch with ThirdPersonFollow body
        /// Professional third-person camera like in EthrA pixel-art 3D games
        /// Includes collision detection, noise, and camera shake support
        /// </summary>
        private void ConfigureFreeLookOrbit(CinemachineCamera camera, CameraFollowConfig config)
        {
            // === BODY COMPONENT: ThirdPersonFollow (fixed orbit radius) ===
            var thirdPersonFollow = camera.GetComponent<CinemachineThirdPersonFollow>();
            if (thirdPersonFollow == null)
            {
                thirdPersonFollow = camera.gameObject.AddComponent<CinemachineThirdPersonFollow>();
                Debug.Log($"[CameraManager] Added ThirdPersonFollow for free-look orbit to '{camera.name}'");
            }

            // Configure orbit parameters
            thirdPersonFollow.CameraDistance = config.cameraDistance;
            thirdPersonFollow.ShoulderOffset = config.shoulderOffset;
            thirdPersonFollow.VerticalArmLength = config.verticalArmLength;
            thirdPersonFollow.CameraSide = 0.5f; // Center
            thirdPersonFollow.Damping = config.positionDamping;

            // === AIM COMPONENT: POV (mouse-driven yaw/pitch) ===
            var pov = camera.GetComponent<CinemachinePOV>();
            if (pov == null)
            {
                pov = camera.gameObject.AddComponent<CinemachinePOV>();
                Debug.Log($"[CameraManager] Added CinemachinePOV for mouse control to '{camera.name}'");
            }

            // Note: Cinemachine 3 POV configuration happens through CinemachineInputAxisController
            // or Unity Input System bindings in the inspector.
            // The POV component itself doesn't expose axis configuration in the same way as CM2.
            // We'll configure basic rotation behavior here, but input binding needs inspector setup.

            // === COLLISION DETECTION: CinemachineDeoccluder ===
            var deoccluder = camera.GetComponent<CinemachineDeoccluder>();
            if (deoccluder == null)
            {
                deoccluder = camera.gameObject.AddComponent<CinemachineDeoccluder>();
                Debug.Log($"[CameraManager] Added CinemachineDeoccluder for collision detection to '{camera.name}'");
            }

            // Configure collision parameters
            deoccluder.CollideAgainst = LayerMask.GetMask("Default", "Environment", "Terrain"); // Adjust layers as needed
            deoccluder.MinimumDistanceFromTarget = 0.8f; // Pull forward to maintain visibility
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.DistanceLimit = config.cameraDistance + 1f; // Check slightly beyond camera distance
            deoccluder.AvoidObstacles.CameraRadius = 0.2f; // Camera collision sphere radius
            // Note: Smoothing time may need to be configured in the Inspector or via specific CM3 properties

            // === SUBTLE CAMERA NOISE: BasicMultiChannelPerlin ===
            var noise = camera.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise == null)
            {
                noise = camera.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
                Debug.Log($"[CameraManager] Added BasicMultiChannelPerlin for subtle camera noise to '{camera.name}'");
            }

            // Configure subtle handheld-style noise
            noise.AmplitudeGain = 0.1f; // Very subtle amplitude
            noise.FrequencyGain = 0.5f; // Low frequency for gentle sway
            // Note: Assign a Noise Profile asset in the inspector for best results

            // === CAMERA SHAKE SUPPORT: ImpulseListener ===
            var impulseListener = camera.GetComponent<CinemachineImpulseListener>();
            if (impulseListener == null)
            {
                impulseListener = camera.gameObject.AddComponent<CinemachineImpulseListener>();
                Debug.Log($"[CameraManager] Added ImpulseListener for camera shake support to '{camera.name}'");
            }

            // Configure impulse reaction
            impulseListener.ReactionSettings.AmplitudeGain = 1.0f; // Full impulse strength
            impulseListener.ReactionSettings.FrequencyGain = 1.0f;
            impulseListener.ReactionSettings.Duration = 1.0f; // How long impulses last

            // Note: POV input sensitivity is configured via CinemachineInputAxisController
            // or through Unity Input System bindings in the inspector

            if (debugMode)
            {
                Debug.Log($"[CameraManager] Configured FreeLookOrbit Camera:");
                Debug.Log($"  - CameraDistance: {config.cameraDistance}");
                Debug.Log($"  - ShoulderOffset: {config.shoulderOffset}");
                Debug.Log($"  - Damping: {config.positionDamping}");
                Debug.Log($"  - Pitch Range: [{config.pitchClamp.x}°, {config.pitchClamp.y}°]");
                Debug.Log($"  - POV Mode: Mouse X/Y controls yaw/pitch");
                Debug.Log($"  - Collision: Enabled with min distance {deoccluder.MinimumDistanceFromTarget}");
                Debug.Log($"  - Noise: Subtle handheld effect (amplitude {noise.AmplitudeGain})");
                Debug.Log($"  - Impulse: Camera shake ready");
            }
        }

        /// <summary>
        /// Configure auto-follow camera: velocity-driven with PositionComposer + RotationComposer
        /// Smooth cinematic camera that follows player movement automatically
        /// </summary>
        private void ConfigureAutoFollow(CinemachineCamera camera, CameraFollowConfig config)
        {
            // === BODY COMPONENT: CinemachinePositionComposer (Framing Transposer) ===
            var framingTransposer = camera.GetComponent<CinemachinePositionComposer>();
            if (framingTransposer == null)
            {
                framingTransposer = camera.gameObject.AddComponent<CinemachinePositionComposer>();
                Debug.Log($"[CameraManager] Added CinemachinePositionComposer (FramingTransposer) to '{camera.name}'");
            }

            // Configure position damping for cinematic lag
            framingTransposer.Damping = config.positionDamping;

            // Configure camera distance
            framingTransposer.CameraDistance = config.cameraDistance;

            // Configure tracked object offset (shoulder offset for head-room)
            framingTransposer.TargetOffset = config.shoulderOffset;

            // Configure composition settings (screen position, dead zone, soft zone)
            // In Cinemachine 3, these are part of the Composition property
            framingTransposer.Composition.ScreenPosition = new Vector2(config.screenX, config.screenY);
            // Note: DeadZone and SoftZone configuration in CM3 may require different approach
            // Configure via Inspector or use specific CM3 composition APIs

            // Configure lookahead for anticipating player movement
            framingTransposer.Lookahead.Time = config.lookaheadTime;
            framingTransposer.Lookahead.Smoothing = config.lookaheadSmoothing;
            framingTransposer.Lookahead.IgnoreY = true; // Don't lookahead vertically for billboard sprites

            // === ROTATION LOCK: Lock camera to fixed angle for 2D sprites ===
            // Add LockedCameraRotation extension to prevent yaw rotation
            // Critical for 2-directional sprite characters that only face left/right
            var rotationLock = camera.GetComponent<LockedCameraRotation>();
            if (rotationLock == null)
            {
                rotationLock = camera.gameObject.AddComponent<LockedCameraRotation>();
                Debug.Log($"[CameraManager] Added LockedCameraRotation for fixed viewing angle to '{camera.name}'");
            }

            // Configure rotation lock: 25° downward pitch, no yaw rotation
            // This matches Octopath Traveler / Triangle Strategy HD-2D camera style
            rotationLock.lockedRotation = new Vector3(25f, 0f, 0f);  // 25° down, straight ahead
            rotationLock.lockPitch = true;   // Lock pitch to 25°
            rotationLock.lockYaw = true;     // Lock yaw to 0° (CRITICAL for 2-directional sprites)
            rotationLock.lockRoll = true;    // Lock roll to 0°

            if (debugMode)
            {
                Debug.Log($"[CameraManager] Configured AutoFollow Camera (FramingTransposer):");
                Debug.Log($"  - Damping: {config.positionDamping}");
                Debug.Log($"  - CameraDistance: {config.cameraDistance}");
                Debug.Log($"  - TrackedOffset: {config.shoulderOffset}");
                Debug.Log($"  - ScreenPosition: ({config.screenX}, {config.screenY})");
                Debug.Log($"  - DeadZone: {config.deadZoneWidth}x{config.deadZoneHeight}");
                Debug.Log($"  - SoftZone: {config.softZoneWidth}x{config.softZoneHeight}");
                Debug.Log($"  - Lookahead: Time={config.lookaheadTime}s, Smoothing={config.lookaheadSmoothing}");
                Debug.Log($"  - PitchClamp: [{config.pitchClamp.x}°, {config.pitchClamp.y}°]");
                Debug.Log($"  - Rotation Lock: (25°, 0°, 0°) - HD-2D style fixed angle");
            }
        }

        /// <summary>
        /// Configures legacy camera mode using ThirdPersonFollow or basic Follow
        /// </summary>
        private void ConfigureLegacyCamera(CinemachineCamera camera, CameraFollowConfig config)
        {
            // Try to get or add CinemachineThirdPersonFollow component
            var thirdPersonFollow = camera.GetComponent<CinemachineThirdPersonFollow>();

            if (thirdPersonFollow != null)
            {
                // Configure third-person follow settings
                thirdPersonFollow.ShoulderOffset = config.shoulderOffset;
                thirdPersonFollow.VerticalArmLength = config.verticalArmLength;
                thirdPersonFollow.CameraDistance = config.cameraDistance;
                thirdPersonFollow.CameraSide = 0.5f; // Center
                thirdPersonFollow.Damping = new Vector3(0.1f, 0.1f, 0.1f); // Smooth damping

                if (debugMode)
                    Debug.Log($"[CameraManager] Configured ThirdPersonFollow: ShoulderOffset={config.shoulderOffset}, " +
                        $"VerticalArmLength={config.verticalArmLength}, CameraDistance={config.cameraDistance}");
            }
            else
            {
                // Try CinemachineFollow as fallback
                var follow = camera.GetComponent<CinemachineFollow>();
                if (follow == null)
                {
                    // Add CinemachineFollow component at runtime
                    follow = camera.gameObject.AddComponent<CinemachineFollow>();
                    Debug.Log($"[CameraManager] Added CinemachineFollow component to '{camera.name}' at runtime");
                }

                // Configure follow offset (behind and above target)
                follow.FollowOffset = new Vector3(0, config.shoulderOffset.y, -config.cameraDistance);

                // Configure damping for smooth movement
                follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
                follow.TrackerSettings.PositionDamping = new Vector3(0.1f, 0.1f, 0.1f);
                follow.TrackerSettings.RotationDamping = Vector3.zero; // No rotation damping needed

                if (debugMode)
                    Debug.Log($"[CameraManager] Configured CinemachineFollow: FollowOffset={follow.FollowOffset}");
            }
        }
        #endregion

        #region Virtual Camera Discovery
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
        #endregion

        #region Camera Access Methods
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
        #endregion

        #region Camera Control Methods
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
        #endregion

        #region Blend Control
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
        #endregion

        #region Camera Effects (Shake, FOV, etc.)

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

        #endregion

        #region Frustum Culling Helpers
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
        #endregion

        #region Debug Helpers
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
        #endregion
    }
}
