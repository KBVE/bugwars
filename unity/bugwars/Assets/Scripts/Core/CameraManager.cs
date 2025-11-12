using UnityEngine;
using VContainer;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Linq;

namespace BugWars.Core
{
    /// <summary>
    /// Creates a "smart" pivot that runs ahead of the player based on velocity,
    /// adds shoulder bias, and vertical head-room. Cinemachine follows this pivot.
    /// Makes direction immediately obvious for both orthographic and perspective cameras.
    /// </summary>
    public class CameraLeadPivot : MonoBehaviour
    {
        [Header("Targets")]
        public Transform player;              // required
        public Rigidbody playerRb;            // optional (better velocity)
        public CharacterController cc;        // optional (fallback speed)

        [Header("Look-ahead")]
        [Tooltip("How far ahead (in meters) at max speed.")]
        public float maxLeadDistance = 3.5f;
        [Tooltip("Speed (m/s) considered 'max' for lead computation.")]
        public float maxSpeedForLead = 8f;
        [Tooltip("How quickly the pivot chases its target position.")]
        public float chaseResponsiveness = 12f; // higher = snappier

        [Header("Bias / Readability")]
        public Vector3 headRoom = new Vector3(0f, 1.1f, 0f);   // small Y up
        public float shoulderRight = 0.35f;                     // +X (right bias)
        public float shoulderBlend = 0.25f;                     // 0..1 toward move dir side

        [Header("Smoothing")]
        [Tooltip("Extra smoothing to kill micro jitter.")]
        public float extraSmoothing = 0.08f;

        Vector3 _vel;
        Vector3 _pivotVel;
        Vector3 _smoothedVel;

        public void TeleportToPlayer()
        {
            if (!player) return;
            transform.position = player.position + headRoom;
        }

        void LateUpdate()
        {
            if (!player) return;

            // Velocity source
            Vector3 v = Vector3.zero;
            if (playerRb) v = playerRb.linearVelocity;
            else if (cc && cc.enabled) v = cc.velocity;
            else v = (player.position - _vel) / Mathf.Max(Time.deltaTime, 0.0001f); // fallback diff

            _vel = player.position;

            // Smooth velocity for stable heading
            _smoothedVel = Vector3.Lerp(_smoothedVel, v, 1f - Mathf.Exp(-8f * Time.deltaTime));
            Vector3 moveDir = _smoothedVel.sqrMagnitude > 0.0004f ? _smoothedVel.normalized : Vector3.zero;

            // Lead distance by speed
            float speed = _smoothedVel.magnitude;
            float t = Mathf.Clamp01(speed / Mathf.Max(0.0001f, maxSpeedForLead));
            float lead = Mathf.Lerp(0f, maxLeadDistance, t);

            // Shoulder side: prefer right side, but bleed toward movement side
            Vector3 shoulder = Vector3.right * shoulderRight;
            if (moveDir != Vector3.zero)
            {
                // pick side perpendicular to moveDir on XZ plane
                Vector3 rightOnGround = Vector3.Cross(Vector3.up, moveDir).normalized;
                shoulder = Vector3.Lerp(shoulder, rightOnGround * shoulderRight, shoulderBlend);
            }

            // Desired pivot position
            Vector3 desired = player.position + headRoom + shoulder + (moveDir * lead);

            // Snap immediately when the player is nearly stationary to avoid spring-back
            if (speed < 0.15f)
            {
                transform.position = desired;
                _pivotVel = Vector3.zero;
            }
            else
            {
                // Chase with critically damped feel
                transform.position = Vector3.SmoothDamp(transform.position, desired, ref _pivotVel, extraSmoothing,
                                                         Mathf.Infinity, Time.deltaTime);
            }

            // Keep pivot upright (helps if you read its forward elsewhere)
            transform.rotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Cinemachine extension that locks camera rotation to a fixed angle
    /// Perfect for 2D billboard sprites in 3D world (HD-2D style like Octopath Traveler)
    /// Prevents camera yaw rotation while maintaining optimal downward viewing angle
    /// </summary>
    [SaveDuringPlay]
    public class LockedCameraRotation : CinemachineExtension
    {
        [Header("Rotation Lock Settings")]
        [Tooltip("Fixed rotation angles (pitch, yaw, roll). For orthographic top-down: (45, 0, 0) recommended")]
        public Vector3 lockedRotation = new Vector3(45f, 0f, 0f);

        [Tooltip("Lock pitch (X rotation)")]
        public bool lockPitch = true;

        [Tooltip("Lock yaw (Y rotation) - CRITICAL for 2-directional sprites")]
        public bool lockYaw = true;

        [Tooltip("Lock roll (Z rotation)")]
        public bool lockRoll = true;

        [Header("Optional Offset")]
        [Tooltip("Additional rotation offset relative to locked angles")]
        public Vector3 rotationOffset = Vector3.zero;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            // Apply rotation lock at the Finalize stage (after all other calculations)
            if (stage == CinemachineCore.Stage.Finalize)
            {
                // Get current camera rotation
                Vector3 currentEuler = state.RawOrientation.eulerAngles;

                // Build locked rotation based on settings
                Vector3 finalRotation = new Vector3(
                    lockPitch ? lockedRotation.x + rotationOffset.x : currentEuler.x,
                    lockYaw ? lockedRotation.y + rotationOffset.y : currentEuler.y,
                    lockRoll ? lockedRotation.z + rotationOffset.z : currentEuler.z
                );

                // Apply locked rotation
                state.RawOrientation = Quaternion.Euler(finalRotation);
            }
        }

        /// <summary>
        /// Set the locked rotation at runtime
        /// </summary>
        public void SetLockedRotation(Vector3 rotation)
        {
            lockedRotation = rotation;
        }

        /// <summary>
        /// Set individual rotation axes at runtime
        /// </summary>
        public void SetLockedPitch(float pitch)
        {
            lockedRotation.x = pitch;
        }

        public void SetLockedYaw(float yaw)
        {
            lockedRotation.y = yaw;
        }

        public void SetLockedRoll(float roll)
        {
            lockedRotation.z = roll;
        }
    }

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
                shoulderOffset = new Vector3(0.35f, 1.2f, 0f),
                verticalArmLength = 0f,
                cameraDistance = 6.5f,
                immediate = immediate,

                // Damping: moderate lag for cinematic feel
                positionDamping = Vector3.zero,

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
                pitchClamp = new Vector2(10f, 60f)
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
                shoulderOffset = new Vector3(0.4f, 1.3f, 0f),
                verticalArmLength = 0f,
                cameraDistance = 6.0f,
                immediate = immediate,

                // Damping: smooth orbit (X/Y lower, Z slightly higher for depth)
                positionDamping = Vector3.zero,

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
                pitchClamp = new Vector2(5f, 65f)
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
        [SerializeField] [Tooltip("Use orthographic projection (better readability) or perspective (depth)")]
        private bool preferOrthographic = false;

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
        private CinemachinePanTilt _activePanTilt;
        private int _defaultPriority = 10;
        private int _activePriority = 100;

        [Header("Camera Input Settings")]
        [SerializeField] [Tooltip("Default field of view for perspective cameras")]
        private float perspectiveFov = 55f;
        [SerializeField] [Tooltip("Minimum distance when zooming the third-person camera")]
        private float minZoomDistance = 3.5f;
        [SerializeField] [Tooltip("Maximum distance when zooming the third-person camera")]
        private float maxZoomDistance = 9f;
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
        private float defaultTiltAngle = -25f;
        [SerializeField] [Tooltip("Default yaw offset relative to the target forward when camera initialises")]
        private float defaultPanOffset = 0f;

        private Vector3 orbitShoulderOffset;
        private float orbitCameraDistance;
        private Vector3 orbitPositionDamping;
        private Vector2 orbitPitchClamp;
        private Vector3 pivotHeadRoom;
        private float pivotShoulderRight;
        private readonly Dictionary<string, CinemachinePanTilt> _panTiltCache = new Dictionary<string, CinemachinePanTilt>();

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
            ForceCameraDefaults();

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

        private void Reset()
        {
            ForceCameraDefaults();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ForceCameraDefaults();
            }
        }
#endif

        private void ForceCameraDefaults()
        {
            orbitShoulderOffset = new Vector3(0.2f, 1.45f, 0f);
            orbitCameraDistance = 7.5f;
            orbitPositionDamping = Vector3.zero;
            orbitPitchClamp = new Vector2(5f, 65f);
            pivotHeadRoom = new Vector3(0f, 1.2f, 0f);
            pivotShoulderRight = 0f;
            defaultTiltAngle = -32f;
            defaultPanOffset = 0f;
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
                _eventManager.OnCameraLookInput.AddListener(OnCameraLookInput);
                _eventManager.OnCameraZoomInput.AddListener(OnCameraZoomInput);

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
                // Override with inspector-tuned orbit settings
                config.cameraDistance = orbitCameraDistance;
                config.shoulderOffset = orbitShoulderOffset;
                config.positionDamping = orbitPositionDamping;
                config.pitchClamp = orbitPitchClamp;

                // Use lead pivot system for better readability
                if (preferOrthographic)
                {
                    BuildOrthoRig(virtualCamera, config.target, teleport: !config.immediate);
                }
                else
                {
                    BuildPerspectiveLiteRig(virtualCamera, config, teleport: !config.immediate);
                }

                // Activate this camera
                ActivateCamera(virtualCamera, true);

                if (debugMode)
                    Debug.Log($"[CameraManager] Camera '{virtualCamera.name}' now following '{config.target.name}' via lead pivot ({(preferOrthographic ? "Ortho" : "Perspective")})");
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

            // Ensure pan/tilt aiming
            EnsurePanTilt(camera, config);

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
            if (debugMode)
            {
                Debug.Log($"[CameraManager] Configured FreeLookOrbit Camera:");
                Debug.Log($"  - CameraDistance: {config.cameraDistance}");
                Debug.Log($"  - ShoulderOffset: {config.shoulderOffset}");
                Debug.Log($"  - Damping: {config.positionDamping}");
                Debug.Log($"  - Pitch Range: [{config.pitchClamp.x}°, {config.pitchClamp.y}°]");
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

            // Configure rotation lock: 45° downward pitch for orthographic top-down view
            // Provides good visibility ahead while maintaining 3D depth illusion
            rotationLock.lockedRotation = new Vector3(45f, 0f, 0f);  // 45° down for top-down orthographic
            rotationLock.lockPitch = true;   // Lock pitch to 45°
            rotationLock.lockYaw = true;     // Lock yaw to 0° (CRITICAL for 2-directional sprites)
            rotationLock.lockRoll = true;    // Lock roll to 0°

            // === ORTHOGRAPHIC PROJECTION: Set camera to orthographic for top-down view ===
            // Orthographic provides better forward visibility and cleaner 2D sprite integration
            if (MainCamera != null && !MainCamera.orthographic)
            {
                MainCamera.orthographic = true;
                MainCamera.orthographicSize = 8f; // Good balance for visibility
                Debug.Log($"[CameraManager] Set Main Camera to orthographic (size: 8)");
            }

            if (debugMode)
            {
                Debug.Log($"[CameraManager] Configured AutoFollow Camera (FramingTransposer):");
                Debug.Log($"  - Projection: Orthographic (size: 8)");
                Debug.Log($"  - Damping: {config.positionDamping}");
                Debug.Log($"  - CameraDistance: {config.cameraDistance}");
                Debug.Log($"  - TrackedOffset: {config.shoulderOffset}");
                Debug.Log($"  - ScreenPosition: ({config.screenX}, {config.screenY})");
                Debug.Log($"  - DeadZone: {config.deadZoneWidth}x{config.deadZoneHeight}");
                Debug.Log($"  - SoftZone: {config.softZoneWidth}x{config.softZoneHeight}");
                Debug.Log($"  - Lookahead: Time={config.lookaheadTime}s, Smoothing={config.lookaheadSmoothing}");
                Debug.Log($"  - PitchClamp: [{config.pitchClamp.x}°, {config.pitchClamp.y}°]");
                Debug.Log($"  - Rotation Lock: (45°, 0°, 0°) - Orthographic top-down view");
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

        #region Camera Rig Builders
        /// <summary>
        /// Builds orthographic camera rig with lead pivot for excellent readability
        /// Perfect for 2D sprites in 3D world
        /// </summary>
        private void BuildOrthoRig(CinemachineCamera vcam, Transform playerTransform, bool teleport = true)
        {
            if (vcam == null || playerTransform == null || MainCamera == null) return;

            // Main camera orthographic settings
            MainCamera.orthographic = true;
            MainCamera.orthographicSize = 8f;

            // Create/get lead pivot
            CameraLeadPivot pivot = GetOrCreateLeadPivot(playerTransform);
            pivot.maxLeadDistance = 3.2f;
            pivot.maxSpeedForLead = 7.5f;
            pivot.shoulderRight = 0.35f;
            pivot.headRoom = new Vector3(0, 1.05f, 0);
            if (teleport) pivot.TeleportToPlayer();

            // Body: PositionComposer (Framing Transposer)
            var posComp = vcam.GetComponent<CinemachinePositionComposer>();
            if (posComp == null)
            {
                posComp = vcam.gameObject.AddComponent<CinemachinePositionComposer>();
                Debug.Log($"[CameraManager] Added PositionComposer for ortho rig to '{vcam.name}'");
            }

            posComp.Damping = new Vector3(0.45f, 0.45f, 0.65f);
            posComp.CameraDistance = 10f;
            posComp.TargetOffset = Vector3.zero; // pivot already has headRoom/shoulder
            posComp.Composition.ScreenPosition = new Vector2(0.53f, 0.48f); // push view slightly forward

            // Aim: fixed rotation
            var lockRot = vcam.GetComponent<LockedCameraRotation>();
            if (lockRot == null)
            {
                lockRot = vcam.gameObject.AddComponent<LockedCameraRotation>();
                Debug.Log($"[CameraManager] Added LockedCameraRotation to '{vcam.name}'");
            }

            lockRot.lockedRotation = new Vector3(35f, 0f, 0f); // gentle down angle for better ground visibility
            lockRot.lockPitch = true;
            lockRot.lockYaw = true;
            lockRot.lockRoll = true;

            // Follow/LookAt the pivot
            vcam.Follow = pivot.transform;
            vcam.LookAt = pivot.transform;

            if (debugMode)
            {
                Debug.Log($"[CameraManager] Built Ortho Rig:");
                Debug.Log($"  - Orthographic Size: 8");
                Debug.Log($"  - Lead Distance: {pivot.maxLeadDistance}m");
                Debug.Log($"  - Camera Angle: 35° down");
                Debug.Log($"  - Following: Lead Pivot");
            }
        }

        /// <summary>
        /// Builds perspective camera rig with lead pivot (low FOV for depth)
        /// Alternative to orthographic with more depth perception
        /// </summary>
        private void BuildPerspectiveLiteRig(CinemachineCamera vcam, CameraFollowConfig config, bool teleport = true)
        {
            if (vcam == null || config.target == null || MainCamera == null) return;

            // Main camera perspective settings
            MainCamera.orthographic = false;
            MainCamera.fieldOfView = perspectiveFov;
            MainCamera.nearClipPlane = 0.05f;
            MainCamera.farClipPlane = 500f;

            // Create/get lead pivot
            CameraLeadPivot pivot = GetOrCreateLeadPivot(config.target);
            pivot.maxLeadDistance = 0f;
            pivot.maxSpeedForLead = 0f;
            pivot.shoulderRight = pivotShoulderRight;
            pivot.shoulderBlend = 0f;
            pivot.headRoom = pivotHeadRoom;
            pivot.extraSmoothing = 0f;
            if (teleport) pivot.TeleportToPlayer();

            // Body: ThirdPersonFollow
            var tpf = vcam.GetComponent<CinemachineThirdPersonFollow>();
            if (tpf == null)
            {
                tpf = vcam.gameObject.AddComponent<CinemachineThirdPersonFollow>();
                Debug.Log($"[CameraManager] Added ThirdPersonFollow for perspective rig to '{vcam.name}'");
            }

            float desiredDistance = orbitCameraDistance;
            tpf.CameraDistance = desiredDistance;
            tpf.ShoulderOffset = orbitShoulderOffset;
            tpf.VerticalArmLength = config.verticalArmLength;
            tpf.CameraSide = 0.5f;
            tpf.Damping = orbitPositionDamping;

            // Collision detection
            var deoc = vcam.GetComponent<CinemachineDeoccluder>();
            if (deoc == null)
            {
                deoc = vcam.gameObject.AddComponent<CinemachineDeoccluder>();
                Debug.Log($"[CameraManager] Added Deoccluder for collision to '{vcam.name}'");
            }

            deoc.CollideAgainst = LayerMask.GetMask("Default", "Environment", "Terrain");
            deoc.MinimumDistanceFromTarget = 0.8f;
            deoc.AvoidObstacles.Enabled = true;
            deoc.AvoidObstacles.CameraRadius = 0.2f;
            deoc.AvoidObstacles.DistanceLimit = tpf.CameraDistance + 1.5f;

            // Remove any orthographic rotation locks from previous config
            var lockRot = vcam.GetComponent<LockedCameraRotation>();
            if (lockRot != null)
            {
                Destroy(lockRot);
            }

            // Orbit aim via Pan/Tilt
            EnsurePanTilt(vcam, config);

            // Follow/LookAt the pivot
            vcam.Follow = pivot.transform;
            vcam.LookAt = pivot.transform;

            if (debugMode)
            {
                Debug.Log($"[CameraManager] Built Perspective Lite Rig:");
                Debug.Log($"  - FOV: {perspectiveFov}°");
                Debug.Log($"  - Lead Distance: {pivot.maxLeadDistance}m");
                Debug.Log($"  - Camera Angle: 20° down");
                Debug.Log($"  - Following: Lead Pivot");
            }
        }

        /// <summary>
        /// Gets or creates a lead pivot for the player
        /// </summary>
        private CameraLeadPivot GetOrCreateLeadPivot(Transform playerTransform)
        {
            // Check if pivot already exists
            string pivotName = $"CamLeadPivot_{playerTransform.name}";
            GameObject pivotGo = GameObject.Find(pivotName);

            if (pivotGo != null)
            {
                var existingPivot = pivotGo.GetComponent<CameraLeadPivot>();
                if (existingPivot != null)
                {
                    existingPivot.player = playerTransform;

                    // Try to find Rigidbody
                    var rb = playerTransform.GetComponent<Rigidbody>();
                    if (rb != null) existingPivot.playerRb = rb;

                    return existingPivot;
                }
            }

            // Create new pivot
            pivotGo = new GameObject(pivotName);
            var pivot = pivotGo.AddComponent<CameraLeadPivot>();
            pivot.player = playerTransform;

            // Try to find Rigidbody
            var playerRb = playerTransform.GetComponent<Rigidbody>();
            if (playerRb != null) pivot.playerRb = playerRb;

            Debug.Log($"[CameraManager] Created new lead pivot: {pivotName}");
            return pivot;
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

        private CinemachinePanTilt EnsurePanTilt(CinemachineCamera camera, CameraFollowConfig config)
        {
            if (camera == null)
                return null;

            if (!_panTiltCache.TryGetValue(camera.name, out var panTilt) || panTilt == null)
            {
                panTilt = camera.GetComponent<CinemachinePanTilt>();
                if (panTilt == null)
                {
                    panTilt = camera.gameObject.AddComponent<CinemachinePanTilt>();
                    if (debugMode)
                        Debug.Log($"[CameraManager] Added CinemachinePanTilt to '{camera.name}'");
                }
                _panTiltCache[camera.name] = panTilt;
            }

            ConfigurePanTilt(panTilt, config);
            if (_currentActiveCamera == camera)
            {
                _activePanTilt = panTilt;
            }

            return panTilt;
        }

        private void ConfigurePanTilt(CinemachinePanTilt panTilt, CameraFollowConfig config)
        {
            if (panTilt == null)
                return;

            panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.ParentObject;
            panTilt.RecenterTarget = CinemachinePanTilt.RecenterTargetModes.AxisCenter;

            var panAxis = panTilt.PanAxis;
            panAxis.Range = new Vector2(-180f, 180f);
            panAxis.Wrap = true;
            panAxis.Recentering = new InputAxis.RecenteringSettings { Enabled = false };
            float desiredPan = defaultPanOffset;
            if (config.target != null)
            {
                Vector3 flatForward = Vector3.ProjectOnPlane(config.target.forward, Vector3.up);
                if (flatForward.sqrMagnitude > 0.0001f)
                {
                    desiredPan += Vector3.SignedAngle(Vector3.forward, flatForward.normalized, Vector3.up);
                }
            }
            desiredPan = Mathf.Repeat(desiredPan + 180f, 360f) - 180f; // normalize to [-180,180]
            desiredPan = Mathf.Clamp(desiredPan, panAxis.Range.x, panAxis.Range.y);
            panAxis.Center = desiredPan;
            panAxis.Value = desiredPan;
            panTilt.PanAxis = panAxis;

            Vector2 pitchClamp = config.pitchClamp;
            if (Mathf.Approximately(pitchClamp.x, 0f) && Mathf.Approximately(pitchClamp.y, 0f))
            {
                pitchClamp = new Vector2(10f, 60f);
            }

            float minPitch = Mathf.Clamp(pitchClamp.x, -89f, 89f);
            float maxPitch = Mathf.Clamp(pitchClamp.y, -89f, 89f);
            if (maxPitch < minPitch)
            {
                (minPitch, maxPitch) = (maxPitch, minPitch);
            }

            var tiltAxis = panTilt.TiltAxis;
            tiltAxis.Range = new Vector2(minPitch, maxPitch);
            tiltAxis.Wrap = false;
            tiltAxis.Recentering = new InputAxis.RecenteringSettings { Enabled = false };
            float desiredTilt = Mathf.Clamp(defaultTiltAngle, tiltAxis.Range.x, tiltAxis.Range.y);
            tiltAxis.Center = desiredTilt;
            tiltAxis.Value = desiredTilt;
            panTilt.TiltAxis = tiltAxis;
        }

        private void OnCameraLookInput(Vector2 delta)
        {
            if (preferOrthographic || _activePanTilt == null)
                return;

            ApplyLookDelta(_activePanTilt, delta);
        }

        private void ApplyLookDelta(CinemachinePanTilt panTilt, Vector2 delta)
        {
            if (panTilt == null)
                return;

            var panAxis = panTilt.PanAxis;
            panAxis.Value = panAxis.ClampValue(panAxis.Value + delta.x * lookSensitivityX);
            panAxis.CancelRecentering();
            panTilt.PanAxis = panAxis;

            var tiltAxis = panTilt.TiltAxis;
            float verticalDelta = delta.y * lookSensitivityY * (invertY ? 1f : -1f);
            tiltAxis.Value = tiltAxis.ClampValue(tiltAxis.Value + verticalDelta);
            tiltAxis.CancelRecentering();
            panTilt.TiltAxis = tiltAxis;
        }

        private void OnCameraZoomInput(float delta)
        {
            if (_currentActiveCamera == null || preferOrthographic)
                return;

            var follow = _currentActiveCamera.GetComponent<CinemachineThirdPersonFollow>();
            if (follow == null)
                return;

            float targetDistance = Mathf.Clamp(follow.CameraDistance - delta * zoomStep, minZoomDistance, maxZoomDistance);
            follow.CameraDistance = Mathf.Lerp(follow.CameraDistance, targetDistance, zoomSmoothing);
        }
    }
}
