using UnityEngine;

namespace BugWars.Entity
{
    /// <summary>
    /// Holds persistent player data tracked by EntityManager
    /// Can be extended to include stats, inventory, preferences, etc.
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        [Header("Identity")]
        [SerializeField] private string playerName = "Player";
        [SerializeField] private string playerId = "";

        [Header("Stats")]
        [SerializeField] private int level = 1;
        [SerializeField] private int experience = 0;
        [SerializeField] private int score = 0;

        [Header("Session Info")]
        [SerializeField] private float playTime = 0f;
        [SerializeField] private Vector3 lastKnownPosition = Vector3.zero;

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
            set => level = Mathf.Max(1, value);
        }

        public int Experience
        {
            get => experience;
            set => experience = Mathf.Max(0, value);
        }

        public int Score
        {
            get => score;
            set => score = Mathf.Max(0, value);
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

            // Simple level up calculation (100 XP per level)
            int xpForNextLevel = level * 100;
            if (experience >= xpForNextLevel)
            {
                level++;
                experience -= xpForNextLevel;
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
        }

        /// <summary>
        /// Clone this player data
        /// </summary>
        public PlayerData Clone()
        {
            PlayerData clone = new PlayerData();
            clone.playerName = this.playerName;
            clone.playerId = this.playerId;
            clone.level = this.level;
            clone.experience = this.experience;
            clone.score = this.score;
            clone.playTime = this.playTime;
            clone.lastKnownPosition = this.lastKnownPosition;
            return clone;
        }

        public override string ToString()
        {
            return $"PlayerData: {playerName} (ID: {playerId}) | Level {level} | XP: {experience} | Score: {score} | Time: {GetFormattedPlayTime()}";
        }
    }
}
