using UnityEngine;
using UnityEngine.UIElements;

namespace BugWars.UI
{
    /// <summary>
    /// HUD Manager - Manages the Heads-Up Display
    /// Always visible during gameplay, displays health, stats, and game information
    /// Managed by VContainer
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDManager : MonoBehaviour
    {
        #region Fields
        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _hudContainer;

        // Player Identity elements
        private Label _playerNameText;

        // Health UI elements
        private Label _healthLabel;
        private Label _healthText;
        private VisualElement _healthBarFill;

        // Game Info elements
        private Label _waveText;
        private Label _scoreText;
        private Label _playTimeText;

        // Player Stats elements
        private Label _levelText;
        private Label _experienceText;
        private Label _speedText;
        private Label _damageText;

        // Current values (for future integration with game systems)
        private string _currentPlayerName = "Player";
        private float _currentHealth = 100f;
        private float _maxHealth = 100f;
        private int _currentWave = 1;
        private int _currentScore = 0;
        private int _currentLevel = 1;
        private int _currentExperience = 0;
        private float _currentPlayTime = 0f;
        private float _currentSpeed = 5.0f;
        private int _currentDamage = 10;
        #endregion

        #region Properties
        /// <summary>
        /// Returns whether the HUD is currently visible
        /// </summary>
        public bool IsVisible { get; private set; } = true;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Component initialized
        }

        private void Start()
        {
            InitializeUI();

            // Start with HUD visible
            ShowHUD();
        }

        private void OnDestroy()
        {
            // Cleanup if needed
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the UI elements
        /// </summary>
        private void InitializeUI()
        {
            _uiDocument = GetComponent<UIDocument>();

            if (_uiDocument == null)
            {
                Debug.LogError("[HUDManager] UIDocument component not found!");
                return;
            }

            _root = _uiDocument.rootVisualElement;
            _hudContainer = _root.Q<VisualElement>("HUDContainer");

            if (_hudContainer == null)
            {
                Debug.LogError("[HUDManager] HUDContainer not found in UXML!");
                return;
            }

            // Get Player Identity references
            _playerNameText = _root.Q<Label>("PlayerNameText");

            // Get Health UI references
            _healthLabel = _root.Q<Label>("HealthLabel");
            _healthText = _root.Q<Label>("HealthText");
            _healthBarFill = _root.Q<VisualElement>("HealthBarFill");

            // Get Game Info references
            _waveText = _root.Q<Label>("WaveText");
            _scoreText = _root.Q<Label>("ScoreText");
            _playTimeText = _root.Q<Label>("PlayTimeText");

            // Get Player Stats references
            _levelText = _root.Q<Label>("LevelText");
            _experienceText = _root.Q<Label>("ExperienceText");
            _speedText = _root.Q<Label>("SpeedText");
            _damageText = _root.Q<Label>("DamageText");

            // Verify all elements were found
            if (_playerNameText == null) Debug.LogWarning("[HUDManager] PlayerNameText not found in UXML!");
            if (_healthText == null) Debug.LogWarning("[HUDManager] HealthText not found in UXML!");
            if (_healthBarFill == null) Debug.LogWarning("[HUDManager] HealthBarFill not found in UXML!");
            if (_waveText == null) Debug.LogWarning("[HUDManager] WaveText not found in UXML!");
            if (_scoreText == null) Debug.LogWarning("[HUDManager] ScoreText not found in UXML!");
            if (_playTimeText == null) Debug.LogWarning("[HUDManager] PlayTimeText not found in UXML!");
            if (_levelText == null) Debug.LogWarning("[HUDManager] LevelText not found in UXML!");
            if (_experienceText == null) Debug.LogWarning("[HUDManager] ExperienceText not found in UXML!");
            if (_speedText == null) Debug.LogWarning("[HUDManager] SpeedText not found in UXML!");
            if (_damageText == null) Debug.LogWarning("[HUDManager] DamageText not found in UXML!");

            // Update UI with initial values
            UpdateAllUI();
        }
        #endregion

        #region Public Methods - Visibility
        /// <summary>
        /// Shows the HUD
        /// </summary>
        public void ShowHUD()
        {
            if (_hudContainer != null)
            {
                _hudContainer.style.display = DisplayStyle.Flex;
                IsVisible = true;
            }
            else
            {
                Debug.LogError("[HUDManager] Cannot show HUD - _hudContainer is null!");
            }
        }

        /// <summary>
        /// Hides the HUD
        /// </summary>
        public void HideHUD()
        {
            if (_hudContainer != null)
            {
                _hudContainer.style.display = DisplayStyle.None;
                IsVisible = false;
            }
            else
            {
                Debug.LogError("[HUDManager] Cannot hide HUD - _hudContainer is null!");
            }
        }

        /// <summary>
        /// Toggles the HUD visibility
        /// </summary>
        public void ToggleHUD()
        {
            if (IsVisible)
            {
                HideHUD();
            }
            else
            {
                ShowHUD();
            }
        }
        #endregion

        #region Public Methods - Update Data
        /// <summary>
        /// Updates the player name display
        /// </summary>
        /// <param name="playerName">Player's name</param>
        public void UpdatePlayerName(string playerName)
        {
            _currentPlayerName = playerName;
            if (_playerNameText != null)
            {
                _playerNameText.text = _currentPlayerName;
            }
        }

        /// <summary>
        /// Updates the health display
        /// </summary>
        /// <param name="current">Current health value</param>
        /// <param name="max">Maximum health value</param>
        public void UpdateHealth(float current, float max)
        {
            _currentHealth = Mathf.Clamp(current, 0, max);
            _maxHealth = max;

            if (_healthText != null)
            {
                _healthText.text = $"{Mathf.RoundToInt(_currentHealth)} / {Mathf.RoundToInt(_maxHealth)}";
            }

            if (_healthBarFill != null)
            {
                float healthPercent = _maxHealth > 0 ? (_currentHealth / _maxHealth) * 100f : 0f;
                _healthBarFill.style.width = Length.Percent(healthPercent);

                // Update health bar color based on health percentage
                UpdateHealthBarColor(healthPercent);
            }
        }

        /// <summary>
        /// Updates the wave display
        /// </summary>
        /// <param name="wave">Current wave number</param>
        public void UpdateWave(int wave)
        {
            _currentWave = wave;
            if (_waveText != null)
            {
                _waveText.text = $"Wave: {_currentWave}";
            }
        }

        /// <summary>
        /// Updates the score display
        /// </summary>
        /// <param name="score">Current score</param>
        public void UpdateScore(int score)
        {
            _currentScore = score;
            if (_scoreText != null)
            {
                _scoreText.text = $"Score: {_currentScore}";
            }
        }

        /// <summary>
        /// Updates the speed stat display
        /// </summary>
        /// <param name="speed">Current speed value</param>
        public void UpdateSpeed(float speed)
        {
            _currentSpeed = speed;
            if (_speedText != null)
            {
                _speedText.text = $"Speed: {_currentSpeed:F1}";
            }
        }

        /// <summary>
        /// Updates the damage stat display
        /// </summary>
        /// <param name="damage">Current damage value</param>
        public void UpdateDamage(int damage)
        {
            _currentDamage = damage;
            if (_damageText != null)
            {
                _damageText.text = $"Damage: {_currentDamage}";
            }
        }

        /// <summary>
        /// Updates the level display
        /// </summary>
        /// <param name="level">Current level</param>
        public void UpdateLevel(int level)
        {
            _currentLevel = level;
            if (_levelText != null)
            {
                _levelText.text = $"Level: {_currentLevel}";
            }
        }

        /// <summary>
        /// Updates the experience display
        /// </summary>
        /// <param name="currentXP">Current experience points</param>
        /// <param name="level">Current level (used to calculate XP needed)</param>
        public void UpdateExperience(int currentXP, int level)
        {
            _currentExperience = currentXP;
            _currentLevel = level;
            if (_experienceText != null)
            {
                int xpForNextLevel = level * 100; // Matches PlayerData calculation
                _experienceText.text = $"XP: {_currentExperience}/{xpForNextLevel}";
            }
        }

        /// <summary>
        /// Updates the play time display
        /// </summary>
        /// <param name="playTime">Current play time in seconds</param>
        public void UpdatePlayTime(float playTime)
        {
            _currentPlayTime = playTime;
            if (_playTimeText != null)
            {
                int hours = Mathf.FloorToInt(playTime / 3600f);
                int minutes = Mathf.FloorToInt((playTime % 3600f) / 60f);
                int seconds = Mathf.FloorToInt(playTime % 60f);
                _playTimeText.text = $"Time: {hours:00}:{minutes:00}:{seconds:00}";
            }
        }

        /// <summary>
        /// Updates all UI elements with current values
        /// </summary>
        public void UpdateAllUI()
        {
            UpdatePlayerName(_currentPlayerName);
            UpdateHealth(_currentHealth, _maxHealth);
            UpdateWave(_currentWave);
            UpdateScore(_currentScore);
            UpdateLevel(_currentLevel);
            UpdateExperience(_currentExperience, _currentLevel);
            UpdatePlayTime(_currentPlayTime);
            UpdateSpeed(_currentSpeed);
            UpdateDamage(_currentDamage);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Updates the health bar color based on health percentage
        /// </summary>
        /// <param name="healthPercent">Health as a percentage (0-100)</param>
        private void UpdateHealthBarColor(float healthPercent)
        {
            if (_healthBarFill == null) return;

            // Remove all health state classes
            _healthBarFill.RemoveFromClassList("low-health");
            _healthBarFill.RemoveFromClassList("medium-health");

            // Add appropriate class based on health percentage
            if (healthPercent <= 25f)
            {
                _healthBarFill.AddToClassList("low-health");
            }
            else if (healthPercent <= 50f)
            {
                _healthBarFill.AddToClassList("medium-health");
            }
            // Above 50% uses the default Lime color
        }
        #endregion
    }
}
