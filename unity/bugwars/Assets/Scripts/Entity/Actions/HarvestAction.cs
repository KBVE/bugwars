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
        [SerializeField] private float harvestRange = 3f;
        [SerializeField] private bool destroyTargetOnComplete = true;

        // Harvest-specific data
        private InteractableObject interactableTarget;
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

            // Get InteractableObject component if not already set
            if (interactableTarget == null)
            {
                interactableTarget = target.GetComponent<InteractableObject>();
                if (interactableTarget == null)
                {
                    if (showDebugLogs)
                        Debug.LogError($"[HarvestAction] Target {target.name} has no InteractableObject component!");
                    return false;
                }

                ConfigureFromInteractable(interactableTarget);
            }

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
            // Check if target still exists and is in range
            if (target == null || !IsInRange())
            {
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
            // Calculate resources gained
            resourceAmount = Random.Range(
                interactableTarget != null ? Mathf.Max(1, (int)(interactableTarget.Resource == ResourceType.Wood ? 5 : 3)) : 1,
                interactableTarget != null ? Mathf.Max(2, (int)(interactableTarget.Resource == ResourceType.Wood ? 8 : 6)) : 2
            );

            ActionResult result = new ActionResult
            {
                Success = true,
                Message = $"Harvested {resourceAmount}x {resourceType}",
                Data = new HarvestResult
                {
                    ResourceType = resourceType,
                    Amount = resourceAmount,
                    HarvestedObject = target
                }
            };

            // CRITICAL: End interaction to release the lock on this object
            if (interactableTarget != null)
            {
                interactableTarget.EndInteraction();
            }

            // Destroy target if configured
            if (destroyTargetOnComplete && target != null)
            {
                if (showDebugLogs)
                    Debug.Log($"[HarvestAction] Destroying {target.name}");

                Destroy(target);
            }

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
