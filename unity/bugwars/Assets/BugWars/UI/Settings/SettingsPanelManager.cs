using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace BugWars.UI
{
    /// <summary>
    /// Settings Panel Manager - Manages the settings UI panel
    /// Handles audio, graphics, and gameplay settings
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SettingsPanelManager : MonoBehaviour
    {
        #region Fields
        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _settingsPanelContainer;

        // Header
        private Button _closeButton;

        // Audio settings
        private Slider _masterVolumeSlider;
        private Label _masterVolumeValue;
        private Slider _musicVolumeSlider;
        private Label _musicVolumeValue;
        private Slider _sfxVolumeSlider;
        private Label _sfxVolumeValue;

        // Graphics settings
        private DropdownField _qualityDropdown;
        private Toggle _fullscreenToggle;
        private Toggle _vsyncToggle;

        // Gameplay settings
        private Slider _mouseSensitivitySlider;
        private Label _mouseSensitivityValue;
        private Toggle _invertYAxisToggle;

        // Footer buttons
        private Button _applyButton;
        private Button _resetButton;

        private bool _isPanelVisible = false;

        // Default values
        private const float DEFAULT_MASTER_VOLUME = 100f;
        private const float DEFAULT_MUSIC_VOLUME = 80f;
        private const float DEFAULT_SFX_VOLUME = 100f;
        private const float DEFAULT_MOUSE_SENSITIVITY = 5f;
        private const bool DEFAULT_FULLSCREEN = true;
        private const bool DEFAULT_VSYNC = true;
        private const bool DEFAULT_INVERT_Y = false;
        #endregion

        #region Properties
        /// <summary>
        /// Returns whether the settings panel is currently visible
        /// </summary>
        public bool IsPanelVisible => _isPanelVisible;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            InitializeUI();
            LoadSettings();
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
                Debug.LogError("[SettingsPanelManager] UIDocument component not found!");
                return;
            }

            _root = _uiDocument.rootVisualElement;
            _settingsPanelContainer = _root.Q<VisualElement>("SettingsPanelContainer");

            if (_settingsPanelContainer == null)
            {
                Debug.LogError("[SettingsPanelManager] SettingsPanelContainer not found in UXML!");
                return;
            }

            // Get references to all UI elements
            GetUIReferences();

            // Initialize quality dropdown options
            InitializeQualityDropdown();

            // Register callbacks
            RegisterCallbacks();

            // Start with panel hidden
            HidePanel();
        }

        /// <summary>
        /// Gets references to all UI elements
        /// </summary>
        private void GetUIReferences()
        {
            // Header
            _closeButton = _root.Q<Button>("CloseButton");

            // Audio
            _masterVolumeSlider = _root.Q<Slider>("MasterVolumeSlider");
            _masterVolumeValue = _root.Q<Label>("MasterVolumeValue");
            _musicVolumeSlider = _root.Q<Slider>("MusicVolumeSlider");
            _musicVolumeValue = _root.Q<Label>("MusicVolumeValue");
            _sfxVolumeSlider = _root.Q<Slider>("SFXVolumeSlider");
            _sfxVolumeValue = _root.Q<Label>("SFXVolumeValue");

            // Graphics
            _qualityDropdown = _root.Q<DropdownField>("QualityDropdown");
            _fullscreenToggle = _root.Q<Toggle>("FullscreenToggle");
            _vsyncToggle = _root.Q<Toggle>("VSyncToggle");

            // Gameplay
            _mouseSensitivitySlider = _root.Q<Slider>("MouseSensitivitySlider");
            _mouseSensitivityValue = _root.Q<Label>("MouseSensitivityValue");
            _invertYAxisToggle = _root.Q<Toggle>("InvertYAxisToggle");

            // Footer
            _applyButton = _root.Q<Button>("ApplyButton");
            _resetButton = _root.Q<Button>("ResetButton");
        }

        /// <summary>
        /// Initializes the quality dropdown with Unity quality levels
        /// </summary>
        private void InitializeQualityDropdown()
        {
            if (_qualityDropdown != null)
            {
                var qualityNames = new List<string>(QualitySettings.names);
                _qualityDropdown.choices = qualityNames;
                _qualityDropdown.index = QualitySettings.GetQualityLevel();
            }
        }

        /// <summary>
        /// Registers all UI callbacks
        /// </summary>
        private void RegisterCallbacks()
        {
            // Header
            if (_closeButton != null)
                _closeButton.clicked += OnCloseButtonClicked;

            // Audio sliders
            if (_masterVolumeSlider != null)
                _masterVolumeSlider.RegisterValueChangedCallback(OnMasterVolumeChanged);
            if (_musicVolumeSlider != null)
                _musicVolumeSlider.RegisterValueChangedCallback(OnMusicVolumeChanged);
            if (_sfxVolumeSlider != null)
                _sfxVolumeSlider.RegisterValueChangedCallback(OnSFXVolumeChanged);

            // Gameplay sliders
            if (_mouseSensitivitySlider != null)
                _mouseSensitivitySlider.RegisterValueChangedCallback(OnMouseSensitivityChanged);

            // Footer buttons
            if (_applyButton != null)
                _applyButton.clicked += OnApplyButtonClicked;
            if (_resetButton != null)
                _resetButton.clicked += OnResetButtonClicked;
        }

        /// <summary>
        /// Unregisters all UI callbacks
        /// </summary>
        private void UnregisterCallbacks()
        {
            if (_closeButton != null)
                _closeButton.clicked -= OnCloseButtonClicked;

            if (_masterVolumeSlider != null)
                _masterVolumeSlider.UnregisterValueChangedCallback(OnMasterVolumeChanged);
            if (_musicVolumeSlider != null)
                _musicVolumeSlider.UnregisterValueChangedCallback(OnMusicVolumeChanged);
            if (_sfxVolumeSlider != null)
                _sfxVolumeSlider.UnregisterValueChangedCallback(OnSFXVolumeChanged);

            if (_mouseSensitivitySlider != null)
                _mouseSensitivitySlider.UnregisterValueChangedCallback(OnMouseSensitivityChanged);

            if (_applyButton != null)
                _applyButton.clicked -= OnApplyButtonClicked;
            if (_resetButton != null)
                _resetButton.clicked -= OnResetButtonClicked;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Shows the settings panel
        /// </summary>
        public void ShowPanel()
        {
            if (_settingsPanelContainer != null)
            {
                _settingsPanelContainer.style.display = DisplayStyle.Flex;
                _isPanelVisible = true;
                Debug.Log("[SettingsPanelManager] Settings panel shown");
            }
        }

        /// <summary>
        /// Hides the settings panel
        /// </summary>
        public void HidePanel()
        {
            if (_settingsPanelContainer != null)
            {
                _settingsPanelContainer.style.display = DisplayStyle.None;
                _isPanelVisible = false;
                Debug.Log("[SettingsPanelManager] Settings panel hidden");
            }
        }

        /// <summary>
        /// Toggles the settings panel visibility
        /// </summary>
        public void TogglePanel()
        {
            if (_isPanelVisible)
                HidePanel();
            else
                ShowPanel();
        }
        #endregion

        #region Settings Management
        /// <summary>
        /// Loads settings from PlayerPrefs
        /// </summary>
        private void LoadSettings()
        {
            // Audio
            if (_masterVolumeSlider != null)
            {
                float masterVolume = PlayerPrefs.GetFloat("MasterVolume", DEFAULT_MASTER_VOLUME);
                _masterVolumeSlider.value = masterVolume;
                UpdateVolumeLabel(_masterVolumeValue, masterVolume);
            }

            if (_musicVolumeSlider != null)
            {
                float musicVolume = PlayerPrefs.GetFloat("MusicVolume", DEFAULT_MUSIC_VOLUME);
                _musicVolumeSlider.value = musicVolume;
                UpdateVolumeLabel(_musicVolumeValue, musicVolume);
            }

            if (_sfxVolumeSlider != null)
            {
                float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", DEFAULT_SFX_VOLUME);
                _sfxVolumeSlider.value = sfxVolume;
                UpdateVolumeLabel(_sfxVolumeValue, sfxVolume);
            }

            // Graphics
            if (_qualityDropdown != null)
            {
                int qualityLevel = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
                _qualityDropdown.index = qualityLevel;
            }

            if (_fullscreenToggle != null)
            {
                bool fullscreen = PlayerPrefs.GetInt("Fullscreen", DEFAULT_FULLSCREEN ? 1 : 0) == 1;
                _fullscreenToggle.value = fullscreen;
            }

            if (_vsyncToggle != null)
            {
                bool vsync = PlayerPrefs.GetInt("VSync", DEFAULT_VSYNC ? 1 : 0) == 1;
                _vsyncToggle.value = vsync;
            }

            // Gameplay
            if (_mouseSensitivitySlider != null)
            {
                float mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", DEFAULT_MOUSE_SENSITIVITY);
                _mouseSensitivitySlider.value = mouseSensitivity;
                UpdateSensitivityLabel(_mouseSensitivityValue, mouseSensitivity);
            }

            if (_invertYAxisToggle != null)
            {
                bool invertY = PlayerPrefs.GetInt("InvertYAxis", DEFAULT_INVERT_Y ? 1 : 0) == 1;
                _invertYAxisToggle.value = invertY;
            }
        }

        /// <summary>
        /// Saves settings to PlayerPrefs
        /// </summary>
        private void SaveSettings()
        {
            // Audio
            if (_masterVolumeSlider != null)
                PlayerPrefs.SetFloat("MasterVolume", _masterVolumeSlider.value);
            if (_musicVolumeSlider != null)
                PlayerPrefs.SetFloat("MusicVolume", _musicVolumeSlider.value);
            if (_sfxVolumeSlider != null)
                PlayerPrefs.SetFloat("SFXVolume", _sfxVolumeSlider.value);

            // Graphics
            if (_qualityDropdown != null)
                PlayerPrefs.SetInt("QualityLevel", _qualityDropdown.index);
            if (_fullscreenToggle != null)
                PlayerPrefs.SetInt("Fullscreen", _fullscreenToggle.value ? 1 : 0);
            if (_vsyncToggle != null)
                PlayerPrefs.SetInt("VSync", _vsyncToggle.value ? 1 : 0);

            // Gameplay
            if (_mouseSensitivitySlider != null)
                PlayerPrefs.SetFloat("MouseSensitivity", _mouseSensitivitySlider.value);
            if (_invertYAxisToggle != null)
                PlayerPrefs.SetInt("InvertYAxis", _invertYAxisToggle.value ? 1 : 0);

            PlayerPrefs.Save();
            Debug.Log("[SettingsPanelManager] Settings saved");
        }

        /// <summary>
        /// Applies the current settings to the game
        /// </summary>
        private void ApplySettings()
        {
            // Graphics settings
            if (_qualityDropdown != null)
            {
                QualitySettings.SetQualityLevel(_qualityDropdown.index, true);
            }

            if (_fullscreenToggle != null)
            {
                Screen.fullScreen = _fullscreenToggle.value;
            }

            if (_vsyncToggle != null)
            {
                QualitySettings.vSyncCount = _vsyncToggle.value ? 1 : 0;
            }

            // Note: Audio and gameplay settings would typically be applied through
            // dedicated audio and input managers in a real implementation

            SaveSettings();
            Debug.Log("[SettingsPanelManager] Settings applied");
        }

        /// <summary>
        /// Resets all settings to default values
        /// </summary>
        private void ResetToDefaults()
        {
            // Audio
            if (_masterVolumeSlider != null)
            {
                _masterVolumeSlider.value = DEFAULT_MASTER_VOLUME;
                UpdateVolumeLabel(_masterVolumeValue, DEFAULT_MASTER_VOLUME);
            }
            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.value = DEFAULT_MUSIC_VOLUME;
                UpdateVolumeLabel(_musicVolumeValue, DEFAULT_MUSIC_VOLUME);
            }
            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.value = DEFAULT_SFX_VOLUME;
                UpdateVolumeLabel(_sfxVolumeValue, DEFAULT_SFX_VOLUME);
            }

            // Graphics
            if (_qualityDropdown != null)
            {
                _qualityDropdown.index = QualitySettings.GetQualityLevel();
            }
            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.value = DEFAULT_FULLSCREEN;
            }
            if (_vsyncToggle != null)
            {
                _vsyncToggle.value = DEFAULT_VSYNC;
            }

            // Gameplay
            if (_mouseSensitivitySlider != null)
            {
                _mouseSensitivitySlider.value = DEFAULT_MOUSE_SENSITIVITY;
                UpdateSensitivityLabel(_mouseSensitivityValue, DEFAULT_MOUSE_SENSITIVITY);
            }
            if (_invertYAxisToggle != null)
            {
                _invertYAxisToggle.value = DEFAULT_INVERT_Y;
            }

            Debug.Log("[SettingsPanelManager] Settings reset to defaults");
        }
        #endregion

        #region UI Callbacks
        private void OnCloseButtonClicked()
        {
            HidePanel();
        }

        private void OnMasterVolumeChanged(ChangeEvent<float> evt)
        {
            UpdateVolumeLabel(_masterVolumeValue, evt.newValue);
        }

        private void OnMusicVolumeChanged(ChangeEvent<float> evt)
        {
            UpdateVolumeLabel(_musicVolumeValue, evt.newValue);
        }

        private void OnSFXVolumeChanged(ChangeEvent<float> evt)
        {
            UpdateVolumeLabel(_sfxVolumeValue, evt.newValue);
        }

        private void OnMouseSensitivityChanged(ChangeEvent<float> evt)
        {
            UpdateSensitivityLabel(_mouseSensitivityValue, evt.newValue);
        }

        private void OnApplyButtonClicked()
        {
            ApplySettings();
        }

        private void OnResetButtonClicked()
        {
            ResetToDefaults();
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Updates a volume label to display the percentage value
        /// </summary>
        private void UpdateVolumeLabel(Label label, float value)
        {
            if (label != null)
            {
                label.text = $"{Mathf.RoundToInt(value)}%";
            }
        }

        /// <summary>
        /// Updates a sensitivity label to display the numeric value
        /// </summary>
        private void UpdateSensitivityLabel(Label label, float value)
        {
            if (label != null)
            {
                label.text = $"{Mathf.RoundToInt(value)}";
            }
        }
        #endregion
    }
}
