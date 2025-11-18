using UnityEngine;
using UnityEngine.InputSystem;
using R3;
using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace BugWars.Interaction
{
    /// <summary>
    /// Manages player interactions using R3 reactive raycast system
    /// Handles proximity detection, raycasting, and interaction prompts
    /// </summary>
    public class InteractionManager : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private float raycastDistance = 5f;
        [SerializeField] private float raycastRadius = 0.5f;
        [SerializeField] private LayerMask interactableLayer;

        [Header("Proximity Settings")]
        [SerializeField] private float proximityCheckRadius = 10f;
        [SerializeField] private float proximityCheckInterval = 0.5f;

        [Header("Input Settings")]
        [SerializeField] private Key interactKey = Key.E;

        // R3 Reactive properties
        private readonly ReactiveProperty<InteractableObject> _currentTarget = new(null);
        private readonly Subject<InteractionResult> _onInteractionCompleted = new();
        private readonly ReactiveProperty<bool> _isInteracting = new(false);

        // Observables for UI and other systems
        public ReadOnlyReactiveProperty<InteractableObject> CurrentTarget => _currentTarget;
        public Observable<InteractionResult> OnInteractionCompleted => _onInteractionCompleted;
        public ReadOnlyReactiveProperty<bool> IsInteracting => _isInteracting;

        // Dependencies
        private Transform playerTransform;
        private Camera playerCamera;

        // Tracked interactables
        private HashSet<InteractableObject> nearbyInteractables = new();

        // Debug visualization
        private LineRenderer raycastLine;

        /// <summary>
        /// Configure interaction settings programmatically and initialize the manager
        /// </summary>
        public void Configure(float distance, LayerMask layer)
        {
            raycastDistance = distance;
            interactableLayer = layer;
            Debug.Log($"[InteractionManager] Configured with distance: {distance}, layer: {layer.value}");

            // Initialize immediately after configuration
            Initialize();
        }

        private void Initialize()
        {
            // Find player (TODO: Inject via VContainer when player system is ready)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (playerTransform == null)
            {
                playerCamera = Camera.main;
            }
            else
            {
                playerCamera = Camera.main;
            }

            // Create LineRenderer for runtime raycast visualization
            CreateRaycastVisualizer();

            SetupRaycastStream();
            SetupProximityStream();
            SetupInputStream();

            Debug.Log($"[InteractionManager] Initialized - Layer mask: {interactableLayer.value}, Raycast distance: {raycastDistance}");

            // Count interactables without spamming logs
            var allInteractables = FindObjectsByType<InteractableObject>(FindObjectsSortMode.None);
            if (allInteractables.Length > 0)
            {
                Debug.Log($"[InteractionManager] Found {allInteractables.Length} interactable objects in scene");
            }
            else
            {
                Debug.LogWarning("[InteractionManager] No InteractableObject components found in scene! Make sure environment objects have been spawned.");
            }
        }

        private void CreateRaycastVisualizer()
        {
            GameObject lineObj = new GameObject("RaycastVisualizer");
            lineObj.transform.SetParent(transform);
            raycastLine = lineObj.AddComponent<LineRenderer>();

            // Configure LineRenderer for visibility
            raycastLine.material = new Material(Shader.Find("Sprites/Default"));
            raycastLine.startWidth = 0.02f;
            raycastLine.endWidth = 0.02f;
            raycastLine.positionCount = 2;
            raycastLine.useWorldSpace = true;
        }

        /// <summary>
        /// R3 Raycast stream - fires every frame when player is looking at an interactable
        /// </summary>
        private void SetupRaycastStream()
        {
            Observable.EveryUpdate()
                .Where(_ => playerCamera != null && !_isInteracting.Value)
                .Select(_ => PerformRaycast())
                .DistinctUntilChanged() // Only trigger when target changes
                .Subscribe(hit =>
                {
                    _currentTarget.Value = hit;
                })
                .AddTo(this);
        }

        /// <summary>
        /// R3 Proximity stream - checks for nearby interactables periodically
        /// More efficient than checking every frame
        /// </summary>
        private void SetupProximityStream()
        {
            Observable.Interval(TimeSpan.FromSeconds(proximityCheckInterval))
                .Where(_ => playerTransform != null)
                .Subscribe(_ => CheckNearbyInteractables())
                .AddTo(this);
        }

        /// <summary>
        /// R3 Input stream - handles interaction key presses reactively
        /// Uses new Input System instead of legacy Input
        /// </summary>
        private void SetupInputStream()
        {
            // E key interaction
            Observable.EveryUpdate()
                .Where(_ => Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
                .Where(_ => _currentTarget.Value != null && !_isInteracting.Value)
                .Subscribe(_ => StartInteraction())
                .AddTo(this);

            // Mouse click interaction
            Observable.EveryUpdate()
                .Where(_ => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                .Where(_ => _currentTarget.Value != null && !_isInteracting.Value)
                .Subscribe(_ => StartInteraction())
                .AddTo(this);
        }

        /// <summary>
        /// Perform raycast from camera to detect interactable objects
        /// </summary>
        private InteractableObject PerformRaycast()
        {
            if (playerCamera == null)
                return null;

            Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

            Color lineColor = Color.red;
            float lineLength = raycastDistance;
            InteractableObject result = null;

            // Check for interactable hit
            if (Physics.SphereCast(ray, raycastRadius, out RaycastHit interactableHit, raycastDistance, interactableLayer))
            {
                lineColor = Color.green;
                lineLength = interactableHit.distance;

                var interactable = interactableHit.collider.GetComponent<InteractableObject>();
                if (interactable != null)
                {
                    result = interactable;
                }
            }
            else if (Physics.SphereCast(ray, raycastRadius, out RaycastHit anyHit, raycastDistance))
            {
                // Yellow = hit something but not interactable
                lineColor = Color.yellow;
                lineLength = anyHit.distance;
            }

            // Update LineRenderer visualization (visible in Game view!)
            if (raycastLine != null)
            {
                raycastLine.SetPosition(0, ray.origin);
                raycastLine.SetPosition(1, ray.origin + ray.direction * lineLength);
                raycastLine.startColor = lineColor;
                raycastLine.endColor = lineColor;
            }

            return result;
        }

        /// <summary>
        /// Check for interactables within proximity range
        /// Updates their "player nearby" state
        /// </summary>
        private void CheckNearbyInteractables()
        {
            if (playerTransform == null)
                return;

            // Find all interactables in range
            Collider[] colliders = Physics.OverlapSphere(
                playerTransform.position,
                proximityCheckRadius,
                interactableLayer
            );

            HashSet<InteractableObject> currentNearby = new();

            foreach (var collider in colliders)
            {
                var interactable = collider.GetComponent<InteractableObject>();
                if (interactable != null)
                {
                    currentNearby.Add(interactable);

                    // Notify if newly in range
                    if (!nearbyInteractables.Contains(interactable))
                    {
                        interactable.SetPlayerNearby(true);
                    }
                }
            }

            // Notify objects that player left range
            foreach (var oldInteractable in nearbyInteractables)
            {
                if (oldInteractable != null && !currentNearby.Contains(oldInteractable))
                {
                    oldInteractable.SetPlayerNearby(false);
                }
            }

            nearbyInteractables = currentNearby;
        }

        /// <summary>
        /// Start interaction with current target using R3 observable
        /// </summary>
        private void StartInteraction()
        {
            var target = _currentTarget.Value;
            if (target == null || playerTransform == null)
                return;

            _isInteracting.Value = true;

            Debug.Log($"[InteractionManager] Starting interaction with {target.name}");

            // Subscribe to interaction observable
            target.Interact(playerTransform)
                .Subscribe(result =>
                {
                    OnInteractionComplete(result);
                    OnInteractionFinished();
                })
                .AddTo(this);
        }

        /// <summary>
        /// Called when interaction completes successfully
        /// </summary>
        private void OnInteractionComplete(InteractionResult result)
        {
            Debug.Log($"[InteractionManager] Interaction complete! Received {result.ResourceAmount}x {result.ResourceType}");
            _onInteractionCompleted.OnNext(result);
        }

        /// <summary>
        /// Called when interaction finishes (success or failure)
        /// </summary>
        private void OnInteractionFinished()
        {
            _isInteracting.Value = false;
            _currentTarget.Value = null;
        }

        /// <summary>
        /// Cancel current interaction (e.g., player moved away)
        /// </summary>
        public void CancelInteraction()
        {
            if (_isInteracting.Value && _currentTarget.Value != null)
            {
                _currentTarget.Value.CancelInteraction();
                OnInteractionFinished();
            }
        }

        private void OnDestroy()
        {
            _currentTarget?.Dispose();
            _onInteractionCompleted?.Dispose();
            _isInteracting?.Dispose();

            if (raycastLine != null)
            {
                Destroy(raycastLine.gameObject);
            }
        }

        private void OnDrawGizmos()
        {
            if (playerTransform != null)
            {
                // Draw proximity check radius
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(playerTransform.position, proximityCheckRadius);
            }

            if (playerCamera != null)
            {
                // Draw raycast line and sphere cast visualization
                Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

                // Test raycast to determine color
                bool hitAnything = Physics.SphereCast(ray, raycastRadius, out RaycastHit anyHit, raycastDistance);
                bool hitInteractable = Physics.SphereCast(ray, raycastRadius, out RaycastHit interactableHit, raycastDistance, interactableLayer);

                // Color coding:
                // Green = hit interactable on Layer 8
                // Yellow = hit something but not on interactable layer
                // Red = hit nothing
                if (hitInteractable)
                {
                    Gizmos.color = Color.green;
                }
                else if (hitAnything)
                {
                    Gizmos.color = Color.yellow;
                }
                else
                {
                    Gizmos.color = Color.red;
                }

                // Draw main raycast line
                Gizmos.DrawRay(ray.origin, ray.direction * raycastDistance);

                // Draw sphere at ray origin to show SphereCast radius
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f); // Semi-transparent white
                Gizmos.DrawWireSphere(ray.origin, raycastRadius);

                // Draw sphere at raycast end point
                Vector3 endPoint = ray.origin + ray.direction * raycastDistance;
                Gizmos.DrawWireSphere(endPoint, raycastRadius);

                // If we hit something, draw hit point and info
                if (hitInteractable)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(interactableHit.point, 0.2f);
                    Gizmos.DrawLine(interactableHit.point, interactableHit.point + interactableHit.normal * 1f);

                    // Draw sphere at hit distance to show where SphereCast detected
                    Gizmos.color = new Color(0f, 1f, 0f, 0.5f); // Semi-transparent green
                    Gizmos.DrawWireSphere(ray.origin + ray.direction * interactableHit.distance, raycastRadius);
                }
                else if (hitAnything)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(anyHit.point, 0.2f);
                    Gizmos.DrawLine(anyHit.point, anyHit.point + anyHit.normal * 1f);

                    // Draw sphere at hit distance
                    Gizmos.color = new Color(1f, 1f, 0f, 0.5f); // Semi-transparent yellow
                    Gizmos.DrawWireSphere(ray.origin + ray.direction * anyHit.distance, raycastRadius);
                }

                // Draw current target outline if we have one
                if (_currentTarget.Value != null)
                {
                    Gizmos.color = Color.cyan;
                    Bounds bounds = _currentTarget.Value.GetComponent<Collider>()?.bounds ?? default;
                    if (bounds != default)
                    {
                        Gizmos.DrawWireCube(bounds.center, bounds.size);
                    }
                }
            }
        }
    }
}
