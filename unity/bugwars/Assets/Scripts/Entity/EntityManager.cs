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
                    instance = FindObjectOfType<EntityManager>();
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
        }

        /// <summary>
        /// Automatically finds and registers all entities in the scene
        /// </summary>
        public void RegisterAllEntitiesInScene()
        {
            Entity[] foundEntities = FindObjectsOfType<Entity>();
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

                Gizmos.color = entity.IsAlive() ? Color.green : Color.red;
                Gizmos.DrawWireSphere(entity.transform.position, 0.5f);
            }
        }
    }
}
