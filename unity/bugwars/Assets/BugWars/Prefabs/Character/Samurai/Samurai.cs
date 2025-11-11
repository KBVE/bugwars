using System.Collections.Generic;
using UnityEngine;
using BugWars.Entity;

namespace BugWars.Character
{
    /// <summary>
    /// Samurai player character
    /// Extends Entity base class and implements frame-based sprite animation using JSON atlas
    /// Maps universal AnimationState enum to Samurai-specific animations
    /// </summary>
    public class Samurai : Entity.Entity
    {
        #region Animation State Mapping

        /// <summary>
        /// Maps universal EntityAnimationState to Samurai-specific animation names
        /// Hardcoded based on SamuraiAtlas.json structure
        /// </summary>
        private static readonly Dictionary<EntityAnimationState, string> AnimationStateMap = new Dictionary<EntityAnimationState, string>
        {
            { EntityAnimationState.Idle, "Idle" },
            { EntityAnimationState.Walk, "Walk" },
            { EntityAnimationState.Run, "Run" },
            { EntityAnimationState.Jump, "Jump" },
            { EntityAnimationState.Attack_1, "Attack_1" },
            { EntityAnimationState.Attack_2, "Attack_2" },
            { EntityAnimationState.Attack_3, "Attack_3" },
            { EntityAnimationState.Hurt, "Hurt" },
            { EntityAnimationState.Dead, "Dead" },
            { EntityAnimationState.Shield, "Shield" }
        };

        #endregion

        [Header("Samurai Animation")]
        [SerializeField] private TextAsset atlasJSON;
        [SerializeField] private Material spriteMaterial;

        private SpriteAtlasData atlasData;
        private string currentAnimation = "Idle";
        private int currentFrameIndex = 0;
        private float animationTimer = 0f;

        // Material property IDs (cached for performance)
        private static readonly int FrameUVMinID = Shader.PropertyToID("_FrameUVMin");
        private static readonly int FrameUVMaxID = Shader.PropertyToID("_FrameUVMax");

        [Header("Animation State")]
        [SerializeField] private bool debugAnimation = false;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Disable Entity's billboard rotation because the shader handles billboarding
            enableBillboard = false;

            LoadAtlasData();
        }

        protected override void Start()
        {
            base.Start();

            // Convert SpriteRenderer to MeshRenderer + MeshFilter for proper material control
            if (spriteMaterial != null && spriteRenderer != null)
            {
                // Get the GameObject that has the SpriteRenderer
                GameObject spriteObject = spriteRenderer.gameObject;

                // Remove SpriteRenderer (it forces sprite texture usage)
                DestroyImmediate(spriteRenderer);
                spriteRenderer = null;

                // Add MeshFilter and MeshRenderer
                MeshFilter meshFilter = spriteObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = spriteObject.AddComponent<MeshRenderer>();

                // Create a simple quad mesh
                meshFilter.mesh = CreateQuadMesh();

                // Assign material
                meshRenderer.material = spriteMaterial;

                // Store reference for MaterialPropertyBlock updates
                spriteRenderer = null; // We're not using SpriteRenderer anymore

                // Cache the MeshRenderer for later use
                _meshRenderer = meshRenderer;
            }
            else
            {
                if (spriteMaterial == null)
                    Debug.LogError("Samurai: spriteMaterial is not assigned!");
                if (spriteRenderer == null)
                    Debug.LogError("Samurai: spriteRenderer is not assigned!");
            }

            // Start playing idle animation AFTER material is set up
            SetAnimationState(EntityAnimationState.Idle);
        }

        private MeshRenderer _meshRenderer;

        /// <summary>
        /// Override from Entity: Maps universal animation states to Samurai-specific animations
        /// </summary>
        protected override void OnAnimationStateChanged(EntityAnimationState previousState, EntityAnimationState newState)
        {
            // Map universal state to Samurai animation name
            if (AnimationStateMap.TryGetValue(newState, out string animationName))
            {
                PlayAnimation(animationName);
            }
            else
            {
                Debug.LogWarning($"[Samurai] No animation mapping for state: {newState}");
            }
        }

        /// <summary>
        /// Creates a simple quad mesh with UVs from (0,0) to (1,1)
        /// Size is 1x1 unit in world space, centered at origin
        /// </summary>
        private Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "SamuraiQuad";

            // Vertices for a 1x1 quad centered at origin
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-0.5f, -0.5f, 0),  // Bottom-left
                new Vector3( 0.5f, -0.5f, 0),  // Bottom-right
                new Vector3(-0.5f,  0.5f, 0),  // Top-left
                new Vector3( 0.5f,  0.5f, 0)   // Top-right
            };

            // UVs from (0,0) to (1,1) - shader will remap these
            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0, 0),  // Bottom-left
                new Vector2(1, 0),  // Bottom-right
                new Vector2(0, 1),  // Top-left
                new Vector2(1, 1)   // Top-right
            };

            // Two triangles to form the quad
            int[] triangles = new int[6]
            {
                0, 2, 1,  // First triangle (bottom-left, top-left, bottom-right)
                2, 3, 1   // Second triangle (top-left, top-right, bottom-right)
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private void Update()
        {
            UpdateAnimation(Time.deltaTime);
        }

        /// <summary>
        /// Load and parse the JSON atlas data
        /// </summary>
        private void LoadAtlasData()
        {
            if (atlasJSON == null)
            {
                Debug.LogError("Samurai: Atlas JSON not assigned!");
                return;
            }

            try
            {
                atlasData = SpriteAtlasData.FromJson(atlasJSON.text);

                if (atlasData == null)
                {
                    Debug.LogError("Samurai: Failed to parse atlas JSON!");
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Samurai: Error loading atlas JSON: {e.Message}");
            }
        }

        /// <summary>
        /// Play a specific animation by name
        /// </summary>
        public void PlayAnimation(string animationName)
        {
            if (atlasData == null || !atlasData.animations.ContainsKey(animationName))
            {
                Debug.LogWarning($"Samurai: Animation '{animationName}' not found!");
                return;
            }

            // Don't restart the same animation
            if (currentAnimation == animationName)
                return;

            currentAnimation = animationName;
            currentFrameIndex = 0;
            animationTimer = 0f;

            SetFrame(0);
        }

        /// <summary>
        /// Update the current animation
        /// </summary>
        private void UpdateAnimation(float deltaTime)
        {
            if (atlasData == null || !atlasData.animations.ContainsKey(currentAnimation))
                return;

            AnimationData anim = atlasData.animations[currentAnimation];
            float frameDuration = anim.GetFrameDuration();

            animationTimer += deltaTime;

            // Advance to next frame
            if (animationTimer >= frameDuration)
            {
                animationTimer -= frameDuration;
                currentFrameIndex = (currentFrameIndex + 1) % anim.frameCount;
                SetFrame(currentFrameIndex);
            }
        }

        /// <summary>
        /// Set the displayed frame by updating shader UV parameters
        /// </summary>
        private void SetFrame(int frameIndex)
        {
            if (atlasData == null || _meshRenderer == null)
                return;

            if (!atlasData.animations.ContainsKey(currentAnimation))
                return;

            AnimationData anim = atlasData.animations[currentAnimation];

            if (frameIndex < 0 || frameIndex >= anim.frames.Count)
                return;

            string frameName = anim.frames[frameIndex];

            if (!atlasData.frames.ContainsKey(frameName))
            {
                Debug.LogWarning($"Samurai: Frame '{frameName}' not found in atlas!");
                return;
            }

            FrameData frame = atlasData.frames[frameName];

            // Get the shared property block from Entity (includes flip state)
            MaterialPropertyBlock propBlock = GetSpritePropertyBlock();

            // Update shader UV parameters for this animation frame
            propBlock.SetVector(FrameUVMinID, new Vector4(frame.uv.min.x, frame.uv.min.y, 0, 0));
            propBlock.SetVector(FrameUVMaxID, new Vector4(frame.uv.max.x, frame.uv.max.y, 0, 0));

            // Apply to mesh renderer instead of sprite renderer
            _meshRenderer.SetPropertyBlock(propBlock);

            if (debugAnimation)
            {
                Debug.Log($"Samurai: Frame {frameIndex} - {frameName} - UV({frame.uv.min.x:F3},{frame.uv.min.y:F3}) to ({frame.uv.max.x:F3},{frame.uv.max.y:F3})");
            }
        }

        /// <summary>
        /// Get current animation name
        /// </summary>
        public string GetCurrentAnimation() => currentAnimation;

        /// <summary>
        /// Check if a specific animation exists
        /// </summary>
        public bool HasAnimation(string animationName)
        {
            return atlasData != null && atlasData.animations.ContainsKey(animationName);
        }

        /// <summary>
        /// Get list of all available animations
        /// </summary>
        public List<string> GetAvailableAnimations()
        {
            if (atlasData == null)
                return new List<string>();

            return new List<string>(atlasData.animations.Keys);
        }

        #region Player Input (Example - replace with your input system)

        /// <summary>
        /// Example input handling - replace with your actual input system
        /// Now uses universal EntityAnimationState enum instead of direct animation names
        /// </summary>
        private void HandleInput()
        {
            // This is just an example of how to trigger animations
            // You should integrate with your actual input/control system

            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 movement = new Vector3(horizontal, 0, vertical);

            if (movement.magnitude > 0.1f)
            {
                // Moving
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    SetAnimationState(EntityAnimationState.Run);
                }
                else
                {
                    SetAnimationState(EntityAnimationState.Walk);
                }

                Move(movement);
            }
            else
            {
                // Idle
                SetAnimationState(EntityAnimationState.Idle);
            }

            // Attack
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                SetAnimationState(EntityAnimationState.Attack_1);
            }

            // Jump
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SetAnimationState(EntityAnimationState.Jump);
            }

            // Shield
            if (Input.GetKey(KeyCode.Mouse1))
            {
                SetAnimationState(EntityAnimationState.Shield);
            }
        }

        #endregion

        #region Debug

        /// <summary>
        /// Debug: Cycle through animations
        /// </summary>
        [ContextMenu("Debug: Next Animation")]
        private void DebugNextAnimation()
        {
            if (atlasData == null) return;

            var animList = new List<string>(atlasData.animations.Keys);
            int currentIndex = animList.IndexOf(currentAnimation);
            int nextIndex = (currentIndex + 1) % animList.Count;

            PlayAnimation(animList[nextIndex]);
        }

        /// <summary>
        /// Debug: Print atlas info
        /// </summary>
        [ContextMenu("Debug: Print Atlas Info")]
        private void DebugPrintAtlasInfo()
        {
            if (atlasData == null)
            {
                Debug.Log("Atlas data not loaded");
                return;
            }

            Debug.Log($"=== Samurai Atlas Info ===");
            Debug.Log($"Version: {atlasData.meta.version}");
            Debug.Log($"Size: {atlasData.meta.size.w}x{atlasData.meta.size.h}");
            Debug.Log($"Total Frames: {atlasData.meta.frameCount}");
            Debug.Log($"\nAnimations:");

            foreach (var anim in atlasData.animations)
            {
                Debug.Log($"  {anim.Key}: {anim.Value.frameCount} frames @ {anim.Value.fps} fps");
            }
        }

        #endregion
    }
}
