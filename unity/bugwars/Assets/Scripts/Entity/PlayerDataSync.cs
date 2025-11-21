using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using BugWars.Network;
using R3;

namespace BugWars.Entity
{
    /// <summary>
    /// Syncs player data (stats, level, experience, score) with the server.
    /// Uses batched sync strategy to avoid spamming server with frequent stat updates.
    /// </summary>
    [NetworkSyncable("player_data", Strategy = SyncStrategy.Batched, BatchIntervalSeconds = 10f)]
    public class PlayerDataSync : INetworkSyncable
    {
        private readonly PlayerData _playerData;
        private bool _isDirty = false;

        // Track what changed to avoid full syncs
        private bool _statsChanged = false;
        private bool _positionChanged = false;

        public string SyncId => "player_data";
        public bool IsDirty => _isDirty;

        public PlayerDataSync(PlayerData playerData)
        {
            _playerData = playerData ?? throw new ArgumentNullException(nameof(playerData));

            // Subscribe to reactive property changes to mark as dirty
            _playerData.LevelObservable.Subscribe(_ => OnStatsChanged());
            _playerData.ExperienceObservable.Subscribe(_ => OnStatsChanged());
            _playerData.ScoreObservable.Subscribe(_ => OnStatsChanged());

            Debug.Log("[PlayerDataSync] Initialized with reactive subscriptions");
        }

        #region INetworkSyncable Implementation

        public string SerializeForSync()
        {
            var syncData = new PlayerDataSyncMessage
            {
                player_id = _playerData.PlayerId,
                display_name = _playerData.DisplayName,
                level = _playerData.Level,
                experience = _playerData.Experience,
                score = _playerData.Score,
                play_time = _playerData.PlayTime,
                position = new PositionData
                {
                    x = _playerData.LastKnownPosition.x,
                    y = _playerData.LastKnownPosition.y,
                    z = _playerData.LastKnownPosition.z
                }
            };

            string json = JsonConvert.SerializeObject(syncData);
            Debug.Log($"[PlayerDataSync] Serialized: {json}");
            return json;
        }

        public void DeserializeFromSync(string json)
        {
            try
            {
                var syncData = JsonConvert.DeserializeObject<PlayerDataSyncMessage>(json);

                // Update local player data from server
                _playerData.Level = syncData.level;
                _playerData.Experience = syncData.experience;
                _playerData.Score = syncData.score;
                _playerData.PlayTime = syncData.play_time;
                _playerData.LastKnownPosition = new Vector3(syncData.position.x, syncData.position.y, syncData.position.z);

                Debug.Log($"[PlayerDataSync] Deserialized from server: Level {syncData.level}, XP {syncData.experience}, Score {syncData.score}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataSync] Failed to deserialize: {e.Message}");
            }
        }

        public async UniTask OnConnected(WebSocketManager wsManager)
        {
            Debug.Log("[PlayerDataSync] Connected to server, requesting initial player data");

            // Request initial state from server
            wsManager.SendMessage("get_player_data", "");

            await UniTask.Yield();
        }

        public void OnDisconnected()
        {
            Debug.Log("[PlayerDataSync] Disconnected from server, will queue changes for next connection");
            // Changes will be marked as dirty and synced on reconnect
        }

        public void MarkClean()
        {
            _isDirty = false;
            _statsChanged = false;
            _positionChanged = false;
        }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        #endregion

        #region Change Tracking

        private void OnStatsChanged()
        {
            _statsChanged = true;
            _isDirty = true;
            Debug.Log("[PlayerDataSync] Stats changed, marked dirty");
        }

        /// <summary>
        /// Called when player position changes significantly
        /// </summary>
        public void OnPositionChanged(Vector3 newPosition)
        {
            _playerData.LastKnownPosition = newPosition;
            _positionChanged = true;
            _isDirty = true;
            Debug.Log($"[PlayerDataSync] Position changed: {newPosition}");
        }

        #endregion
    }

    #region Data Structures

    [Serializable]
    internal class PlayerDataSyncMessage
    {
        public string player_id;
        public string display_name;
        public int level;
        public int experience;
        public int score;
        public float play_time;
        public PositionData position;
    }

    [Serializable]
    internal class PositionData
    {
        public float x;
        public float y;
        public float z;
    }

    #endregion
}
