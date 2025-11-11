using UnityEngine;

namespace BugWars.Entity.Player
{
    /// <summary>
    /// Player entity class - represents the player-controlled character
    /// Now supports billboard 2D sprites in 3D environment
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
        }

        private void Update()
        {
            HandleInput();
        }

        private void FixedUpdate()
        {
            MovePlayer();
        }

        private void HandleInput()
        {
            // Get input for movement
            float horizontal = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
            float vertical = Input.GetAxisRaw("Vertical");     // W/S or Up/Down

            moveDirection = new Vector3(horizontal, 0, vertical);
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
