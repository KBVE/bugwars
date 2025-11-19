using UnityEngine;
using R3;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

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

        // Cancellation token support
        private CancellationTokenSource _actionCancellationTokenSource;
        protected CancellationToken ActionCancellationToken => _actionCancellationTokenSource?.Token ?? CancellationToken.None;

        // CRITICAL: Per-action subscriptions cleanup
        // Prevents subscription accumulation when Execute() is called multiple times
        private CompositeDisposable _activeActionSubscriptions = new CompositeDisposable();

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

            // CRITICAL: Clear any previous action subscriptions to prevent accumulation
            _activeActionSubscriptions.Clear();

            // Create cancellation token for this action
            // Links to GameObject destruction for automatic cleanup
            _actionCancellationTokenSource?.Cancel();
            _actionCancellationTokenSource?.Dispose();
            _actionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy()
            );

            // Register callback for cancellation
            _actionCancellationTokenSource.Token.Register(() =>
            {
                if (_state.Value == ActionState.InProgress)
                {
                    if (showDebugLogs)
                        Debug.Log($"[EntityAction] {actionName} cancelled via CancellationToken");
                    Cancel();
                }
            });

            _state.Value = ActionState.Starting;
            startTime = Time.time;
            elapsedTime = 0f;

            if (showDebugLogs)
                Debug.Log($"[EntityAction] {entity.name} started {actionName} on {actionTarget.name}");

            // Perform initialization
            if (!OnActionStart())
            {
                _state.Value = ActionState.Failed;
                CleanupCancellationToken();

                // CRITICAL: Reset to Idle immediately so action can be restarted
                _state.Value = ActionState.Idle;

                return Observable.Return(new ActionResult
                {
                    Success = false,
                    Message = $"{actionName} failed to start"
                });
            }

            _state.Value = ActionState.InProgress;

            // CRITICAL: Subscribe to progress updates - tied to this specific action execution
            Observable.EveryUpdate()
                .TakeWhile(_ => _state.Value == ActionState.InProgress)
                .Subscribe(_ => UpdateProgress())
                .AddTo(_activeActionSubscriptions);

            // CRITICAL: Listen for Completing state - tied to this specific action execution
            _state
                .Where(state => state == ActionState.Completing)
                .Take(1)
                .Subscribe(_ =>
                {
                    CompleteAction().Subscribe().AddTo(_activeActionSubscriptions);
                })
                .AddTo(_activeActionSubscriptions);

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

            // Cleanup cancellation token
            CleanupCancellationToken();

            // CRITICAL: Clear active subscriptions when cancelled
            _activeActionSubscriptions.Clear();

            if (showDebugLogs)
                Debug.Log($"[EntityAction] {actionName} cancelled");

            // CRITICAL: Reset to Idle immediately so action can be restarted
            _state.Value = ActionState.Idle;
        }

        /// <summary>
        /// Request cancellation via CancellationToken
        /// Use this for external cancellation sources (entity death, AI state change, etc.)
        /// </summary>
        public void RequestCancellation()
        {
            _actionCancellationTokenSource?.Cancel();
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

            // Cleanup cancellation token
            CleanupCancellationToken();

            // CRITICAL: Clear active subscriptions when completed
            _activeActionSubscriptions.Clear();

            if (showDebugLogs)
                Debug.Log($"[EntityAction] {actionName} completed: {result.Message}");

            // CRITICAL: Reset to Idle immediately so action can be restarted
            // Removed 0.1s delay that prevented immediate restart
            _state.Value = ActionState.Idle;

            return Observable.Return(result);
        }

        // Abstract methods to be implemented by specific actions
        protected abstract bool OnActionStart();
        protected abstract void OnProgressUpdate(float progress);
        protected abstract ActionResult OnActionComplete();
        protected abstract void OnActionCancel();

        /// <summary>
        /// Cleanup cancellation token resources
        /// </summary>
        private void CleanupCancellationToken()
        {
            if (_actionCancellationTokenSource != null)
            {
                if (!_actionCancellationTokenSource.IsCancellationRequested)
                {
                    _actionCancellationTokenSource.Cancel();
                }
                _actionCancellationTokenSource.Dispose();
                _actionCancellationTokenSource = null;
            }
        }

        protected virtual void OnDestroy()
        {
            // Cleanup active action subscriptions
            _activeActionSubscriptions?.Dispose();

            // Cleanup cancellation token
            CleanupCancellationToken();

            // Dispose reactive properties
            _state?.Dispose();
            _progress?.Dispose();
            _onActionCompleted?.Dispose();
            _onActionCancelled?.Dispose();
        }
    }
}
