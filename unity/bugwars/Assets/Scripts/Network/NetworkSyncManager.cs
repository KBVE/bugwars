using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BugWars.Network
{
    /// <summary>
    /// Central manager for all network-synchronized data.
    /// Handles registration, lifecycle, and orchestration of INetworkSyncable implementations.
    ///
    /// Design Philosophy:
    /// - Single point of truth for what gets synced
    /// - Automatic lifecycle management (connect/disconnect)
    /// - Support for different sync strategies (immediate, batched, manual)
    /// - Easy to extend with new syncable types
    ///
    /// Usage:
    /// 1. Register syncables: RegisterSyncable(playerDataSync)
    /// 2. Manager handles connect/disconnect automatically
    /// 3. Call SyncAll() to force sync all dirty data
    /// </summary>
    public class NetworkSyncManager : MonoBehaviour, ITickable, IAsyncStartable
    {
        private WebSocketManager _webSocketManager;
        private readonly Dictionary<string, INetworkSyncable> _syncables = new();
        private readonly Dictionary<string, SyncConfig> _syncConfigs = new();

        // Batched sync tracking
        private float _timeSinceLastBatchSync = 0f;

        // State
        private bool _isInitialized = false;

        private struct SyncConfig
        {
            public SyncStrategy Strategy;
            public float BatchInterval;
        }

        // VContainer injection
        [Inject]
        public void Construct(WebSocketManager webSocketManager)
        {
            _webSocketManager = webSocketManager;
            Debug.Log("[NetworkSyncManager] WebSocketManager injected via VContainer");
        }

        #region Registration

        /// <summary>
        /// Register a syncable object with the manager.
        /// Automatically detects sync strategy from attribute if present.
        /// </summary>
        public void RegisterSyncable(INetworkSyncable syncable)
        {
            if (syncable == null)
            {
                Debug.LogError("[NetworkSyncManager] Cannot register null syncable");
                return;
            }

            string syncId = syncable.SyncId;

            if (_syncables.ContainsKey(syncId))
            {
                Debug.LogWarning($"[NetworkSyncManager] Syncable '{syncId}' already registered, replacing");
            }

            _syncables[syncId] = syncable;

            // Detect sync strategy from attribute
            var attr = Attribute.GetCustomAttribute(syncable.GetType(), typeof(NetworkSyncableAttribute)) as NetworkSyncableAttribute;
            var config = new SyncConfig
            {
                Strategy = attr?.Strategy ?? SyncStrategy.Immediate,
                BatchInterval = attr?.BatchIntervalSeconds ?? 5f
            };

            _syncConfigs[syncId] = config;

            Debug.Log($"[NetworkSyncManager] Registered syncable '{syncId}' with strategy: {config.Strategy}");

            // If already connected, notify the syncable
            if (_webSocketManager.IsConnected)
            {
                syncable.OnConnected(_webSocketManager).Forget();
            }
        }

        /// <summary>
        /// Unregister a syncable object
        /// </summary>
        public void UnregisterSyncable(string syncId)
        {
            if (_syncables.Remove(syncId))
            {
                _syncConfigs.Remove(syncId);
                Debug.Log($"[NetworkSyncManager] Unregistered syncable '{syncId}'");
            }
        }

        /// <summary>
        /// Get a registered syncable by ID
        /// </summary>
        public INetworkSyncable GetSyncable(string syncId)
        {
            return _syncables.TryGetValue(syncId, out var syncable) ? syncable : null;
        }

        #endregion

        #region Lifecycle (VContainer)

        public async UniTask StartAsync(System.Threading.CancellationToken cancellationToken)
        {
            Debug.Log("[NetworkSyncManager] Starting async initialization");

            // Wait for WebSocketManager to be connected
            while (!_webSocketManager.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.LogWarning("[NetworkSyncManager] Cancelled before connection established");
                return;
            }

            // Notify all registered syncables that we're connected
            Debug.Log($"[NetworkSyncManager] Connection established, notifying {_syncables.Count} syncables");
            foreach (var syncable in _syncables.Values)
            {
                await syncable.OnConnected(_webSocketManager);
            }

            _isInitialized = true;
            Debug.Log("[NetworkSyncManager] Initialization complete");
        }

        public void Tick()
        {
            if (!_isInitialized || !_webSocketManager.IsConnected)
                return;

            _timeSinceLastBatchSync += Time.deltaTime;

            // Process batched syncs
            foreach (var kvp in _syncConfigs)
            {
                string syncId = kvp.Key;
                var config = kvp.Value;

                if (config.Strategy == SyncStrategy.Batched && _timeSinceLastBatchSync >= config.BatchInterval)
                {
                    var syncable = _syncables[syncId];
                    if (syncable.IsDirty)
                    {
                        SyncToServer(syncable).Forget();
                    }
                }
            }

            // Reset batch timer after processing
            if (_timeSinceLastBatchSync >= GetMinBatchInterval())
            {
                _timeSinceLastBatchSync = 0f;
            }
        }

        private void OnDestroy()
        {
            Debug.Log("[NetworkSyncManager] OnDestroy - cleaning up");

            // Notify all syncables of disconnection
            foreach (var syncable in _syncables.Values)
            {
                syncable.OnDisconnected();
            }

            _syncables.Clear();
            _syncConfigs.Clear();
        }

        #endregion

        #region Sync Operations

        /// <summary>
        /// Sync a specific syncable to the server immediately
        /// </summary>
        public async UniTask SyncToServer(INetworkSyncable syncable)
        {
            if (!_webSocketManager.IsConnected)
            {
                Debug.LogWarning($"[NetworkSyncManager] Cannot sync '{syncable.SyncId}' - not connected");
                return;
            }

            if (!syncable.IsDirty)
            {
                Debug.Log($"[NetworkSyncManager] Skipping sync for '{syncable.SyncId}' - not dirty");
                return;
            }

            try
            {
                string json = syncable.SerializeForSync();
                _webSocketManager.SendMessage($"sync_{syncable.SyncId}", json);
                syncable.MarkClean();
                Debug.Log($"[NetworkSyncManager] Synced '{syncable.SyncId}' to server");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkSyncManager] Failed to sync '{syncable.SyncId}': {e.Message}");
            }

            await UniTask.Yield();
        }

        /// <summary>
        /// Sync all dirty syncables to the server
        /// </summary>
        public async UniTask SyncAll()
        {
            Debug.Log("[NetworkSyncManager] Syncing all dirty data to server");

            foreach (var syncable in _syncables.Values)
            {
                if (syncable.IsDirty)
                {
                    await SyncToServer(syncable);
                }
            }
        }

        /// <summary>
        /// Request sync from server for a specific syncable
        /// </summary>
        public void RequestFromServer(string syncId)
        {
            if (!_webSocketManager.IsConnected)
            {
                Debug.LogWarning($"[NetworkSyncManager] Cannot request '{syncId}' - not connected");
                return;
            }

            if (!_syncables.ContainsKey(syncId))
            {
                Debug.LogWarning($"[NetworkSyncManager] Cannot request '{syncId}' - not registered");
                return;
            }

            _webSocketManager.SendMessage($"request_{syncId}", "");
            Debug.Log($"[NetworkSyncManager] Requested '{syncId}' from server");
        }

        /// <summary>
        /// Handle incoming sync data from server
        /// Called by WebSocketManager when it receives sync messages
        /// </summary>
        public void HandleSyncFromServer(string syncId, string json)
        {
            if (!_syncables.TryGetValue(syncId, out var syncable))
            {
                Debug.LogWarning($"[NetworkSyncManager] Received sync for unregistered type '{syncId}'");
                return;
            }

            try
            {
                syncable.DeserializeFromSync(json);
                syncable.MarkClean();
                Debug.Log($"[NetworkSyncManager] Applied sync from server for '{syncId}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkSyncManager] Failed to deserialize sync for '{syncId}': {e.Message}");
            }
        }

        #endregion

        #region Helpers

        private float GetMinBatchInterval()
        {
            float min = float.MaxValue;
            foreach (var config in _syncConfigs.Values)
            {
                if (config.Strategy == SyncStrategy.Batched && config.BatchInterval < min)
                {
                    min = config.BatchInterval;
                }
            }
            return min == float.MaxValue ? 5f : min;
        }

        #endregion

        #region Connection Event Handlers

        /// <summary>
        /// Called when WebSocket connection is established
        /// </summary>
        public async void OnWebSocketConnected()
        {
            Debug.Log($"[NetworkSyncManager] WebSocket connected, notifying {_syncables.Count} syncables");

            foreach (var syncable in _syncables.Values)
            {
                await syncable.OnConnected(_webSocketManager);
            }
        }

        /// <summary>
        /// Called when WebSocket connection is lost
        /// </summary>
        public void OnWebSocketDisconnected()
        {
            Debug.Log($"[NetworkSyncManager] WebSocket disconnected, notifying {_syncables.Count} syncables");

            foreach (var syncable in _syncables.Values)
            {
                syncable.OnDisconnected();
            }
        }

        #endregion
    }
}
