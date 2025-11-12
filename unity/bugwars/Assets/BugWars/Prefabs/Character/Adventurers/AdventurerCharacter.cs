using UnityEngine;

namespace BugWars.Characters
{
    /// <summary>
    /// Character controller for adventurer characters (Knight, Mage, Rogue, Barbarian, Ranger)
    /// Manages character properties, animation, and basic movement
    /// </summary>
    public class AdventurerCharacter : MonoBehaviour
    {
        [Header("Character Properties")]
        [SerializeField] private string characterClass = "Knight";
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        
        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Animation Rig References")]
        [SerializeField] private GameObject generalRigReference;          // Rig_Medium_General
        [SerializeField] private GameObject movementBasicRigReference;    // Rig_Medium_MovementBasic
        [SerializeField] private GameObject movementAdvancedRigReference; // Rig_Medium_MovementAdvanced
        [SerializeField] private GameObject combatMeleeRigReference;      // Rig_Medium_CombatMelee
        [SerializeField] private GameObject combatRangedRigReference;     // Rig_Medium_CombatRanged
        [SerializeField] private GameObject simulationRigReference;       // Rig_Medium_Simulation
        [SerializeField] private GameObject specialRigReference;          // Rig_Medium_Special
        
        [Header("Rendering")]
        [SerializeField] private SkinnedMeshRenderer meshRenderer;
        [SerializeField] private Material characterMaterial;
        
        private Vector3 moveDirection;
        private bool isInitialized = false;
        
        void Awake()
        {
            InitializeComponents();
        }
        
        void Start()
        {
            // Apply the character material if set
            if (characterMaterial != null && meshRenderer != null)
            {
                meshRenderer.material = characterMaterial;
            }
        }
        
        void Update()
        {
            if (!isInitialized) return;
            
            // Basic update logic - can be expanded for movement
            UpdateAnimation();
        }
        
        /// <summary>
        /// Initialize all required components
        /// </summary>
        private void InitializeComponents()
        {
            // Get or add Animator
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            
            // Get SkinnedMeshRenderer
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }
            
            isInitialized = true;
        }
        
        /// <summary>
        /// Update animation state based on movement
        /// </summary>
        private void UpdateAnimation()
        {
            if (animator == null) return;
            
            // Set animator parameters if available
            bool isMoving = moveDirection.magnitude > 0.01f;
            if (animator.parameters.Length > 0)
            {
                // Common animator parameters
                animator.SetBool("IsMoving", isMoving);
                animator.SetFloat("Speed", moveDirection.magnitude);
            }
        }
        
        /// <summary>
        /// Set the movement direction for the character
        /// </summary>
        public void SetMoveDirection(Vector3 direction)
        {
            moveDirection = direction;
        }
        
        /// <summary>
        /// Get the current character class
        /// </summary>
        public string GetCharacterClass()
        {
            return characterClass;
        }
        
        /// <summary>
        /// Set the character material
        /// </summary>
        public void SetMaterial(Material material)
        {
            characterMaterial = material;
            if (meshRenderer != null)
            {
                meshRenderer.material = material;
            }
        }
        
        /// <summary>
        /// Set the animator controller
        /// </summary>
        public void SetAnimator(RuntimeAnimatorController controller)
        {
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
            }
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Editor helper to visualize character in scene
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            if (moveDirection.magnitude > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position, moveDirection * 2f);
            }
        }
        #endif
    }
}