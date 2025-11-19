using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using VContainer;
using Cysharp.Threading.Tasks;

namespace BugWars.Interaction
{
    /// <summary>
    /// World-space or screen-space UI that shows interaction prompts
    /// Uses R3 to reactively update when target changes
    /// Example: "Tree - Press E to chop"
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private Image progressBar;

        [Header("Settings")]
        [SerializeField] private bool useWorldSpace = true;
        [SerializeField] private Vector3 worldOffset = new Vector3(0, 2f, 0);

        // Dependencies (injected via VContainer)
        private InteractionManager interactionManager;
        private BugWars.Entity.Actions.EntityActionManager playerActionManager;
        private BugWars.Entity.Actions.HarvestAction harvestAction;
        private Camera mainCamera;

        [Inject]
        public void Construct(InteractionManager manager)
        {
            interactionManager = manager;
        }

        private async void Start()
        {
            mainCamera = Camera.main;

            if (interactionManager == null)
            {
                Debug.LogError("[InteractionPromptUI] InteractionManager not injected! Make sure it's registered in GameLifetimeScope.");
                enabled = false;
                return;
            }

            // Wait for InteractionManager to initialize and get EntityActionManager
            await Cysharp.Threading.Tasks.UniTask.WaitUntil(() => interactionManager.PlayerEntityActionManager != null, cancellationToken: this.GetCancellationTokenOnDestroy());

            playerActionManager = interactionManager.PlayerEntityActionManager;
            if (playerActionManager == null)
            {
                Debug.LogError("[InteractionPromptUI] EntityActionManager not found on InteractionManager after wait!");
                enabled = false;
                return;
            }

            // Get harvest action component
            harvestAction = playerActionManager.GetComponent<BugWars.Entity.Actions.HarvestAction>();
            if (harvestAction == null)
            {
                Debug.LogError("[InteractionPromptUI] HarvestAction not found on player!");
                enabled = false;
                return;
            }

            SetupReactiveUI();
        }

        /// <summary>
        /// Setup R3 reactive bindings for UI updates
        /// Subscribes directly to EntityActionManager (single source of truth)
        /// </summary>
        private void SetupReactiveUI()
        {
            // Hide prompt initially to prevent flash
            HidePrompt();

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
                    else if (target == null)
                    {
                        HidePrompt();
                    }
                })
                .AddTo(this);

            // CRITICAL: Subscribe directly to EntityActionManager.IsPerformingAction (single source of truth)
            playerActionManager.IsPerformingAction
                .Where(isPerforming => isPerforming)
                .Subscribe(_ => ShowProgressBar())
                .AddTo(this);

            playerActionManager.IsPerformingAction
                .Where(isPerforming => !isPerforming)
                .Subscribe(_ => HideProgressBar())
                .AddTo(this);
        }

        private void ShowPrompt(InteractableObject target)
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(true);
            }

            if (promptText != null)
            {
                // Format: "Tree - Press E to chop"
                string objectName = target.name.Replace("(Clone)", "").Trim();
                promptText.text = $"{objectName}\n{target.InteractionPrompt}";
            }

            if (useWorldSpace && target != null)
            {
                PositionWorldSpaceUI(target.transform);
            }
        }

        private void HidePrompt()
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(false);
            }
        }

        private void ShowProgressBar()
        {
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(true);
                progressBar.fillAmount = 0f;

                // CRITICAL: Subscribe directly to HarvestAction.Progress (single source of truth)
                harvestAction.Progress
                    .TakeWhile(_ => playerActionManager.IsPerformingAction.CurrentValue)
                    .Subscribe(progress =>
                    {
                        if (progressBar != null)
                        {
                            progressBar.fillAmount = progress;
                        }
                    })
                    .AddTo(this);
            }
        }

        private void HideProgressBar()
        {
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(false);
                progressBar.fillAmount = 0f;
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
