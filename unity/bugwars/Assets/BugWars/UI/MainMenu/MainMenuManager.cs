using UnityEngine;
using UnityEngine.UIElements;

namespace BugWars.UI
{
    /// <summary>
    /// Main Menu Manager - Managed by VContainer
    /// Manages the main menu UI including visibility and button interactions
    /// Controlled by GameManager for escape key toggle functionality
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuManager : MonoBehaviour
    {

        #region Fields
        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _mainMenuContainer;
        private Button _settingsButton;
        private Button _exitButton;
        private bool _isMenuVisible = false;
        #endregion

        #region Properties
        /// <summary>
        /// Returns whether the menu is currently visible
        /// </summary>
        public bool IsMenuVisible => _isMenuVisible;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Component initialized
        }

        private void Start()
        {
            // Initialize UI after VContainer has configured the UIDocument
            InitializeUI();

            // Note: Escape key handling is managed by GameManager
            // GameManager subscribes to OnEscapePressed and calls ToggleMenu()
            // This avoids double-toggling from duplicate event subscriptions
        }

        private void OnDestroy()
        {
            // Unregister button callbacks
            if (_settingsButton != null)
            {
                _settingsButton.clicked -= OnSettingsButtonClicked;
            }

            if (_exitButton != null)
            {
                _exitButton.clicked -= OnExitButtonClicked;
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the UI elements and registers button callbacks
        /// </summary>
        private void InitializeUI()
        {
            _uiDocument = GetComponent<UIDocument>();

            if (_uiDocument == null)
            {
                Debug.LogError("[MainMenuManager] UIDocument component not found!");
                return;
            }

            _root = _uiDocument.rootVisualElement;
            _mainMenuContainer = _root.Q<VisualElement>("MainMenuContainer");

            if (_mainMenuContainer == null)
            {
                Debug.LogError("[MainMenuManager] MainMenuContainer not found in UXML!");
                return;
            }

            // Get button references
            _settingsButton = _root.Q<Button>("SettingsButton");
            _exitButton = _root.Q<Button>("ExitButton");

            // Register button callbacks
            if (_settingsButton != null)
            {
                _settingsButton.clicked += OnSettingsButtonClicked;
            }
            else
            {
                Debug.LogWarning("[MainMenuManager] SettingsButton not found in UXML!");
            }

            if (_exitButton != null)
            {
                _exitButton.clicked += OnExitButtonClicked;
            }
            else
            {
                Debug.LogWarning("[MainMenuManager] ExitButton not found in UXML!");
            }

            // Start with menu visible
            ShowMenu();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Shows the main menu
        /// </summary>
        public void ShowMenu()
        {
            if (_mainMenuContainer != null)
            {
                _mainMenuContainer.style.display = DisplayStyle.Flex;
                _isMenuVisible = true;
            }
        }

        /// <summary>
        /// Hides the main menu
        /// </summary>
        public void HideMenu()
        {
            if (_mainMenuContainer != null)
            {
                _mainMenuContainer.style.display = DisplayStyle.None;
                _isMenuVisible = false;
            }
        }

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
        #endregion

        #region Button Callbacks
        /// <summary>
        /// Called when the Settings button is clicked
        /// </summary>
        private void OnSettingsButtonClicked()
        {
            // TODO: Implement settings panel
        }

        /// <summary>
        /// Called when the Exit button is clicked
        /// </summary>
        private void OnExitButtonClicked()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
        #endregion
    }
}
