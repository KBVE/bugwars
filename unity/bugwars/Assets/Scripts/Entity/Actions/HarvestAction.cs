using UnityEngine;
using BugWars.Interaction;

namespace BugWars.Entity.Actions
{
    /// <summary>
    /// Harvest action for gathering resources from interactable objects
    /// Works with trees, rocks, bushes, etc.
    /// Supports both Players and NPCs
    /// </summary>
    public class HarvestAction : EntityAction
    {
        [Header("Harvest Settings")]
        [SerializeField] private float harvestRange = 6f; // Slightly larger than raycast (5f) to account for object size and prevent edge cases
        [SerializeField] private bool destroyTargetOnComplete = true;

        // Harvest-specific data
        private InteractableObject interactableTarget;

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
                if (showDebugLogs)
                    Debug.LogWarning($"[HarvestAction] Target {target.name} is out of range!");
                return false;
            }

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
            if (!IsInRange())
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[HarvestAction] Player moved out of range - cancelling harvest");
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
            // CRITICAL: Destroy object FIRST before calculating/giving rewards
            // This prevents exploit where player moves away but still gets reward

            // SAFETY CHECK: Verify target still exists at completion
            if (target == null || interactableTarget == null)
            {
                if (showDebugLogs)
                    Debug.LogError($"[HarvestAction] Target was destroyed before completion - no reward given");

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

                return new ActionResult
                {
                    Success = false,
                    Message = "Moved out of range",
                    Data = null
                };
            }

            // Calculate resources gained (cache before destroying object)
            resourceAmount = Random.Range(
                interactableTarget != null ? Mathf.Max(1, (int)(interactableTarget.Resource == ResourceType.Wood ? 5 : 3)) : 1,
                interactableTarget != null ? Mathf.Max(2, (int)(interactableTarget.Resource == ResourceType.Wood ? 8 : 6)) : 2
            );

            // Cache target reference before destroying
            GameObject harvestedObject = target;

            // STEP 1: End interaction to release the lock on this object
            if (interactableTarget != null)
            {
                interactableTarget.EndInteraction();
            }

            // STEP 2: Destroy target BEFORE creating reward result
            if (destroyTargetOnComplete && target != null)
            {
                if (showDebugLogs)
                    Debug.Log($"[HarvestAction] Destroying {target.name}");

                Destroy(target);
                target = null; // Clear reference immediately
            }

            // STEP 3: Clear interactableTarget reference for next action
            interactableTarget = null;

            // STEP 4: NOW create result with reward data (AFTER object is destroyed)
            ActionResult result = new ActionResult
            {
                Success = true,
                Message = $"Harvested {resourceAmount}x {resourceType}",
                Data = new HarvestResult
                {
                    ResourceType = resourceType,
                    Amount = resourceAmount,
                    HarvestedObject = harvestedObject // Use cached reference
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
            return distance <= harvestRange;
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
