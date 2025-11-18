using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BugWars.Core;
using VContainer;

namespace BugWars.Entity
{
    /// <summary>
    /// Manages all entities in the game (Players, NPCs, etc.)
    /// Provides centralized access to all entities
    /// Tracks player data (name, level, score, play time, etc.)
    /// Integrates with CameraManager for player camera tracking
    /// </summary>
    public class EntityManager : MonoBehaviour
    {
        private static EntityManager instance;
        public static EntityManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<EntityManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("EntityManager");
                        instance = go.AddComponent<EntityManager>();
                    }
                }
                return instance;
            }
        }

        [Header("Entity Tracking")]
        [SerializeField] private List<Entity> allEntities = new List<Entity>();
        [SerializeField] private bool autoRegisterEntities = true;

        [Header("Player Spawn Configuration")]
        [SerializeField] [Tooltip("Player prefab to spawn at game start (AdventurerCharacter recommended)")]
        private GameObject playerPrefab;
        [SerializeField] [Tooltip("Auto-spawn player after initialization. Set to false if GameManager controls spawn timing.")]
        private bool autoSpawnPlayer = false;
        [SerializeField] [Tooltip("Spawn position for the player. If zero, spawns at (0, 2, 0)")]
        private Vector3 playerSpawnPosition = Vector3.zero;
        [SerializeField] [Tooltip("Automatically set camera to follow player on spawn")]
        private bool autoCameraFollow = true;

        // Player Runtime Reference (Auto-Set - Not Serialized)
        private Entity playerEntity;
        public Entity Player => playerEntity;

        [Header("Player Data")]
        [SerializeField] private PlayerData playerData;
        public PlayerData PlayerData => playerData;

        private bool playerSpawned = false;

        // Dependency Injection
        private EventManager _eventManager;
        private BugWars.Terrain.TerrainManager _terrainManager;

        [Inject]
        public void Construct(EventManager eventManager, BugWars.Terrain.TerrainManager terrainManager)
        {
            _eventManager = eventManager;
            _terrainManager = terrainManager;
        }

        // Terrain streaming
        [Header("Terrain Streaming")]
        [SerializeField] [Tooltip("Update terrain chunks as player moves")]
        private bool enableTerrainStreaming = true;
        [SerializeField] [Tooltip("Check player position every N seconds")]
        private float terrainStreamingInterval = 1.0f;
        private Vector3 lastPlayerChunkCheckPosition = Vector3.zero;
        private float terrainStreamingTimer = 0f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            // Note: DontDestroyOnLoad is handled by VContainer's LifetimeScope
            // No need to call it here - the LifetimeScope GameObject persists across scenes

            // Initialize player data if not already set
            if (playerData == null)
            {
                playerData = new PlayerData();
            }

            if (autoRegisterEntities)
            {
                RegisterAllEntitiesInScene();
            }
        }

        private void Start()
        {
            // Auto-spawn player if enabled
            if (autoSpawnPlayer && !playerSpawned)
            {
                // Delay player spawn by one frame to ensure CameraManager has initialized
                StartCoroutine(SpawnPlayerAfterCameraInit());
            }
        }

        private void Update()
        {
            // Update player data (play time and position)
            if (playerEntity != null && playerData != null)
            {
                // Track play time
                playerData.PlayTime += Time.deltaTime;

                // Update last known position
                playerData.LastKnownPosition = playerEntity.transform.position;
            }

            // Update terrain chunks based on player movement
            if (enableTerrainStreaming && playerEntity != null && _terrainManager != null)
            {
                terrainStreamingTimer += Time.deltaTime;

                if (terrainStreamingTimer >= terrainStreamingInterval)
                {
                    terrainStreamingTimer = 0f;

                    Vector3 playerPos = playerEntity.transform.position;
                    float distanceMoved = Vector3.Distance(playerPos, lastPlayerChunkCheckPosition);

                    // Only update if player moved a significant distance (20 units = 1/4 chunk)
                    if (distanceMoved > 20f || lastPlayerChunkCheckPosition == Vector3.zero)
                    {
                        lastPlayerChunkCheckPosition = playerPos;
                        // Fire and forget - don't await (async streaming)
                        _ = _terrainManager.UpdateChunksAroundPosition(playerPos);
                    }
                }
            }
        }

        /// <summary>
        /// Spawns player after waiting one frame for CameraManager to initialize
        /// </summary>
        private System.Collections.IEnumerator SpawnPlayerAfterCameraInit()
        {
            // Wait for end of frame to ensure CameraManager.Start() has run
            yield return new WaitForEndOfFrame();
            SpawnPlayer();
        }

        /// <summary>
        /// Spawn the player at the configured position
        /// </summary>
        public void SpawnPlayer()
        {
            if (playerSpawned)
            {
                Debug.LogWarning("[EntityManager] Player already spawned");
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[EntityManager] Cannot spawn player - playerPrefab is not assigned!");
                return;
            }

            // Determine spawn position
            Vector3 spawnPos = playerSpawnPosition;
            if (spawnPos == Vector3.zero)
            {
                spawnPos = new Vector3(0, 2f, 0); // Default spawn slightly above origin
            }

            // Instantiate player
            GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            playerObj.name = playerData != null ? playerData.PlayerName : "Player";

            // Get Entity component
            Entity entity = playerObj.GetComponent<Entity>();
            if (entity != null)
            {
                // Set as player (will auto-register if not already)
                SetPlayer(entity, false); // Don't log during spawn
                playerSpawned = true;

                // Sync player data name with entity
                if (playerData != null)
                {
                    // If player data has a name, use it for the entity
                    // Otherwise, use entity name for player data
                    if (!string.IsNullOrEmpty(playerData.PlayerName))
                    {
                        entity.GetType().GetField("entityName",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            ?.SetValue(entity, playerData.PlayerName);
                    }
                }

                // Set up camera to follow player using event system
                if (autoCameraFollow)
                {
                    RequestCameraFollow(playerObj.transform);
                }
            }
            else
            {
                Debug.LogError("[EntityManager] Player prefab does not have an Entity component!");
                Destroy(playerObj);
            }
        }

        /// <summary>
        /// Spawn player at a specific position
        /// </summary>
        public void SpawnPlayerAt(Vector3 position)
        {
            playerSpawnPosition = position;
            SpawnPlayer();
        }

        /// <summary>
        /// Check if player has been spawned
        /// </summary>
        public bool IsPlayerSpawned()
        {
            return playerSpawned && playerEntity != null;
        }

        /// <summary>
        /// Automatically finds and registers all entities in the scene
        /// </summary>
        public void RegisterAllEntitiesInScene()
        {
            Entity[] foundEntities = FindObjectsByType<Entity>(FindObjectsSortMode.None);
            foreach (Entity entity in foundEntities)
            {
                RegisterEntity(entity, false);
            }
        }

        /// <summary>
        /// Register an entity with the manager
        /// </summary>
        public void RegisterEntity(Entity entity, bool logRegistration = false)
        {
            if (entity == null)
            {
                Debug.LogWarning("[EntityManager] Attempted to register null entity");
                return;
            }

            if (allEntities.Contains(entity))
            {
                return; // Silently skip already registered entities
            }

            allEntities.Add(entity);

            // Check if this is the player entity
            // Primary: AdventurerCharacter (inherits from Player)
            // Legacy support: Samurai and BlankPlayer for backward compatibility
            bool isPlayer = entity.CompareTag("Player") ||
                          entity.CompareTag(Core.CameraTags.Camera3D) ||      // 3D characters (AdventurerCharacter)
                          entity.CompareTag(Core.CameraTags.CameraBillboard) ||  // Billboard sprites (legacy)
                          entity is BugWars.Entity.Player.Player ||           // ✅ PRIMARY: AdventurerCharacter
                          entity is BugWars.Character.Samurai ||              // Legacy support
                          entity is BugWars.Character.BlankPlayer;            // Legacy support

            if (isPlayer)
            {
                SetPlayer(entity, false);

                // IMPORTANT: Trigger camera follow for manually placed players in scene
                // (Players spawned by SpawnPlayer() already have camera follow setup)
                if (autoCameraFollow && !playerSpawned)
                {
                    Debug.Log($"[EntityManager] Player detected in scene: {entity.GetEntityName()}. Setting up camera follow.");
                    RequestCameraFollow(entity.transform);
                }
            }
        }

        /// <summary>
        /// Unregister an entity from the manager
        /// </summary>
        public void UnregisterEntity(Entity entity)
        {
            if (entity == null) return;

            allEntities.Remove(entity);
        }

        /// <summary>
        /// Get all registered entities
        /// </summary>
        public List<Entity> GetAllEntities()
        {
            return new List<Entity>(allEntities);
        }

        /// <summary>
        /// Get all alive entities
        /// </summary>
        public List<Entity> GetAliveEntities()
        {
            return allEntities.Where(e => e != null && e.IsAlive()).ToList();
        }

        /// <summary>
        /// Get all dead entities
        /// </summary>
        public List<Entity> GetDeadEntities()
        {
            return allEntities.Where(e => e != null && !e.IsAlive()).ToList();
        }

        /// <summary>
        /// Get entity by name
        /// </summary>
        public Entity GetEntityByName(string name)
        {
            return allEntities.FirstOrDefault(e => e != null && e.GetEntityName() == name);
        }

        /// <summary>
        /// Get entities within a certain radius of a position
        /// </summary>
        public List<Entity> GetEntitiesInRadius(Vector3 position, float radius, bool onlyAlive = true)
        {
            List<Entity> entitiesInRadius = new List<Entity>();

            foreach (Entity entity in allEntities)
            {
                if (entity == null) continue;
                if (onlyAlive && !entity.IsAlive()) continue;

                float distance = Vector3.Distance(position, entity.transform.position);
                if (distance <= radius)
                {
                    entitiesInRadius.Add(entity);
                }
            }

            return entitiesInRadius;
        }

        /// <summary>
        /// Get the closest entity to a position
        /// </summary>
        public Entity GetClosestEntity(Vector3 position, bool onlyAlive = true)
        {
            Entity closest = null;
            float closestDistance = float.MaxValue;

            foreach (Entity entity in allEntities)
            {
                if (entity == null) continue;
                if (onlyAlive && !entity.IsAlive()) continue;

                float distance = Vector3.Distance(position, entity.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = entity;
                }
            }

            return closest;
        }

        /// <summary>
        /// Get entities of a specific type
        /// </summary>
        public List<T> GetEntitiesOfType<T>() where T : Entity
        {
            return allEntities.OfType<T>().ToList();
        }

        /// <summary>
        /// Clean up null references from the entity list
        /// </summary>
        public void CleanupNullEntities()
        {
            int removedCount = allEntities.RemoveAll(e => e == null);
            if (removedCount > 0)
            {
                Debug.Log($"[EntityManager] Cleaned up {removedCount} null entity references");
            }
        }

        /// <summary>
        /// Get count of entities
        /// </summary>
        public int GetEntityCount(bool onlyAlive = false)
        {
            if (onlyAlive)
            {
                return allEntities.Count(e => e != null && e.IsAlive());
            }
            return allEntities.Count(e => e != null);
        }

        #region Camera Integration

        /// <summary>
        /// Request camera to follow player using event system
        /// Decouples EntityManager from CameraManager
        /// Uses ICameraPreference interface if available, with tag validation
        /// Falls back to default configuration based on character tags
        /// </summary>
        private void RequestCameraFollow(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                Debug.LogWarning("[EntityManager] Cannot request camera follow - player transform is null");
                return;
            }

            if (_eventManager == null)
            {
                Debug.LogWarning("[EntityManager] Cannot request camera follow - EventManager is null");
                return;
            }

            Core.CameraFollowConfig config;
            GameObject playerObject = playerTransform.gameObject;

            // Check if the player implements ICameraPreference (Option 1: Interface-based)
            var cameraPreference = playerObject.GetComponent<Core.ICameraPreference>();

            if (cameraPreference != null)
            {
                // Player explicitly defines camera preferences
                config = cameraPreference.GetPreferredCameraConfig(playerTransform);

                // Validate tags (defensive programming with warnings)
                ValidateCameraTags(playerObject, cameraPreference);

                Debug.Log($"[EntityManager] Using custom camera config from ICameraPreference for {playerObject.name} " +
                          $"(Billboard: {cameraPreference.UsesBillboarding}, Expected Tag: {cameraPreference.GetExpectedCameraTag()})");
            }
            else
            {
                // Fallback: Use tags to determine camera type (Option 2/3: Tag-based)
                config = DetermineCameraConfigFromTags(playerObject, playerTransform);

                // Warn if character doesn't implement ICameraPreference
                Debug.LogWarning($"[EntityManager] Player '{playerObject.name}' does not implement ICameraPreference. " +
                                 $"Using tag-based fallback. Consider implementing ICameraPreference for explicit camera control.");
            }

            // Fire event for CameraManager to handle
            _eventManager.RequestCameraFollow(config);
        }

        /// <summary>
        /// Validate that the GameObject has the correct tags matching its camera preferences
        /// Logs warnings if tags are missing or incorrect
        /// </summary>
        private void ValidateCameraTags(GameObject playerObject, Core.ICameraPreference cameraPreference)
        {
            string expectedTag = cameraPreference.GetExpectedCameraTag();

            // Check if the expected camera tag exists
            if (!playerObject.CompareTag(expectedTag))
            {
                Debug.LogWarning($"[EntityManager] Tag Mismatch: Player '{playerObject.name}' expects tag '{expectedTag}' " +
                                 $"but has tag '{playerObject.tag}'. Please update the prefab tags in the Unity Editor.");
                Debug.LogWarning($"[EntityManager] To fix: Select '{playerObject.name}' prefab and set Tag to '{expectedTag}'");
            }

            // Ensure Player tag is also present (defensive check)
            if (!playerObject.CompareTag(Core.CameraTags.Player) && !playerObject.CompareTag(expectedTag))
            {
                Debug.LogWarning($"[EntityManager] Player '{playerObject.name}' should be tagged as '{Core.CameraTags.Player}' " +
                                 $"or '{expectedTag}' for proper identification.");
            }
        }

        /// <summary>
        /// Determine camera configuration from GameObject tags (fallback method)
        /// Used when ICameraPreference is not implemented
        /// </summary>
        private Core.CameraFollowConfig DetermineCameraConfigFromTags(GameObject playerObject, Transform playerTransform)
        {
            // Check for Camera3D tag first (PRIMARY: AdventurerCharacter and other 3D characters)
            if (playerObject.CompareTag(Core.CameraTags.Camera3D))
            {
                Debug.Log($"[EntityManager] Detected Camera3D tag - using SimpleThirdPerson camera for {playerObject.name}");
                return Core.CameraFollowConfig.SimpleThirdPerson(playerTransform);
            }
            // Check for CameraBillboard tag (LEGACY: Samurai, BlankPlayer - 2D billboard sprites)
            else if (playerObject.CompareTag(Core.CameraTags.CameraBillboard))
            {
                Debug.Log($"[EntityManager] Detected CameraBillboard tag - using SimpleThirdPerson camera for {playerObject.name}");
                return Core.CameraFollowConfig.SimpleThirdPerson(playerTransform);
            }
            // Default fallback: Use SimpleThirdPerson (optimized for 3D characters like AdventurerCharacter)
            else
            {
                Debug.LogWarning($"[EntityManager] No camera-specific tag found on '{playerObject.name}' (Current tag: '{playerObject.tag}'). " +
                                 $"Using default SimpleThirdPerson camera. Consider adding '{Core.CameraTags.Camera3D}' or " +
                                 $"'{Core.CameraTags.CameraBillboard}' tag, or implement ICameraPreference.");
                return Core.CameraFollowConfig.SimpleThirdPerson(playerTransform);
            }
        }

        /// <summary>
        /// Manually request camera to follow a specific target
        /// </summary>
        public void SetCameraFollowTarget(Transform target, string cameraName = null)
        {
            if (target == null)
            {
                Debug.LogWarning("[EntityManager] Cannot set camera follow - target is null");
                return;
            }

            if (_eventManager == null)
            {
                Debug.LogWarning("[EntityManager] Cannot set camera follow - EventManager is null");
                return;
            }

            var config = Core.CameraFollowConfig.SimpleThirdPerson(target, cameraName);
            _eventManager.RequestCameraFollow(config);
        }

        #endregion

        #region Player Management

        /// <summary>
        /// Set the player entity reference
        /// </summary>
        public void SetPlayer(Entity entity, bool logChange = true)
        {
            if (entity == null)
            {
                Debug.LogWarning("[EntityManager] Attempted to set null player entity");
                return;
            }

            if (playerEntity != null && playerEntity != entity)
            {
                if (logChange)
                {
                    Debug.LogWarning($"[EntityManager] Replacing existing player {playerEntity.GetEntityName()} with {entity.GetEntityName()}");
                }
            }

            playerEntity = entity;
        }

        /// <summary>
        /// Get the player entity
        /// </summary>
        public Entity GetPlayer()
        {
            return playerEntity;
        }

        /// <summary>
        /// Check if player entity exists and is alive
        /// </summary>
        public bool IsPlayerAlive()
        {
            return playerEntity != null && playerEntity.IsAlive();
        }

        /// <summary>
        /// Get player position (safe accessor)
        /// </summary>
        public Vector3 GetPlayerPosition()
        {
            if (playerEntity != null)
            {
                return playerEntity.transform.position;
            }
            return Vector3.zero;
        }

        #endregion

        #region Player Data Management

        /// <summary>
        /// Get the current player data
        /// </summary>
        public PlayerData GetPlayerData()
        {
            return playerData;
        }

        /// <summary>
        /// Set player data (useful for loading saved data)
        /// </summary>
        public void SetPlayerData(PlayerData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[EntityManager] Attempted to set null player data");
                return;
            }

            playerData = data;

            // Sync entity name if player entity exists
            if (playerEntity != null)
            {
                playerEntity.GetType().GetField("entityName",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(playerEntity, playerData.PlayerName);
            }
        }

        /// <summary>
        /// Set player name
        /// </summary>
        public void SetPlayerName(string name)
        {
            if (playerData == null)
            {
                playerData = new PlayerData();
            }

            playerData.PlayerName = name;

            // Sync entity name if player entity exists
            if (playerEntity != null)
            {
                playerEntity.GetType().GetField("entityName",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(playerEntity, name);
            }
        }

        /// <summary>
        /// Get player name
        /// </summary>
        public string GetPlayerName()
        {
            if (playerData != null)
            {
                return playerData.PlayerName;
            }
            return playerEntity != null ? playerEntity.GetEntityName() : "Unknown";
        }

        /// <summary>
        /// Add experience to player
        /// </summary>
        /// <returns>True if player leveled up</returns>
        public bool AddPlayerExperience(int amount)
        {
            if (playerData == null) return false;

            bool leveledUp = playerData.AddExperience(amount);

            if (leveledUp)
            {
                Debug.Log($"[EntityManager] {playerData.PlayerName} leveled up to level {playerData.Level}!");
            }

            return leveledUp;
        }

        /// <summary>
        /// Add score to player
        /// </summary>
        public void AddPlayerScore(int points)
        {
            if (playerData != null)
            {
                playerData.AddScore(points);
            }
        }

        /// <summary>
        /// Get player level
        /// </summary>
        public int GetPlayerLevel()
        {
            return playerData?.Level ?? 1;
        }

        /// <summary>
        /// Get player experience
        /// </summary>
        public int GetPlayerExperience()
        {
            return playerData?.Experience ?? 0;
        }

        /// <summary>
        /// Get player score
        /// </summary>
        public int GetPlayerScore()
        {
            return playerData?.Score ?? 0;
        }

        /// <summary>
        /// Get player play time
        /// </summary>
        public float GetPlayerPlayTime()
        {
            return playerData?.PlayTime ?? 0f;
        }

        /// <summary>
        /// Get formatted player play time (HH:MM:SS)
        /// </summary>
        public string GetFormattedPlayerPlayTime()
        {
            return playerData?.GetFormattedPlayTime() ?? "00:00:00";
        }

        /// <summary>
        /// Reset player data to defaults
        /// </summary>
        public void ResetPlayerData()
        {
            if (playerData != null)
            {
                playerData.Reset();
                Debug.Log("[EntityManager] Player data reset to defaults");
            }
        }

        /// <summary>
        /// Log current player data to console (useful for debugging)
        /// </summary>
        public void LogPlayerData()
        {
            if (playerData != null)
            {
                Debug.Log($"[EntityManager] {playerData.ToString()}");
            }
            else
            {
                Debug.LogWarning("[EntityManager] No player data available");
            }
        }

        #endregion

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // Debug visualization
        private void OnDrawGizmos()
        {
            if (allEntities == null) return;

            foreach (Entity entity in allEntities)
            {
                if (entity == null) continue;

                // Highlight player entity in blue
                if (entity == playerEntity)
                {
                    Gizmos.color = entity.IsAlive() ? Color.cyan : Color.magenta;
                    Gizmos.DrawWireSphere(entity.transform.position, 0.75f);
                }
                else
                {
                    Gizmos.color = entity.IsAlive() ? Color.green : Color.red;
                    Gizmos.DrawWireSphere(entity.transform.position, 0.5f);
                }
            }
        }
    }
}
