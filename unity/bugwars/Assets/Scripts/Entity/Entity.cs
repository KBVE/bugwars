using UnityEngine;
using BugWars.Core;

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
        [SerializeField] protected bool autoRegisterWithManager = true;

        [Header("Billboard Sprite")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected bool enableBillboard = true;
        [SerializeField] protected Vector3 spriteOffset = new Vector3(0, 0.5f, 0);
        [SerializeField] protected bool autoFlipSprite = true; // Automatically flip sprite based on movement

        [Header("Physics")]
        [SerializeField] protected float moveSpeed = 5f;
        [SerializeField] protected float rotationSpeed = 720f;

        protected bool isAlive = true;
        protected Rigidbody rb;
        protected CapsuleCollider capsuleCollider;
        protected Transform cameraTransform;

        // Sprite flipping
        protected int facingDirection = 1; // 1 = right, -1 = left
        protected MaterialPropertyBlock spritePropertyBlock;
        private static readonly int FlipXID = Shader.PropertyToID("_FlipX");
        private static readonly int FlipYID = Shader.PropertyToID("_FlipY");

        protected virtual void Awake()
        {
            Initialize();
        }

        protected virtual void Start()
        {
            // Cache camera reference for billboarding using CameraManager
            if (CameraManager.Instance != null && CameraManager.Instance.MainCamera != null)
            {
                cameraTransform = CameraManager.Instance.MainCamera.transform;

                if (enableBillboard)
                {
                    Debug.Log($"[Entity] {entityName}: Billboard enabled, using camera: {CameraManager.Instance.MainCamera.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[Entity] {entityName}: CameraManager or MainCamera not found! Billboard effect will not work.");
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

            // Initialize material property block for sprite effects
            spritePropertyBlock = new MaterialPropertyBlock();
            InitializeSpriteFlip();

            // Auto-register with EntityManager
            if (autoRegisterWithManager)
            {
                RegisterWithEntityManager();
            }
        }

        /// <summary>
        /// Makes the sprite always face the camera (billboard effect)
        /// </summary>
        protected virtual void ApplyBillboard()
        {
            if (spriteRenderer == null)
            {
                Debug.LogWarning($"[Entity] {entityName}: ApplyBillboard called but spriteRenderer is null");
                return;
            }

            if (cameraTransform == null)
            {
                // Try to re-acquire camera transform from CameraManager
                if (CameraManager.Instance != null && CameraManager.Instance.MainCamera != null)
                {
                    cameraTransform = CameraManager.Instance.MainCamera.transform;
                    Debug.Log($"[Entity] {entityName}: Re-acquired camera transform from CameraManager");
                }
                else
                {
                    return;
                }
            }

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

        protected virtual void OnDestroy()
        {
            // Unregister from EntityManager when destroyed
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.UnregisterEntity(this);
            }
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

            // Auto-flip sprite based on movement direction
            if (autoFlipSprite && direction.magnitude > 0.1f)
            {
                UpdateFacingDirection(direction);
            }
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
        public int GetFacingDirection() => facingDirection;

        #region Sprite Flipping

        /// <summary>
        /// Initialize sprite flip state
        /// </summary>
        protected virtual void InitializeSpriteFlip()
        {
            SetSpriteFlip(false, false); // Start facing right, not flipped
        }

        /// <summary>
        /// Update facing direction based on movement
        /// </summary>
        protected virtual void UpdateFacingDirection(Vector3 movementDirection)
        {
            if (movementDirection.magnitude < 0.1f) return;

            // Determine facing direction based on horizontal movement
            // Use X and Z for 3D movement (ignore Y)
            Vector3 horizontal = new Vector3(movementDirection.x, 0, movementDirection.z);

            if (horizontal.magnitude > 0.1f)
            {
                // Get camera-relative direction for proper flipping
                Transform activeCameraTransform = cameraTransform;
                if (activeCameraTransform == null && CameraManager.Instance != null && CameraManager.Instance.MainCamera != null)
                {
                    activeCameraTransform = CameraManager.Instance.MainCamera.transform;
                }

                if (activeCameraTransform != null)
                {
                    // Project movement onto camera's right vector to determine left/right
                    Vector3 cameraRight = activeCameraTransform.right;
                    cameraRight.y = 0;
                    cameraRight.Normalize();

                    float dotRight = Vector3.Dot(horizontal.normalized, cameraRight);

                    // Flip if moving left (negative dot product with camera right)
                    if (dotRight < -0.1f && facingDirection != -1)
                    {
                        facingDirection = -1;
                        SetSpriteFlip(true, false);
                    }
                    else if (dotRight > 0.1f && facingDirection != 1)
                    {
                        facingDirection = 1;
                        SetSpriteFlip(false, false);
                    }
                }
                else
                {
                    // Fallback: use world-space X axis if no camera
                    if (horizontal.x < -0.1f && facingDirection != -1)
                    {
                        facingDirection = -1;
                        SetSpriteFlip(true, false);
                    }
                    else if (horizontal.x > 0.1f && facingDirection != 1)
                    {
                        facingDirection = 1;
                        SetSpriteFlip(false, false);
                    }
                }
            }
        }

        /// <summary>
        /// Set sprite flip state (updates shader parameters)
        /// </summary>
        public virtual void SetSpriteFlip(bool flipX, bool flipY)
        {
            if (spriteRenderer == null || spritePropertyBlock == null) return;

            // Get existing properties if any
            spriteRenderer.GetPropertyBlock(spritePropertyBlock);

            // Set flip parameters
            spritePropertyBlock.SetFloat(FlipXID, flipX ? 1f : 0f);
            spritePropertyBlock.SetFloat(FlipYID, flipY ? 1f : 0f);

            // Apply to sprite renderer
            spriteRenderer.SetPropertyBlock(spritePropertyBlock);
        }

        /// <summary>
        /// Manually set facing direction
        /// </summary>
        public virtual void SetFacingDirection(int direction)
        {
            if (direction == 0) return;

            facingDirection = direction > 0 ? 1 : -1;
            SetSpriteFlip(facingDirection < 0, false);
        }

        /// <summary>
        /// Get the material property block (for derived classes to add properties)
        /// </summary>
        protected MaterialPropertyBlock GetSpritePropertyBlock()
        {
            if (spritePropertyBlock == null)
            {
                spritePropertyBlock = new MaterialPropertyBlock();
            }

            // Always get current state from renderer
            if (spriteRenderer != null)
            {
                spriteRenderer.GetPropertyBlock(spritePropertyBlock);
            }

            return spritePropertyBlock;
        }

        /// <summary>
        /// Apply property block changes to the sprite renderer
        /// </summary>
        protected void ApplySpritePropertyBlock()
        {
            if (spriteRenderer != null && spritePropertyBlock != null)
            {
                spriteRenderer.SetPropertyBlock(spritePropertyBlock);
            }
        }

        #endregion

        #region EntityManager Integration

        /// <summary>
        /// Register this entity with the EntityManager
        /// </summary>
        protected virtual void RegisterWithEntityManager()
        {
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.RegisterEntity(this);
            }
            else
            {
                Debug.LogWarning($"[Entity] {entityName}: EntityManager instance not found. Entity not registered.");
            }
        }

        #endregion
    }
}
