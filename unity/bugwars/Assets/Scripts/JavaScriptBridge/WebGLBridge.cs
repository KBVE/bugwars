using System;
using System.Runtime.InteropServices;
using UnityEngine;
using VContainer;
using BugWars.Core;

namespace BugWars.JavaScriptBridge
{
    /// <summary>
    /// Main bridge component for Unity WebGL <-> JavaScript communication.
    /// This GameObject receives messages from the JavaScript/React layer via react-unity-webgl.
    ///
    /// CRITICAL REQUIREMENT:
    /// This component MUST be on a GameObject named "WebGLBridge" (case-sensitive)
    /// because React uses: sendMessage('WebGLBridge', 'OnSessionUpdate', jsonData)
    ///
    /// When using VContainer, this is handled automatically via RegisterComponentOnNewGameObject.
    /// If manually adding to scene, ensure GameObject.name == "WebGLBridge"
    ///
    /// Initialization Flow:
    /// 1. VContainer creates/injects this component during container build
    /// 2. Awake() runs and sets up the singleton
    /// 3. Start() runs after VContainer injection is complete
    /// 4. Unity sends "BridgeReady" signal to React
    /// 5. React waits for "BridgeReady" before sending session data
    ///
    /// Usage:
    /// - JavaScript calls: sendMessage('WebGLBridge', 'OnSessionUpdate', jsonData)
    /// - Unity sends back: SendMessageToWeb("GameReady", jsonData)
    /// </summary>
    public class WebGLBridge : MonoBehaviour
    {
        // Singleton instance
        public static WebGLBridge Instance { get; private set; }

        [Inject] private EventManager _eventManager;

        private bool _isReady = false;

        // Authentication state - fully delegated to EntityManager.PlayerData
        public string AccessToken => BugWars.Entity.EntityManager.Instance?.PlayerData?.AccessToken;
        public string RefreshToken => BugWars.Entity.EntityManager.Instance?.PlayerData?.RefreshToken;
        public bool IsAuthenticated => BugWars.Entity.EntityManager.Instance?.PlayerData?.IsAuthenticated ?? false;

        // External JavaScript functions (from react-unity-webgl)
        [DllImport("__Internal")]
        private static extern void SendMessageToWeb(string eventType, string data);

        [DllImport("__Internal")]
        private static extern void SendErrorToWeb(string errorMessage);

        // Environment detection functions (from WebGLBridge.jslib)
        [DllImport("__Internal")]
        private static extern string GetHostname();

        [DllImport("__Internal")]
        private static extern string GetWebSocketUrl();

        [DllImport("__Internal")]
        private static extern int IsLocalhost();

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[WebGLBridge] Duplicate instance detected. Destroying duplicate on '{gameObject.name}'.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Validate GameObject name matches React's expectations
            if (gameObject.name != "WebGLBridge")
            {
                Debug.LogWarning($"[WebGLBridge] GameObject name is '{gameObject.name}' but should be 'WebGLBridge'. " +
                                "React Unity sendMessage calls may fail!");
            }

            Debug.Log("[WebGLBridge] Awake complete. GameObject instantiated.");
        }

        private void Start()
        {
            // Start() runs after VContainer injection is complete
            // Send ready signal to React so it knows Unity is fully initialized
            _isReady = true;

            Debug.Log("[WebGLBridge] VContainer injection complete. Sending BridgeReady signal to React.");

            // Notify React that Unity bridge is ready to receive messages
            SendToWeb("BridgeReady", new BridgeReadyData
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                gameObjectName = gameObject.name,
                version = Application.version
            });
        }

        #region Incoming Messages from JavaScript

        /// <summary>
        /// Called from JavaScript when session data is available.
        /// JavaScript: sendMessage('WebGLBridge', 'OnSessionUpdate', JSON.stringify({userId, email, username, displayName, accessToken, refreshToken, expiresAt}))
        /// </summary>
        public void OnSessionUpdate(string sessionJson)
        {
            try
            {
                if (!_isReady)
                {
                    Debug.LogWarning("[WebGLBridge] Received session update before bridge was ready! " +
                                    "This may indicate a race condition. Message will still be processed.");
                }

                Debug.Log($"[WebGLBridge] Received session update (JWT tokens included)");
                var sessionData = JsonUtility.FromJson<SessionData>(sessionJson);

                // Log token info (without exposing the actual tokens)
                Debug.Log($"[WebGLBridge] User ID: {sessionData.userId}");
                Debug.Log($"[WebGLBridge] Access Token: {(!string.IsNullOrEmpty(sessionData.accessToken) ? "✓ Present" : "✗ Missing")}");
                Debug.Log($"[WebGLBridge] Refresh Token: {(!string.IsNullOrEmpty(sessionData.refreshToken) ? "✓ Present" : "✗ Missing")}");
                Debug.Log($"[WebGLBridge] Token Expires At: {sessionData.expiresAt}");

                // Store session data in centralized PlayerData via EntityManager
                if (BugWars.Entity.EntityManager.Instance != null)
                {
                    BugWars.Entity.EntityManager.Instance.UpdatePlayerSession(
                        sessionData.userId,
                        sessionData.displayName,
                        sessionData.username,
                        sessionData.email,
                        sessionData.avatarUrl,
                        sessionData.accessToken,
                        sessionData.refreshToken,
                        sessionData.expiresAt
                    );
                    Debug.Log($"[WebGLBridge] Updated player session in EntityManager (including tokens)");
                }

                // Broadcast event through the game's event system
                _eventManager?.TriggerEvent("SessionUpdated", sessionData);

                // Acknowledge receipt with token status
                SendToWeb("SessionReceived", new SessionReceivedData {
                    success = true,
                    userId = sessionData.userId,
                    hasAccessToken = !string.IsNullOrEmpty(sessionData.accessToken),
                    hasRefreshToken = !string.IsNullOrEmpty(sessionData.refreshToken)
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error parsing session data: {e.Message}");
                SendErrorToWebSafe($"Failed to parse session data: {e.Message}");
            }
        }

        /// <summary>
        /// Called from JavaScript when player profile data is available.
        /// JavaScript: sendMessage('WebGLBridge', 'OnPlayerProfile', JSON.stringify({username, displayName, level, avatarUrl}))
        /// </summary>
        public void OnPlayerProfile(string profileJson)
        {
            try
            {
                Debug.Log($"[WebGLBridge] Received player profile: {profileJson}");
                var profileData = JsonUtility.FromJson<PlayerProfileData>(profileJson);

                if (BugWars.Entity.EntityManager.Instance != null)
                {
                    // Get existing player data to preserve auth tokens
                    var playerData = BugWars.Entity.EntityManager.Instance.PlayerData;

                    // Update player session from profile data
                    // Note: OnPlayerProfile doesn't include auth tokens, use existing values from PlayerData
                    BugWars.Entity.EntityManager.Instance.UpdatePlayerSession(
                        playerData?.PlayerId ?? "", // Keep existing userId
                        profileData.displayName,
                        profileData.username,
                        playerData?.Email ?? "", // Keep existing email
                        profileData.avatarUrl,
                        playerData?.AccessToken ?? "", // Keep existing access token
                        playerData?.RefreshToken ?? "", // Keep existing refresh token
                        playerData?.ExpiresAt ?? 0 // Keep existing expiration
                    );
                    Debug.Log($"[WebGLBridge] Updated player profile in EntityManager");

                    // Update player stats if provided
                    if (playerData != null)
                    {
                        if (profileData.level > 0)
                        {
                            playerData.Level = profileData.level;
                            Debug.Log($"[WebGLBridge] Updated player level to: {profileData.level}");
                        }

                        if (profileData.experience > 0)
                        {
                            playerData.Experience = profileData.experience;
                            Debug.Log($"[WebGLBridge] Updated player experience to: {profileData.experience}");
                        }

                        if (profileData.score > 0)
                        {
                            playerData.Score = profileData.score;
                            Debug.Log($"[WebGLBridge] Updated player score to: {profileData.score}");
                        }
                    }
                }

                // Broadcast event through the game's event system
                _eventManager?.TriggerEvent("PlayerProfileUpdated", profileData);

                // Acknowledge receipt
                SendToWeb("ProfileReceived", new ProfileReceivedData { success = true, username = profileData.username });
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error parsing player profile: {e.Message}");
                SendErrorToWebSafe($"Failed to parse player profile: {e.Message}");
            }
        }

        /// <summary>
        /// Called from JavaScript when data save is confirmed.
        /// JavaScript: sendMessage('WebGLBridge', 'OnDataSaved', JSON.stringify({table, success}))
        /// </summary>
        public void OnDataSaved(string resultJson)
        {
            try
            {
                Debug.Log($"[WebGLBridge] Data save confirmed: {resultJson}");
                var result = JsonUtility.FromJson<DataSaveResult>(resultJson);
                _eventManager?.TriggerEvent("DataSaved", result);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error parsing save result: {e.Message}");
            }
        }

        /// <summary>
        /// Called from JavaScript when data is loaded.
        /// JavaScript: sendMessage('WebGLBridge', 'OnDataLoaded', JSON.stringify({table, data}))
        /// </summary>
        public void OnDataLoaded(string resultJson)
        {
            try
            {
                Debug.Log($"[WebGLBridge] Data loaded: {resultJson}");
                _eventManager?.TriggerEvent("DataLoaded", resultJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error handling loaded data: {e.Message}");
            }
        }

        /// <summary>
        /// Generic message receiver for custom messages from JavaScript.
        /// JavaScript: sendMessage('WebGLBridge', 'OnMessage', JSON.stringify({type, payload}))
        /// </summary>
        public void OnMessage(string messageJson)
        {
            try
            {
                Debug.Log($"[WebGLBridge] Received message: {messageJson}");
                var message = JsonUtility.FromJson<WebMessage>(messageJson);
                _eventManager?.TriggerEvent($"Web_{message.type}", message.payload);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error parsing message: {e.Message}");
            }
        }

        /// <summary>
        /// Receives binary data as base64 encoded string from JavaScript.
        /// JavaScript: sendMessage('WebGLBridge', 'OnBinaryData', base64String)
        /// </summary>
        public void OnBinaryData(string base64Data)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64Data);
                Debug.Log($"[WebGLBridge] Received {bytes.Length} bytes of binary data");
                _eventManager?.TriggerEvent("BinaryDataReceived", bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error decoding binary data: {e.Message}");
            }
        }

        /// <summary>
        /// Receives array data from JavaScript.
        /// JavaScript: sendMessage('WebGLBridge', 'OnArrayData', JSON.stringify(arrayData))
        /// </summary>
        public void OnArrayData(string arrayJson)
        {
            try
            {
                Debug.Log($"[WebGLBridge] Received array data: {arrayJson}");
                // Arrays can be parsed based on expected type
                // Example for float array:
                var arrayData = JsonUtility.FromJson<ArrayWrapper<float>>(arrayJson);
                _eventManager?.TriggerEvent("ArrayDataReceived", arrayData.data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error parsing array data: {e.Message}");
            }
        }

        #endregion

        #region Outgoing Messages to JavaScript

        /// <summary>
        /// Send a message to JavaScript with JSON data.
        /// </summary>
        public static void SendToWeb(string eventType, object data)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string jsonData = JsonUtility.ToJson(data);
                SendMessageToWeb(eventType, jsonData);
                Debug.Log($"[WebGLBridge] Sent to web - Type: {eventType}, Data: {jsonData}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error sending to web: {e.Message}");
            }
#else
            Debug.Log($"[WebGLBridge] (Editor Mode) Would send to web - Type: {eventType}, Data: {JsonUtility.ToJson(data)}");
#endif
        }

        /// <summary>
        /// Send binary data to JavaScript as base64 encoded string.
        /// </summary>
        public static void SendBinaryToWeb(string eventType, byte[] data)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string base64 = Convert.ToBase64String(data);
                SendMessageToWeb(eventType, base64);
                Debug.Log($"[WebGLBridge] Sent {data.Length} bytes to web as base64");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error sending binary data: {e.Message}");
            }
#else
            Debug.Log($"[WebGLBridge] (Editor Mode) Would send {data.Length} bytes to web");
#endif
        }

        /// <summary>
        /// Send array data to JavaScript.
        /// </summary>
        public static void SendArrayToWeb<T>(string eventType, T[] data)
        {
            var wrapper = new ArrayWrapper<T> { data = data };
            SendToWeb(eventType, wrapper);
        }

        /// <summary>
        /// Send error message to JavaScript.
        /// </summary>
        public static void SendErrorToWebSafe(string errorMessage)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                SendErrorToWeb(errorMessage);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error sending error to web: {e.Message}");
            }
#else
            Debug.LogError($"[WebGLBridge] (Editor Mode) Error: {errorMessage}");
#endif
        }

        #endregion

        #region Public API Methods

        /// <summary>
        /// Notify JavaScript that the game is ready.
        /// </summary>
        public void NotifyGameReady()
        {
            SendToWeb("GameReady", new GameReadyData { timestamp = DateTime.UtcNow.ToString("o") });
        }

        /// <summary>
        /// Notify JavaScript that a player has spawned.
        /// </summary>
        public void NotifyPlayerSpawned(string playerId, Vector3 position)
        {
            SendToWeb("PlayerSpawned", new PlayerSpawnedData
            {
                playerId = playerId,
                position = new Vector3Data { x = position.x, y = position.y, z = position.z }
            });
        }

        /// <summary>
        /// Request data from JavaScript/Supabase.
        /// </summary>
        public void RequestData(string table, string filters = null)
        {
            SendToWeb("DataRequest", new DataRequestData { table = table, filters = filters });
        }

        /// <summary>
        /// Save data to JavaScript/Supabase.
        /// </summary>
        public void SaveData(string table, object data)
        {
            string jsonData = JsonUtility.ToJson(data);
            SendToWeb("DataSave", new DataSaveData { table = table, data = jsonData });
        }

        /// <summary>
        /// Request player profile from JavaScript/Supabase.
        /// </summary>
        public void RequestPlayerProfile()
        {
            SendToWeb("PlayerProfileRequest", new PlayerProfileRequestData { timestamp = DateTime.UtcNow.ToString("o") });
        }

        /// <summary>
        /// Send current player data to JavaScript/Supabase for saving.
        /// </summary>
        public void SavePlayerData()
        {
            if (BugWars.Entity.EntityManager.Instance != null)
            {
                var playerData = BugWars.Entity.EntityManager.Instance.GetPlayerData();
                if (playerData != null)
                {
                    SendToWeb("PlayerDataSave", new
                    {
                        playerName = playerData.PlayerName,
                        level = playerData.Level,
                        experience = playerData.Experience,
                        score = playerData.Score,
                        playTime = playerData.PlayTime
                    });
                }
            }
        }

        /// <summary>
        /// Request a token refresh from JavaScript/Supabase.
        /// This should be called when the access token is about to expire.
        /// Delegates to PlayerData for centralized token management.
        /// </summary>
        public void RequestTokenRefresh()
        {
            Debug.Log("[WebGLBridge] Requesting token refresh from JavaScript");

            var playerData = BugWars.Entity.EntityManager.Instance?.PlayerData;
            if (playerData == null)
            {
                Debug.LogError("[WebGLBridge] Cannot refresh token - PlayerData not available");
                return;
            }

            SendToWeb("TokenRefreshRequest", new TokenRefreshRequestData
            {
                userId = playerData.PlayerId,
                refreshToken = playerData.RefreshToken,
                timestamp = DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>
        /// Check if the current access token is expired or about to expire.
        /// Delegates to PlayerData for centralized token management.
        /// </summary>
        public bool IsTokenExpired()
        {
            var playerData = BugWars.Entity.EntityManager.Instance?.PlayerData;
            return playerData?.IsTokenExpired() ?? true;
        }

        /// <summary>
        /// Get the current access token, requesting a refresh if needed.
        /// Delegates to PlayerData for centralized token management.
        /// </summary>
        public string GetValidAccessToken()
        {
            var playerData = BugWars.Entity.EntityManager.Instance?.PlayerData;
            if (playerData == null)
            {
                Debug.LogWarning("[WebGLBridge] Cannot get access token - PlayerData not available");
                return null;
            }

            string token = playerData.GetValidAccessToken();
            if (token == null && !string.IsNullOrEmpty(playerData.RefreshToken))
            {
                RequestTokenRefresh();
            }
            return token;
        }

        #endregion

        #region Environment Detection

        /// <summary>
        /// Get the current hostname from the browser.
        /// Returns "localhost", "bugwars.kbve.com", etc.
        /// </summary>
        public string GetCurrentHostname()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return GetHostname();
#else
            return "localhost"; // Fallback for editor testing
#endif
        }

        /// <summary>
        /// Get the appropriate WebSocket URL for the current environment.
        /// Automatically detects localhost vs production and uses correct protocol/port.
        /// </summary>
        /// <returns>WebSocket URL (e.g., "ws://localhost:4321/ws" or "wss://bugwars.kbve.com/ws")</returns>
        public string GetWebSocketEndpoint()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return GetWebSocketUrl();
#else
            return "ws://localhost:4321/ws"; // Fallback for editor testing
#endif
        }

        /// <summary>
        /// Check if running on localhost (development environment).
        /// </summary>
        /// <returns>True if localhost, false if production</returns>
        public bool IsRunningOnLocalhost()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return IsLocalhost() == 1;
#else
            return true; // Fallback for editor testing
#endif
        }

        #endregion
    }

    #region Data Structures

    [Serializable]
    public class BridgeReadyData
    {
        public string timestamp;
        public string gameObjectName;
        public string version;
    }

    [Serializable]
    public class SessionData
    {
        public string userId;
        public string email;
        public string username;
        public string displayName;
        public string avatarUrl;
        public string accessToken;
        public string refreshToken;
        public long expiresAt;
    }

    [Serializable]
    public class PlayerProfileData
    {
        public string username;
        public string displayName;
        public string avatarUrl;
        public int level;
        public int experience;
        public int score;
    }

    [Serializable]
    public class DataSaveResult
    {
        public string table;
        public bool success;
        public string error;
    }

    [Serializable]
    public class WebMessage
    {
        public string type;
        public string payload;
    }

    [Serializable]
    public class ArrayWrapper<T>
    {
        public T[] data;
    }

    [Serializable]
    public class SessionReceivedData
    {
        public bool success;
        public string userId;
        public bool hasAccessToken;
        public bool hasRefreshToken;
    }

    [Serializable]
    public class ProfileReceivedData
    {
        public bool success;
        public string username;
    }

    [Serializable]
    public class GameReadyData
    {
        public string timestamp;
    }

    [Serializable]
    public class DataRequestData
    {
        public string table;
        public string filters;
    }

    [Serializable]
    public class DataSaveData
    {
        public string table;
        public string data;
    }

    [Serializable]
    public class PlayerProfileRequestData
    {
        public string timestamp;
    }

    [Serializable]
    public class TokenRefreshRequestData
    {
        public string userId;
        public string refreshToken;
        public string timestamp;
    }

    [Serializable]
    public class Base64DataWrapper
    {
        public string data;
    }

    [Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class PlayerSpawnedData
    {
        public string playerId;
        public Vector3Data position;
    }

    #endregion
}
