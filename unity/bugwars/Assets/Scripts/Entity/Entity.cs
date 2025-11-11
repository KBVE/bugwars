using UnityEngine;

namespace BugWars.Entity
{
    /// <summary>
    /// Base Entity class for all game entities (Players, NPCs, etc.)
    /// Supports billboard 2D sprites in 3D environment
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public abstract class Entity : MonoBehaviour
    {
        [Header("Entity Properties")]
        [SerializeField] protected string entityName;
        [SerializeField] protected float health = 100f;
        [SerializeField] protected float maxHealth = 100f;

        [Header("Billboard Sprite")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected bool enableBillboard = true;
        [SerializeField] protected Vector3 spriteOffset = new Vector3(0, 0.5f, 0);

        [Header("Physics")]
        [SerializeField] protected float moveSpeed = 5f;
        [SerializeField] protected float rotationSpeed = 720f;

        protected bool isAlive = true;
        protected Rigidbody rb;
        protected CapsuleCollider capsuleCollider;
        protected Transform cameraTransform;

        protected virtual void Awake()
        {
            Initialize();
        }

        protected virtual void Start()
        {
            // Cache camera reference for billboarding
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        protected virtual void LateUpdate()
        {
            // Billboard sprite to always face camera
            if (enableBillboard && spriteRenderer != null && cameraTransform != null)
            {
                ApplyBillboard();
            }
        }

        protected virtual void Initialize()
        {
            isAlive = true;
            health = maxHealth;

            // Cache physics components
            rb = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();

            // Configure rigidbody for entity
            if (rb != null)
            {
                rb.freezeRotation = true; // Prevent physics rotation interfering with billboard
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // Auto-find sprite renderer if not assigned
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        /// <summary>
        /// Makes the sprite always face the camera (billboard effect)
        /// </summary>
        protected virtual void ApplyBillboard()
        {
            if (spriteRenderer == null || cameraTransform == null) return;

            // Get direction to camera
            Vector3 directionToCamera = cameraTransform.position - spriteRenderer.transform.position;
            directionToCamera.y = 0; // Keep billboard upright

            // Only rotate if there's a significant direction
            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                spriteRenderer.transform.rotation = targetRotation;
            }
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

        /// <summary>
        /// Move entity using physics
        /// </summary>
        protected virtual void Move(Vector3 direction)
        {
            if (rb == null || !isAlive) return;

            Vector3 movement = direction.normalized * moveSpeed;
            movement.y = rb.linearVelocity.y; // Preserve vertical velocity for gravity
            rb.linearVelocity = movement;
        }

        /// <summary>
        /// Set the sprite for this entity
        /// </summary>
        public virtual void SetSprite(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        // Public getters
        public string GetEntityName() => entityName;
        public float GetHealth() => health;
        public float GetMaxHealth() => maxHealth;
        public bool IsAlive() => isAlive;
        public SpriteRenderer GetSpriteRenderer() => spriteRenderer;
        public Rigidbody GetRigidbody() => rb;
    }
}
