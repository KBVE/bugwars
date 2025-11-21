using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;
using R3;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace BugWars.UI
{
    /// <summary>
    /// HUD Manager - Manages the Heads-Up Display
    /// Always visible during gameplay, displays health, stats, and game information
    /// Directly observes EntityManager.PlayerData via R3 reactive properties
    /// Uses IAsyncStartable + UniTask for efficient async initialization without frame blocking
    /// No event subscriptions needed - pure reactive architecture
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDManager : MonoBehaviour, IAsyncStartable
    {
        #region Fields
        // Cancellation token for async operations
        private CancellationTokenSource _cts;

        // R3 reactive subscriptions (auto-disposed on destroy)
        private IDisposable _displayNameSubscription;
        private IDisposable _levelSubscription;
        private IDisposable _experienceSubscription;
        private IDisposable _scoreSubscription;
        private IDisposable _authStatusSubscription;

        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _hudContainer;

        // Player Identity elements
        private Label _playerNameText;
        private VisualElement _authStatusIndicator;

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
            // Create cancellation token source
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// IAsyncStartable - VContainer calls this for async initialization
        /// Waits for PlayerData without blocking frames
        /// </summary>
        public async UniTask StartAsync(CancellationToken cancellationToken)
        {
            InitializeUI();

            // Start with HUD visible
            ShowHUD();

            // Wait for EntityManager.PlayerData to be available (async, no frame blocking)
            await SetupReactiveSubscriptionsAsync(cancellationToken);
        }

        private void OnDestroy()
        {
            // Cancel any ongoing async operations
            _cts?.Cancel();
            _cts?.Dispose();

            // Dispose all R3 subscriptions
            _displayNameSubscription?.Dispose();
            _levelSubscription?.Dispose();
            _experienceSubscription?.Dispose();
            _scoreSubscription?.Dispose();
            _authStatusSubscription?.Dispose();
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
            _authStatusIndicator = _root.Q<VisualElement>("AuthStatusIndicator");

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
            if (_authStatusIndicator == null) Debug.LogWarning("[HUDManager] AuthStatusIndicator not found in UXML!");
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

            // Set initial auth status to guest (yellow) until authentication is confirmed
            UpdateAuthStatus(false);
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
            Debug.Log($"[HUDManager] UpdatePlayerName called with: '{playerName}' | _playerNameText null? {_playerNameText == null}");
            if (_playerNameText != null)
            {
                _playerNameText.text = _currentPlayerName;
                Debug.Log($"[HUDManager] Set _playerNameText.text to: '{_playerNameText.text}'");
            }
        }

        /// <summary>
        /// Updates the authentication status indicator
        /// </summary>
        /// <param name="isAuthenticated">Whether the player is authenticated</param>
        public void UpdateAuthStatus(bool isAuthenticated)
        {
            if (_authStatusIndicator == null)
            {
                Debug.LogWarning("[HUDManager] Cannot update auth status - indicator is null");
                return;
            }

            // Remove all status classes
            _authStatusIndicator.RemoveFromClassList("auth-guest");
            _authStatusIndicator.RemoveFromClassList("auth-authenticated");
            _authStatusIndicator.RemoveFromClassList("auth-error");

            // Add appropriate class based on authentication state
            if (isAuthenticated)
            {
                _authStatusIndicator.AddToClassList("auth-authenticated");
                Debug.Log("[HUDManager] Auth status: AUTHENTICATED (Green)");
            }
            else
            {
                _authStatusIndicator.AddToClassList("auth-guest");
                Debug.Log("[HUDManager] Auth status: GUEST (Yellow)");
            }
        }

        /// <summary>
        /// Sets the authentication status to error state
        /// </summary>
        /// <param name="errorMessage">Optional error message to log</param>
        public void SetAuthError(string errorMessage = "")
        {
            if (_authStatusIndicator == null) return;

            _authStatusIndicator.RemoveFromClassList("auth-guest");
            _authStatusIndicator.RemoveFromClassList("auth-authenticated");
            _authStatusIndicator.AddToClassList("auth-error");

            Debug.LogWarning($"[HUDManager] Auth status: ERROR (Red) - {errorMessage}");
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

        /// <summary>
        /// Setup reactive subscriptions to EntityManager.PlayerData for automatic UI updates
        /// Pure reactive architecture - no events needed
        /// Uses UniTask for efficient async waiting without frame blocking
        /// </summary>
        private async UniTask SetupReactiveSubscriptionsAsync(CancellationToken cancellationToken)
        {
            // Wait for EntityManager.PlayerData to be available (async, no frame blocking)
            BugWars.Entity.PlayerData playerData = null;
            while (playerData == null && !cancellationToken.IsCancellationRequested)
            {
                playerData = BugWars.Entity.EntityManager.Instance?.PlayerData;
                if (playerData == null)
                {
                    // Wait one frame before checking again (non-blocking)
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.LogWarning("[HUDManager] Setup cancelled before PlayerData was available");
                return;
            }

            Debug.Log("[HUDManager] PlayerData available, setting up reactive subscriptions");
            Debug.Log($"[HUDManager] Current display name before subscription: {playerData.GetBestDisplayName()}");

            // Subscribe to display name changes
            _displayNameSubscription = playerData.DisplayNameObservable
                .Subscribe(displayName =>
                {
                    Debug.Log($"[HUDManager] DisplayName subscription fired: '{displayName}'");
                    UpdatePlayerName(displayName);
                });

            // Subscribe to level changes
            _levelSubscription = playerData.LevelObservable
                .Subscribe(level => UpdateLevel(level));

            // Subscribe to experience changes (capture playerData for level access)
            _experienceSubscription = playerData.ExperienceObservable
                .Subscribe(xp => UpdateExperience(xp, playerData.Level));

            // Subscribe to score changes
            _scoreSubscription = playerData.ScoreObservable
                .Subscribe(score => UpdateScore(score));

            // Subscribe to authentication status changes
            _authStatusSubscription = playerData.IsAuthenticatedObservable
                .Subscribe(isAuth =>
                {
                    Debug.Log($"[HUDManager] Auth subscription fired: {(isAuth ? "✓ Authenticated" : "Guest")} | Player: {playerData.GetBestDisplayName()}");
                    UpdateAuthStatus(isAuth);
                });

            Debug.Log($"[HUDManager] Reactive subscriptions established for {playerData.GetBestDisplayName()}");
        }
        #endregion
    }
}
