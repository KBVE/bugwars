using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace BugWars.UI
{
    /// <summary>
    /// Socials Manager - Modular social media links UI component
    /// Displays configurable social media buttons (Discord, Twitch, etc.)
    /// Can be easily instantiated and configured from anywhere
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SocialsManager : MonoBehaviour
    {
        #region Fields
        [Header("Social Links Configuration")]
        [Tooltip("List of social media links to display")]
        [SerializeField] private List<SocialLink> _socialLinks = new List<SocialLink>();

        [Header("UI Configuration")]
        [Tooltip("Horizontal layout for buttons (true) or vertical (false)")]
        [SerializeField] private bool _horizontalLayout = true;

        [Tooltip("Show panel on start")]
        [SerializeField] private bool _showOnStart = true;

        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _socialsContainer;
        private VisualElement _buttonContainer;
        private Button _closeButton;
        private List<Button> _socialButtons = new List<Button>();
        private bool _isPanelVisible = false;
        #endregion

        #region Properties
        /// <summary>
        /// Returns whether the socials panel is currently visible
        /// </summary>
        public bool IsPanelVisible => _isPanelVisible;

        /// <summary>
        /// Gets or sets the list of social links
        /// </summary>
        public List<SocialLink> SocialLinks
        {
            get => _socialLinks;
            set
            {
                _socialLinks = value;
                RefreshSocialButtons();
            }
        }
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            InitializeUI();

            if (_showOnStart)
            {
                ShowPanel();
            }
            else
            {
                HidePanel();
            }
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the UI elements and registers callbacks
        /// </summary>
        private void InitializeUI()
        {
            _uiDocument = GetComponent<UIDocument>();

            if (_uiDocument == null)
            {
                Debug.LogError("[SocialsManager] UIDocument component not found!");
                return;
            }

            _root = _uiDocument.rootVisualElement;
            _socialsContainer = _root.Q<VisualElement>("SocialsContainer");

            if (_socialsContainer == null)
            {
                Debug.LogError("[SocialsManager] SocialsContainer not found in UXML!");
                return;
            }

            _buttonContainer = _root.Q<VisualElement>("ButtonContainer");
            _closeButton = _root.Q<Button>("CloseButton");

            if (_closeButton != null)
            {
                _closeButton.clicked += OnCloseButtonClicked;
            }

            // Set layout direction
            if (_buttonContainer != null)
            {
                _buttonContainer.style.flexDirection = _horizontalLayout
                    ? FlexDirection.Row
                    : FlexDirection.Column;
            }

            // Create social buttons
            CreateSocialButtons();
        }

        /// <summary>
        /// Creates social media buttons based on the configured links
        /// </summary>
        private void CreateSocialButtons()
        {
            if (_buttonContainer == null)
            {
                Debug.LogError("[SocialsManager] ButtonContainer not found!");
                return;
            }

            // Clear existing buttons
            ClearSocialButtons();

            // Create a button for each social link
            foreach (var socialLink in _socialLinks)
            {
                CreateSocialButton(socialLink);
            }

            Debug.Log($"[SocialsManager] Created {_socialButtons.Count} social buttons");
        }

        /// <summary>
        /// Creates a single social media button
        /// </summary>
        private void CreateSocialButton(SocialLink socialLink)
        {
            if (socialLink == null || _buttonContainer == null)
                return;

            var button = new Button();
            button.text = socialLink.platformName;
            button.AddToClassList("social-button");

            // Add platform-specific class for styling
            button.AddToClassList($"social-{socialLink.platformName.ToLower()}");

            // Apply custom colors if specified
            if (!string.IsNullOrEmpty(socialLink.buttonColor))
            {
                if (TryParseColor(socialLink.buttonColor, out Color bgColor))
                {
                    button.style.backgroundColor = bgColor;
                }
            }

            // Store hover color in userData for hover effect
            button.userData = socialLink;

            // Register click callback
            button.clicked += () => OnSocialButtonClicked(socialLink);

            // Add hover effects
            button.RegisterCallback<MouseEnterEvent>(evt => OnButtonHoverEnter(evt, socialLink));
            button.RegisterCallback<MouseLeaveEvent>(evt => OnButtonHoverLeave(evt, socialLink));

            _buttonContainer.Add(button);
            _socialButtons.Add(button);
        }

        /// <summary>
        /// Clears all social buttons
        /// </summary>
        private void ClearSocialButtons()
        {
            foreach (var button in _socialButtons)
            {
                if (button != null)
                {
                    button.clicked -= () => OnSocialButtonClicked(button.userData as SocialLink);
                    button.RemoveFromHierarchy();
                }
            }
            _socialButtons.Clear();
        }

        /// <summary>
        /// Refreshes social buttons when links are changed
        /// </summary>
        private void RefreshSocialButtons()
        {
            if (_buttonContainer != null)
            {
                CreateSocialButtons();
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Shows the socials panel
        /// </summary>
        public void ShowPanel()
        {
            if (_socialsContainer != null)
            {
                _socialsContainer.style.display = DisplayStyle.Flex;
                _isPanelVisible = true;
                Debug.Log("[SocialsManager] Socials panel shown");
            }
        }

        /// <summary>
        /// Hides the socials panel
        /// </summary>
        public void HidePanel()
        {
            if (_socialsContainer != null)
            {
                _socialsContainer.style.display = DisplayStyle.None;
                _isPanelVisible = false;
                Debug.Log("[SocialsManager] Socials panel hidden");
            }
        }

        /// <summary>
        /// Toggles the socials panel visibility
        /// </summary>
        public void TogglePanel()
        {
            if (_isPanelVisible)
                HidePanel();
            else
                ShowPanel();
        }

        /// <summary>
        /// Adds a social link dynamically
        /// </summary>
        public void AddSocialLink(SocialLink link)
        {
            if (link != null)
            {
                _socialLinks.Add(link);
                CreateSocialButton(link);
            }
        }

        /// <summary>
        /// Removes a social link by platform name
        /// </summary>
        public void RemoveSocialLink(string platformName)
        {
            var link = _socialLinks.Find(l => l.platformName == platformName);
            if (link != null)
            {
                _socialLinks.Remove(link);
                RefreshSocialButtons();
            }
        }

        /// <summary>
        /// Clears all social links
        /// </summary>
        public void ClearAllLinks()
        {
            _socialLinks.Clear();
            ClearSocialButtons();
        }
        #endregion

        #region Button Callbacks
        /// <summary>
        /// Called when a social button is clicked
        /// </summary>
        private void OnSocialButtonClicked(SocialLink socialLink)
        {
            if (socialLink == null)
                return;

            Debug.Log($"[SocialsManager] Opening {socialLink.platformName}: {socialLink.url}");
            Application.OpenURL(socialLink.url);
        }

        /// <summary>
        /// Called when the close button is clicked
        /// </summary>
        private void OnCloseButtonClicked()
        {
            HidePanel();
        }

        /// <summary>
        /// Called when mouse enters a button
        /// </summary>
        private void OnButtonHoverEnter(MouseEnterEvent evt, SocialLink socialLink)
        {
            if (evt.target is Button button && !string.IsNullOrEmpty(socialLink.hoverColor))
            {
                if (TryParseColor(socialLink.hoverColor, out Color hoverColor))
                {
                    button.style.backgroundColor = hoverColor;
                }
            }
        }

        /// <summary>
        /// Called when mouse leaves a button
        /// </summary>
        private void OnButtonHoverLeave(MouseLeaveEvent evt, SocialLink socialLink)
        {
            if (evt.target is Button button && !string.IsNullOrEmpty(socialLink.buttonColor))
            {
                if (TryParseColor(socialLink.buttonColor, out Color bgColor))
                {
                    button.style.backgroundColor = bgColor;
                }
            }
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// Unregisters all callbacks
        /// </summary>
        private void UnregisterCallbacks()
        {
            if (_closeButton != null)
            {
                _closeButton.clicked -= OnCloseButtonClicked;
            }

            ClearSocialButtons();
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Tries to parse a hex color string
        /// </summary>
        private bool TryParseColor(string hexColor, out Color color)
        {
            color = Color.white;

            if (string.IsNullOrEmpty(hexColor))
                return false;

            // Add # if not present
            if (!hexColor.StartsWith("#"))
                hexColor = "#" + hexColor;

            return ColorUtility.TryParseHtmlString(hexColor, out color);
        }
        #endregion

        #region Editor Helpers
        #if UNITY_EDITOR
        /// <summary>
        /// Adds default social links (Discord and Twitch) in the editor
        /// </summary>
        [ContextMenu("Add Default Links (Discord & Twitch)")]
        private void AddDefaultLinks()
        {
            _socialLinks.Clear();
            _socialLinks.Add(SocialLink.CreateDiscordLink("https://discord.gg/example"));
            _socialLinks.Add(SocialLink.CreateTwitchLink("https://twitch.tv/example"));
            Debug.Log("[SocialsManager] Added default Discord and Twitch links");
        }

        /// <summary>
        /// Tests the panel visibility toggle
        /// </summary>
        [ContextMenu("Toggle Panel")]
        private void TestTogglePanel()
        {
            TogglePanel();
        }
        #endif
        #endregion
    }
}
