using System;
using UnityEngine;

namespace BugWars.Core
{
    /// <summary>
    /// Camera follow configuration for event-based camera control
    /// </summary>
    public struct CameraFollowConfig
    {
        public Transform target;
        public string cameraName;
        public Vector3 shoulderOffset;
        public float verticalArmLength;
        public float cameraDistance;
        public bool immediate;

        public static CameraFollowConfig ThirdPerson(Transform target, string cameraName = null, bool immediate = false)
        {
            return new CameraFollowConfig
            {
                target = target,
                cameraName = cameraName,
                shoulderOffset = new Vector3(0, 1.5f, 0), // Look slightly above player center
                verticalArmLength = 0f,
                cameraDistance = 8f, // 8 units behind player
                immediate = immediate
            };
        }
    }

    /// <summary>
    /// Event-based camera control system
    /// Decouples camera control from EntityManager and other systems
    /// </summary>
    public static class CameraEvents
    {
        /// <summary>
        /// Fired when a camera should follow a new target
        /// </summary>
        public static event Action<CameraFollowConfig> OnCameraFollowRequested;

        /// <summary>
        /// Fired when camera should stop following
        /// </summary>
        public static event Action<string> OnCameraStopFollowRequested;

        /// <summary>
        /// Request camera to follow a target with specific configuration
        /// </summary>
        public static void RequestCameraFollow(CameraFollowConfig config)
        {
            if (config.target == null)
            {
                Debug.LogWarning("[CameraEvents] Cannot request camera follow - target is null");
                return;
            }

            OnCameraFollowRequested?.Invoke(config);
        }

        /// <summary>
        /// Request camera to stop following
        /// </summary>
        public static void RequestCameraStopFollow(string cameraName = null)
        {
            OnCameraStopFollowRequested?.Invoke(cameraName);
        }
    }
}
