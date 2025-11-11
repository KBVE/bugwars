using UnityEngine;
using VContainer;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Linq;

namespace BugWars.Core
{
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
            // Subscribe to camera control events
            CameraEvents.OnCameraFollowRequested += HandleCameraFollowRequest;
            CameraEvents.OnCameraStopFollowRequested += HandleCameraStopFollowRequest;

            if (debugMode)
                Debug.Log("[CameraManager] Subscribed to CameraEvents");
        }

        private void UnsubscribeFromEvents()
        {
            // Unsubscribe from camera control events
            CameraEvents.OnCameraFollowRequested -= HandleCameraFollowRequest;
            CameraEvents.OnCameraStopFollowRequested -= HandleCameraStopFollowRequest;

            if (debugMode)
                Debug.Log("[CameraManager] Unsubscribed from CameraEvents");
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
        /// Configures Cinemachine 3 third-person follow component with offset settings
        /// Automatically adds missing components at runtime
        /// </summary>
        private void ConfigureThirdPersonFollow(CinemachineCamera camera, CameraFollowConfig config)
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
