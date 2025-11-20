using UnityEngine;
using R3;

namespace BugWars.Entity
{
    /// <summary>
    /// Holds persistent player data tracked by EntityManager
    /// Can be extended to include stats, inventory, preferences, etc.
    /// Uses R3 ReactiveProperties for reactive observation of changes
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        [Header("Identity")]
        [SerializeField] private string playerName = "Player";
        [SerializeField] private string playerId = "";
        [SerializeField] private string displayName = "Player";
        [SerializeField] private string username = "";
        [SerializeField] private string email = "";
        [SerializeField] private string avatarUrl = "";

        [Header("Stats")]
        [SerializeField] private int level = 1;
        [SerializeField] private int experience = 0;
        [SerializeField] private int score = 0;

        [Header("Session Info")]
        [SerializeField] private float playTime = 0f;
        [SerializeField] private Vector3 lastKnownPosition = Vector3.zero;
        [SerializeField] private bool isAuthenticated = false;

        [Header("Authentication Tokens")]
        [SerializeField] private string accessToken = "";
        [SerializeField] private string refreshToken = "";
        [SerializeField] private long expiresAt = 0;

        // Reactive properties for observing authentication state changes
        private readonly ReactiveProperty<bool> _isAuthenticatedReactive = new(false);
        private readonly ReactiveProperty<string> _displayNameReactive = new("Player");
        private readonly ReactiveProperty<int> _levelReactive = new(1);
        private readonly ReactiveProperty<int> _experienceReactive = new(0);
        private readonly ReactiveProperty<int> _scoreReactive = new(0);

        // Read-only reactive observables for external subscribers
        public ReadOnlyReactiveProperty<bool> IsAuthenticatedObservable => _isAuthenticatedReactive;
        public ReadOnlyReactiveProperty<string> DisplayNameObservable => _displayNameReactive;
        public ReadOnlyReactiveProperty<int> LevelObservable => _levelReactive;
        public ReadOnlyReactiveProperty<int> ExperienceObservable => _experienceReactive;
        public ReadOnlyReactiveProperty<int> ScoreObservable => _scoreReactive;

        // Properties for easy access
        public string PlayerName
        {
            get => playerName;
            set => playerName = value;
        }

        public string PlayerId
        {
            get => playerId;
            set => playerId = value;
        }

        public int Level
        {
            get => level;
            set
            {
                level = Mathf.Max(1, value);
                _levelReactive.Value = level;
            }
        }

        public int Experience
        {
            get => experience;
            set
            {
                experience = Mathf.Max(0, value);
                _experienceReactive.Value = experience;
            }
        }

        public int Score
        {
            get => score;
            set
            {
                score = Mathf.Max(0, value);
                _scoreReactive.Value = score;
            }
        }

        public float PlayTime
        {
            get => playTime;
            set => playTime = Mathf.Max(0f, value);
        }

        public Vector3 LastKnownPosition
        {
            get => lastKnownPosition;
            set => lastKnownPosition = value;
        }

        public string DisplayName
        {
            get => displayName;
            set
            {
                displayName = value;
                _displayNameReactive.Value = GetBestDisplayName();
            }
        }

        public string Username
        {
            get => username;
            set => username = value;
        }

        public string Email
        {
            get => email;
            set => email = value;
        }

        public string AvatarUrl
        {
            get => avatarUrl;
            set => avatarUrl = value;
        }

        public bool IsAuthenticated
        {
            get => isAuthenticated;
            set
            {
                isAuthenticated = value;
                _isAuthenticatedReactive.Value = value;
            }
        }

        public string AccessToken
        {
            get => accessToken;
            set => accessToken = value;
        }

        public string RefreshToken
        {
            get => refreshToken;
            set => refreshToken = value;
        }

        public long ExpiresAt
        {
            get => expiresAt;
            set => expiresAt = value;
        }

        /// <summary>
        /// Create default player data
        /// </summary>
        public PlayerData()
        {
            playerName = "Player";
            playerId = System.Guid.NewGuid().ToString();
            level = 1;
            experience = 0;
            score = 0;
            playTime = 0f;
            lastKnownPosition = Vector3.zero;
        }

        /// <summary>
        /// Create player data with a specific name
        /// </summary>
        public PlayerData(string name)
        {
            playerName = name;
            playerId = System.Guid.NewGuid().ToString();
            level = 1;
            experience = 0;
            score = 0;
            playTime = 0f;
            lastKnownPosition = Vector3.zero;
        }

        /// <summary>
        /// Reset player data to defaults
        /// </summary>
        public void Reset()
        {
            playerName = "Player";
            level = 1;
            experience = 0;
            score = 0;
            playTime = 0f;
            lastKnownPosition = Vector3.zero;
        }

        /// <summary>
        /// Get formatted play time as string (HH:MM:SS)
        /// </summary>
        public string GetFormattedPlayTime()
        {
            int hours = Mathf.FloorToInt(playTime / 3600f);
            int minutes = Mathf.FloorToInt((playTime % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(playTime % 60f);
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// Add experience and handle level ups
        /// </summary>
        public bool AddExperience(int amount)
        {
            if (amount <= 0) return false;

            experience += amount;
            _experienceReactive.Value = experience;

            // Simple level up calculation (100 XP per level)
            int xpForNextLevel = level * 100;
            if (experience >= xpForNextLevel)
            {
                level++;
                _levelReactive.Value = level;
                experience -= xpForNextLevel;
                _experienceReactive.Value = experience;
                return true; // Leveled up
            }

            return false; // No level up
        }

        /// <summary>
        /// Add score
        /// </summary>
        public void AddScore(int points)
        {
            score += points;
            score = Mathf.Max(0, score);
            _scoreReactive.Value = score;
        }

        /// <summary>
        /// Update player data from authenticated session
        /// </summary>
        public void UpdateFromSession(string userId, string userDisplayName, string userUsername, string userEmail, string userAvatarUrl, string userAccessToken, string userRefreshToken, long tokenExpiresAt)
        {
            playerId = userId;
            displayName = userDisplayName;
            username = userUsername;
            email = userEmail;
            avatarUrl = userAvatarUrl;
            accessToken = userAccessToken;
            refreshToken = userRefreshToken;
            expiresAt = tokenExpiresAt;
            isAuthenticated = !string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(accessToken);

            // Update playerName to displayName for consistency
            if (!string.IsNullOrEmpty(displayName))
            {
                playerName = displayName;
            }
            else if (!string.IsNullOrEmpty(username))
            {
                playerName = username;
            }

            // Trigger reactive property updates
            _isAuthenticatedReactive.Value = isAuthenticated;
            _displayNameReactive.Value = GetBestDisplayName();
        }

        /// <summary>
        /// Get the best display name available
        /// Priority: displayName > username > playerName > "Player"
        /// </summary>
        public string GetBestDisplayName()
        {
            if (!string.IsNullOrEmpty(displayName))
                return displayName;
            if (!string.IsNullOrEmpty(username))
                return username;
            if (!string.IsNullOrEmpty(playerName))
                return playerName;
            return "Player";
        }

        /// <summary>
        /// Check if the access token is expired or about to expire.
        /// </summary>
        public bool IsTokenExpired()
        {
            if (string.IsNullOrEmpty(accessToken))
                return true;

            // Convert Unix timestamp to DateTime
            var expiresAtTime = System.DateTimeOffset.FromUnixTimeSeconds(expiresAt).UtcDateTime;
            var now = System.DateTime.UtcNow;

            // Consider token expired if it expires in less than 5 minutes
            return (expiresAtTime - now).TotalMinutes < 5;
        }

        /// <summary>
        /// Get a valid access token. Returns null if token is expired.
        /// </summary>
        public string GetValidAccessToken()
        {
            return IsTokenExpired() ? null : accessToken;
        }

        /// <summary>
        /// Clone this player data
        /// </summary>
        public PlayerData Clone()
        {
            PlayerData clone = new PlayerData();
            clone.playerName = this.playerName;
            clone.playerId = this.playerId;
            clone.displayName = this.displayName;
            clone.username = this.username;
            clone.email = this.email;
            clone.avatarUrl = this.avatarUrl;
            clone.isAuthenticated = this.isAuthenticated;
            clone.accessToken = this.accessToken;
            clone.refreshToken = this.refreshToken;
            clone.expiresAt = this.expiresAt;
            clone.level = this.level;
            clone.experience = this.experience;
            clone.score = this.score;
            clone.playTime = this.playTime;
            clone.lastKnownPosition = this.lastKnownPosition;
            return clone;
        }

        public override string ToString()
        {
            string authStatus = isAuthenticated ? $"✓ {email}" : "Guest";
            return $"PlayerData: {GetBestDisplayName()} ({authStatus}) | Level {level} | XP: {experience} | Score: {score} | Time: {GetFormattedPlayTime()}";
        }
    }
}
