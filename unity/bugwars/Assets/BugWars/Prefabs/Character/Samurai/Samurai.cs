using System.Collections.Generic;
using UnityEngine;
using BugWars.Entity;

namespace BugWars.Character
{
    /// <summary>
    /// Samurai player character
    /// Extends Entity base class and implements frame-based sprite animation using JSON atlas
    /// </summary>
    public class Samurai : Entity.Entity
    {
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

            LoadAtlasData();
            PlayAnimation("Idle");
        }

        protected override void Start()
        {
            base.Start();

            // Ensure we have a material instance
            if (spriteMaterial != null && spriteRenderer != null)
            {
                spriteRenderer.material = spriteMaterial;
            }
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

                Debug.Log($"Samurai: Loaded atlas with {atlasData.animations.Count} animations");
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

            if (debugAnimation)
            {
                Debug.Log($"Samurai: Playing animation '{animationName}'");
            }
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
            if (atlasData == null || spriteRenderer == null)
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

            // Apply all properties (flip + UV) to sprite renderer
            ApplySpritePropertyBlock();

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
                    PlayAnimation("Run");
                }
                else
                {
                    PlayAnimation("Walk");
                }

                Move(movement);
            }
            else
            {
                // Idle
                PlayAnimation("Idle");
            }

            // Attack
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                PlayAnimation("Attack_1");
            }

            // Jump
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PlayAnimation("Jump");
            }

            // Shield
            if (Input.GetKey(KeyCode.Mouse1))
            {
                PlayAnimation("Shield");
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
