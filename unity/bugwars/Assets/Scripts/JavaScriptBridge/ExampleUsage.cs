using UnityEngine;
using VContainer;
using BugWars.JavaScriptBridge;
using BugWars.Core;

namespace BugWars.Examples
{
    /// <summary>
    /// Example script demonstrating how to use the JavaScript Bridge.
    /// This shows common patterns for Unity <-> JavaScript communication.
    ///
    /// Usage:
    /// - Attach this script to any GameObject in your scene
    /// - The WebGLBridge GameObject must exist in the scene
    /// - Press keyboard keys to trigger example messages
    /// </summary>
    public class ExampleUsage : MonoBehaviour
    {
        [Inject] private EventManager _eventManager;

        private void Start()
        {
            // Subscribe to events from JavaScript
            SubscribeToWebEvents();

            // Notify web that game is ready
            if (WebGLBridge.Instance != null)
            {
                WebGLBridge.Instance.NotifyGameReady();
            }
        }

        private void Update()
        {
            // Press keys to test different message types

            // 1 - Send simple JSON message
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SendSimpleMessage();
            }

            // 2 - Send player data
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SendPlayerData();
            }

            // 3 - Send transform data
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SendTransformData();
            }

            // 4 - Send array data
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                SendArrayData();
            }

            // 5 - Send binary data
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                SendBinaryData();
            }

            // 6 - Send mesh data
            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                SendMeshData();
            }

            // 7 - Request data from JavaScript
            if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                RequestDataFromWeb();
            }

            // 8 - Save data to Supabase
            if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                SaveDataToSupabase();
            }
        }

        #region Outgoing Messages to JavaScript

        /// <summary>
        /// Example 1: Send a simple JSON message
        /// </summary>
        private void SendSimpleMessage()
        {
            Debug.Log("[Example] Sending simple message (Press 1)");

            WebGLBridge.SendToWeb("SimpleMessage", new
            {
                message = "Hello from Unity!",
                timestamp = System.DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>
        /// Example 2: Send player data
        /// </summary>
        private void SendPlayerData()
        {
            Debug.Log("[Example] Sending player data (Press 2)");

            var playerData = new PlayerData
            {
                playerId = "player-123",
                playerName = "TestPlayer",
                level = 5,
                experience = 1250,
                health = 80,
                maxHealth = 100,
                position = new Vector3Data
                {
                    x = transform.position.x,
                    y = transform.position.y,
                    z = transform.position.z
                }
            };

            WebGLBridge.SendToWeb(MessageTypes.PLAYER_SPAWNED, playerData);
        }

        /// <summary>
        /// Example 3: Send transform data
        /// </summary>
        private void SendTransformData()
        {
            Debug.Log("[Example] Sending transform data (Press 3)");

            string transformJson = JSONBridge.SerializeTransform(transform);
            WebGLBridge.SendToWeb("TransformUpdate", new
            {
                objectName = gameObject.name,
                transform = transformJson
            });
        }

        /// <summary>
        /// Example 4: Send array data
        /// </summary>
        private void SendArrayData()
        {
            Debug.Log("[Example] Sending array data (Press 4)");

            // Example: Send heightmap data
            float[] heightmap = new float[100];
            for (int i = 0; i < heightmap.Length; i++)
            {
                heightmap[i] = Mathf.PerlinNoise(i * 0.1f, 0) * 10f;
            }

            BufferBridge.SendFloatArray("HeightmapData", heightmap);
        }

        /// <summary>
        /// Example 5: Send binary data
        /// </summary>
        private void SendBinaryData()
        {
            Debug.Log("[Example] Sending binary data (Press 5)");

            // Example: Send some binary data
            byte[] binaryData = new byte[256];
            for (int i = 0; i < binaryData.Length; i++)
            {
                binaryData[i] = (byte)i;
            }

            BufferBridge.SendByteArray("BinaryData", binaryData);

            // Or send as base64 through JSON
            string base64 = BufferBridge.EncodeToBase64(binaryData);
            WebGLBridge.SendToWeb("Base64Data", new { data = base64 });
        }

        /// <summary>
        /// Example 6: Send mesh data
        /// </summary>
        private void SendMeshData()
        {
            Debug.Log("[Example] Sending mesh data (Press 6)");

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                BufferBridge.SendMeshData("MeshData", meshFilter.mesh);
            }
            else
            {
                Debug.LogWarning("[Example] No MeshFilter found on this GameObject");
            }
        }

        /// <summary>
        /// Example 7: Request data from JavaScript/Supabase
        /// </summary>
        private void RequestDataFromWeb()
        {
            Debug.Log("[Example] Requesting data from web (Press 7)");

            if (WebGLBridge.Instance != null)
            {
                WebGLBridge.Instance.RequestData("player_data", "userId=player-123");
            }
        }

        /// <summary>
        /// Example 8: Save data to Supabase via JavaScript
        /// </summary>
        private void SaveDataToSupabase()
        {
            Debug.Log("[Example] Saving data to Supabase (Press 8)");

            var gameState = new GameStateData
            {
                gameId = "game-123",
                sessionId = System.Guid.NewGuid().ToString(),
                currentLevel = 3,
                playTime = Time.time,
                lastSaveTime = System.DateTime.UtcNow.ToString("o"),
                gameMode = "survival",
                isPaused = false
            };

            if (WebGLBridge.Instance != null)
            {
                WebGLBridge.Instance.SaveData("game_states", gameState);
            }
        }

        #endregion

        #region Incoming Messages from JavaScript

        /// <summary>
        /// Subscribe to events from JavaScript.
        /// These events are triggered when JavaScript sends messages to Unity.
        /// </summary>
        private void SubscribeToWebEvents()
        {
            if (_eventManager == null)
            {
                Debug.LogWarning("[Example] EventManager not injected. Make sure VContainer is set up.");
                return;
            }

            // Listen for session updates from web
            _eventManager.AddListener<SessionData>("SessionUpdated", OnSessionUpdated);

            // Listen for data saved confirmation
            _eventManager.AddListener<DataSaveResult>("DataSaved", OnDataSaved);

            // Listen for loaded data
            _eventManager.AddListener<string>("DataLoaded", OnDataLoaded);

            // Listen for custom messages
            _eventManager.AddListener<string>("Web_CustomCommand", OnCustomCommand);

            Debug.Log("[Example] Subscribed to web events");
        }

        private void OnSessionUpdated(SessionData data)
        {
            Debug.Log($"[Example] Session updated - User: {data.userId}, Email: {data.email}");

            // You can now use the session data in your game
            // Example: Load player data for this user
            if (WebGLBridge.Instance != null)
            {
                WebGLBridge.Instance.RequestData("player_data", $"userId={data.userId}");
            }
        }

        private void OnDataSaved(DataSaveResult result)
        {
            if (result.success)
            {
                Debug.Log($"[Example] Data saved successfully to table: {result.table}");
            }
            else
            {
                Debug.LogError($"[Example] Data save failed: {result.error}");
            }
        }

        private void OnDataLoaded(string jsonData)
        {
            Debug.Log($"[Example] Data loaded from web: {jsonData}");

            // Try to parse as player data
            if (JSONBridge.TryDeserialize<PlayerData>(jsonData, out var playerData))
            {
                Debug.Log($"[Example] Loaded player: {playerData.playerName} (Level {playerData.level})");
                // Apply the loaded data to your game
            }
        }

        private void OnCustomCommand(string payload)
        {
            Debug.Log($"[Example] Received custom command: {payload}");

            // Example: Parse a teleport command
            if (JSONBridge.TryDeserialize<Vector3Data>(payload, out var position))
            {
                transform.position = new Vector3(position.x, position.y, position.z);
                Debug.Log($"[Example] Teleported to: {position.x}, {position.y}, {position.z}");
            }
        }

        #endregion

        #region Helper Methods for Testing

        /// <summary>
        /// Create a test mesh for demonstration purposes.
        /// </summary>
        private Mesh CreateTestMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "TestMesh";

            // Simple quad
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 1, 0),
                new Vector3(0, 1, 0)
            };

            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };

            mesh.normals = new Vector3[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };

            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            return mesh;
        }

        #endregion

        private void OnDestroy()
        {
            // Clean up event listeners
            if (_eventManager != null)
            {
                _eventManager.RemoveListener<SessionData>("SessionUpdated", OnSessionUpdated);
                _eventManager.RemoveListener<DataSaveResult>("DataSaved", OnDataSaved);
                _eventManager.RemoveListener<string>("DataLoaded", OnDataLoaded);
                _eventManager.RemoveListener<string>("Web_CustomCommand", OnCustomCommand);
            }
        }
    }
}
