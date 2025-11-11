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
    /// Usage:
    /// - Attach this script to a GameObject named "WebGLBridge" in your scene
    /// - JavaScript calls: sendMessage('WebGLBridge', 'OnSessionUpdate', jsonData)
    /// - Unity sends back: SendMessageToWeb("GameReady", jsonData)
    /// </summary>
    public class WebGLBridge : MonoBehaviour
    {
        // Singleton instance
        public static WebGLBridge Instance { get; private set; }

        [Inject] private EventManager _eventManager;

        // External JavaScript functions (from react-unity-webgl)
        [DllImport("__Internal")]
        private static extern void SendMessageToWeb(string eventType, string data);

        [DllImport("__Internal")]
        private static extern void SendErrorToWeb(string errorMessage);

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[WebGLBridge] Initialized and ready for JavaScript communication");
        }

        #region Incoming Messages from JavaScript

        /// <summary>
        /// Called from JavaScript when session data is available.
        /// JavaScript: sendMessage('WebGLBridge', 'OnSessionUpdate', JSON.stringify({userId, email}))
        /// </summary>
        public void OnSessionUpdate(string sessionJson)
        {
            try
            {
                Debug.Log($"[WebGLBridge] Received session update: {sessionJson}");
                var sessionData = JsonUtility.FromJson<SessionData>(sessionJson);

                // Broadcast event through the game's event system
                _eventManager?.TriggerEvent("SessionUpdated", sessionData);

                // Acknowledge receipt
                SendToWeb("SessionReceived", new { success = true, userId = sessionData.userId });
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLBridge] Error parsing session data: {e.Message}");
                SendErrorToWebSafe($"Failed to parse session data: {e.Message}");
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
            SendToWeb("GameReady", new { timestamp = DateTime.UtcNow.ToString("o") });
        }

        /// <summary>
        /// Notify JavaScript that a player has spawned.
        /// </summary>
        public void NotifyPlayerSpawned(string playerId, Vector3 position)
        {
            SendToWeb("PlayerSpawned", new
            {
                playerId = playerId,
                position = new { x = position.x, y = position.y, z = position.z }
            });
        }

        /// <summary>
        /// Request data from JavaScript/Supabase.
        /// </summary>
        public void RequestData(string table, string filters = null)
        {
            SendToWeb("DataRequest", new { table = table, filters = filters });
        }

        /// <summary>
        /// Save data to JavaScript/Supabase.
        /// </summary>
        public void SaveData(string table, object data)
        {
            SendToWeb("DataSave", new { table = table, data = data });
        }

        #endregion
    }

    #region Data Structures

    [Serializable]
    public class SessionData
    {
        public string userId;
        public string email;
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

    #endregion
}
