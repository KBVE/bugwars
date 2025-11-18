using UnityEngine;
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
    public class InteractionManager : MonoBehaviour, IStartable
    {
        [Header("Raycast Settings")]
        [SerializeField] private float raycastDistance = 5f;
        [SerializeField] private float raycastRadius = 0.5f;
        [SerializeField] private LayerMask interactableLayer;

        [Header("Proximity Settings")]
        [SerializeField] private float proximityCheckRadius = 10f;
        [SerializeField] private float proximityCheckInterval = 0.5f;

        [Header("Input Settings")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

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

        void IStartable.Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Find player (TODO: Inject via VContainer when player system is ready)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (playerTransform == null)
            {
                Debug.LogWarning("[InteractionManager] Player not found! Using main camera instead.");
                playerCamera = Camera.main;
            }
            else
            {
                playerCamera = Camera.main;
            }

            SetupRaycastStream();
            SetupProximityStream();
            SetupInputStream();
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
        /// </summary>
        private void SetupInputStream()
        {
            // Detect E key press
            Observable.EveryUpdate()
                .Where(_ => Input.GetKeyDown(interactKey))
                .Where(_ => _currentTarget.Value != null && !_isInteracting.Value)
                .Subscribe(_ => StartInteraction())
                .AddTo(this);

            // Alternative: Mouse click interaction
            Observable.EveryUpdate()
                .Where(_ => Input.GetMouseButtonDown(0)) // Left click
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

            // Use SphereCast for more forgiving targeting
            if (Physics.SphereCast(ray, raycastRadius, out RaycastHit hit, raycastDistance, interactableLayer))
            {
                var interactable = hit.collider.GetComponent<InteractableObject>();
                if (interactable != null)
                {
                    return interactable;
                }
            }

            return null;
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
                // Draw raycast line
                Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
                Gizmos.color = _currentTarget.Value != null ? Color.green : Color.red;
                Gizmos.DrawRay(ray.origin, ray.direction * raycastDistance);
            }
        }
    }
}
