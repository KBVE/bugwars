using UnityEngine;
using BugWars.Entity.Player;

namespace BugWars.Characters
{
    /// <summary>
    /// Character controller for adventurer characters (Knight, Mage, Rogue, Barbarian, Ranger)
    /// Manages character properties, animation, and 3D model rendering
    /// Extends Player to integrate with the entity system
    /// </summary>
    public class AdventurerCharacter : Player
    {
        [Header("Adventurer Properties")]
        [SerializeField] private string characterClass = "Knight";

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

        private bool isInitialized = false;

        protected override void Awake()
        {
            // Disable billboarding since we're using 3D models, not 2D sprites
            enableBillboard = false;

            base.Awake(); // Call Entity/Player initialization
            InitializeComponents();
            entityName = characterClass; // Set entity name to character class
        }

        protected override void Start()
        {
            base.Start(); // Call Player/Entity start (handles EntityManager registration)

            // Apply the character material to all mesh renderers if set
            if (characterMaterial != null)
            {
                ApplyMaterialToAllMeshes();
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

            // Get velocity from rigidbody (inherited from Entity)
            bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.01f;
            float speed = rb != null ? new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude : 0f;

            if (animator.parameters.Length > 0)
            {
                // Common animator parameters
                animator.SetBool("IsMoving", isMoving);
                animator.SetFloat("Speed", speed);
            }
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
            ApplyMaterialToAllMeshes();
        }

        /// <summary>
        /// Apply the character material to all SkinnedMeshRenderers in the hierarchy
        /// </summary>
        private void ApplyMaterialToAllMeshes()
        {
            if (characterMaterial == null) return;

            // Get all SkinnedMeshRenderers in children (this includes the character body parts)
            SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                renderer.material = characterMaterial;
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

            // Visualize movement direction from rigidbody velocity
            if (rb != null && rb.linearVelocity.magnitude > 0.01f)
            {
                Gizmos.color = Color.yellow;
                Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                Gizmos.DrawRay(transform.position, horizontalVelocity.normalized * 2f);
            }
        }
        #endif
    }
}