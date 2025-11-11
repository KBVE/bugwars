using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BugWars.Entity
{
    /// <summary>
    /// Manages all entities in the game (Players, NPCs, etc.)
    /// Provides centralized access to all entities
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

        [Header("Player Configuration")]
        [SerializeField] [Tooltip("Player prefab to spawn at game start")]
        private GameObject playerPrefab;
        [SerializeField] [Tooltip("Auto-spawn player after initialization")]
        private bool autoSpawnPlayer = true;
        [SerializeField] [Tooltip("Spawn position for the player. If zero, spawns at (0, 2, 0)")]
        private Vector3 playerSpawnPosition = Vector3.zero;

        [Header("Player Reference")]
        [SerializeField] private Entity playerEntity;
        public Entity Player => playerEntity;

        private bool playerSpawned = false;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (autoRegisterEntities)
            {
                RegisterAllEntitiesInScene();
            }
        }

        private void Start()
        {
            Debug.Log($"[EntityManager] Initialized with {allEntities.Count} entities");

            // Auto-spawn player if enabled
            if (autoSpawnPlayer && !playerSpawned)
            {
                SpawnPlayer();
            }
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
            playerObj.name = "Player";

            // Get Entity component
            Entity entity = playerObj.GetComponent<Entity>();
            if (entity != null)
            {
                // Set as player (will auto-register if not already)
                SetPlayer(entity, true);
                playerSpawned = true;
                Debug.Log($"[EntityManager] Player spawned at {spawnPos}");
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
            Debug.Log($"[EntityManager] Auto-registered {foundEntities.Length} entities from scene");
        }

        /// <summary>
        /// Register an entity with the manager
        /// </summary>
        public void RegisterEntity(Entity entity, bool logRegistration = true)
        {
            if (entity == null)
            {
                Debug.LogWarning("[EntityManager] Attempted to register null entity");
                return;
            }

            if (allEntities.Contains(entity))
            {
                Debug.LogWarning($"[EntityManager] Entity {entity.GetEntityName()} already registered");
                return;
            }

            allEntities.Add(entity);

            // Check if this is the player entity
            if (entity.CompareTag("Player") || entity is BugWars.Character.Samurai)
            {
                SetPlayer(entity, false);
            }

            if (logRegistration)
            {
                Debug.Log($"[EntityManager] Registered entity: {entity.GetEntityName()}");
            }
        }

        /// <summary>
        /// Unregister an entity from the manager
        /// </summary>
        public void UnregisterEntity(Entity entity)
        {
            if (entity == null) return;

            if (allEntities.Remove(entity))
            {
                Debug.Log($"[EntityManager] Unregistered entity: {entity.GetEntityName()}");
            }
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

            if (logChange)
            {
                Debug.Log($"[EntityManager] Player entity set: {entity.GetEntityName()}");
            }
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
