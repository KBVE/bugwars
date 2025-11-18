using UnityEngine;
using UnityEngine.UI;
using TMPro;
using R3;
using VContainer;

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
        private Camera mainCamera;

        [Inject]
        public void Construct(InteractionManager manager)
        {
            interactionManager = manager;
        }

        private void Start()
        {
            mainCamera = Camera.main;

            if (interactionManager == null)
            {
                Debug.LogError("[InteractionPromptUI] InteractionManager not injected! Make sure it's registered in GameLifetimeScope.");
                enabled = false;
                return;
            }

            SetupReactiveUI();
        }

        /// <summary>
        /// Setup R3 reactive bindings for UI updates
        /// </summary>
        private void SetupReactiveUI()
        {
            // Show/hide prompt based on current target
            interactionManager.CurrentTarget
                .Subscribe(target =>
                {
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

            // Update progress bar during interaction
            interactionManager.IsInteracting
                .Where(isInteracting => isInteracting)
                .Subscribe(_ => ShowProgressBar())
                .AddTo(this);

            interactionManager.IsInteracting
                .Where(isInteracting => !isInteracting)
                .Subscribe(_ => HideProgressBar())
                .AddTo(this);

            // Hide prompt initially
            HidePrompt();
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

                // Animate progress bar with R3
                Observable.EveryUpdate()
                    .TakeUntil(interactionManager.IsInteracting.Where(x => !x))
                    .Subscribe(_ =>
                    {
                        // TODO: Get actual progress from InteractableObject
                        progressBar.fillAmount = Mathf.Min(progressBar.fillAmount + Time.deltaTime * 0.5f, 1f);
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
