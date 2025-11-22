using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using R3;

namespace BugWars.Auth
{
    /// <summary>
    /// Manages JWT token refresh to prevent authentication expiration.
    /// Monitors token expiration and proactively requests refresh from JavaScript layer.
    ///
    /// Flow:
    /// 1. Subscribe to PlayerData authentication changes
    /// 2. Monitor token expiration time
    /// 3. Request refresh 5 minutes before expiration
    /// 4. Notify WebSocketManager to reconnect with new token
    /// </summary>
    public class TokenRefreshManager : MonoBehaviour
    {
        private BugWars.Entity.PlayerData _playerData;
        private BugWars.JavaScriptBridge.WebGLBridge _webGLBridge;
        private BugWars.Network.WebSocketManager _webSocketManager;

        // Refresh settings
        private const float RefreshCheckIntervalSeconds = 30f; // Check every 30 seconds
        private const float RefreshBeforeExpiryMinutes = 5f;    // Refresh 5 minutes before expiration

        // State
        private bool _isMonitoring = false;
        private float _timeSinceLastCheck = 0f;
        private IDisposable _authSubscription;

        [Inject]
        public void Construct(
            BugWars.JavaScriptBridge.WebGLBridge webGLBridge,
            BugWars.Network.WebSocketManager webSocketManager)
        {
            _webGLBridge = webGLBridge;
            _webSocketManager = webSocketManager;
            Debug.Log("[TokenRefreshManager] Dependencies injected");
        }

        private void Start()
        {
            // Get PlayerData from EntityManager
            _playerData = BugWars.Entity.EntityManager.Instance?.PlayerData;

            if (_playerData == null)
            {
                Debug.LogError("[TokenRefreshManager] PlayerData not available, token refresh will not work!");
                return;
            }

            // Subscribe to authentication state changes
            _authSubscription = _playerData.IsAuthenticatedObservable.Subscribe(isAuth =>
            {
                if (isAuth)
                {
                    Debug.Log("[TokenRefreshManager] User authenticated, starting token monitoring");
                    _isMonitoring = true;
                    _timeSinceLastCheck = RefreshCheckIntervalSeconds; // Check immediately
                }
                else
                {
                    Debug.Log("[TokenRefreshManager] User logged out, stopping token monitoring");
                    _isMonitoring = false;
                }
            });

            // If already authenticated, start monitoring
            if (_playerData.IsAuthenticated)
            {
                _isMonitoring = true;
            }

            Debug.Log("[TokenRefreshManager] Initialized");
        }

        private void Update()
        {
            if (!_isMonitoring || _playerData == null)
                return;

            _timeSinceLastCheck += Time.deltaTime;

            if (_timeSinceLastCheck >= RefreshCheckIntervalSeconds)
            {
                _timeSinceLastCheck = 0f;
                CheckAndRefreshToken();
            }
        }

        private void CheckAndRefreshToken()
        {
            if (_playerData == null || !_playerData.IsAuthenticated)
                return;

            // Get time until expiration
            var expiresAtTime = DateTimeOffset.FromUnixTimeSeconds(_playerData.ExpiresAt).UtcDateTime;
            var now = DateTime.UtcNow;
            var timeUntilExpiry = expiresAtTime - now;

            Debug.Log($"[TokenRefreshManager] Token expires in {timeUntilExpiry.TotalMinutes:F1} minutes");

            // If token expires in less than RefreshBeforeExpiryMinutes, request refresh
            if (timeUntilExpiry.TotalMinutes < RefreshBeforeExpiryMinutes)
            {
                Debug.Log($"[TokenRefreshManager] Token expiring soon ({timeUntilExpiry.TotalMinutes:F1} min), requesting refresh");
                RequestTokenRefresh();
            }

            // If token is already expired, force refresh
            if (timeUntilExpiry.TotalSeconds <= 0)
            {
                Debug.LogWarning("[TokenRefreshManager] Token has EXPIRED! Forcing immediate refresh");
                RequestTokenRefresh();
            }
        }

        private void RequestTokenRefresh()
        {
            if (_webGLBridge == null)
            {
                Debug.LogError("[TokenRefreshManager] WebGLBridge not available, cannot request token refresh");
                return;
            }

            Debug.Log("[TokenRefreshManager] Sending token refresh request to JavaScript");
            _webGLBridge.RequestTokenRefresh();

            // Note: The new token will come back via WebGLBridge.OnSessionUpdate
            // which will update PlayerData and trigger a reconnection
        }

        /// <summary>
        /// Manually force a token refresh (useful for testing or manual triggers)
        /// </summary>
        public void ForceRefresh()
        {
            Debug.Log("[TokenRefreshManager] Manual token refresh requested");
            RequestTokenRefresh();
        }

        /// <summary>
        /// Check if token is expired or about to expire
        /// </summary>
        public bool IsTokenExpiringSoon()
        {
            if (_playerData == null || !_playerData.IsAuthenticated)
                return true;

            var expiresAtTime = DateTimeOffset.FromUnixTimeSeconds(_playerData.ExpiresAt).UtcDateTime;
            var now = DateTime.UtcNow;
            var timeUntilExpiry = expiresAtTime - now;

            return timeUntilExpiry.TotalMinutes < RefreshBeforeExpiryMinutes;
        }

        private void OnDestroy()
        {
            _authSubscription?.Dispose();
            _isMonitoring = false;
        }

        #region Debug Helpers

        [ContextMenu("Force Token Refresh")]
        private void DebugForceRefresh()
        {
            ForceRefresh();
        }

        [ContextMenu("Check Token Status")]
        private void DebugCheckTokenStatus()
        {
            if (_playerData == null || !_playerData.IsAuthenticated)
            {
                Debug.Log("[TokenRefreshManager] Not authenticated");
                return;
            }

            var expiresAtTime = DateTimeOffset.FromUnixTimeSeconds(_playerData.ExpiresAt).UtcDateTime;
            var now = DateTime.UtcNow;
            var timeUntilExpiry = expiresAtTime - now;

            Debug.Log($"[TokenRefreshManager] Token Status:\n" +
                     $"  Expires At: {expiresAtTime:yyyy-MM-dd HH:mm:ss} UTC\n" +
                     $"  Current Time: {now:yyyy-MM-dd HH:mm:ss} UTC\n" +
                     $"  Time Until Expiry: {timeUntilExpiry.TotalMinutes:F1} minutes\n" +
                     $"  Expiring Soon: {IsTokenExpiringSoon()}");
        }

        #endregion
    }
}
