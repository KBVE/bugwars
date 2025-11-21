using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;
using VContainer.Unity;
using R3;

namespace BugWars.Network
{
    /// <summary>
    /// WebSocket Manager - Handles real-time connection to Axum server
    /// Uses NativeWebSocket for WebGL-compatible WebSocket communication
    /// Connects with JWT authentication from PlayerData
    /// Implements IAsyncStartable for async initialization with UniTask
    /// </summary>
    public class WebSocketManager : MonoBehaviour, IAsyncStartable
    {
        #region Fields
        private WebSocket _webSocket;
        private CancellationTokenSource _cts;
        private bool _isConnected = false;
        private bool _isConnecting = false;
        private string _webSocketUrl;
        private string _accessToken;

        // Connection state tracking
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 5;
        private const float ReconnectDelaySeconds = 3f;

        // Message queue for outgoing messages while disconnected
        private Queue<string> _messageQueue = new Queue<string>();
        private const int MaxQueueSize = 100;

        // Heartbeat tracking
        private float _lastHeartbeatTime = 0f;
        private float _lastPongReceivedTime = 0f;
        private const float HeartbeatIntervalSeconds = 30f; // Send ping every 30 seconds
        private const float HeartbeatTimeoutSeconds = 60f; // Disconnect if no pong for 60 seconds

        // R3 reactive subscription
        private IDisposable _authSubscription;
        #endregion

        #region Properties
        public bool IsConnected => _isConnected;
        public bool IsConnecting => _isConnecting;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// IAsyncStartable - VContainer calls this for async initialization
        /// </summary>
        public async UniTask StartAsync(CancellationToken cancellationToken)
        {
            Debug.Log("[WebSocketManager] Starting async initialization");

            // Wait for EntityManager.PlayerData to be available
            BugWars.Entity.PlayerData playerData = null;
            while (playerData == null && !cancellationToken.IsCancellationRequested)
            {
                playerData = BugWars.Entity.EntityManager.Instance?.PlayerData;
                if (playerData == null)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.LogWarning("[WebSocketManager] Cancelled before PlayerData was available");
                return;
            }

            Debug.Log("[WebSocketManager] PlayerData available, setting up WebSocket connection");

            // Subscribe to authentication status changes
            _authSubscription = playerData.IsAuthenticatedObservable
                .Subscribe(isAuth =>
                {
                    Debug.Log($"[WebSocketManager] Auth status changed: {(isAuth ? "Authenticated" : "Guest")}");
                    if (isAuth)
                    {
                        // Player authenticated, connect to WebSocket
                        ConnectAsync(_cts.Token).Forget();
                    }
                    else
                    {
                        // Player logged out, disconnect WebSocket
                        DisconnectAsync().Forget();
                    }
                });

            // If already authenticated, connect immediately
            if (playerData.IsAuthenticated)
            {
                Debug.Log("[WebSocketManager] Player already authenticated, connecting to WebSocket");
                await ConnectAsync(_cts.Token);
            }
            else
            {
                Debug.Log("[WebSocketManager] Player not authenticated yet, waiting for authentication");
            }
        }

        private void Update()
        {
            // NativeWebSocket requires DispatchMessageQueue to be called in Update
            // This processes incoming WebSocket messages
#if !UNITY_WEBGL || UNITY_EDITOR
            _webSocket?.DispatchMessageQueue();
#endif

            // Handle heartbeat (ping/pong)
            if (_isConnected && _webSocket != null)
            {
                float currentTime = Time.time;

                // Send ping every HeartbeatIntervalSeconds
                if (currentTime - _lastHeartbeatTime >= HeartbeatIntervalSeconds)
                {
                    SendMessage("ping");
                    _lastHeartbeatTime = currentTime;
                }

                // Check if we've received a pong recently
                if (_lastPongReceivedTime > 0 && currentTime - _lastPongReceivedTime >= HeartbeatTimeoutSeconds)
                {
                    Debug.LogWarning($"[WebSocketManager] No pong received for {HeartbeatTimeoutSeconds}s, connection may be dead");
                    // Disconnect and attempt reconnection
                    DisconnectAsync().Forget();
                }
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _authSubscription?.Dispose();

            // Close WebSocket connection
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                _webSocket.Close();
            }
        }

        private async void OnApplicationQuit()
        {
            await DisconnectAsync();
        }
        #endregion

        #region Connection Management
        /// <summary>
        /// Connect to WebSocket server with JWT authentication
        /// </summary>
        private async UniTask ConnectAsync(CancellationToken cancellationToken)
        {
            if (_isConnecting || _isConnected)
            {
                Debug.LogWarning("[WebSocketManager] Already connected or connecting");
                return;
            }

            _isConnecting = true;

            try
            {
                // Get WebSocket URL from WebGLBridge
                _webSocketUrl = BugWars.JavaScriptBridge.WebGLBridge.Instance?.GetWebSocketEndpoint() ?? "ws://localhost:4321/ws";
                Debug.Log($"[WebSocketManager] Connecting to: {_webSocketUrl}");

                // Get access token from PlayerData
                var playerData = BugWars.Entity.EntityManager.Instance?.PlayerData;
                if (playerData == null || !playerData.IsAuthenticated)
                {
                    Debug.LogError("[WebSocketManager] Cannot connect - player not authenticated");
                    _isConnecting = false;
                    return;
                }

                _accessToken = playerData.AccessToken;
                if (string.IsNullOrEmpty(_accessToken))
                {
                    Debug.LogError("[WebSocketManager] Cannot connect - access token is null or empty");
                    _isConnecting = false;
                    return;
                }

                Debug.Log($"[WebSocketManager] Using access token (length: {_accessToken.Length})");

                // Create WebSocket with Authorization header
                var headers = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {_accessToken}" }
                };

                _webSocket = new WebSocket(_webSocketUrl, headers);

                // Set up event handlers
                _webSocket.OnOpen += OnWebSocketOpen;
                _webSocket.OnMessage += OnWebSocketMessage;
                _webSocket.OnError += OnWebSocketError;
                _webSocket.OnClose += OnWebSocketClose;

                // Connect to server
                Debug.Log("[WebSocketManager] Initiating WebSocket connection...");
                await _webSocket.Connect();

                // Wait for connection to be established (with timeout)
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

                while (_webSocket.State == WebSocketState.Connecting && !timeoutCts.Token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, timeoutCts.Token);
                }

                if (_webSocket.State != WebSocketState.Open)
                {
                    Debug.LogError($"[WebSocketManager] Connection failed - State: {_webSocket.State}");
                    _isConnecting = false;
                    _reconnectAttempts++;

                    if (_reconnectAttempts < MaxReconnectAttempts)
                    {
                        Debug.Log($"[WebSocketManager] Retrying connection in {ReconnectDelaySeconds}s (attempt {_reconnectAttempts}/{MaxReconnectAttempts})");
                        await UniTask.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds), cancellationToken: cancellationToken);
                        await ConnectAsync(cancellationToken);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketManager] Connection error: {e.Message}");
                _isConnecting = false;
                _reconnectAttempts++;

                if (_reconnectAttempts < MaxReconnectAttempts)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds), cancellationToken: cancellationToken);
                    await ConnectAsync(cancellationToken);
                }
            }
        }

        /// <summary>
        /// Disconnect from WebSocket server
        /// </summary>
        private async UniTask DisconnectAsync()
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                return;
            }

            Debug.Log("[WebSocketManager] Disconnecting...");
            await _webSocket.Close();
            _isConnected = false;
            _isConnecting = false;
            _reconnectAttempts = 0;
        }
        #endregion

        #region WebSocket Event Handlers
        private void OnWebSocketOpen()
        {
            _isConnected = true;
            _isConnecting = false;
            _reconnectAttempts = 0;

            // Initialize heartbeat timers
            _lastHeartbeatTime = Time.time;
            _lastPongReceivedTime = Time.time;

            var playerData = BugWars.Entity.EntityManager.Instance?.PlayerData;
            Debug.Log($"[WebSocketManager] ✓ Connected to WebSocket server as {playerData?.GetBestDisplayName() ?? "Unknown"}");

            // Send queued messages
            while (_messageQueue.Count > 0)
            {
                var message = _messageQueue.Dequeue();
                SendMessageInternal(message);
            }
        }

        private void OnWebSocketMessage(byte[] data)
        {
            try
            {
                string message = System.Text.Encoding.UTF8.GetString(data);
                Debug.Log($"[WebSocketManager] ← Received: {message}");

                // Parse and handle message
                HandleIncomingMessage(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketManager] Error handling message: {e.Message}");
            }
        }

        private void OnWebSocketError(string errorMessage)
        {
            Debug.LogError($"[WebSocketManager] WebSocket error: {errorMessage}");
        }

        private void OnWebSocketClose(WebSocketCloseCode closeCode)
        {
            Debug.Log($"[WebSocketManager] Connection closed: {closeCode}");
            _isConnected = false;
            _isConnecting = false;

            // Attempt reconnection if not intentional disconnect
            if (closeCode != WebSocketCloseCode.Normal && _reconnectAttempts < MaxReconnectAttempts)
            {
                _reconnectAttempts++;
                Debug.Log($"[WebSocketManager] Attempting reconnection ({_reconnectAttempts}/{MaxReconnectAttempts})...");
                UniTask.Void(async () =>
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds));
                    await ConnectAsync(_cts.Token);
                });
            }
        }
        #endregion

        #region Message Handling
        /// <summary>
        /// Handle incoming WebSocket messages
        /// </summary>
        private void HandleIncomingMessage(string message)
        {
            try
            {
                // Parse JSON message
                var messageData = JsonUtility.FromJson<WebSocketMessage>(message);

                switch (messageData.type)
                {
                    case "connected":
                        Debug.Log($"[WebSocketManager] Server confirmed connection: {message}");
                        break;

                    case "pong":
                        // Update last pong time - connection is alive
                        _lastPongReceivedTime = Time.time;
                        Debug.Log("[WebSocketManager] ♥ Pong received");
                        break;

                    case "echo":
                        Debug.Log($"[WebSocketManager] Echo response: {messageData.message}");
                        break;

                    case "error":
                        Debug.LogError($"[WebSocketManager] Server error: {messageData.message}");
                        break;

                    default:
                        Debug.Log($"[WebSocketManager] Unknown message type: {messageData.type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebSocketManager] Could not parse message as JSON: {e.Message}");
                // Message might be plain text, log it anyway
                Debug.Log($"[WebSocketManager] Raw message: {message}");
            }
        }

        /// <summary>
        /// Send a message to the WebSocket server
        /// </summary>
        public void SendMessage(string type, string data = "")
        {
            var message = new WebSocketMessage
            {
                type = type,
                message = data
            };

            string json = JsonUtility.ToJson(message);
            SendMessageInternal(json);
        }

        /// <summary>
        /// Internal method to send raw message
        /// </summary>
        private void SendMessageInternal(string message)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                Debug.LogWarning("[WebSocketManager] Not connected, queueing message");

                if (_messageQueue.Count < MaxQueueSize)
                {
                    _messageQueue.Enqueue(message);
                }
                else
                {
                    Debug.LogWarning("[WebSocketManager] Message queue full, dropping message");
                }
                return;
            }

            Debug.Log($"[WebSocketManager] → Sending: {message}");
            _webSocket.SendText(message);
        }
        #endregion
    }

    #region Data Structures
    [Serializable]
    public class WebSocketMessage
    {
        public string type;
        public string message;
    }
    #endregion
}
