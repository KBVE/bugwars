using UnityEngine;
using BugWars.Core;

namespace BugWars.Entity.Player
{
    /// <summary>
    /// Player entity class - represents the player-controlled character
    /// Now supports billboard 2D sprites in 3D environment
    /// Receives movement input from InputManager via EventManager
    /// </summary>
    public class Player : Entity
    {
        [Header("Player Properties")]
        [SerializeField] private float jumpForce = 10f;

        private Vector3 moveDirection;
        private bool isGrounded;

        protected override void Awake()
        {
            base.Awake();
            entityName = "Player";
        }

        protected override void Start()
        {
            base.Start();

            // Register with EntityManager
            EntityManager.Instance.RegisterEntity(this);

            // Subscribe to movement input events
            EventManager eventManager = FindFirstObjectByType<EventManager>();
            if (eventManager != null)
            {
                eventManager.OnPlayerMovementInput.AddListener(OnMovementInput);
            }
            else
            {
                Debug.LogWarning("[Player] EventManager not found! Movement input will not work.");
            }
        }

        protected virtual void Update()
        {
            // Movement is now handled via event subscription
            // Derived classes can still override this for additional logic
        }

        private void FixedUpdate()
        {
            MovePlayer();
        }

        /// <summary>
        /// Event handler for movement input from InputManager
        /// </summary>
        private void OnMovementInput(Vector2 input)
        {
            // Convert 2D input (x, y) to 3D movement direction (x, 0, y)
            moveDirection = new Vector3(input.x, 0, input.y);
        }

        private void MovePlayer()
        {
            // Use base class Move method for physics-based movement
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                Move(moveDirection);
            }
            else
            {
                // Stop movement when no input
                Move(Vector3.zero);
            }
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            // Player-specific death behavior
            Debug.Log("[Player] Player has died!");
            // TODO: Trigger game over, respawn, etc.
        }

        private void OnDestroy()
        {
            // Unsubscribe from movement input events
            EventManager eventManager = FindFirstObjectByType<EventManager>();
            if (eventManager != null)
            {
                eventManager.OnPlayerMovementInput.RemoveListener(OnMovementInput);
            }

            // Unregister from EntityManager when destroyed
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.UnregisterEntity(this);
            }
        }

        // Player-specific methods
        public void Jump()
        {
            if (isGrounded && rb != null)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }
    }
}
