using UnityEngine;
using R3;
using System;

namespace BugWars.Interaction
{
    /// <summary>
    /// Base class for all interactable objects in the world (trees, rocks, bushes, etc.)
    /// Uses R3 reactive streams for clean event handling
    /// </summary>
    public class InteractableObject : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private string interactionPrompt = "Press E";
        [SerializeField] private float interactionDistance = 3f;
        [SerializeField] private InteractionType interactionType = InteractionType.Chop;

        [Header("Resource Settings")]
        [SerializeField] private ResourceType resourceType = ResourceType.Wood;
        [SerializeField] private int resourceAmount = 5;
        [SerializeField] private float harvestTime = 2f;

        // R3 Reactive properties
        private readonly ReactiveProperty<bool> _isPlayerNearby = new(false);
        private readonly ReactiveProperty<bool> _isBeingInteracted = new(false);
        private readonly Subject<InteractionEvent> _onInteractionStarted = new();
        private readonly Subject<InteractionEvent> _onInteractionCompleted = new();
        private readonly Subject<Unit> _onDestroyed = new();

        // Observables exposed to other systems
        public ReadOnlyReactiveProperty<bool> IsPlayerNearby => _isPlayerNearby;
        public ReadOnlyReactiveProperty<bool> IsBeingInteracted => _isBeingInteracted;
        public Observable<InteractionEvent> OnInteractionStarted => _onInteractionStarted;
        public Observable<InteractionEvent> OnInteractionCompleted => _onInteractionCompleted;
        public Observable<Unit> OnDestroyed => _onDestroyed;

        // Public properties
        public string InteractionPrompt => $"{interactionPrompt} to {interactionType.ToString().ToLower()}";
        public float InteractionDistance => interactionDistance;
        public InteractionType Type => interactionType;
        public ResourceType Resource => resourceType;

        private void Start()
        {
            // Example: Log when player gets nearby (can be used for UI, audio, etc.)
            _isPlayerNearby
                .Where(nearby => nearby)
                .Subscribe(_ => OnPlayerEnterRange())
                .AddTo(this);

            _isPlayerNearby
                .Where(nearby => !nearby)
                .Subscribe(_ => OnPlayerExitRange())
                .AddTo(this);
        }

        /// <summary>
        /// Called by InteractionManager via raycast or proximity detection
        /// </summary>
        public void SetPlayerNearby(bool nearby)
        {
            _isPlayerNearby.Value = nearby;
        }

        /// <summary>
        /// Start interaction (e.g., player pressed E)
        /// Returns an observable that completes when interaction is done
        /// </summary>
        public Observable<InteractionResult> Interact(Transform player)
        {
            if (_isBeingInteracted.Value)
                return Observable.Empty<InteractionResult>();

            _isBeingInteracted.Value = true;

            var interactionEvent = new InteractionEvent
            {
                Interactor = player.gameObject,
                Target = gameObject,
                InteractionType = interactionType,
                StartTime = Time.time
            };

            _onInteractionStarted.OnNext(interactionEvent);

            // Simulate harvest time with R3's timer
            return Observable.Timer(TimeSpan.FromSeconds(harvestTime))
                .Select(_ =>
                {
                    var result = new InteractionResult
                    {
                        Success = true,
                        ResourceType = resourceType,
                        ResourceAmount = resourceAmount,
                        InteractionType = interactionType
                    };

                    _onInteractionCompleted.OnNext(interactionEvent);
                    _isBeingInteracted.Value = false;

                    // Destroy object after harvesting
                    DestroyObject();

                    return result;
                });
        }

        /// <summary>
        /// Cancel ongoing interaction
        /// </summary>
        public void CancelInteraction()
        {
            if (_isBeingInteracted.Value)
            {
                _isBeingInteracted.Value = false;
                Debug.Log($"[InteractableObject] Interaction cancelled: {gameObject.name}");
            }
        }

        private void OnPlayerEnterRange()
        {
            Debug.Log($"[InteractableObject] Player nearby: {gameObject.name}");
        }

        private void OnPlayerExitRange()
        {
            Debug.Log($"[InteractableObject] Player left: {gameObject.name}");
            CancelInteraction();
        }

        private void DestroyObject()
        {
            _onDestroyed.OnNext(Unit.Default);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Cleanup R3 subscriptions
            _isPlayerNearby?.Dispose();
            _isBeingInteracted?.Dispose();
            _onInteractionStarted?.Dispose();
            _onInteractionCompleted?.Dispose();
            _onDestroyed?.Dispose();
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize interaction range in editor
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);
        }
    }

    public enum InteractionType
    {
        Chop,
        Mine,
        Harvest,
        Pickup,
        Open,
        Use
    }

    public enum ResourceType
    {
        Wood,
        Stone,
        Berries,
        Herbs,
        None
    }

    public struct InteractionEvent
    {
        public GameObject Interactor;
        public GameObject Target;
        public InteractionType InteractionType;
        public float StartTime;
    }

    public struct InteractionResult
    {
        public bool Success;
        public ResourceType ResourceType;
        public int ResourceAmount;
        public InteractionType InteractionType;
    }
}
