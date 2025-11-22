using UnityEngine;
using R3;
using System;
using MessagePipe;

namespace BugWars.Interaction
{
    /// <summary>
    /// Base class for all interactable objects in the world (trees, rocks, bushes, etc.)
    /// Uses R3 reactive streams for clean event handling
    /// Uses MessagePipe for decoupled object pooling
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

        [Header("Server Sync (Phase 3)")]
        [SerializeField] private string objectId = ""; // Server-assigned unique ID for this object

        private readonly ReactiveProperty<bool> _isPlayerNearby = new(false);
        private readonly ReactiveProperty<bool> _isBeingInteracted = new(false);
        private readonly Subject<InteractionEvent> _onInteractionStarted = new();
        private readonly Subject<InteractionEvent> _onInteractionCompleted = new();
        private readonly Subject<Unit> _onDestroyed = new();

        private string _cachedPrompt;
        private static readonly string[] InteractionTypeStrings = { "chop", "mine", "harvest", "pickup", "open", "use" };

        private IPublisher<ObjectHarvestedMessage> _harvestedPublisher;
        private string _assetName;

        public ReadOnlyReactiveProperty<bool> IsPlayerNearby => _isPlayerNearby;
        public ReadOnlyReactiveProperty<bool> IsBeingInteracted => _isBeingInteracted;
        public Observable<InteractionEvent> OnInteractionStarted => _onInteractionStarted;
        public Observable<InteractionEvent> OnInteractionCompleted => _onInteractionCompleted;
        public Observable<Unit> OnDestroyed => _onDestroyed;

        public string InteractionPrompt => _cachedPrompt;
        public float InteractionDistance => interactionDistance;
        public InteractionType Type => interactionType;
        public ResourceType Resource => resourceType;
        public float HarvestTime => harvestTime;
        public string ObjectId => objectId;

        public void Configure(InteractionType type, ResourceType resource, int amount, float harvestDuration = 2f, string prompt = "Press E", string assetName = null)
        {
            interactionType = type;
            resourceType = resource;
            resourceAmount = amount;
            harvestTime = harvestDuration;
            interactionPrompt = prompt;
            _assetName = assetName;
            UpdateCachedPrompt();
        }

        public void SetMessagePublisher(IPublisher<ObjectHarvestedMessage> publisher)
        {
            _harvestedPublisher = publisher;
        }

        /// <summary>
        /// Set the server-assigned unique ID for this object (Phase 3)
        /// Called by EnvironmentManager when spawning from server data
        /// </summary>
        public void SetObjectId(string serverId)
        {
            objectId = serverId;
        }

        private void UpdateCachedPrompt()
        {
            _cachedPrompt = $"{interactionPrompt} to {InteractionTypeStrings[(int)interactionType]}";
        }

        private void Awake()
        {
            UpdateCachedPrompt();
        }

        private void Start()
        {
            _isPlayerNearby
                .Subscribe(nearby =>
                {
                    if (!nearby)
                    {
                        CancelInteraction();
                    }
                })
                .AddTo(this);
        }

        public void SetPlayerNearby(bool nearby)
        {
            _isPlayerNearby.Value = nearby;
        }

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

                    DestroyObject();

                    return result;
                });
        }

        public void CancelInteraction()
        {
            if (_isBeingInteracted.Value)
            {
                _isBeingInteracted.Value = false;
            }
        }

        public bool BeginInteraction(Transform interactor)
        {
            if (_isBeingInteracted.Value)
                return false;

            _isBeingInteracted.Value = true;

            var interactionEvent = new InteractionEvent
            {
                Interactor = interactor.gameObject,
                Target = gameObject,
                InteractionType = interactionType,
                StartTime = Time.time
            };

            _onInteractionStarted.OnNext(interactionEvent);
            return true;
        }

        public void EndInteraction()
        {
            if (_isBeingInteracted.Value)
            {
                _isBeingInteracted.Value = false;
            }
        }

        private void DestroyObject()
        {
            _onDestroyed.OnNext(Unit.Default);

            if (_harvestedPublisher != null)
            {
                var message = new ObjectHarvestedMessage(
                    gameObject,
                    _assetName ?? gameObject.name,
                    resourceType,
                    resourceAmount
                );
                _harvestedPublisher.Publish(message);
            }
            else
            {
                Debug.LogWarning($"[InteractableObject] No publisher set for {gameObject.name}, destroying instead of returning to pool");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            _isPlayerNearby?.Dispose();
            _isBeingInteracted?.Dispose();
            _onInteractionStarted?.Dispose();
            _onInteractionCompleted?.Dispose();
            _onDestroyed?.Dispose();
        }

        private void OnDrawGizmosSelected()
        {
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
