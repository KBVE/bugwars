using UnityEngine;
using BugWars.Entity.Player;
using BugWars.Core;

namespace BugWars.Characters
{
    /// <summary>
    /// Character controller for adventurer characters (Knight, Mage, Rogue, Barbarian, Ranger)
    /// Manages character properties, animation, and 3D model rendering
    /// Extends Player to integrate with the entity system
    /// Implements ICameraPreference for 3D camera configuration
    /// </summary>
    public class AdventurerCharacter : Player, ICameraPreference
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

            // Call base.Update() for any base Player class logic
            base.Update();

            // Update animation based on movement
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

            // Debug logging for animation setup
            if (animator != null)
            {
                Debug.Log($"[AdventurerCharacter] Animator found: {animator.name}");
                Debug.Log($"[AdventurerCharacter] Animator Controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NULL")}");

                // List all available parameters
                if (animator.parameters.Length > 0)
                {
                    string paramList = string.Join(", ", System.Array.ConvertAll(animator.parameters, p => $"{p.name} ({p.type})"));
                    Debug.Log($"[AdventurerCharacter] Animator Parameters: {paramList}");
                }
                else
                {
                    Debug.LogWarning("[AdventurerCharacter] Animator has NO parameters! Animation will not work.");
                }
            }
            else
            {
                Debug.LogError("[AdventurerCharacter] Animator NOT found! Animations will not play.");
            }

            isInitialized = true;
        }
        
        /// <summary>
        /// Update animation state based on movement
        /// </summary>
        private void UpdateAnimation()
        {
            if (animator == null)
            {
                Debug.LogWarning("[AdventurerCharacter] UpdateAnimation() called but animator is null!");
                return;
            }

            // Get velocity from rigidbody (inherited from Entity)
            bool isMoving = rb != null && rb.linearVelocity.magnitude > 0.01f;
            float speed = rb != null ? new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude : 0f;

            // Debug logging every 60 frames (~1 second) to avoid spam
            if (Time.frameCount % 60 == 0)
            {
                Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
                Debug.Log($"[AdventurerCharacter] Animation Update - Velocity: {velocity} | Speed: {speed:F2} | IsMoving: {isMoving} | Animator Params: {animator.parameters.Length}");
            }

            if (animator.parameters.Length > 0)
            {
                // Check if parameters exist before setting
                bool hasIsMoving = System.Array.Exists(animator.parameters, p => p.name == "IsMoving");
                bool hasSpeed = System.Array.Exists(animator.parameters, p => p.name == "Speed");

                if (hasIsMoving)
                {
                    animator.SetBool("IsMoving", isMoving);
                }
                else
                {
                    Debug.LogWarning("[AdventurerCharacter] Animator missing 'IsMoving' parameter!");
                }

                if (hasSpeed)
                {
                    animator.SetFloat("Speed", speed);
                }
                else
                {
                    Debug.LogWarning("[AdventurerCharacter] Animator missing 'Speed' parameter!");
                }
            }
            else
            {
                Debug.LogWarning("[AdventurerCharacter] Animator has no parameters! Cannot control animations.");
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

        #region ICameraPreference Implementation

        /// <summary>
        /// Get preferred camera configuration for 3D adventurer characters
        /// Uses perspective camera with free-look orbit controls
        /// </summary>
        public CameraFollowConfig GetPreferredCameraConfig(Transform target)
        {
            // Use FreeLookOrbit for 3D characters - mouse-driven third-person camera
            // with shoulder offset, collision detection, and smooth follow
            var config = CameraFollowConfig.FreeLookOrbit(target);

            // Optionally customize for adventurer characters
            // Could adjust based on character class (e.g., melee vs ranged)
            return config;
        }

        /// <summary>
        /// Expected camera tag for 3D characters
        /// </summary>
        public string GetExpectedCameraTag()
        {
            return CameraTags.Camera3D;
        }

        /// <summary>
        /// Adventurer characters use 3D models, not billboarding
        /// </summary>
        public bool UsesBillboarding => false;

        #endregion

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