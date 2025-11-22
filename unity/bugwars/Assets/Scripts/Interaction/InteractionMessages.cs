using UnityEngine;

namespace BugWars.Interaction
{
    /// <summary>
    /// Message published when an object has been harvested and should be returned to pool
    /// Published by InteractableObject, consumed by EnvironmentManager
    /// </summary>
    public readonly struct ObjectHarvestedMessage
    {
        public readonly GameObject GameObject;
        public readonly string AssetName;
        public readonly ResourceType ResourceType;
        public readonly int ResourceAmount;

        public ObjectHarvestedMessage(GameObject gameObject, string assetName, ResourceType resourceType, int resourceAmount)
        {
            GameObject = gameObject;
            AssetName = assetName;
            ResourceType = resourceType;
            ResourceAmount = resourceAmount;
        }
    }

    /// <summary>
    /// Message published when an object needs to be spawned from pool
    /// Published by EnvironmentManager or network sync
    /// </summary>
    public readonly struct SpawnObjectMessage
    {
        public readonly string AssetName;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public SpawnObjectMessage(string assetName, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            AssetName = assetName;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }
}
