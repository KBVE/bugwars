using UnityEngine;
using R3;
using System;

namespace BugWars.Entity.Actions
{
    /// <summary>
    /// Result of an entity action execution
    /// </summary>
    public struct ActionResult
    {
        public bool Success;
        public string Message;
        public object Data; // Flexible data payload (resources, items, etc.)
    }

    /// <summary>
    /// State of an ongoing action
    /// </summary>
    public enum ActionState
    {
        Idle,       // Not performing any action
        Starting,   // Action is initializing
        InProgress, // Action is actively running
        Completing, // Action is finishing up
        Completed,  // Action finished successfully
        Cancelled,  // Action was cancelled
        Failed      // Action failed
    }

    /// <summary>
    /// Base class for all entity actions (Harvest, Mine, Craft, etc.)
    /// Supports both Players and NPCs with R3 reactive observables
    /// WebGL-safe: Uses UniTask for async operations
    /// </summary>
    public abstract class EntityAction : MonoBehaviour
    {
        [Header("Action Settings")]
        [SerializeField] protected string actionName = "Unknown Action";
        [SerializeField] protected float actionDuration = 2f;
        [SerializeField] protected bool canBeCancelled = true;
        [SerializeField] protected bool showDebugLogs = true;

        // R3 Reactive properties
        protected readonly ReactiveProperty<ActionState> _state = new(ActionState.Idle);
        protected readonly ReactiveProperty<float> _progress = new(0f);
        protected readonly Subject<ActionResult> _onActionCompleted = new();
        protected readonly Subject<Unit> _onActionCancelled = new();

        // Public observables
        public ReadOnlyReactiveProperty<ActionState> State => _state;
        public ReadOnlyReactiveProperty<float> Progress => _progress;
        public Observable<ActionResult> OnActionCompleted => _onActionCompleted;
        public Observable<Unit> OnActionCancelled => _onActionCancelled;

        // Entity performing the action
        protected Entity executingEntity;
        protected GameObject target;

        // Timing
        protected float startTime;
        protected float elapsedTime;

        /// <summary>
        /// Execute the action on a target
        /// Returns an observable that completes when action finishes
        /// </summary>
        public virtual Observable<ActionResult> Execute(Entity entity, GameObject actionTarget)
        {
            if (_state.Value != ActionState.Idle)
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[EntityAction] {actionName} is already in progress for {entity.name}");
                return Observable.Empty<ActionResult>();
            }

            executingEntity = entity;
            target = actionTarget;

            _state.Value = ActionState.Starting;
            startTime = Time.time;
            elapsedTime = 0f;

            if (showDebugLogs)
                Debug.Log($"[EntityAction] {entity.name} started {actionName} on {actionTarget.name}");

            // Perform initialization
            if (!OnActionStart())
            {
                _state.Value = ActionState.Failed;
                return Observable.Return(new ActionResult
                {
                    Success = false,
                    Message = $"{actionName} failed to start"
                });
            }

            _state.Value = ActionState.InProgress;

            // Observable that updates progress every frame and completes when done
            Observable.EveryUpdate()
                .TakeWhile(_ => _state.Value == ActionState.InProgress)
                .Subscribe(_ => UpdateProgress())
                .AddTo(this);

            // Listen for Completing state and trigger completion
            _state
                .Where(state => state == ActionState.Completing)
                .Take(1)
                .Subscribe(_ =>
                {
                    CompleteAction().Subscribe().AddTo(this);
                })
                .AddTo(this);

            // Return an observable that fires when action completes
            return _onActionCompleted.Take(1);
        }

        /// <summary>
        /// Cancel the current action
        /// </summary>
        public virtual void Cancel()
        {
            if (!canBeCancelled)
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[EntityAction] {actionName} cannot be cancelled");
                return;
            }

            if (_state.Value != ActionState.InProgress)
                return;

            _state.Value = ActionState.Cancelled;
            _progress.Value = 0f;

            OnActionCancel();
            _onActionCancelled.OnNext(Unit.Default);

            if (showDebugLogs)
                Debug.Log($"[EntityAction] {actionName} cancelled");
        }

        /// <summary>
        /// Update action progress every frame
        /// </summary>
        protected virtual void UpdateProgress()
        {
            elapsedTime = Time.time - startTime;
            float progress = Mathf.Clamp01(elapsedTime / actionDuration);
            _progress.Value = progress;

            // Check if action is complete
            if (progress >= 1f)
            {
                _state.Value = ActionState.Completing;
            }

            // Custom progress update logic
            OnProgressUpdate(progress);
        }

        /// <summary>
        /// Complete the action and return result
        /// </summary>
        protected virtual Observable<ActionResult> CompleteAction()
        {
            _state.Value = ActionState.Completing;

            ActionResult result = OnActionComplete();
            result.Success = true;

            _state.Value = ActionState.Completed;
            _progress.Value = 1f;

            _onActionCompleted.OnNext(result);

            if (showDebugLogs)
                Debug.Log($"[EntityAction] {actionName} completed: {result.Message}");

            // Reset to idle after a short delay
            Observable.Timer(TimeSpan.FromSeconds(0.1f))
                .Subscribe(_ => _state.Value = ActionState.Idle)
                .AddTo(this);

            return Observable.Return(result);
        }

        // Abstract methods to be implemented by specific actions
        protected abstract bool OnActionStart();
        protected abstract void OnProgressUpdate(float progress);
        protected abstract ActionResult OnActionComplete();
        protected abstract void OnActionCancel();

        protected virtual void OnDestroy()
        {
            _state?.Dispose();
            _progress?.Dispose();
            _onActionCompleted?.Dispose();
            _onActionCancelled?.Dispose();
        }
    }
}
