using UnityEngine;
using R3;
using System.Collections.Generic;

namespace BugWars.Entity.Actions
{
    /// <summary>
    /// Manages all actions for an entity (Player or NPC)
    /// Handles action queuing, execution, and cancellation
    /// Only one action can be active at a time
    /// </summary>
    [RequireComponent(typeof(Entity))]
    public class EntityActionManager : MonoBehaviour
    {
        [Header("Action Components")]
        [SerializeField] private HarvestAction harvestAction;

        [Header("Settings")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private bool allowActionQueuing = false; // Future feature

        // R3 Reactive properties
        private readonly ReactiveProperty<EntityAction> _currentAction = new(null);
        private readonly ReactiveProperty<bool> _isPerformingAction = new(false);

        // Public observables
        public ReadOnlyReactiveProperty<EntityAction> CurrentAction => _currentAction;
        public ReadOnlyReactiveProperty<bool> IsPerformingAction => _isPerformingAction;

        // Entity reference
        private Entity entity;

        // Action queue (for future feature)
        private Queue<(EntityAction action, GameObject target)> actionQueue = new Queue<(EntityAction, GameObject)>();

        private void Awake()
        {
            entity = GetComponent<Entity>();

            // Auto-create action components if not assigned
            if (harvestAction == null)
            {
                harvestAction = gameObject.AddComponent<HarvestAction>();
                if (showDebugLogs)
                    Debug.Log($"[EntityActionManager] Auto-created HarvestAction for {entity.name}");
            }
        }

        /// <summary>
        /// Execute a harvest action on a target
        /// </summary>
        public void StartHarvest(GameObject target)
        {
            if (_isPerformingAction.Value)
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[EntityActionManager] {entity.name} is already performing an action!");
                return;
            }

            if (harvestAction == null)
            {
                Debug.LogError($"[EntityActionManager] No HarvestAction component on {entity.name}!");
                return;
            }

            // Execute the harvest action
            _currentAction.Value = harvestAction;
            _isPerformingAction.Value = true;

            harvestAction.Execute(entity, target)
                .Subscribe(result =>
                {
                    OnActionCompleted(result);
                })
                .AddTo(this);

            // Listen for cancellation
            harvestAction.OnActionCancelled
                .Subscribe(_ => OnActionCancelled())
                .AddTo(this);

            if (showDebugLogs)
                Debug.Log($"[EntityActionManager] {entity.name} started harvest on {target.name}");
        }

        /// <summary>
        /// Cancel the current action
        /// </summary>
        public void CancelCurrentAction()
        {
            if (!_isPerformingAction.Value || _currentAction.Value == null)
                return;

            _currentAction.Value.Cancel();
        }

        /// <summary>
        /// Check if entity can harvest a specific target
        /// </summary>
        public bool CanHarvest(GameObject target)
        {
            if (harvestAction == null)
                return false;

            return harvestAction.CanHarvest(target);
        }

        /// <summary>
        /// Get the current action progress (0-1)
        /// </summary>
        public float GetCurrentActionProgress()
        {
            if (_currentAction.Value == null)
                return 0f;

            return _currentAction.Value.Progress.CurrentValue;
        }

        /// <summary>
        /// Get the current action state
        /// </summary>
        public ActionState GetCurrentActionState()
        {
            if (_currentAction.Value == null)
                return ActionState.Idle;

            return _currentAction.Value.State.CurrentValue;
        }

        private void OnActionCompleted(ActionResult result)
        {
            if (showDebugLogs)
                Debug.Log($"[EntityActionManager] {entity.name} completed action: {result.Message}");

            _isPerformingAction.Value = false;
            _currentAction.Value = null;

            // Process result (could notify inventory system, etc.)
            ProcessActionResult(result);
        }

        private void OnActionCancelled()
        {
            if (showDebugLogs)
                Debug.Log($"[EntityActionManager] {entity.name} action cancelled");

            _isPerformingAction.Value = false;
            _currentAction.Value = null;
        }

        private void ProcessActionResult(ActionResult result)
        {
            // This is where you'd update inventory, give experience, etc.
            // For now, just log the result
            if (result.Data is HarvestResult harvestResult)
            {
                if (showDebugLogs)
                    Debug.Log($"[EntityActionManager] {entity.name} harvested {harvestResult.Amount}x {harvestResult.ResourceType}");

                // TODO: Add to inventory system when implemented
                // inventory.AddResource(harvestResult.ResourceType, harvestResult.Amount);
            }
        }

        private void OnDestroy()
        {
            _currentAction?.Dispose();
            _isPerformingAction?.Dispose();
        }
    }
}
