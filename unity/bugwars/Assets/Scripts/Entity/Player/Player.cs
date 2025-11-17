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
        [SerializeField] [Tooltip("Rotation speed for A/D keys (degrees per second). This controls how fast the character rotates when pressing A/D.")]
        private float playerRotationSpeed = 200f; // Change this for a/d rotation speed

        private Vector3 moveDirection;
        private float rotationInput;
        private bool isGrounded;
        private bool useStandardWASD = true; // Use standard WASD controls (A/D rotate, W/S move forward/back)

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

            // Subscribe to movement and rotation input events
            EventManager eventManager = FindFirstObjectByType<EventManager>();
            if (eventManager != null)
            {
                eventManager.OnPlayerMovementInput.AddListener(OnMovementInput);
                eventManager.OnPlayerRotationInput.AddListener(OnRotationInput);
            }
            else
            {
                Debug.LogWarning("[Player] EventManager not found! Movement input will not work.");
            }
        }

        protected virtual void Update()
        {
            // Handle rotation input (A/D keys)
            if (useStandardWASD && Mathf.Abs(rotationInput) > 0.01f)
            {
                RotatePlayer(rotationInput);
            }
        }

        private void FixedUpdate()
        {
            MovePlayer();
        }

        /// <summary>
        /// Event handler for movement input from InputManager
        /// Standard WASD: input.y is forward/backward (W/S), relative to character facing
        /// </summary>
        private void OnMovementInput(Vector2 input)
        {
            if (useStandardWASD)
            {
                // Standard WASD: W/S moves forward/backward relative to character facing
                // input.y: +1 = forward (W), -1 = backward (S)
                // Movement is relative to character's forward direction
                moveDirection = transform.forward * input.y;
            }
            else
            {
                // Legacy billboarding: A/D strafe left/right, W/S move forward/back
                moveDirection = new Vector3(input.x, 0, input.y);
            }
        }

        /// <summary>
        /// Event handler for rotation input from InputManager
        /// Standard WASD: A/D rotates character left/right
        /// </summary>
        private void OnRotationInput(float rotation)
        {
            rotationInput = rotation; // Store for Update() to process
        }

        /// <summary>
        /// Rotate the player character (A/D keys)
        /// </summary>
        private void RotatePlayer(float rotationAmount)
        {
            if (rb == null) return;

            // Rotate around Y-axis (yaw)
            // Use playerRotationSpeed (not Entity's rotationSpeed) for A/D key rotation
            float rotationDelta = rotationAmount * playerRotationSpeed * Time.deltaTime;
            transform.Rotate(0f, rotationDelta, 0f, Space.Self);
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
            // Unsubscribe from movement and rotation input events
            EventManager eventManager = FindFirstObjectByType<EventManager>();
            if (eventManager != null)
            {
                eventManager.OnPlayerMovementInput.RemoveListener(OnMovementInput);
                eventManager.OnPlayerRotationInput.RemoveListener(OnRotationInput);
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

        /// <summary>
        /// Check if player is using standard WASD controls
        /// </summary>
        public bool IsUsingStandardWASD()
        {
            return useStandardWASD;
        }

        /// <summary>
        /// Get the player's rotation speed (for camera sync)
        /// </summary>
        public float GetRotationSpeed()
        {
            return playerRotationSpeed;
        }
    }
}
