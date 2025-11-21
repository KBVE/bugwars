using System;
using Cysharp.Threading.Tasks;

namespace BugWars.Network
{
    /// <summary>
    /// Interface for any data type that can be synchronized with the server.
    /// Provides a clean, extensible pattern for bidirectional sync.
    ///
    /// Examples:
    /// - PlayerData (stats, level, experience)
    /// - EntityInventory (items, equipment)
    /// - QuestProgress (active quests, completions)
    /// - AchievementData (unlocked achievements)
    /// - BuildingState (placed structures)
    /// </summary>
    public interface INetworkSyncable
    {
        /// <summary>
        /// Unique identifier for this syncable data type.
        /// Used to route server responses to correct handler.
        /// </summary>
        string SyncId { get; }

        /// <summary>
        /// Serialize current state to JSON for sending to server
        /// </summary>
        string SerializeForSync();

        /// <summary>
        /// Deserialize and apply state from server response
        /// </summary>
        void DeserializeFromSync(string json);

        /// <summary>
        /// Called when connection to server is established.
        /// Implementer should request initial state from server.
        /// </summary>
        UniTask OnConnected(WebSocketManager wsManager);

        /// <summary>
        /// Called when connection to server is lost.
        /// Implementer can queue changes for later sync.
        /// </summary>
        void OnDisconnected();

        /// <summary>
        /// Check if local data is dirty (needs to be pushed to server)
        /// </summary>
        bool IsDirty { get; }

        /// <summary>
        /// Mark data as clean (synced with server)
        /// </summary>
        void MarkClean();

        /// <summary>
        /// Mark data as dirty (needs sync)
        /// </summary>
        void MarkDirty();
    }

    /// <summary>
    /// Sync strategy for different data types
    /// </summary>
    public enum SyncStrategy
    {
        /// <summary>
        /// Push changes to server immediately when they occur
        /// Best for: Critical state changes (inventory transactions, combat)
        /// </summary>
        Immediate,

        /// <summary>
        /// Batch changes and push periodically (every N seconds)
        /// Best for: Frequent but non-critical updates (position, stats)
        /// </summary>
        Batched,

        /// <summary>
        /// Only sync when explicitly requested
        /// Best for: Infrequent changes (settings, preferences)
        /// </summary>
        Manual,

        /// <summary>
        /// Server is authoritative, only pull from server
        /// Best for: Read-only data (leaderboards, server config)
        /// </summary>
        ServerAuthoritative
    }

    /// <summary>
    /// Metadata for configuring sync behavior
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NetworkSyncableAttribute : Attribute
    {
        public string SyncId { get; set; }
        public SyncStrategy Strategy { get; set; } = SyncStrategy.Immediate;
        public float BatchIntervalSeconds { get; set; } = 5f;

        public NetworkSyncableAttribute(string syncId)
        {
            SyncId = syncId;
        }
    }
}
