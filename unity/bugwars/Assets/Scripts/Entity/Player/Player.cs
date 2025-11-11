using UnityEngine;

namespace BugWars.Entity.Player
{
    /// <summary>
    /// Player entity class - represents the player-controlled character
    /// </summary>
    public class Player : Entity
    {
        [Header("Player Properties")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 10f;

        private Vector3 moveDirection;
        private bool isGrounded;

        protected override void Awake()
        {
            base.Awake();
            entityName = "Player";
        }

        private void Update()
        {
            HandleInput();
        }

        private void FixedUpdate()
        {
            Move();
        }

        private void HandleInput()
        {
            // Input handling will be implemented here
            // This is a placeholder for player-specific input logic
        }

        private void Move()
        {
            // Movement logic will be implemented here
            // This is a placeholder for player-specific movement
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            // Player-specific death behavior
            // TODO: Trigger game over, respawn, etc.
        }

        // Player-specific methods
        public void SetMoveSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0, speed);
        }

        public float GetMoveSpeed() => moveSpeed;
    }
}
