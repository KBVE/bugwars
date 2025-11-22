using UnityEngine;
using BugWars.Interaction;
using MessagePipe;
using VContainer;
using Cysharp.Threading.Tasks;

namespace BugWars.Entity.Actions
{
    /// <summary>
    /// Harvest action for gathering resources from interactable objects
    /// Works with trees, rocks, bushes, etc.
    /// Supports both Players and NPCs
    /// Server-authoritative: Sends harvest request to server and waits for validation
    /// </summary>
    public class HarvestAction : EntityAction
    {
        [Header("Harvest Settings")]
        [SerializeField] private float harvestRange = 6f; // Slightly larger than raycast (5f) to account for object size and prevent edge cases
        [SerializeField] private bool destroyTargetOnComplete = true;

        // Harvest-specific data
        private InteractableObject interactableTarget;

        // MessagePipe publisher for resource harvesting events
        private IPublisher<ResourceHarvestedMessage> resourcePublisher;

        // Server-authoritative network sync
        [Inject] private BugWars.Network.EnvironmentNetworkSync _environmentNetworkSync;

        /// <summary>
        /// Public setter to configure harvest range at runtime
        /// Allows InteractionManager to sync with its raycast distance
        /// </summary>
        public void SetHarvestRange(float range)
        {
            harvestRange = range;
            if (showDebugLogs)
                Debug.Log($"[HarvestAction] Harvest range set to: {range}");
        }

        /// <summary>
        /// Set MessagePipe publisher for resource harvesting events
        /// Called by EntityActionManager during initialization
        /// </summary>
        public void SetResourcePublisher(IPublisher<ResourceHarvestedMessage> publisher)
        {
            resourcePublisher = publisher;
        }
        private ResourceType resourceType;
        private int resourceAmount;

        /// <summary>
        /// Initialize harvest action with specific duration from interactable
        /// </summary>
        public void ConfigureFromInteractable(InteractableObject interactable)
        {
            if (interactable == null)
                return;

            interactableTarget = interactable;
            actionDuration = interactable.HarvestTime;
            resourceType = interactable.Resource;
            actionName = $"Harvest {interactable.Type}";

            if (showDebugLogs)
                Debug.Log($"[HarvestAction] Configured: {actionName}, duration: {actionDuration}s");
        }

        protected override bool OnActionStart()
        {
            // Validate target
            if (target == null)
            {
                if (showDebugLogs)
                    Debug.LogError("[HarvestAction] Target is null!");
                return false;
            }

            // CRITICAL: ALWAYS get and configure from the current target
            // This ensures we use the correct harvest time for THIS specific object
            interactableTarget = target.GetComponent<InteractableObject>();
            if (interactableTarget == null)
            {
                if (showDebugLogs)
                    Debug.LogError($"[HarvestAction] Target {target.name} has no InteractableObject component!");
                return false;
            }

            // Configure action duration from this specific interactable
            ConfigureFromInteractable(interactableTarget);

            if (showDebugLogs)
                Debug.Log($"[HarvestAction] Action duration set to: {actionDuration}s for {target.name}");

            // Check range
            if (!IsInRange())
            {
                float distance = Vector3.Distance(executingEntity.transform.position, target.transform.position);
                Debug.LogWarning($"[HarvestAction] Target {target.name} is out of range! (distance: {distance:F2}, harvestRange: {harvestRange:F2})");
                return false;
            }

            // Log the configured range at start (always, for debugging)
            Debug.Log($"[HarvestAction] Starting harvest with range: {harvestRange:F2}");

            // CRITICAL: Use BeginInteraction() to properly set the _isBeingInteracted state
            // This prevents race conditions where multiple entities try to harvest the same object
            if (!interactableTarget.BeginInteraction(executingEntity.transform))
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[HarvestAction] Failed to begin interaction with {target.name} - already being interacted with!");
                return false;
            }

            // Notify interactable that harvesting started
            // This could trigger animations, particles, etc.
            if (showDebugLogs)
                Debug.Log($"[HarvestAction] {executingEntity.name} started harvesting {target.name}");

            return true;
        }

        protected override void OnProgressUpdate(float progress)
        {
            // CRITICAL: Don't check target if action is completing/completed
            // This prevents race condition warning when object is destroyed on completion
            if (State.CurrentValue != ActionState.InProgress)
                return;

            // CRITICAL: Cancel if target no longer exists (destroyed by another player/NPC)
            if (target == null)
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[HarvestAction] Target destroyed during harvest - cancelling");
                Cancel();
                return;
            }

            // CRITICAL: Cancel if player moved out of range
            bool inRange = IsInRange();
            if (!inRange)
            {
                float distance = Vector3.Distance(executingEntity.transform.position, target.transform.position);
                Debug.LogWarning($"[HarvestAction] Player moved out of range - cancelling harvest (distance: {distance:F2}, harvestRange: {harvestRange:F2})");
                Cancel();
                return;
            }

            // Optional: Update animations, particles, sounds based on progress
            // For now, just log progress milestones
            if (showDebugLogs && progress >= 0.5f && progress < 0.51f)
            {
                Debug.Log($"[HarvestAction] Harvest 50% complete");
            }
        }

        protected override ActionResult OnActionComplete()
        {
            // PHASE 3: Server-Authoritative Harvest
            // Send harvest request to server, wait for validation before giving resources

            // SAFETY CHECK: Verify target still exists at completion
            if (target == null || interactableTarget == null)
            {
                if (showDebugLogs)
                    Debug.LogError($"[HarvestAction] Target was destroyed before completion - no reward given");

                // End interaction to release lock
                if (interactableTarget != null)
                {
                    interactableTarget.EndInteraction();
                    interactableTarget = null;
                }

                return new ActionResult
                {
                    Success = false,
                    Message = "Target no longer exists",
                    Data = null
                };
            }

            // SAFETY CHECK: Verify still in range at completion
            if (!IsInRange())
            {
                if (showDebugLogs)
                    Debug.LogError($"[HarvestAction] Player moved out of range before completion - no reward given");

                // End interaction to release lock
                if (interactableTarget != null)
                {
                    interactableTarget.EndInteraction();
                    interactableTarget = null;
                }

                return new ActionResult
                {
                    Success = false,
                    Message = "Moved out of range",
                    Data = null
                };
            }

            // Cache target data before async call
            string objectId = interactableTarget.ObjectId;
            GameObject harvestedObject = target;
            Vector3 harvestPosition = target.transform.position;
            ResourceType harvestResourceType = interactableTarget.Resource;

            // Check if server sync is available
            if (_environmentNetworkSync == null)
            {
                Debug.LogWarning("[HarvestAction] No EnvironmentNetworkSync available - falling back to local harvest (Phase 2 mode)");
                // Fall back to local harvest for testing without server
                return OnActionComplete_Local();
            }

            // Check if object has a server ID
            if (string.IsNullOrEmpty(objectId))
            {
                Debug.LogWarning($"[HarvestAction] Object {target.name} has no server ID - falling back to local harvest");
                return OnActionComplete_Local();
            }

            // Send harvest request to server (async, fire-and-forget)
            // The server will validate and send back a response
            RequestHarvestFromServer(objectId, harvestedObject, harvestPosition, harvestResourceType).Forget();

            // Return optimistic success (object will be despawned when server confirms)
            // NOTE: If server rejects, the object will respawn (handled by EnvironmentNetworkSync)
            ActionResult result = new ActionResult
            {
                Success = true,
                Message = $"Harvesting {harvestResourceType}...",
                Data = new HarvestResult
                {
                    ResourceType = harvestResourceType,
                    Amount = 0, // Server will provide actual amount
                    HarvestedObject = harvestedObject
                }
            };

            return result;
        }

        /// <summary>
        /// Send harvest request to server and handle response
        /// </summary>
        private async UniTaskVoid RequestHarvestFromServer(string objectId, GameObject harvestedObject, Vector3 harvestPosition, ResourceType harvestResourceType)
        {
            if (showDebugLogs)
                Debug.Log($"[HarvestAction] Sending harvest request to server for object {objectId}");

            try
            {
                // Send request and wait for response
                var response = await _environmentNetworkSync.RequestHarvestAsync(objectId);

                if (response.success)
                {
                    if (showDebugLogs)
                        Debug.Log($"[HarvestAction] Server approved harvest: {response.resourceAmount}x {response.resourceType}");

                    // Server approved - publish resource message for inventory
                    if (resourcePublisher != null)
                    {
                        var message = new ResourceHarvestedMessage(
                            executingEntity.gameObject,
                            harvestedObject,
                            response.resourceType,
                            response.resourceAmount,
                            harvestPosition
                        );
                        resourcePublisher.Publish(message);
                    }

                    // Object will be despawned by EnvironmentNetworkSync.OnHarvestResult
                }
                else
                {
                    Debug.LogWarning($"[HarvestAction] Server rejected harvest: {response.errorMessage}");
                    // Server rejected - object should respawn if it was despawned
                    // TODO: Show error message to player
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HarvestAction] Error requesting harvest from server: {e.Message}");
            }
            finally
            {
                // End interaction to release lock
                if (interactableTarget != null)
                {
                    interactableTarget.EndInteraction();
                    interactableTarget = null;
                }
            }
        }

        /// <summary>
        /// Local harvest fallback (Phase 2 compatibility mode)
        /// Used when server sync is not available or object has no server ID
        /// </summary>
        private ActionResult OnActionComplete_Local()
        {
            // Calculate resources gained
            resourceAmount = Random.Range(
                interactableTarget != null ? Mathf.Max(1, (int)(interactableTarget.Resource == ResourceType.Wood ? 5 : 3)) : 1,
                interactableTarget != null ? Mathf.Max(2, (int)(interactableTarget.Resource == ResourceType.Wood ? 8 : 6)) : 2
            );

            // Cache target reference and position before destroying
            GameObject harvestedObject = target;
            Vector3 harvestPosition = target.transform.position;

            // End interaction to release the lock
            if (interactableTarget != null)
            {
                interactableTarget.EndInteraction();
            }

            // Publish resource harvested message
            if (resourcePublisher != null)
            {
                var message = new ResourceHarvestedMessage(
                    executingEntity.gameObject,
                    harvestedObject,
                    resourceType,
                    resourceAmount,
                    harvestPosition
                );
                resourcePublisher.Publish(message);
            }

            // Destroy target locally
            if (destroyTargetOnComplete && target != null)
            {
                if (showDebugLogs)
                    Debug.Log($"[HarvestAction] Destroying {target.name} (local mode)");

                Destroy(target);
                target = null;
            }

            // Clear reference
            interactableTarget = null;

            ActionResult result = new ActionResult
            {
                Success = true,
                Message = $"Harvested {resourceAmount}x {resourceType}",
                Data = new HarvestResult
                {
                    ResourceType = resourceType,
                    Amount = resourceAmount,
                    HarvestedObject = harvestedObject
                }
            };

            return result;
        }

        protected override void OnActionCancel()
        {
            if (showDebugLogs)
                Debug.Log($"[HarvestAction] Harvest cancelled on {target?.name ?? "null"}");

            // CRITICAL: End interaction to release the lock when cancelled
            if (interactableTarget != null)
            {
                interactableTarget.EndInteraction();
                interactableTarget = null; // Clear reference for next action
            }

            // Clean up any visual effects, sounds, etc.
        }

        /// <summary>
        /// Check if target is within harvest range
        /// </summary>
        private bool IsInRange()
        {
            if (executingEntity == null || target == null)
                return false;

            float distance = Vector3.Distance(executingEntity.transform.position, target.transform.position);
            bool inRange = distance <= harvestRange;

            // Debug logging to diagnose range checking
            if (showDebugLogs && !inRange)
            {
                Debug.Log($"[HarvestAction] Range check: distance={distance:F2}, harvestRange={harvestRange:F2}, inRange={inRange}");
            }

            return inRange;
        }

        /// <summary>
        /// Public method to check if entity can harvest a specific target
        /// </summary>
        public bool CanHarvest(GameObject potentialTarget)
        {
            if (potentialTarget == null || executingEntity == null)
                return false;

            // Check if target has InteractableObject
            if (potentialTarget.GetComponent<InteractableObject>() == null)
                return false;

            // Check range
            float distance = Vector3.Distance(executingEntity.transform.position, potentialTarget.transform.position);
            return distance <= harvestRange;
        }
    }

    /// <summary>
    /// Result data specific to harvest actions
    /// </summary>
    public struct HarvestResult
    {
        public ResourceType ResourceType;
        public int Amount;
        public GameObject HarvestedObject;
    }
}
