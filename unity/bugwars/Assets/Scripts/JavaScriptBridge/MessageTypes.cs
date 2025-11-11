using System;
using UnityEngine;

namespace BugWars.JavaScriptBridge
{
    /// <summary>
    /// Standardized message type definitions for Unity <-> JavaScript communication.
    /// These constants ensure consistency between Unity and React/JavaScript layers.
    /// </summary>
    public static class MessageTypes
    {
        #region Unity -> JavaScript Events

        // Game lifecycle events
        public const string GAME_LOADED = "GameLoaded";
        public const string GAME_READY = "GameReady";
        public const string GAME_PAUSED = "GamePaused";
        public const string GAME_RESUMED = "GameResumed";
        public const string GAME_OVER = "GameOver";
        public const string LEVEL_STARTED = "LevelStarted";
        public const string LEVEL_COMPLETED = "LevelCompleted";

        // Player events
        public const string PLAYER_SPAWNED = "PlayerSpawned";
        public const string PLAYER_DIED = "PlayerDied";
        public const string PLAYER_RESPAWNED = "PlayerRespawned";
        public const string PLAYER_MOVED = "PlayerMoved";
        public const string PLAYER_STATS_UPDATED = "PlayerStatsUpdated";
        public const string PLAYER_INVENTORY_UPDATED = "PlayerInventoryUpdated";

        // Entity events
        public const string ENTITY_SPAWNED = "EntitySpawned";
        public const string ENTITY_DESTROYED = "EntityDestroyed";
        public const string ENTITY_UPDATED = "EntityUpdated";

        // Terrain events
        public const string CHUNK_LOADED = "ChunkLoaded";
        public const string CHUNK_UNLOADED = "ChunkUnloaded";
        public const string TERRAIN_GENERATED = "TerrainGenerated";

        // Score and progress events
        public const string SCORE_UPDATED = "ScoreUpdated";
        public const string ACHIEVEMENT_UNLOCKED = "AchievementUnlocked";
        public const string PROGRESS_UPDATED = "ProgressUpdated";

        // Data persistence events
        public const string DATA_SAVE_REQUEST = "DataSaveRequest";
        public const string DATA_LOAD_REQUEST = "DataLoadRequest";
        public const string DATA_SAVED = "DataSaved";
        public const string DATA_LOADED = "DataLoaded";

        // Error events
        public const string ERROR = "Error";
        public const string WARNING = "Warning";

        #endregion

        #region JavaScript -> Unity Methods

        // Session management
        public const string ON_SESSION_UPDATE = "OnSessionUpdate";
        public const string ON_SESSION_ENDED = "OnSessionEnded";

        // Data management
        public const string ON_DATA_SAVED = "OnDataSaved";
        public const string ON_DATA_LOADED = "OnDataLoaded";

        // Commands
        public const string ON_COMMAND = "OnCommand";
        public const string ON_MESSAGE = "OnMessage";

        // Binary data
        public const string ON_BINARY_DATA = "OnBinaryData";
        public const string ON_ARRAY_DATA = "OnArrayData";

        // Player controls (for testing/admin)
        public const string ON_PLAYER_SPAWN = "OnPlayerSpawn";
        public const string ON_PLAYER_TELEPORT = "OnPlayerTeleport";

        #endregion
    }

    /// <summary>
    /// Standard message wrapper for Unity -> JavaScript communication.
    /// </summary>
    [Serializable]
    public class UnityMessage
    {
        public string type;
        public string data;
        public string timestamp;

        public UnityMessage(string messageType, object messageData)
        {
            type = messageType;
            data = messageData != null ? JsonUtility.ToJson(messageData) : string.Empty;
            timestamp = DateTime.UtcNow.ToString("o");
        }
    }

    /// <summary>
    /// Standard message wrapper for JavaScript -> Unity communication.
    /// </summary>
    [Serializable]
    public class JavaScriptMessage
    {
        public string type;
        public string payload;
        public string timestamp;
    }

    #region Game Event Data Structures

    /// <summary>
    /// Game state change event data.
    /// </summary>
    [Serializable]
    public class GameStateEvent
    {
        public string state; // "loaded", "ready", "paused", "resumed", "over"
        public string reason;
        public float gameTime;
    }

    /// <summary>
    /// Level event data.
    /// </summary>
    [Serializable]
    public class LevelEvent
    {
        public int levelNumber;
        public string levelName;
        public bool completed;
        public float completionTime;
        public int score;
    }

    /// <summary>
    /// Player spawn event data.
    /// </summary>
    [Serializable]
    public class PlayerSpawnEvent
    {
        public string playerId;
        public string playerName;
        public Vector3Data position;
        public int health;
        public int maxHealth;
    }

    /// <summary>
    /// Player stats update event.
    /// </summary>
    [Serializable]
    public class PlayerStatsEvent
    {
        public string playerId;
        public int health;
        public int maxHealth;
        public int level;
        public int experience;
        public float[] customStats;
    }

    /// <summary>
    /// Entity spawn/update event.
    /// </summary>
    [Serializable]
    public class EntityEvent
    {
        public string entityId;
        public string entityType;
        public Vector3Data position;
        public QuaternionData rotation;
        public int health;
        public bool isActive;
        public string state;
    }

    /// <summary>
    /// Terrain chunk event.
    /// </summary>
    [Serializable]
    public class ChunkEvent
    {
        public int chunkX;
        public int chunkZ;
        public bool isLoaded;
        public int vertexCount;
        public float generationTime;
    }

    /// <summary>
    /// Score update event.
    /// </summary>
    [Serializable]
    public class ScoreEvent
    {
        public int score;
        public int delta;
        public string reason;
        public string playerId;
    }

    /// <summary>
    /// Achievement event.
    /// </summary>
    [Serializable]
    public class AchievementEvent
    {
        public string achievementId;
        public string achievementName;
        public string description;
        public int points;
        public string playerId;
    }

    /// <summary>
    /// Error event.
    /// </summary>
    [Serializable]
    public class ErrorEvent
    {
        public string errorType;
        public string message;
        public string stackTrace;
        public string source;
        public string timestamp;
    }

    /// <summary>
    /// Data save/load request.
    /// </summary>
    [Serializable]
    public class DataRequest
    {
        public string table;
        public string userId;
        public string filters;
        public object data;
    }

    /// <summary>
    /// Data operation result.
    /// </summary>
    [Serializable]
    public class DataResult
    {
        public bool success;
        public string table;
        public string message;
        public string error;
        public string timestamp;
    }

    /// <summary>
    /// Command message from JavaScript.
    /// </summary>
    [Serializable]
    public class CommandMessage
    {
        public string command;
        public string[] args;
        public string userId;
    }

    #endregion

    #region Helper Extension Methods

    /// <summary>
    /// Extension methods for easy message sending.
    /// </summary>
    public static class MessageExtensions
    {
        /// <summary>
        /// Send a typed message to web with automatic serialization.
        /// </summary>
        public static void SendTypedMessage<T>(string messageType, T data) where T : class
        {
            WebGLBridge.SendToWeb(messageType, data);
        }

        /// <summary>
        /// Send a game state event.
        /// </summary>
        public static void SendGameState(string state, string reason = null, float gameTime = 0f)
        {
            SendTypedMessage(MessageTypes.GAME_READY, new GameStateEvent
            {
                state = state,
                reason = reason,
                gameTime = gameTime
            });
        }

        /// <summary>
        /// Send a player spawned event.
        /// </summary>
        public static void SendPlayerSpawned(string playerId, string playerName, Vector3 position, int health, int maxHealth)
        {
            SendTypedMessage(MessageTypes.PLAYER_SPAWNED, new PlayerSpawnEvent
            {
                playerId = playerId,
                playerName = playerName,
                position = new Vector3Data { x = position.x, y = position.y, z = position.z },
                health = health,
                maxHealth = maxHealth
            });
        }

        /// <summary>
        /// Send a chunk loaded event.
        /// </summary>
        public static void SendChunkLoaded(int chunkX, int chunkZ, int vertexCount, float generationTime)
        {
            SendTypedMessage(MessageTypes.CHUNK_LOADED, new ChunkEvent
            {
                chunkX = chunkX,
                chunkZ = chunkZ,
                isLoaded = true,
                vertexCount = vertexCount,
                generationTime = generationTime
            });
        }

        /// <summary>
        /// Send a score update event.
        /// </summary>
        public static void SendScoreUpdate(int score, int delta, string reason, string playerId)
        {
            SendTypedMessage(MessageTypes.SCORE_UPDATED, new ScoreEvent
            {
                score = score,
                delta = delta,
                reason = reason,
                playerId = playerId
            });
        }

        /// <summary>
        /// Send an error event.
        /// </summary>
        public static void SendError(string errorType, string message, string source = null)
        {
            SendTypedMessage(MessageTypes.ERROR, new ErrorEvent
            {
                errorType = errorType,
                message = message,
                source = source ?? "Unity",
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }
    }

    #endregion
}
