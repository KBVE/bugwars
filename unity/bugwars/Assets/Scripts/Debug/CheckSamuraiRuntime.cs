using UnityEngine;
using UnityEditor;

namespace BugWars.Editor
{
    /// <summary>
    /// Runtime debugging tool to check if Samurai is properly animating
    /// Add this to a GameObject in the scene to debug at runtime
    /// </summary>
    public class CheckSamuraiRuntime : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool logEveryFrame = false;
        [SerializeField] private float logInterval = 2f;

        private float nextLogTime = 0f;
        private BugWars.Character.Samurai samurai;

        private void Start()
        {
            // Find the Samurai in the scene
            samurai = FindFirstObjectByType<BugWars.Character.Samurai>();

            if (samurai == null)
            {
                Debug.LogError("[CheckSamuraiRuntime] No Samurai found in scene!");
                enabled = false;
                return;
            }

            Debug.Log($"[CheckSamuraiRuntime] Found Samurai: {samurai.name}");
            LogSamuraiStatus();
        }

        private void Update()
        {
            if (samurai == null) return;

            if (logEveryFrame || Time.time >= nextLogTime)
            {
                LogSamuraiStatus();
                nextLogTime = Time.time + logInterval;
            }
        }

        private void LogSamuraiStatus()
        {
            Debug.Log("=== SAMURAI RUNTIME STATUS ===");

            // Check current animation
            string currentAnim = samurai.GetCurrentAnimation();
            Debug.Log($"Current Animation: {currentAnim}");

            // Check available animations
            var availableAnims = samurai.GetAvailableAnimations();
            if (availableAnims.Count == 0)
            {
                Debug.LogError("❌ No animations loaded! Check if atlas JSON is being parsed correctly.");
            }
            else
            {
                Debug.Log($"✓ {availableAnims.Count} animations available: {string.Join(", ", availableAnims)}");
            }

            // Check SpriteRenderer
            var spriteRenderer = samurai.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("❌ SpriteRenderer not found!");
            }
            else
            {
                Debug.Log($"✓ SpriteRenderer found");
                Debug.Log($"  Sprite: {(spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "NULL")}");
                Debug.Log($"  Material: {(spriteRenderer.material != null ? spriteRenderer.material.name : "NULL")}");
                Debug.Log($"  Enabled: {spriteRenderer.enabled}");
                Debug.Log($"  Color: {spriteRenderer.color}");

                // Check MaterialPropertyBlock values
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                spriteRenderer.GetPropertyBlock(block);

                // Try to read the UV parameters (these are set by the Samurai script)
                // Note: Can't directly read from MaterialPropertyBlock, but we can check the material
                if (spriteRenderer.material != null)
                {
                    Vector4 uvMin = spriteRenderer.material.GetVector("_FrameUVMin");
                    Vector4 uvMax = spriteRenderer.material.GetVector("_FrameUVMax");
                    Debug.Log($"  Frame UV Min: {uvMin}");
                    Debug.Log($"  Frame UV Max: {uvMax}");

                    if (uvMin == Vector4.zero && uvMax == new Vector4(1,1,0,0))
                    {
                        Debug.LogWarning("⚠️  UVs are at default (0,0) to (1,1) - animation may not be playing");
                    }

                    // Check texture
                    Texture tex = spriteRenderer.material.GetTexture("_BaseMap");
                    if (tex == null)
                    {
                        Debug.LogError("❌ No texture assigned to material's _BaseMap!");
                    }
                    else
                    {
                        Debug.Log($"  Texture: {tex.name} ({tex.width}x{tex.height})");
                    }
                }
            }

            Debug.Log("=========================\n");
        }

        [ContextMenu("Force Log Status Now")]
        private void ForceLog()
        {
            if (samurai != null)
            {
                LogSamuraiStatus();
            }
        }

        [ContextMenu("Try Play Idle Animation")]
        private void TryPlayIdle()
        {
            if (samurai != null)
            {
                samurai.PlayAnimation("Idle");
                Debug.Log("[CheckSamuraiRuntime] Attempted to play Idle animation");
            }
        }

        [ContextMenu("Try Play Walk Animation")]
        private void TryPlayWalk()
        {
            if (samurai != null)
            {
                samurai.PlayAnimation("Walk");
                Debug.Log("[CheckSamuraiRuntime] Attempted to play Walk animation");
            }
        }
    }
}
