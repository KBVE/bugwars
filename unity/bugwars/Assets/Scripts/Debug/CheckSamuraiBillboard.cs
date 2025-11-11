using UnityEngine;
using BugWars.Character;
using BugWars.Core;

namespace BugWars.Debugging
{
    /// <summary>
    /// Runtime debugging component to monitor Samurai billboard and camera setup
    /// Attach this to the Samurai prefab to debug rendering issues
    /// </summary>
    public class CheckSamuraiBillboard : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool enableLogging = true;
        [SerializeField] private float logInterval = 2f; // Log every 2 seconds

        private Samurai samurai;
        private SpriteRenderer spriteRenderer;
        private float nextLogTime;

        private void Start()
        {
            samurai = GetComponent<Samurai>();
            if (samurai == null)
            {
                Debug.LogError("[CheckSamuraiBillboard] No Samurai component found!");
                enabled = false;
                return;
            }

            spriteRenderer = samurai.GetSpriteRenderer();
            if (spriteRenderer == null)
            {
                Debug.LogError("[CheckSamuraiBillboard] No SpriteRenderer found!");
                enabled = false;
                return;
            }

            nextLogTime = Time.time + logInterval;
            LogBillboardStatus();
        }

        private void Update()
        {
            if (!enableLogging) return;

            if (Time.time >= nextLogTime)
            {
                LogBillboardStatus();
                nextLogTime = Time.time + logInterval;
            }
        }

        private void LogBillboardStatus()
        {
            Debug.Log("=== SAMURAI BILLBOARD STATUS ===");

            // Camera Manager status
            if (CameraManager.Instance == null)
            {
                Debug.LogError("❌ CameraManager.Instance is NULL!");
            }
            else
            {
                Debug.Log("✓ CameraManager.Instance found");

                if (CameraManager.Instance.MainCamera == null)
                {
                    Debug.LogError("❌ CameraManager.Instance.MainCamera is NULL!");
                }
                else
                {
                    Camera mainCam = CameraManager.Instance.MainCamera;
                    Debug.Log($"✓ MainCamera: {mainCam.name}");
                    Debug.Log($"  Camera Position: {mainCam.transform.position}");
                    Debug.Log($"  Camera Rotation: {mainCam.transform.rotation.eulerAngles}");
                    Debug.Log($"  Camera Forward: {mainCam.transform.forward}");
                }
            }

            // Samurai position and rotation
            Debug.Log($"Samurai Position: {transform.position}");
            Debug.Log($"Samurai Rotation: {transform.rotation.eulerAngles}");

            // SpriteRenderer info
            if (spriteRenderer != null)
            {
                Debug.Log($"SpriteRenderer Position: {spriteRenderer.transform.position}");
                Debug.Log($"SpriteRenderer Rotation: {spriteRenderer.transform.rotation.eulerAngles}");
                Debug.Log($"SpriteRenderer Forward: {spriteRenderer.transform.forward}");
                Debug.Log($"SpriteRenderer Enabled: {spriteRenderer.enabled}");
                Debug.Log($"SpriteRenderer Sprite: {(spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "NULL")}");

                // Material info
                if (spriteRenderer.material != null)
                {
                    Debug.Log($"Material: {spriteRenderer.material.name}");
                    Debug.Log($"Shader: {spriteRenderer.material.shader.name}");

                    // Check if texture is assigned
                    Texture mainTex = spriteRenderer.material.GetTexture("_BaseMap");
                    if (mainTex != null)
                    {
                        Debug.Log($"✓ Texture (_BaseMap): {mainTex.name}");
                    }
                    else
                    {
                        Debug.LogError("❌ No texture assigned to _BaseMap!");
                    }

                    // Check UV frame parameters from MaterialPropertyBlock (the actual values used)
                    if (spriteRenderer.sharedMaterial.HasProperty("_FrameUVMin"))
                    {
                        // Read from MaterialPropertyBlock to get the actual per-instance values
                        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
                        spriteRenderer.GetPropertyBlock(propBlock);

                        Vector4 uvMin = propBlock.GetVector("_FrameUVMin");
                        Vector4 uvMax = propBlock.GetVector("_FrameUVMax");
                        Debug.Log($"UV Frame (from PropertyBlock): Min({uvMin.x:F3}, {uvMin.y:F3}) Max({uvMax.x:F3}, {uvMax.y:F3})");

                        // Also check material defaults for comparison
                        Vector4 matUvMin = spriteRenderer.material.GetVector("_FrameUVMin");
                        Vector4 matUvMax = spriteRenderer.material.GetVector("_FrameUVMax");
                        Debug.Log($"UV Frame (from Material defaults): Min({matUvMin.x:F3}, {matUvMin.y:F3}) Max({matUvMax.x:F3}, {matUvMax.y:F3})");
                    }
                }
                else
                {
                    Debug.LogError("❌ SpriteRenderer.material is NULL!");
                }

                // Check if sprite is facing camera
                if (CameraManager.Instance != null && CameraManager.Instance.MainCamera != null)
                {
                    Camera mainCam = CameraManager.Instance.MainCamera;
                    Vector3 directionToCamera = mainCam.transform.position - spriteRenderer.transform.position;
                    directionToCamera.y = 0;
                    directionToCamera.Normalize();

                    Vector3 spriteForward = spriteRenderer.transform.forward;
                    spriteForward.y = 0;
                    spriteForward.Normalize();

                    float dotProduct = Vector3.Dot(spriteForward, directionToCamera);
                    Debug.Log($"Sprite-to-Camera Alignment: {dotProduct:F3} (1.0 = facing camera, -1.0 = facing away)");

                    if (dotProduct < 0.5f)
                    {
                        Debug.LogWarning($"⚠️  Sprite may not be facing camera correctly! Dot product: {dotProduct:F3}");
                    }

                    // Calculate expected rotation
                    Quaternion expectedRotation = Quaternion.LookRotation(directionToCamera);
                    Debug.Log($"Expected Billboard Rotation: {expectedRotation.eulerAngles}");
                    Debug.Log($"Actual SpriteRenderer Rotation: {spriteRenderer.transform.rotation.eulerAngles}");
                }
            }

            // Animation status
            Debug.Log($"Current Animation: {samurai.GetCurrentAnimation()}");

            Debug.Log("=== END BILLBOARD STATUS ===\n");
        }

        [ContextMenu("Force Log Billboard Status")]
        public void ForceLog()
        {
            LogBillboardStatus();
        }
    }
}
