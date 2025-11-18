using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using BugWars.Entity.Player;
using BugWars.Entity;
using BugWars.Core;

namespace BugWars.Character
{
    /// <summary>
    /// BlankPlayer character - 4-directional player character
    /// Extends Player base class and implements frame-based sprite animation using JSON atlas
    /// Supports 4 directions: Down, Left, Right, Up
    /// Implements ICameraPreference for billboard 2D sprite camera configuration
    /// </summary>
    public class BlankPlayer : Player, ICameraPreference
    {
        #region Animation State Mapping

        /// <summary>
        /// Maps universal EntityAnimationState to BlankPlayer-specific animation base names
        /// Direction suffix (Down, Left, Right, Up) will be appended dynamically
        /// </summary>
        private static readonly Dictionary<EntityAnimationState, string> AnimationStateMap = new Dictionary<EntityAnimationState, string>
        {
            { EntityAnimationState.Idle, "Idle" },
            { EntityAnimationState.Walk, "Walk" },
            { EntityAnimationState.Run, "Run" },
            { EntityAnimationState.Attack_1, "Attack" },
            { EntityAnimationState.Hurt, "Hurt" },
            { EntityAnimationState.Dead, "Death" }
        };

        #endregion

        #region Direction Support

        /// <summary>
        /// Current facing direction for 4-directional sprite selection
        /// </summary>
        public enum Direction
        {
            Down = 0,
            Left = 1,
            Right = 2,
            Up = 3
        }

        private Direction currentDirection = Direction.Down;

        #endregion

        [Header("BlankPlayer Animation")]
        [SerializeField] private TextAsset atlasJSON;
        [SerializeField] private Material spriteMaterial;

        [Header("Movement Smoothing")]
        [SerializeField] private float movementSmoothTime = 0.1f;

        [Header("Direction Control")]
        [SerializeField] private bool autoUpdateDirection = true;
        [SerializeField] private float directionChangeThreshold = 0.1f;

        private SpriteAtlasData atlasData;
        private Vector3 currentVelocity = Vector3.zero;
        private Vector3 smoothedMovement = Vector3.zero;
        private string currentAnimation = "Idle_Down";
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

                // Cache the MeshRenderer for later use
                _meshRenderer = meshRenderer;
            }
            else
            {
                if (spriteMaterial == null)
                    Debug.LogError("BlankPlayer: spriteMaterial is not assigned!");
                if (spriteRenderer == null)
                    Debug.LogError("BlankPlayer: spriteRenderer is not assigned!");
            }

            // Setup camera to follow this player
            SetupCameraFollow();

            // Start playing idle animation AFTER material is set up
            SetAnimationState(EntityAnimationState.Idle);
        }

        /// <summary>
        /// Configures camera to follow the player with optimal settings for 2D billboard sprites
        /// </summary>
        private void SetupCameraFollow()
        {
            StartCoroutine(SetupCameraFollowCoroutine());
        }

        private System.Collections.IEnumerator SetupCameraFollowCoroutine()
        {
            yield return new WaitForEndOfFrame();

            if (EntityManager.Instance == null)
            {
                Debug.LogError("[BlankPlayer] Cannot setup camera - EntityManager not found");
                yield break;
            }

            var eventManagerField = typeof(EntityManager).GetField("_eventManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (eventManagerField == null)
            {
                Debug.LogError("[BlankPlayer] Cannot access EventManager from EntityManager");
                yield break;
            }

            var eventManager = eventManagerField.GetValue(EntityManager.Instance) as Core.EventManager;

            if (eventManager == null)
            {
                Debug.LogError("[BlankPlayer] EventManager is null");
                yield break;
            }

            var config = Core.CameraFollowConfig.SimpleThirdPerson(
                target: transform,
                cameraName: "VirtualCamera",
                immediate: false
            );

            config.cameraDistance = 7f;
            config.shoulderOffset = new Vector3(0, 1.2f, 0);

            eventManager.RequestCameraFollow(config);

            Debug.Log("[BlankPlayer] Camera follow configured");
        }

        private MeshRenderer _meshRenderer;

        /// <summary>
        /// Override from Entity: Maps universal animation states to BlankPlayer-specific animations
        /// Automatically appends current direction to animation name
        /// </summary>
        protected override void OnAnimationStateChanged(EntityAnimationState previousState, EntityAnimationState newState)
        {
            if (AnimationStateMap.TryGetValue(newState, out string baseAnimationName))
            {
                // Append direction to animation name: "Idle" + "_Down" = "Idle_Down"
                string directionSuffix = currentDirection.ToString();
                string fullAnimationName = $"{baseAnimationName}_{directionSuffix}";
                PlayAnimation(fullAnimationName);
            }
            else
            {
                Debug.LogWarning($"[BlankPlayer] No animation mapping for state: {newState}");
            }
        }

        /// <summary>
        /// Creates a simple quad mesh with UVs from (0,0) to (1,1)
        /// </summary>
        private Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "BlankPlayerQuad";

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0)
            };

            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            int[] triangles = new int[6]
            {
                0, 2, 1,
                2, 3, 1
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
            GatherInput();
        }

        private void FixedUpdate()
        {
            ApplyMovement();
        }

        /// <summary>
        /// Load and parse the JSON atlas data
        /// </summary>
        private void LoadAtlasData()
        {
            if (atlasJSON == null)
            {
                Debug.LogError("BlankPlayer: Atlas JSON not assigned!");
                return;
            }

            try
            {
                atlasData = SpriteAtlasData.FromJson(atlasJSON.text);

                if (atlasData == null)
                {
                    Debug.LogError("BlankPlayer: Failed to parse atlas JSON!");
                    return;
                }

                if (atlasData.frames == null)
                {
                    atlasData.frames = new Dictionary<string, FrameData>();
                    Debug.LogError("BlankPlayer: Atlas JSON missing 'frames' data.");
                }

                if (atlasData.animations == null)
                {
                    atlasData.animations = new Dictionary<string, AnimationData>();
                    Debug.LogError("BlankPlayer: Atlas JSON missing 'animations' data.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlankPlayer: Error loading atlas JSON: {e.Message}");
            }
        }

        private bool TryGetAnimation(string animationName, out AnimationData animation)
        {
            animation = null;

            if (string.IsNullOrEmpty(animationName))
                return false;

            if (atlasData == null || atlasData.animations == null)
                return false;

            return atlasData.animations.TryGetValue(animationName, out animation);
        }

        /// <summary>
        /// Play a specific animation by name
        /// </summary>
        public void PlayAnimation(string animationName)
        {
            if (!TryGetAnimation(animationName, out var anim))
            {
                Debug.LogWarning($"BlankPlayer: Animation '{animationName}' not found!");
                return;
            }

            if (currentAnimation == animationName)
                return;

            currentAnimation = animationName;
            currentFrameIndex = 0;
            animationTimer = 0f;

            SetFrame(0, anim);
        }

        /// <summary>
        /// Update the current animation
        /// </summary>
        private void UpdateAnimation(float deltaTime)
        {
            if (!TryGetAnimation(currentAnimation, out var anim))
                return;

            float frameDuration = anim.GetFrameDuration();

            animationTimer += deltaTime;

            if (animationTimer >= frameDuration)
            {
                animationTimer -= frameDuration;
                currentFrameIndex = (currentFrameIndex + 1) % anim.frameCount;
                SetFrame(currentFrameIndex, anim);
            }
        }

        /// <summary>
        /// Set the displayed frame by updating shader UV parameters
        /// </summary>
        private void SetFrame(int frameIndex, AnimationData animation = null)
        {
            if (atlasData == null || _meshRenderer == null)
                return;

            AnimationData anim = animation;
            if (anim == null && !TryGetAnimation(currentAnimation, out anim))
                return;

            if (frameIndex < 0 || frameIndex >= anim.frames.Count)
                return;

            string frameName = anim.frames[frameIndex];

            if (atlasData.frames == null || !atlasData.frames.TryGetValue(frameName, out var frame))
            {
                Debug.LogWarning($"BlankPlayer: Frame '{frameName}' not found in atlas!");
                return;
            }

            MaterialPropertyBlock propBlock = GetSpritePropertyBlock();

            propBlock.SetVector(FrameUVMinID, new Vector4(frame.uv.min.x, frame.uv.min.y, 0, 0));
            propBlock.SetVector(FrameUVMaxID, new Vector4(frame.uv.max.x, frame.uv.max.y, 0, 0));

            _meshRenderer.SetPropertyBlock(propBlock);

            if (debugAnimation)
            {
                Debug.Log($"BlankPlayer: Frame {frameIndex} - {frameName} - UV({frame.uv.min.x:F3},{frame.uv.min.y:F3}) to ({frame.uv.max.x:F3},{frame.uv.max.y:F3})");
            }
        }

        /// <summary>
        /// Override SetSpriteFlip to work with MeshRenderer
        /// </summary>
        public override void SetSpriteFlip(bool flipX, bool flipY)
        {
            if (_meshRenderer == null || spritePropertyBlock == null) return;

            _meshRenderer.GetPropertyBlock(spritePropertyBlock);
            spritePropertyBlock.SetFloat(FlipXID, flipX ? 1f : 0f);
            spritePropertyBlock.SetFloat(FlipYID, flipY ? 1f : 0f);
            _meshRenderer.SetPropertyBlock(spritePropertyBlock);
        }

        /// <summary>
        /// Update facing direction based on movement input
        /// Uses hysteresis to prevent flickering between directions
        /// </summary>
        private void UpdateDirection(Vector3 movement)
        {
            if (!autoUpdateDirection || movement.magnitude < directionChangeThreshold)
                return;

            Direction newDirection = currentDirection;

            // Determine direction based on largest component
            // Add hysteresis (10%) to prevent flickering between directions
            float absX = Mathf.Abs(movement.x);
            float absZ = Mathf.Abs(movement.z);

            // Require a 10% difference to change from current axis
            float hysteresis = 1.1f;

            if (currentDirection == Direction.Left || currentDirection == Direction.Right)
            {
                // Currently moving horizontally, require stronger vertical input to switch
                if (absZ > absX * hysteresis)
                {
                    newDirection = movement.z > 0 ? Direction.Up : Direction.Down;
                }
                else if (absX > directionChangeThreshold)
                {
                    newDirection = movement.x > 0 ? Direction.Right : Direction.Left;
                }
            }
            else
            {
                // Currently moving vertically, require stronger horizontal input to switch
                if (absX > absZ * hysteresis)
                {
                    newDirection = movement.x > 0 ? Direction.Right : Direction.Left;
                }
                else if (absZ > directionChangeThreshold)
                {
                    newDirection = movement.z > 0 ? Direction.Up : Direction.Down;
                }
            }

            // If direction changed, refresh the animation
            if (newDirection != currentDirection)
            {
                currentDirection = newDirection;
                // Re-trigger current animation state with new direction
                SetAnimationState(GetAnimationState());
            }
        }

        public string GetCurrentAnimation() => currentAnimation;
        public Direction GetCurrentDirection() => currentDirection;

        #region Player Input

        private Vector3 inputDirection;
        private bool isRunning;

        private void GatherInput()
        {
            if (Keyboard.current == null || Mouse.current == null)
                return;

            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed) horizontal += 1f;
            if (Keyboard.current.wKey.isPressed) vertical += 1f;
            if (Keyboard.current.sKey.isPressed) vertical -= 1f;

            inputDirection = new Vector3(horizontal, 0, vertical);

            if (inputDirection.magnitude > 1f)
                inputDirection = inputDirection.normalized;

            isRunning = Keyboard.current.leftShiftKey.isPressed;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetAnimationState(EntityAnimationState.Attack_1);
            }
        }

        private void ApplyMovement()
        {
            smoothedMovement = Vector3.SmoothDamp(smoothedMovement, inputDirection, ref currentVelocity, movementSmoothTime);

            if (smoothedMovement.magnitude > 0.01f)
            {
                // Use raw input for immediate direction updates, smoothed for actual movement
                UpdateDirection(inputDirection);

                if (isRunning)
                {
                    SetAnimationState(EntityAnimationState.Run);
                }
                else
                {
                    SetAnimationState(EntityAnimationState.Walk);
                }

                Move(smoothedMovement);
            }
            else
            {
                SetAnimationState(EntityAnimationState.Idle);
                smoothedMovement = Vector3.zero;
                currentVelocity = Vector3.zero;

                if (rb != null)
                {
                    Vector3 currentRbVelocity = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(0, currentRbVelocity.y, 0);
                }
            }
        }

        #endregion

        #region ICameraPreference Implementation

        /// <summary>
        /// Get preferred camera configuration for billboard 2D sprite characters
        /// Uses cinematic follow with fixed viewing angle for optimal 4-directional sprite visibility
        /// </summary>
        public CameraFollowConfig GetPreferredCameraConfig(Transform target)
        {
            // Use SimpleThirdPerson for 2D billboard sprites
            // This provides smooth auto-follow with fixed viewing angle
            var config = CameraFollowConfig.SimpleThirdPerson(target);

            // Customize for 4-directional sprites
            config.cameraDistance = 7f;
            config.shoulderOffset = new Vector3(0, 1.2f, 0);

            return config;
        }

        /// <summary>
        /// Expected camera tag for billboard 2D sprite characters
        /// </summary>
        public string GetExpectedCameraTag()
        {
            return CameraTags.CameraBillboard;
        }

        /// <summary>
        /// BlankPlayer uses billboard 2D sprites with 4-directional animations
        /// </summary>
        public bool UsesBillboarding => true;

        #endregion
    }
}
