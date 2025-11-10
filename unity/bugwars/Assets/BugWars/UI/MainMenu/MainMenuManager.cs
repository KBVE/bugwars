using UnityEngine;
using UnityEngine.UIElements;
using BugWars.Core;

namespace BugWars.UI
{
    /// <summary>
    /// Main Menu Manager - Singleton pattern
    /// Manages the main menu UI including visibility and button interactions
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuManager : MonoBehaviour
    {
        #region Singleton
        private static MainMenuManager _instance;

        /// <summary>
        /// Singleton instance - managed by VContainer
        /// </summary>
        public static MainMenuManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MainMenuManager>();
                    if (_instance == null)
                    {
                        Debug.LogWarning("[MainMenuManager] Instance not found! Make sure GameLifetimeScope is in the scene.");
                    }
                }
                return _instance;
            }
        }
        #endregion

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
            // Ensure singleton pattern
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            // Note: DontDestroyOnLoad is handled by VContainer
        }

        private void Start()
        {
            // Initialize UI after VContainer has configured the UIDocument
            InitializeUI();

            // Subscribe to Escape key event from EventManager
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnEscapePressed.AddListener(ToggleMenu);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from EventManager
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnEscapePressed.RemoveListener(ToggleMenu);
            }

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
                Debug.Log("[MainMenuManager] Menu shown");
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
                Debug.Log("[MainMenuManager] Menu hidden");
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
            Debug.Log("[MainMenuManager] Settings button clicked");
            // TODO: Implement settings panel
        }

        /// <summary>
        /// Called when the Exit button is clicked
        /// </summary>
        private void OnExitButtonClicked()
        {
            Debug.Log("[MainMenuManager] Exit button clicked - Quitting game");

            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
        #endregion
    }
}
