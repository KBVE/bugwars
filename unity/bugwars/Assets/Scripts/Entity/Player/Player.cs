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
        [SerializeField] [Tooltip("Movement speed (units per second)")]
        private float playerMoveSpeed = 10f;
        [SerializeField] private float jumpForce = 10f;
        [SerializeField] [Tooltip("Rotation speed for A/D keys (degrees per second). This controls how fast the character rotates when pressing A/D.")]
        private float playerRotationSpeed = 85f; // Change this for a/d rotation speed
        
        [Header("Ground Detection")]
        [SerializeField] [Tooltip("Distance to check for ground below player")]
        private float groundCheckDistance = 0.2f;
        [SerializeField] [Tooltip("Layer mask for ground detection")]
        private LayerMask groundLayerMask = -1; // All layers by default
        [SerializeField] [Tooltip("Distance to snap player to ground surface")]
        private float groundSnapDistance = 0.1f;
        [SerializeField] [Tooltip("Maximum slope angle player can walk on (degrees)")]
        private float maxSlopeAngle = 45f;

        private Vector3 moveDirection;
        private float rotationInput;
        private bool isGrounded;
        private bool useStandardWASD = true; // Use standard WASD controls (A/D rotate, W/S move forward/back)
        private RaycastHit groundHit;
        private Vector3 groundNormal = Vector3.up;

        // OPTIMIZED: Reduce ground check frequency
        private int groundCheckFrameCounter = 0;
        private const int groundCheckInterval = 3; // Check every 3 FixedUpdates (reduces from 50Hz to ~16Hz)

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
            // OPTIMIZED: Check ground every N frames instead of every frame
            groundCheckFrameCounter++;
            if (groundCheckFrameCounter >= groundCheckInterval)
            {
                CheckGrounded();
                groundCheckFrameCounter = 0;
            }

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

        /// <summary>
        /// Check if player is grounded using raycast
        /// </summary>
        private void CheckGrounded()
        {
            if (capsuleCollider == null) return;
            
            // Calculate raycast origin (bottom of capsule collider)
            Vector3 rayOrigin = transform.position;
            float capsuleHeight = capsuleCollider.height;
            float capsuleRadius = capsuleCollider.radius;
            rayOrigin.y -= (capsuleHeight * 0.5f - capsuleRadius);
            
            // Raycast down to detect ground
            float rayDistance = groundCheckDistance + capsuleRadius;
            isGrounded = Physics.Raycast(rayOrigin, Vector3.down, out groundHit, rayDistance, groundLayerMask);
            
            if (isGrounded)
            {
                groundNormal = groundHit.normal;
                
                // Check if slope is too steep
                float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
                if (slopeAngle > maxSlopeAngle)
                {
                    isGrounded = false; // Too steep to walk on
                }
            }
            else
            {
                groundNormal = Vector3.up;
            }
        }
        
        /// <summary>
        /// Snap player to ground surface if close enough
        /// </summary>
        private void SnapToGround()
        {
            if (!isGrounded || rb == null || capsuleCollider == null) return;
            
            // Calculate desired Y position (ground hit point + capsule bottom offset)
            float capsuleHeight = capsuleCollider.height;
            float capsuleRadius = capsuleCollider.radius;
            float bottomOffset = capsuleHeight * 0.5f - capsuleRadius;
            float desiredY = groundHit.point.y + bottomOffset;
            
            // Only snap if player is close to ground (not falling/jumping)
            float currentY = transform.position.y;
            float distanceToGround = Mathf.Abs(currentY - desiredY);
            
            if (distanceToGround < groundSnapDistance && rb.linearVelocity.y <= 0.1f)
            {
                // Smoothly snap to ground
                Vector3 newPosition = transform.position;
                newPosition.y = Mathf.Lerp(currentY, desiredY, Time.fixedDeltaTime * 10f);
                transform.position = newPosition;
            }
        }
        
        private void MovePlayer()
        {
            // Use base class Move method for physics-based movement
            // Override moveSpeed to use playerMoveSpeed
            float originalMoveSpeed = moveSpeed;
            moveSpeed = playerMoveSpeed; // Use player's move speed
            
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                // Adjust movement direction based on slope normal (slope-aligned movement)
                Vector3 slopeAlignedDirection = GetSlopeAlignedDirection(moveDirection);
                Move(slopeAlignedDirection);
            }
            else
            {
                // Stop movement when no input
                Move(Vector3.zero);
            }
            
            // Snap to ground after movement
            SnapToGround();
            
            moveSpeed = originalMoveSpeed; // Restore (though it shouldn't matter since we're the only one using it)
        }
        
        /// <summary>
        /// Adjusts movement direction to align with slope surface
        /// Projects movement direction onto slope plane
        /// </summary>
        private Vector3 GetSlopeAlignedDirection(Vector3 inputDirection)
        {
            if (!isGrounded || groundNormal == Vector3.up)
            {
                // Flat ground or not grounded - use input direction as-is
                return inputDirection;
            }
            
            // Project movement direction onto slope plane
            // This allows player to move along slopes naturally
            Vector3 slopeRight = Vector3.Cross(groundNormal, Vector3.up).normalized;
            Vector3 slopeForward = Vector3.Cross(slopeRight, groundNormal).normalized;
            
            // Project input direction onto slope plane
            Vector3 projectedDirection = Vector3.ProjectOnPlane(inputDirection, groundNormal).normalized;
            
            // Maintain input magnitude for consistent speed
            return projectedDirection * inputDirection.magnitude;
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
