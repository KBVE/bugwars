using UnityEngine;
using UnityEngine.UIElements;
using R3;
using VContainer;
using System;
using Cysharp.Threading.Tasks;

namespace BugWars.Interaction
{
    /// <summary>
    /// UI Toolkit-based interaction prompt for screen-space HUD or world-space UI
    /// Uses UXML/USS and R3 reactive patterns
    /// Supports states: Idle (prompt), Processing, Complete
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InteractionPromptUIToolkit : MonoBehaviour
    {
        [Header("World-Space Settings")]
        [SerializeField] private bool useWorldSpace = true;
        [SerializeField] private Vector3 worldOffset = new Vector3(0, 2.5f, 0);
        [SerializeField] private float worldSpaceScale = 1f; // Scale for world-space UI (1920x1080 panel at 1:1 scale)

        [Header("Message Settings")]
        [SerializeField] private string processingMessage = "Processing...";
        [SerializeField] private string completeMessage = "Complete!";
        [SerializeField] private string cancelledMessage = "Cancelled";
        [SerializeField] private float completeMessageDuration = 1.5f;
        [SerializeField] private float cancelledMessageDuration = 1.5f;

        // UI Elements (queried from UXML)
        private VisualElement promptContainer;
        private VisualElement promptPanel;
        private Label promptText;
        private VisualElement progressBarBackground;
        private VisualElement progressBarFill;

        // UI State
        private enum UIState { Hidden, Prompt, Processing, Complete, Cancelled }
        private UIState currentState = UIState.Hidden;

        // Dependencies (injected via VContainer)
        private InteractionManager interactionManager;
        private BugWars.Entity.Actions.EntityActionManager playerActionManager;
        private BugWars.Entity.Actions.HarvestAction harvestAction;
        private UIDocument uiDocument;
        private Camera mainCamera;

        [Inject]
        public void Construct(InteractionManager manager)
        {
            interactionManager = manager;
        }

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        private async void Start()
        {
            mainCamera = Camera.main;

            if (interactionManager == null)
            {
                Debug.LogError("[InteractionPromptUIToolkit] InteractionManager not injected! Make sure it's registered in GameLifetimeScope.");
                enabled = false;
                return;
            }

            // Wait for InteractionManager to initialize and get EntityActionManager
            await UniTask.WaitUntil(() => interactionManager.PlayerEntityActionManager != null, cancellationToken: this.GetCancellationTokenOnDestroy());

            playerActionManager = interactionManager.PlayerEntityActionManager;
            if (playerActionManager == null)
            {
                Debug.LogError("[InteractionPromptUIToolkit] EntityActionManager not found on InteractionManager after wait!");
                enabled = false;
                return;
            }

            // Get harvest action component
            harvestAction = playerActionManager.GetComponent<BugWars.Entity.Actions.HarvestAction>();
            if (harvestAction == null)
            {
                Debug.LogError("[InteractionPromptUIToolkit] HarvestAction not found on player!");
                enabled = false;
                return;
            }

            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("[InteractionPromptUIToolkit] UIDocument not found or root element is null!");
                enabled = false;
                return;
            }

            QueryUIElements();

            // Apply world-space scale if enabled (Unity 6.2 world-space UI)
            if (useWorldSpace && worldSpaceScale > 0)
            {
                transform.localScale = Vector3.one * worldSpaceScale;
            }

            // CRITICAL: Hide UI immediately before setting up subscriptions to prevent flash
            HidePrompt();

            SetupReactiveUI();
        }

        /// <summary>
        /// Query UI elements from UXML
        /// </summary>
        private void QueryUIElements()
        {
            var root = uiDocument.rootVisualElement;

            promptContainer = root.Q<VisualElement>("InteractionPromptContainer");
            promptPanel = root.Q<VisualElement>("PromptPanel");
            promptText = root.Q<Label>("PromptText");
            progressBarBackground = root.Q<VisualElement>("ProgressBarBackground");
            progressBarFill = root.Q<VisualElement>("ProgressBarFill");

            if (promptContainer == null || promptPanel == null || promptText == null)
            {
                Debug.LogError("[InteractionPromptUIToolkit] Failed to query required UI elements from UXML!");
            }
        }

        /// <summary>
        /// Setup R3 reactive bindings for UI updates
        /// Subscribes directly to EntityActionManager (single source of truth)
        /// </summary>
        private void SetupReactiveUI()
        {
            // Show/hide prompt based on current target (only when not performing action)
            // Skip initial value to prevent flash
            interactionManager.CurrentTarget
                .Skip(1) // CRITICAL: Skip initial null/default value to prevent UI flash on startup
                .Subscribe(target =>
                {
                    if (target != null && !playerActionManager.IsPerformingAction.CurrentValue)
                    {
                        ShowPrompt(target);
                    }
                    else if (target == null && currentState == UIState.Prompt)
                    {
                        HidePrompt();
                    }
                })
                .AddTo(this);

            // CRITICAL: Subscribe directly to EntityActionManager.IsPerformingAction (single source of truth)
            playerActionManager.IsPerformingAction
                .Subscribe(isPerforming =>
                {
                    if (isPerforming)
                    {
                        ShowProcessing();
                    }
                })
                .AddTo(this);

            // CRITICAL: Subscribe directly to HarvestAction.OnActionCompleted
            harvestAction.OnActionCompleted
                .Subscribe(actionResult =>
                {
                    // Convert EntityAction result to UI result
                    if (actionResult.Success && actionResult.Data is BugWars.Entity.Actions.HarvestResult harvestResult)
                    {
                        var uiResult = new InteractionResult
                        {
                            Success = true,
                            ResourceType = harvestResult.ResourceType,
                            ResourceAmount = harvestResult.Amount,
                            InteractionType = InteractionType.Harvest // Default for now
                        };
                        ShowComplete(uiResult);
                    }
                })
                .AddTo(this);

            // CRITICAL: Subscribe to HarvestAction.OnActionCancelled to show cancellation message
            harvestAction.OnActionCancelled
                .Subscribe(_ =>
                {
                    ShowCancelled();
                })
                .AddTo(this);
        }

        private void ShowPrompt(InteractableObject target)
        {
            currentState = UIState.Prompt;

            if (promptContainer != null)
            {
                promptContainer.style.display = DisplayStyle.Flex;
            }
            else
            {
                Debug.LogError("[InteractionPromptUIToolkit] promptContainer is null!");
            }

            if (promptText != null)
            {
                // Format: "Tree\nPress E to chop"
                string objectName = target.name.Replace("(Clone)", "").Trim();
                promptText.text = $"{objectName}\n{target.InteractionPrompt}";
            }
            else
            {
                Debug.LogError("[InteractionPromptUIToolkit] promptText is null!");
            }

            // Hide progress bar in prompt state
            if (progressBarBackground != null)
            {
                progressBarBackground.style.display = DisplayStyle.None;
            }

            // Position world-space UI if enabled
            if (useWorldSpace && target != null)
            {
                PositionWorldSpaceUI(target.transform);
            }
        }

        private void ShowProcessing()
        {
            currentState = UIState.Processing;

            if (promptContainer != null)
            {
                promptContainer.style.display = DisplayStyle.Flex;
            }

            if (promptText != null)
            {
                promptText.text = processingMessage;
            }

            // Show and animate progress bar
            if (progressBarBackground != null)
            {
                progressBarBackground.style.display = DisplayStyle.Flex;
            }

            if (progressBarFill != null)
            {
                progressBarFill.style.width = Length.Percent(0);

                // CRITICAL: Subscribe directly to HarvestAction.Progress (single source of truth)
                harvestAction.Progress
                    .TakeWhile(_ => playerActionManager.IsPerformingAction.CurrentValue)
                    .Subscribe(progress =>
                    {
                        if (progressBarFill != null)
                        {
                            progressBarFill.style.width = Length.Percent(progress * 100);
                        }
                    })
                    .AddTo(this);
            }
        }

        private void ShowComplete(InteractionResult result)
        {
            currentState = UIState.Complete;

            if (promptText != null)
            {
                promptText.text = $"{completeMessage}\n+{result.ResourceAmount} {result.ResourceType}";
            }

            // Hide progress bar
            if (progressBarBackground != null)
            {
                progressBarBackground.style.display = DisplayStyle.None;
            }

            // Auto-hide after duration
            Observable.Timer(TimeSpan.FromSeconds(completeMessageDuration))
                .Subscribe(_ => HidePrompt())
                .AddTo(this);
        }

        private void ShowCancelled()
        {
            currentState = UIState.Cancelled;

            if (promptText != null)
            {
                promptText.text = cancelledMessage;
            }

            // Hide progress bar
            if (progressBarBackground != null)
            {
                progressBarBackground.style.display = DisplayStyle.None;
            }

            // Auto-hide after duration
            Observable.Timer(TimeSpan.FromSeconds(cancelledMessageDuration))
                .Subscribe(_ => HidePrompt())
                .AddTo(this);
        }

        private void HidePrompt()
        {
            currentState = UIState.Hidden;

            if (promptContainer != null)
            {
                promptContainer.style.display = DisplayStyle.None;
            }

            if (progressBarBackground != null)
            {
                progressBarBackground.style.display = DisplayStyle.None;
            }
        }

        private void PositionWorldSpaceUI(Transform target)
        {
            if (mainCamera == null || target == null)
                return;

            Vector3 worldPosition = target.position + worldOffset;
            transform.position = worldPosition;

            // Make UI face camera
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                mainCamera.transform.rotation * Vector3.up);
        }

        private void LateUpdate()
        {
            // Update world-space UI position every frame when visible
            if (useWorldSpace && currentState != UIState.Hidden)
            {
                var currentTarget = interactionManager.CurrentTarget.CurrentValue;
                if (currentTarget != null)
                {
                    PositionWorldSpaceUI(currentTarget.transform);
                }
                // If processing, follow the action's target
                else if (currentState == UIState.Processing && playerActionManager != null && playerActionManager.CurrentAction.CurrentValue != null)
                {
                    var actionTarget = playerActionManager.CurrentAction.CurrentValue.GetType().GetField("target",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(playerActionManager.CurrentAction.CurrentValue) as GameObject;
                    if (actionTarget != null)
                    {
                        PositionWorldSpaceUI(actionTarget.transform);
                    }
                }
            }
        }
    }
}
