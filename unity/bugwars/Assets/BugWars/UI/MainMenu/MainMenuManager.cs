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
            Debug.Log("[MainMenuManager] Start called");
            // Initialize UI after VContainer has configured the UIDocument
            InitializeUI();

            // Note: Escape key handling is managed by GameManager
            // GameManager subscribes to OnEscapePressed and calls ToggleMenu()
            // This avoids double-toggling from duplicate event subscriptions
            Debug.Log("[MainMenuManager] Start complete");
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
            Debug.Log("[MainMenuManager] InitializeUI called");
            _uiDocument = GetComponent<UIDocument>();

            if (_uiDocument == null)
            {
                Debug.LogError("[MainMenuManager] UIDocument component not found!");
                return;
            }

            Debug.Log($"[MainMenuManager] UIDocument found: {_uiDocument.name}");
            _root = _uiDocument.rootVisualElement;
            Debug.Log($"[MainMenuManager] Root element: {(_root != null ? "found" : "NULL")}");

            _mainMenuContainer = _root.Q<VisualElement>("MainMenuContainer");

            if (_mainMenuContainer == null)
            {
                Debug.LogError("[MainMenuManager] MainMenuContainer not found in UXML!");
                return;
            }

            Debug.Log("[MainMenuManager] MainMenuContainer found successfully");

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
            Debug.Log($"[MainMenuManager] ShowMenu called - _mainMenuContainer is {(_mainMenuContainer != null ? "available" : "NULL")}");
            if (_mainMenuContainer != null)
            {
                _mainMenuContainer.style.display = DisplayStyle.Flex;
                _isMenuVisible = true;
                Debug.Log("[MainMenuManager] Menu shown successfully - DisplayStyle set to Flex");
            }
            else
            {
                Debug.LogError("[MainMenuManager] Cannot show menu - _mainMenuContainer is null!");
            }
        }

        /// <summary>
        /// Hides the main menu
        /// </summary>
        public void HideMenu()
        {
            Debug.Log($"[MainMenuManager] HideMenu called - _mainMenuContainer is {(_mainMenuContainer != null ? "available" : "NULL")}");
            if (_mainMenuContainer != null)
            {
                _mainMenuContainer.style.display = DisplayStyle.None;
                _isMenuVisible = false;
                Debug.Log("[MainMenuManager] Menu hidden successfully - DisplayStyle set to None");
            }
            else
            {
                Debug.LogError("[MainMenuManager] Cannot hide menu - _mainMenuContainer is null!");
            }
        }

        /// <summary>
        /// Toggles the menu visibility
        /// </summary>
        public void ToggleMenu()
        {
            Debug.Log($"[MainMenuManager] ToggleMenu called - current state: {_isMenuVisible}");
            if (_isMenuVisible)
            {
                Debug.Log("[MainMenuManager] Menu is visible, hiding...");
                HideMenu();
            }
            else
            {
                Debug.Log("[MainMenuManager] Menu is hidden, showing...");
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
