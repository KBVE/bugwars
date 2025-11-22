using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using BugWars.Interaction;
using BugWars.Terrain;
using R3;

namespace BugWars.Network
{
    /// <summary>
    /// Handles network synchronization for server-authoritative environment objects.
    /// Subscribes to WebSocket messages and coordinates with EnvironmentManager for spawning/despawning.
    ///
    /// Flow:
    /// 1. Server sends EnvironmentObjects on connect → Spawn objects via EnvironmentManager
    /// 2. Player harvests object → Send HarvestObject request to server
    /// 3. Server validates and responds → Apply result (despawn, add resources)
    /// 4. Server sends ObjectRespawned → Respawn object via EnvironmentManager
    ///
    /// Pure C# class (not MonoBehaviour) - registered via VContainer as IAsyncStartable + IDisposable
    /// Implements IDisposable for proper cleanup of UniTask cancellations and R3 subscriptions
    /// </summary>
    public class EnvironmentNetworkSync : IAsyncStartable, IDisposable
    {
        private readonly WebSocketManager _webSocketManager;
        private readonly EnvironmentManager _environmentManager;
        private readonly BugWars.Entity.EntityManager _entityManager;

        // Resource cleanup - VContainer best practice for MessagePipe + UniTask + R3
        // NOTE: CancellationTokenSource is WebGL-safe (no threading, just token management)
        // All async operations use UniTask (Unity PlayerLoop), not System.Threading.Tasks
        private readonly CompositeDisposable _disposables = new();
        private readonly CancellationTokenSource _cts = new();

        // Track pending harvest requests
        private Dictionary<string, Action<HarvestObjectResponse>> _pendingHarvests = new();

        // Constructor for VContainer injection
        [Inject]
        public EnvironmentNetworkSync(
            WebSocketManager webSocketManager,
            EnvironmentManager environmentManager,
            BugWars.Entity.EntityManager entityManager)
        {
            _webSocketManager = webSocketManager;
            _environmentManager = environmentManager;
            _entityManager = entityManager;
        }

        /// <summary>
        /// IAsyncStartable - Called by VContainer during initialization
        /// </summary>
        public async UniTask StartAsync(CancellationToken cancellationToken)
        {
            // Create linked token source that responds to both VContainer lifecycle and our own disposal
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

            Debug.Log("[EnvironmentNetworkSync] Initialized and ready to handle environment messages");

            // TODO: Subscribe to MessagePipe events here (if needed)
            // Example: _publisher.Subscribe(OnSomeEvent).AddTo(_disposables);

            await UniTask.CompletedTask;
        }

        #region Public API

        /// <summary>
        /// Send harvest request to server and wait for response.
        /// Returns true if harvest was successful.
        /// </summary>
        public async UniTask<HarvestObjectResponse> RequestHarvestAsync(string objectId)
        {
            if (!_webSocketManager.IsConnected)
            {
                Debug.LogWarning($"[EnvironmentNetworkSync] Cannot harvest - not connected to server");
                return new HarvestObjectResponse
                {
                    success = false,
                    objectId = objectId,
                    errorMessage = "Not connected to server"
                };
            }

            // Get player position for server validation
            var playerData = _entityManager?.PlayerData;
            var playerPosition = _entityManager?.transform.position ?? Vector3.zero;

            // Create harvest request message
            var request = new
            {
                type = "harvest_object",
                object_id = objectId,
                player_position = new
                {
                    x = playerPosition.x,
                    y = playerPosition.y,
                    z = playerPosition.z
                }
            };

            // Send request to server
            string requestJson = JsonUtility.ToJson(request);
            Debug.Log($"[EnvironmentNetworkSync] Sending harvest request for {objectId}");

            // Send via WebSocketManager
            _webSocketManager.SendRawMessage(requestJson);

            // Create a UniTaskCompletionSource to wait for response
            var tcs = new UniTaskCompletionSource<HarvestObjectResponse>();
            _pendingHarvests[objectId] = (response) => tcs.TrySetResult(response);

            // Wait for response with timeout, respecting cancellation token
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: _cts.Token);
            var responseTask = tcs.Task;

            // WhenAny with different task types returns (bool hasResultLeft, T result)
            // hasResultLeft is true if the first task (responseTask) completed
            var (hasResponse, response) = await UniTask.WhenAny(responseTask, timeoutTask);

            if (hasResponse)
            {
                // Response received
                _pendingHarvests.Remove(objectId);
                return response;
            }
            else
            {
                // Timeout
                _pendingHarvests.Remove(objectId);
                Debug.LogWarning($"[EnvironmentNetworkSync] Harvest request timed out for {objectId}");
                return new HarvestObjectResponse
                {
                    success = false,
                    objectId = objectId,
                    errorMessage = "Request timed out"
                };
            }
        }

        #endregion

        #region Message Handlers (Called by WebSocketManager)

        /// <summary>
        /// Handle initial environment objects from server.
        /// Called when server sends "environment_objects" message.
        /// </summary>
        public void OnEnvironmentObjects(string jsonMessage)
        {
            try
            {
                // Parse server message
                var wrapper = JsonUtility.FromJson<EnvironmentObjectsWrapper>(jsonMessage);

                if (wrapper.objects == null || wrapper.objects.Length == 0)
                {
                    Debug.Log("[EnvironmentNetworkSync] Received empty environment objects list");
                    return;
                }

                Debug.Log($"[EnvironmentNetworkSync] Received {wrapper.objects.Length} environment objects from server");

                // Convert to EnvironmentObjectData array
                var objectsToSpawn = new List<EnvironmentObjectData>();

                foreach (var obj in wrapper.objects)
                {
                    var envObject = new EnvironmentObjectData
                    {
                        objectId = obj.id,
                        assetName = GetAssetNameFromType(obj.object_type),
                        position = new Vector3(obj.position.x, obj.position.y, obj.position.z),
                        rotation = Quaternion.Euler(obj.rotation.x, obj.rotation.y, obj.rotation.z),
                        scale = new Vector3(obj.scale.x, obj.scale.y, obj.scale.z),
                        objectType = (EnvironmentObjectType)obj.object_type,
                        resourceType = (ResourceType)obj.resource_type,
                        resourceAmount = obj.resource_amount,
                        harvestTime = obj.harvest_time
                    };

                    objectsToSpawn.Add(envObject);
                }

                // Spawn all objects via EnvironmentManager
                _environmentManager?.SpawnObjectsFromServer(objectsToSpawn.ToArray());

                Debug.Log($"[EnvironmentNetworkSync] Successfully spawned {objectsToSpawn.Count} objects");
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnvironmentNetworkSync] Error parsing environment objects: {e.Message}");
            }
        }

        /// <summary>
        /// Handle harvest result from server.
        /// Called when server sends "harvest_result" message.
        /// </summary>
        public void OnHarvestResult(string jsonMessage)
        {
            try
            {
                var response = JsonUtility.FromJson<HarvestResultWrapper>(jsonMessage);

                var harvestResponse = new HarvestObjectResponse
                {
                    success = response.success,
                    objectId = response.object_id,
                    playerId = response.player_id ?? "",
                    resourceType = ResourceType.Wood, // Parse from response.resources
                    resourceAmount = response.resources != null && response.resources.Length > 0 ? response.resources[0].amount : 0,
                    errorMessage = response.message ?? ""
                };

                Debug.Log($"[EnvironmentNetworkSync] Harvest result - Success: {response.success}, Object: {response.object_id}");

                // If harvest succeeded, despawn the object and add resources
                if (response.success)
                {
                    // Despawn object locally
                    _environmentManager?.DespawnObject(response.object_id);

                    // Add resources to player inventory
                    if (response.resources != null && response.resources.Length > 0)
                    {
                        foreach (var resource in response.resources)
                        {
                            Debug.Log($"[EnvironmentNetworkSync] Player received: {resource.amount}x {resource.resource_type}");
                            // TODO: Add to inventory via EntityManager/InventorySystem
                        }
                    }
                }

                // Resolve pending harvest request
                if (_pendingHarvests.TryGetValue(response.object_id, out var callback))
                {
                    callback(harvestResponse);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnvironmentNetworkSync] Error parsing harvest result: {e.Message}");
            }
        }

        /// <summary>
        /// Handle object respawn from server.
        /// Called when server sends "object_respawned" message.
        /// </summary>
        public void OnObjectRespawned(string jsonMessage)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<ObjectRespawnedWrapper>(jsonMessage);

                Debug.Log($"[EnvironmentNetworkSync] Object respawned: {wrapper.object_id}");

                // Parse object data and respawn
                var obj = wrapper.object_data;
                var envObject = new EnvironmentObjectData
                {
                    objectId = obj.id,
                    assetName = GetAssetNameFromType(obj.object_type),
                    position = new Vector3(obj.position.x, obj.position.y, obj.position.z),
                    rotation = Quaternion.Euler(obj.rotation.x, obj.rotation.y, obj.rotation.z),
                    scale = new Vector3(obj.scale.x, obj.scale.y, obj.scale.z),
                    objectType = (EnvironmentObjectType)obj.object_type,
                    resourceType = (ResourceType)obj.resource_type,
                    resourceAmount = obj.resource_amount,
                    harvestTime = obj.harvest_time
                };

                // Respawn via EnvironmentManager
                _environmentManager?.RespawnObject(envObject);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnvironmentNetworkSync] Error parsing object respawn: {e.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private string GetAssetNameFromType(int objectType)
        {
            return objectType switch
            {
                0 => "Tree", // EnvironmentObjectType.Tree
                1 => "Rock", // EnvironmentObjectType.Rock
                2 => "Bush", // EnvironmentObjectType.Bush
                3 => "Grass", // EnvironmentObjectType.Grass
                _ => "Tree"
            };
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Cleanup resources when VContainer disposes this instance
        /// Called automatically by VContainer on scene unload or application quit
        /// </summary>
        public void Dispose()
        {
            //Debug.Log("[EnvironmentNetworkSync] Disposing - cleaning up resources");

            // Cancel any pending async operations
            _cts?.Cancel();
            _cts?.Dispose();

            // Dispose all R3 subscriptions and MessagePipe subscriptions
            _disposables?.Dispose();

            // Clear pending harvests
            _pendingHarvests?.Clear();
        }

        #endregion
    }

    #region Server Message Wrappers

    [Serializable]
    public class EnvironmentObjectsWrapper
    {
        public string type;
        public ServerEnvironmentObject[] objects;
    }

    [Serializable]
    public class ServerEnvironmentObject
    {
        public string id;
        public int object_type;
        public int resource_type;
        public int resource_amount;
        public float harvest_time;
        public ServerVector3 position;
        public ServerVector3 rotation;
        public ServerVector3 scale;
    }

    [Serializable]
    public class ServerVector3
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class HarvestResultWrapper
    {
        public string type;
        public bool success;
        public string object_id;
        public string player_id;
        public string message;
        public HarvestResource[] resources;
    }

    [Serializable]
    public class HarvestResource
    {
        public string resource_type;
        public int amount;
    }

    [Serializable]
    public class ObjectRespawnedWrapper
    {
        public string type;
        public string object_id;
        public ServerEnvironmentObject object_data;
    }

    #endregion
}
