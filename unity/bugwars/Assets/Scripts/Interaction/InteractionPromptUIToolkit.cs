using UnityEngine;
using UnityEngine.UIElements;
using R3;
using VContainer;
using System;

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
        [SerializeField] private float worldSpaceScale = 0.01f; // Scale for world-space UI

        [Header("Message Settings")]
        [SerializeField] private string processingMessage = "Processing...";
        [SerializeField] private string completeMessage = "Complete!";
        [SerializeField] private float completeMessageDuration = 1.5f;

        // UI Elements (queried from UXML)
        private VisualElement promptContainer;
        private VisualElement promptPanel;
        private Label promptText;
        private VisualElement progressBarBackground;
        private VisualElement progressBarFill;

        // UI State
        private enum UIState { Hidden, Prompt, Processing, Complete }
        private UIState currentState = UIState.Hidden;

        // Dependencies (injected via VContainer)
        private InteractionManager interactionManager;
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

        private void Start()
        {
            mainCamera = Camera.main;

            if (interactionManager == null)
            {
                Debug.LogError("[InteractionPromptUIToolkit] InteractionManager not injected! Make sure it's registered in GameLifetimeScope.");
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
            else
            {
                Debug.Log("[InteractionPromptUIToolkit] UI elements queried successfully");
            }
        }

        /// <summary>
        /// Setup R3 reactive bindings for UI updates
        /// </summary>
        private void SetupReactiveUI()
        {
            // Show/hide prompt based on current target (only when not interacting)
            interactionManager.CurrentTarget
                .Subscribe(target =>
                {
                    if (target != null && !interactionManager.IsInteracting.CurrentValue)
                    {
                        ShowPrompt(target);
                    }
                    else if (target == null && currentState == UIState.Prompt)
                    {
                        HidePrompt();
                    }
                })
                .AddTo(this);

            // Handle interaction state changes
            interactionManager.IsInteracting
                .Subscribe(isInteracting =>
                {
                    if (isInteracting)
                    {
                        ShowProcessing();
                    }
                })
                .AddTo(this);

            // Handle interaction completion
            interactionManager.OnInteractionCompleted
                .Subscribe(result =>
                {
                    ShowComplete(result);
                })
                .AddTo(this);

            // Hide prompt initially
            HidePrompt();
        }

        private void ShowPrompt(InteractableObject target)
        {
            currentState = UIState.Prompt;
            Debug.Log($"[InteractionPromptUIToolkit] ShowPrompt called for {target.name}");

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
                Debug.Log($"[InteractionPromptUIToolkit] Set prompt text: {promptText.text}");
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

                // Animate progress bar with R3
                var target = interactionManager.CurrentTarget.CurrentValue;
                float harvestTime = target != null ? target.HarvestTime : 2f;
                float startTime = Time.time;

                Observable.EveryUpdate()
                    .TakeUntil(interactionManager.IsInteracting.Where(x => !x))
                    .Subscribe(_ =>
                    {
                        if (progressBarFill != null)
                        {
                            float elapsed = Time.time - startTime;
                            float progress = Mathf.Clamp01(elapsed / harvestTime);
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

            Debug.Log($"[InteractionPromptUIToolkit] Positioning UI at world position: {worldPosition}, target: {target.position}");

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
            }
        }
    }
}
