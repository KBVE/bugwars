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

        [Header("Raycast Settings - Performance")]
        [SerializeField] private float raycastCheckInterval = 0.33f; // 3 checks per second

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
        private HashSet<InteractableObject> tempNearbySet = new(); // Reusable set for proximity checks

        // Reusable buffers to avoid per-frame/periodic allocations
        private readonly Collider[] proximityBuffer = new Collider[32];
        private readonly Collider[] closeRaycastBuffer = new Collider[16]; // Buffer for close-range raycast checks

        // Component cache to avoid repeated GetComponent calls
        private readonly Dictionary<Collider, InteractableObject> componentCache = new();

        // Cached values to avoid per-call allocations
        private Vector3 cachedHeightOffset;

        /// <summary>
        /// Configure interaction settings programmatically and initialize the manager
        /// </summary>
        public void Configure(float distance, LayerMask layer)
        {
            raycastDistance = distance;
            interactableLayer = layer;
            //Debug.Log($"[InteractionManager] Configured with distance: {distance}, layer: {layer.value}");
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
            await UniTask.Yield(cancellationToken);

            var harvestAction = playerActionManager.GetComponent<BugWars.Entity.Actions.HarvestAction>();
            if (harvestAction != null)
            {
                float newRange = raycastDistance + 1f;
                harvestAction.SetHarvestRange(newRange);
            }
            else
            {
                Debug.LogWarning("[InteractionManager] HarvestAction not found after EntityActionManager creation!");
            }

            // Get camera
            playerCamera = Camera.main;

            if (playerCamera == null)
            {
                Debug.LogError("[InteractionManager] No main camera found!");
            }

            // Cache height offset vector to avoid repeated allocation
            cachedHeightOffset = Vector3.up * raycastHeightOffset;

            SetupRaycastStream();
            SetupProximityStream();
            SetupInputStream();
        }

        /// <summary>
        /// R3 Raycast stream - checks for interactables at fixed interval for WebGL performance
        /// Optimized from EveryUpdate to reduce CPU overhead
        /// </summary>
        private void SetupRaycastStream()
        {
            Observable.Interval(TimeSpan.FromSeconds(raycastCheckInterval))
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
        /// Combined stream for E key and mouse click to reduce overhead
        /// </summary>
        private void SetupInputStream()
        {
            Observable.EveryUpdate()
                .Where(_ =>
                {
                    bool keyPressed = Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame;
                    bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                    return keyPressed || mousePressed;
                })
                .Where(_ => _currentTarget.Value != null && playerActionManager != null && !playerActionManager.IsPerformingAction.CurrentValue)
                .Subscribe(_ => StartInteraction())
                .AddTo(this);
        }

        /// <summary>
        /// Perform raycast from player position (not camera) to detect interactable objects
        /// Uses player's forward direction from chest/center height for better ground object detection
        /// CRITICAL: Checks very close objects first (OverlapSphere) then distant objects (SphereCast)
        /// This prevents missing objects when player is standing inside/very close to them
        /// Optimized with NonAlloc, component caching, and vector reuse to eliminate allocations
        /// </summary>
        private InteractableObject PerformRaycast()
        {
            if (playerTransform == null || playerCamera == null)
                return null;

            Vector3 rayOrigin = playerTransform.position + cachedHeightOffset;
            Vector3 rayDirection = playerCamera.transform.forward;

            // CRITICAL: First check for very close objects using OverlapSphereNonAlloc (zero allocation)
            // Ignore triggers for better performance
            int closeHitCount = Physics.OverlapSphereNonAlloc(
                rayOrigin,
                raycastRadius * 2f,
                closeRaycastBuffer,
                interactableLayer,
                QueryTriggerInteraction.Ignore
            );

            if (closeHitCount >= closeRaycastBuffer.Length)
            {
                Debug.LogWarning($"[InteractionManager] Close raycast buffer overflow! Found {closeHitCount}+ objects, buffer size is {closeRaycastBuffer.Length}. Some objects may be missed.");
            }

            if (closeHitCount > 0)
            {
                InteractableObject closestInFront = null;
                float closestDot = 0.5f; // Only consider objects somewhat in front (45 degree cone)

                for (int i = 0; i < closeHitCount; i++)
                {
                    var collider = closeRaycastBuffer[i];
                    if (collider == null) continue;

                    // Use unnormalized vector for dot product (avoids sqrt)
                    Vector3 toObject = collider.transform.position - rayOrigin;
                    float sqrMag = toObject.sqrMagnitude;

                    // Early skip if too far
                    if (sqrMag > raycastDistance * raycastDistance) continue;

                    // Normalize manually only when needed
                    float dot = Vector3.Dot(rayDirection, toObject) / Mathf.Sqrt(sqrMag);

                    if (dot > closestDot)
                    {
                        var interactable = GetCachedComponent(collider);
                        if (interactable != null)
                        {
                            closestDot = dot;
                            closestInFront = interactable;
                        }
                    }
                }

                if (closestInFront != null)
                    return closestInFront;
            }

            // No close objects - use SphereCast for distant objects
            if (Physics.SphereCast(rayOrigin, raycastRadius, rayDirection, out RaycastHit hit, raycastDistance, interactableLayer, QueryTriggerInteraction.Ignore))
            {
                return GetCachedComponent(hit.collider);
            }

            return null;
        }

        /// <summary>
        /// Get InteractableObject component from collider with caching to avoid repeated GetComponent calls
        /// </summary>
        private InteractableObject GetCachedComponent(Collider collider)
        {
            if (collider == null)
                return null;

            if (!componentCache.TryGetValue(collider, out var interactable))
            {
                interactable = collider.GetComponent<InteractableObject>();
                if (interactable != null)
                {
                    componentCache[collider] = interactable;
                }
            }

            return interactable;
        }

        /// <summary>
        /// Check for interactables within proximity range
        /// Updates their "player nearby" state
        /// Optimized with NonAlloc and reusable HashSet to eliminate allocations
        /// </summary>
        private void CheckNearbyInteractables()
        {
            if (playerTransform == null)
            {
                Debug.LogWarning("[InteractionManager] CheckNearbyInteractables called with null playerTransform!");
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                playerTransform.position,
                proximityCheckRadius,
                proximityBuffer,
                interactableLayer
            );

            if (hitCount >= proximityBuffer.Length)
            {
                Debug.LogWarning($"[InteractionManager] Proximity buffer overflow! Found {hitCount}+ objects, buffer size is {proximityBuffer.Length}. Consider increasing buffer size.");
            }

            // Reuse temp set instead of allocating new one
            tempNearbySet.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                var collider = proximityBuffer[i];
                if (collider == null) continue;

                var interactable = GetCachedComponent(collider);
                if (interactable != null)
                {
                    tempNearbySet.Add(interactable);

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
                if (oldInteractable != null && !tempNearbySet.Contains(oldInteractable))
                {
                    oldInteractable.SetPlayerNearby(false);
                }
            }

            // Swap sets instead of allocating new one
            (nearbyInteractables, tempNearbySet) = (tempNearbySet, nearbyInteractables);
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
            componentCache?.Clear();
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
