using UnityEngine;
using UnityEngine.UIElements;
using R3;
using VContainer;

namespace BugWars.Interaction
{
    /// <summary>
    /// UI Toolkit-based interaction prompt for screen-space HUD
    /// Uses UXML/USS and R3 reactive patterns
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InteractionPromptUIToolkit : MonoBehaviour
    {
        [Header("World-Space Settings")]
        [SerializeField] private bool useWorldSpace = false;
        [SerializeField] private Vector3 worldOffset = new Vector3(0, 2.5f, 0);

        // UI Elements (queried from UXML)
        private VisualElement promptContainer;
        private VisualElement promptPanel;
        private Label promptText;
        private VisualElement progressBarBackground;
        private VisualElement progressBarFill;

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
        }

        /// <summary>
        /// Setup R3 reactive bindings for UI updates
        /// </summary>
        private void SetupReactiveUI()
        {
            Debug.Log("[InteractionPromptUIToolkit] SetupReactiveUI() called");

            // Show/hide prompt based on current target
            interactionManager.CurrentTarget
                .Subscribe(target =>
                {
                    Debug.Log($"[InteractionPromptUIToolkit] CurrentTarget changed: {(target != null ? target.name : "NULL")}");
                    if (target != null)
                    {
                        ShowPrompt(target);
                    }
                    else
                    {
                        HidePrompt();
                    }
                })
                .AddTo(this);

            // Show/hide progress bar during interaction
            interactionManager.IsInteracting
                .Subscribe(isInteracting =>
                {
                    Debug.Log($"[InteractionPromptUIToolkit] IsInteracting changed: {isInteracting}");
                    if (isInteracting)
                    {
                        ShowProgressBar();
                    }
                    else
                    {
                        HideProgressBar();
                    }
                })
                .AddTo(this);

            // Hide prompt initially
            HidePrompt();
            Debug.Log("[InteractionPromptUIToolkit] Reactive UI setup complete!");
        }

        private void ShowPrompt(InteractableObject target)
        {
            Debug.Log($"[InteractionPromptUIToolkit] ShowPrompt() called for {target.name}");

            if (promptContainer != null)
            {
                promptContainer.style.display = DisplayStyle.Flex;
                Debug.Log("[InteractionPromptUIToolkit] Set promptContainer display to Flex");
            }
            else
            {
                Debug.LogError("[InteractionPromptUIToolkit] promptContainer is NULL!");
            }

            if (promptText != null)
            {
                // Format: "Tree\nPress E to chop"
                string objectName = target.name.Replace("(Clone)", "").Trim();
                promptText.text = $"{objectName}\n{target.InteractionPrompt}";
                Debug.Log($"[InteractionPromptUIToolkit] Set promptText to: {promptText.text}");
            }
            else
            {
                Debug.LogError("[InteractionPromptUIToolkit] promptText is NULL!");
            }

            // Position world-space UI if enabled
            if (useWorldSpace && target != null)
            {
                PositionWorldSpaceUI(target.transform);
            }
        }

        private void HidePrompt()
        {
            Debug.Log("[InteractionPromptUIToolkit] HidePrompt() called");

            if (promptContainer != null)
            {
                promptContainer.style.display = DisplayStyle.None;
            }
        }

        private void ShowProgressBar()
        {
            if (progressBarBackground != null)
            {
                progressBarBackground.style.display = DisplayStyle.Flex;
            }

            if (progressBarFill != null)
            {
                progressBarFill.style.width = Length.Percent(0);

                // Animate progress bar with R3
                Observable.EveryUpdate()
                    .TakeUntil(interactionManager.IsInteracting.Where(x => !x))
                    .Subscribe(_ =>
                    {
                        if (progressBarFill != null)
                        {
                            // TODO: Get actual progress from InteractableObject
                            float currentProgress = progressBarFill.resolvedStyle.width / progressBarBackground.resolvedStyle.width;
                            float newProgress = Mathf.Min(currentProgress + Time.deltaTime * 0.5f, 1f);
                            progressBarFill.style.width = Length.Percent(newProgress * 100);
                        }
                    })
                    .AddTo(this);
            }
        }

        private void HideProgressBar()
        {
            if (progressBarBackground != null)
            {
                progressBarBackground.style.display = DisplayStyle.None;
            }

            if (progressBarFill != null)
            {
                progressBarFill.style.width = Length.Percent(0);
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
            // Update world-space UI position every frame
            if (useWorldSpace && interactionManager.CurrentTarget.CurrentValue != null)
            {
                PositionWorldSpaceUI(interactionManager.CurrentTarget.CurrentValue.transform);
            }
        }
    }
}
