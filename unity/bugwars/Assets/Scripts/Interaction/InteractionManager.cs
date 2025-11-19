using UnityEngine;
using UnityEngine.InputSystem;
using R3;
using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace BugWars.Interaction
{
    /// <summary>
    /// Pure input handler for player interactions using R3 reactive raycast system
    /// Handles proximity detection, raycasting, and input → EntityActionManager
    /// Does NOT manage action state - that's EntityActionManager's job
    /// </summary>
    public class InteractionManager : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private float raycastDistance = 5f;
        [SerializeField] private float raycastRadius = 0.5f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private float raycastHeightOffset = 1.0f; // Height above player feet to start raycast

        [Header("Proximity Settings")]
        [SerializeField] private float proximityCheckRadius = 10f;
        [SerializeField] private float proximityCheckInterval = 0.5f;

        [Header("Input Settings")]
        [SerializeField] private Key interactKey = Key.E;

        // R3 Reactive properties - ONLY for raycast/targeting
        // Action state is managed by EntityActionManager (single source of truth)
        private readonly ReactiveProperty<InteractableObject> _currentTarget = new(null);

        // Observables for UI - only target, not state
        public ReadOnlyReactiveProperty<InteractableObject> CurrentTarget => _currentTarget;

        // Public accessor for player's EntityActionManager (UI subscribes to this directly)
        public BugWars.Entity.Actions.EntityActionManager PlayerEntityActionManager => playerActionManager;

        // Dependencies
        private Transform playerTransform;
        private Camera playerCamera;
        private BugWars.Entity.Actions.EntityActionManager playerActionManager;

        // Tracked interactables
        private HashSet<InteractableObject> nearbyInteractables = new();

        // Debug visualization
        private LineRenderer raycastLine;

        // Reusable buffer for proximity checks (avoids allocations)
        private Collider[] proximityBuffer = new Collider[32];

        /// <summary>
        /// Configure interaction settings programmatically and initialize the manager
        /// </summary>
        public void Configure(float distance, LayerMask layer)
        {
            raycastDistance = distance;
            interactableLayer = layer;
            Debug.Log($"[InteractionManager] Configured with distance: {distance}, layer: {layer.value}");
        }

        private async void Start()
        {
            // Initialize in Start() with UniTask to wait for player to spawn
            await InitializeAsync(this.GetCancellationTokenOnDestroy());
        }

        private async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            // Cache player reference
            GameObject playerObj = null;

            // Wait for player to spawn using UniTask.WaitUntil
            await UniTask.WaitUntil(() =>
            {
                playerObj = GameObject.FindGameObjectWithTag("Camera3D");
                return playerObj != null;
            }, cancellationToken: cancellationToken);

            // Player found!
            playerTransform = playerObj.transform;
            playerActionManager = playerObj.GetComponent<BugWars.Entity.Actions.EntityActionManager>();

            if (playerActionManager == null)
            {
                Debug.LogWarning("[InteractionManager] Player has no EntityActionManager! Adding component...");
                playerActionManager = playerObj.AddComponent<BugWars.Entity.Actions.EntityActionManager>();
            }

            // CRITICAL: Configure HarvestAction range to match raycast distance + buffer
            // This ensures UI prompts only show when harvesting will succeed
            // Wait one frame to ensure EntityActionManager.Awake() has created HarvestAction
            await UniTask.Yield();

            var harvestAction = playerActionManager.GetComponent<BugWars.Entity.Actions.HarvestAction>();
            if (harvestAction != null)
            {
                // Set harvest range slightly larger than raycast to account for object size
                harvestAction.SetHarvestRange(raycastDistance + 1f);
                Debug.Log($"[InteractionManager] Configured harvest range to: {raycastDistance + 1f}");
            }
            else
            {
                Debug.LogWarning("[InteractionManager] HarvestAction not found after EntityActionManager creation!");
            }

            Debug.Log($"[InteractionManager] Found player: {playerObj.name}");

            // Get camera
            playerCamera = Camera.main;

            if (playerCamera == null)
            {
                Debug.LogError("[InteractionManager] No main camera found!");
            }
            else
            {
                Debug.Log($"[InteractionManager] Found camera: {playerCamera.name}");
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
                .Where(_ => playerCamera != null && playerActionManager != null && !playerActionManager.IsPerformingAction.CurrentValue)
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
                .Where(_ => _currentTarget.Value != null && playerActionManager != null && !playerActionManager.IsPerformingAction.CurrentValue)
                .Subscribe(_ => StartInteraction())
                .AddTo(this);

            // Mouse click interaction
            Observable.EveryUpdate()
                .Where(_ => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                .Where(_ => _currentTarget.Value != null && playerActionManager != null && !playerActionManager.IsPerformingAction.CurrentValue)
                .Subscribe(_ => StartInteraction())
                .AddTo(this);
        }

        /// <summary>
        /// Perform raycast from player position (not camera) to detect interactable objects
        /// Uses player's forward direction from chest/center height for better ground object detection
        /// CRITICAL: Checks very close objects first (OverlapSphere) then distant objects (SphereCast)
        /// This prevents missing objects when player is standing inside/very close to them
        /// </summary>
        private InteractableObject PerformRaycast()
        {
            if (playerTransform == null || playerCamera == null)
                return null;

            // Raycast from player position at chest height, in camera forward direction
            Vector3 rayOrigin = playerTransform.position + Vector3.up * raycastHeightOffset;
            Vector3 rayDirection = playerCamera.transform.forward;
            Ray ray = new Ray(rayOrigin, rayDirection);

            // CRITICAL: First check for very close objects using OverlapSphere
            // SphereCast misses objects when the sphere starts inside them
            Collider[] nearbyColliders = Physics.OverlapSphere(rayOrigin, raycastRadius * 2f, interactableLayer);
            if (nearbyColliders.Length > 0)
            {
                // Find the closest one that's in front of the camera
                InteractableObject closestInFront = null;
                float closestDot = 0.5f; // Only consider objects somewhat in front (45 degree cone)

                foreach (var collider in nearbyColliders)
                {
                    Vector3 toObject = (collider.transform.position - rayOrigin).normalized;
                    float dot = Vector3.Dot(rayDirection, toObject);

                    if (dot > closestDot)
                    {
                        var interactable = collider.GetComponent<InteractableObject>();
                        if (interactable != null)
                        {
                            closestDot = dot;
                            closestInFront = interactable;
                        }
                    }
                }

                if (closestInFront != null)
                {
                    // Update visualization for close object
                    if (raycastLine != null)
                    {
                        Vector3 targetPos = closestInFront.transform.position;
                        raycastLine.SetPosition(0, ray.origin);
                        raycastLine.SetPosition(1, targetPos);
                        raycastLine.startColor = Color.yellow; // Yellow for close detection
                        raycastLine.endColor = Color.yellow;
                    }
                    return closestInFront;
                }
            }

            // No close objects - use SphereCast for distant objects
            if (Physics.SphereCast(ray, raycastRadius, out RaycastHit hit, raycastDistance, interactableLayer))
            {
                // Update visualization
                if (raycastLine != null)
                {
                    raycastLine.SetPosition(0, ray.origin);
                    raycastLine.SetPosition(1, ray.origin + ray.direction * hit.distance);
                    raycastLine.startColor = Color.green;
                    raycastLine.endColor = Color.green;
                }

                return hit.collider.GetComponent<InteractableObject>();
            }

            // No hit - show red line
            if (raycastLine != null)
            {
                raycastLine.SetPosition(0, ray.origin);
                raycastLine.SetPosition(1, ray.origin + ray.direction * raycastDistance);
                raycastLine.startColor = Color.red;
                raycastLine.endColor = Color.red;
            }

            return null;
        }

        /// <summary>
        /// Check for interactables within proximity range
        /// Updates their "player nearby" state
        /// Optimized with NonAlloc to reduce GC pressure
        /// </summary>
        private void CheckNearbyInteractables()
        {
            if (playerTransform == null)
                return;

            // Use NonAlloc with reusable buffer to avoid garbage collection
            int hitCount = Physics.OverlapSphereNonAlloc(
                playerTransform.position,
                proximityCheckRadius,
                proximityBuffer,
                interactableLayer
            );

            HashSet<InteractableObject> currentNearby = new();

            for (int i = 0; i < hitCount; i++)
            {
                var interactable = proximityBuffer[i].GetComponent<InteractableObject>();
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
        /// Pure input handler - triggers EntityActionManager to start harvest
        /// No state management here - that's EntityActionManager's job
        /// </summary>
        private void StartInteraction()
        {
            var target = _currentTarget.Value;
            if (target == null || playerTransform == null)
                return;

            // Check if target is already being interacted with (race condition guard)
            if (target.IsBeingInteracted.CurrentValue)
            {
                Debug.LogWarning($"[InteractionManager] {target.name} is already being interacted with!");
                return;
            }

            // Check if player is already performing an action (race condition guard)
            if (playerActionManager != null && playerActionManager.IsPerformingAction.CurrentValue)
            {
                Debug.LogWarning($"[InteractionManager] Player is already performing an action!");
                return;
            }

            Debug.Log($"[InteractionManager] Triggering harvest action on {target.name}");

            // Simple delegation - let EntityActionManager handle everything
            if (playerActionManager == null)
            {
                Debug.LogError("[InteractionManager] No EntityActionManager found! Cannot start interaction.");
                return;
            }

            // Just trigger the action - EntityActionManager owns the state
            playerActionManager.StartHarvest(target.gameObject);
        }

        private void OnDestroy()
        {
            _currentTarget?.Dispose();

            if (raycastLine != null)
            {
                Destroy(raycastLine.gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Only draw when this GameObject is selected in hierarchy
            // Avoids performance hit when not debugging
            if (playerTransform != null)
            {
                // Draw proximity check radius
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(playerTransform.position, proximityCheckRadius);
            }

            if (playerTransform != null && playerCamera != null)
            {
                // Use same raycast logic as PerformRaycast()
                Vector3 rayOrigin = playerTransform.position + Vector3.up * raycastHeightOffset;
                Vector3 rayDirection = playerCamera.transform.forward;
                Ray ray = new Ray(rayOrigin, rayDirection);

                // Single raycast check (optimized)
                bool hitInteractable = Physics.SphereCast(ray, raycastRadius, out RaycastHit hit, raycastDistance, interactableLayer);

                // Color based on hit
                Gizmos.color = hitInteractable ? Color.green : Color.red;

                // Draw main raycast line
                float lineLength = hitInteractable ? hit.distance : raycastDistance;
                Gizmos.DrawRay(ray.origin, ray.direction * lineLength);

                // Draw sphere at ray origin
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                Gizmos.DrawWireSphere(ray.origin, raycastRadius);

                // Draw hit point if we hit something
                if (hitInteractable)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(hit.point, 0.2f);
                    Gizmos.DrawWireSphere(ray.origin + ray.direction * hit.distance, raycastRadius);
                }

                // Draw current target outline
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
