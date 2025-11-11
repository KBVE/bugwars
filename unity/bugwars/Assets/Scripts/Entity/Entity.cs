using UnityEngine;

namespace BugWars.Entity
{
    /// <summary>
    /// Base Entity class for all game entities (Players, NPCs, etc.)
    /// </summary>
    public abstract class Entity : MonoBehaviour
    {
        [Header("Entity Properties")]
        [SerializeField] protected string entityName;
        [SerializeField] protected float health = 100f;
        [SerializeField] protected float maxHealth = 100f;

        protected bool isAlive = true;

        protected virtual void Awake()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            isAlive = true;
            health = maxHealth;
        }

        public virtual void TakeDamage(float damage)
        {
            if (!isAlive) return;

            health -= damage;
            health = Mathf.Max(0, health);

            if (health <= 0)
            {
                Die();
            }
        }

        public virtual void Heal(float amount)
        {
            if (!isAlive) return;

            health += amount;
            health = Mathf.Min(health, maxHealth);
        }

        protected virtual void Die()
        {
            isAlive = false;
            OnDeath();
        }

        protected virtual void OnDeath()
        {
            // Override in derived classes for specific death behavior
        }

        // Public getters
        public string GetEntityName() => entityName;
        public float GetHealth() => health;
        public float GetMaxHealth() => maxHealth;
        public bool IsAlive() => isAlive;
    }
}
