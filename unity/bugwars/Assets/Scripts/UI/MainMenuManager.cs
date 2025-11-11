using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace BugWars.UI
{
    /// <summary>
    /// Main Menu Manager - Managed by VContainer
    /// Controls the visibility and behavior of the main menu UI
    /// Uses Unity UI Toolkit (UIDocument) for rendering
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        #region Dependencies
        private BugWars.Core.EventManager _eventManager;

        [Inject]
        public void Construct(BugWars.Core.EventManager eventManager)
        {
            _eventManager = eventManager;
        }
        #endregion

        #region UI References
        private UIDocument _uiDocument;
        private VisualElement _rootElement;
        private VisualElement _mainPanel;
        #endregion

        #region State
        private bool _isMenuVisible = true; // Start visible by default

        /// <summary>
        /// Indicates whether the menu is currently visible
        /// </summary>
        public bool IsMenuVisible => _isMenuVisible;
        #endregion

        #region Settings
        [Header("Menu Settings")]
        [Tooltip("Enable debug logging for menu operations")]
        [SerializeField] private bool debugMode = false;

        [Tooltip("Should the menu be visible on start?")]
        [SerializeField] private bool showOnStart = true;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Get UIDocument component (should be added by GameLifetimeScope)
            _uiDocument = GetComponent<UIDocument>();

            if (_uiDocument == null)
            {
                Debug.LogError("[MainMenuManager] UIDocument component not found!");
                return;
            }

            // Wait for UIDocument to be ready
            if (_uiDocument.rootVisualElement == null)
            {
                Debug.LogWarning("[MainMenuManager] UIDocument rootVisualElement is null in Awake");
            }
        }

        private void Start()
        {
            // Initialize UI references
            InitializeUI();

            // Set initial visibility based on settings
            if (showOnStart)
            {
                ShowMenu();
            }
            else
            {
                HideMenu();
            }
        }

        private void OnDestroy()
        {
            // Clean up any event listeners if needed
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes UI element references
        /// </summary>
        private void InitializeUI()
        {
            if (_uiDocument == null)
            {
                Debug.LogError("[MainMenuManager] Cannot initialize UI - UIDocument is null");
                return;
            }

            _rootElement = _uiDocument.rootVisualElement;

            if (_rootElement == null)
            {
                Debug.LogError("[MainMenuManager] Cannot initialize UI - rootVisualElement is null");
                return;
            }

            // Try to find the main panel element
            _mainPanel = _rootElement.Q<VisualElement>("main-panel");

            if (_mainPanel == null)
            {
                Debug.LogWarning("[MainMenuManager] 'main-panel' element not found, using root element");
                _mainPanel = _rootElement;
            }

            if (debugMode)
            {
                Debug.Log("[MainMenuManager] UI initialized successfully");
            }
        }
        #endregion

        #region Menu Control
        /// <summary>
        /// Toggles the menu visibility
        /// </summary>
        public void ToggleMenu()
        {
            if (_isMenuVisible)
            {
                HideMenu();
            }
            else
            {
                ShowMenu();
            }
        }

        /// <summary>
        /// Shows the main menu
        /// </summary>
        public void ShowMenu()
        {
            if (_mainPanel == null)
            {
                Debug.LogWarning("[MainMenuManager] Cannot show menu - main panel is null");
                return;
            }

            _mainPanel.style.display = DisplayStyle.Flex;
            _isMenuVisible = true;

            if (debugMode)
            {
                Debug.Log("[MainMenuManager] Menu shown");
            }

            // Trigger event if EventManager is available
            _eventManager?.TriggerMainMenuOpened();
        }

        /// <summary>
        /// Hides the main menu
        /// </summary>
        public void HideMenu()
        {
            if (_mainPanel == null)
            {
                Debug.LogWarning("[MainMenuManager] Cannot hide menu - main panel is null");
                return;
            }

            _mainPanel.style.display = DisplayStyle.None;
            _isMenuVisible = false;

            if (debugMode)
            {
                Debug.Log("[MainMenuManager] Menu hidden");
            }

            // Trigger event if EventManager is available
            _eventManager?.TriggerMainMenuClosed();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Enables or disables debug logging
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            debugMode = enabled;
            Debug.Log($"[MainMenuManager] Debug mode {(enabled ? "enabled" : "disabled")}");
        }
        #endregion
    }
}
