using UnityEngine;

namespace BugWars.Terrain
{
    /// <summary>
    /// Level-of-Detail system for environment objects
    /// Disables colliders and renderers based on distance from player to improve WebGL performance
    /// </summary>
    public class EnvironmentObjectLOD : MonoBehaviour
    {
        [Header("LOD Configuration")]
        [Tooltip("Distance beyond which colliders are disabled")]
        public float colliderCullingDistance = 50f;

        [Tooltip("Distance beyond which renderers are disabled")]
        public float rendererCullingDistance = 100f;

        [Header("Performance")]
        [Tooltip("How often to update LOD state (in seconds)")]
        public float updateInterval = 0.2f;

        [Header("Debug")]
        public bool showDebugLogs = false;

        private Transform _playerTransform;
        private Collider[] _colliders;
        private Renderer[] _renderers;
        private float _nextUpdateTime;

        private bool _collidersEnabled = true;
        private bool _renderersEnabled = true;

        private void Awake()
        {
            // Cache all colliders and renderers on this object and its children
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Start()
        {
            // Find the player - try multiple tags
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Camera3D");
            }
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("MainCamera");
            }

            if (player != null)
            {
                _playerTransform = player.transform;
                if (showDebugLogs)
                {
                    Debug.Log($"[EnvironmentObjectLOD] Found player for LOD calculations (tag: {player.tag})");
                }
            }
            else
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning($"[EnvironmentObjectLOD] Player not found for LOD calculations on {gameObject.name} (tried tags: Player, Camera3D, MainCamera)");
                }
            }
        }

        private void Update()
        {
            // Only update at specified intervals to reduce overhead
            if (Time.time < _nextUpdateTime) return;
            _nextUpdateTime = Time.time + updateInterval;

            // Skip if player not found
            if (_playerTransform == null) return;

            // Calculate distance to player
            float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);

            // Update colliders based on distance
            bool shouldEnableColliders = distanceToPlayer <= colliderCullingDistance;
            if (shouldEnableColliders != _collidersEnabled)
            {
                SetCollidersEnabled(shouldEnableColliders);
                _collidersEnabled = shouldEnableColliders;

                if (showDebugLogs)
                {
                    Debug.Log($"[EnvironmentObjectLOD] {gameObject.name} colliders: {(shouldEnableColliders ? "ENABLED" : "DISABLED")} (distance: {distanceToPlayer:F1}m)");
                }
            }

            // Update renderers based on distance
            bool shouldEnableRenderers = distanceToPlayer <= rendererCullingDistance;
            if (shouldEnableRenderers != _renderersEnabled)
            {
                SetRenderersEnabled(shouldEnableRenderers);
                _renderersEnabled = shouldEnableRenderers;

                if (showDebugLogs)
                {
                    Debug.Log($"[EnvironmentObjectLOD] {gameObject.name} renderers: {(shouldEnableRenderers ? "ENABLED" : "DISABLED")} (distance: {distanceToPlayer:F1}m)");
                }
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (var collider in _colliders)
            {
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }
        }

        private void SetRenderersEnabled(bool enabled)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = enabled;
                }
            }
        }

        /// <summary>
        /// Call this to force an immediate LOD update (useful after spawning)
        /// </summary>
        public void ForceUpdate()
        {
            _nextUpdateTime = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize culling distances in editor
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, colliderCullingDistance);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, rendererCullingDistance);
        }
    }
}
