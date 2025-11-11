using UnityEngine;
using Unity.Cinemachine;

namespace BugWars.Core
{
    /// <summary>
    /// Cinemachine extension that locks camera rotation to a fixed angle
    /// Perfect for 2D billboard sprites in 3D world (HD-2D style like Octopath Traveler)
    /// Prevents camera yaw rotation while maintaining optimal downward viewing angle
    /// </summary>
    [SaveDuringPlay]
    public class LockedCameraRotation : CinemachineExtension
    {
        [Header("Rotation Lock Settings")]
        [Tooltip("Fixed rotation angles (pitch, yaw, roll). For HD-2D: (25, 0, 0) recommended")]
        public Vector3 lockedRotation = new Vector3(25f, 0f, 0f);

        [Tooltip("Lock pitch (X rotation)")]
        public bool lockPitch = true;

        [Tooltip("Lock yaw (Y rotation) - CRITICAL for 2-directional sprites")]
        public bool lockYaw = true;

        [Tooltip("Lock roll (Z rotation)")]
        public bool lockRoll = true;

        [Header("Optional Offset")]
        [Tooltip("Additional rotation offset relative to locked angles")]
        public Vector3 rotationOffset = Vector3.zero;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            // Apply rotation lock at the Finalize stage (after all other calculations)
            if (stage == CinemachineCore.Stage.Finalize)
            {
                // Get current camera rotation
                Vector3 currentEuler = state.RawOrientation.eulerAngles;

                // Build locked rotation based on settings
                Vector3 finalRotation = new Vector3(
                    lockPitch ? lockedRotation.x + rotationOffset.x : currentEuler.x,
                    lockYaw ? lockedRotation.y + rotationOffset.y : currentEuler.y,
                    lockRoll ? lockedRotation.z + rotationOffset.z : currentEuler.z
                );

                // Apply locked rotation
                state.RawOrientation = Quaternion.Euler(finalRotation);
            }
        }

        /// <summary>
        /// Set the locked rotation at runtime
        /// </summary>
        public void SetLockedRotation(Vector3 rotation)
        {
            lockedRotation = rotation;
        }

        /// <summary>
        /// Set individual rotation axes at runtime
        /// </summary>
        public void SetLockedPitch(float pitch)
        {
            lockedRotation.x = pitch;
        }

        public void SetLockedYaw(float yaw)
        {
            lockedRotation.y = yaw;
        }

        public void SetLockedRoll(float roll)
        {
            lockedRotation.z = roll;
        }
    }
}
