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

    /// <summary>
    /// Message published when a player/entity harvests resources
    /// Published by HarvestAction, consumed by InventoryManager, UI, etc.
    /// Decouples action system from inventory/UI for better server-authoritative architecture
    /// </summary>
    public readonly struct ResourceHarvestedMessage
    {
        public readonly GameObject Harvester; // Player/entity who harvested
        public readonly GameObject HarvestedObject; // Object that was harvested
        public readonly ResourceType ResourceType;
        public readonly int Amount;
        public readonly Vector3 HarvestPosition; // For VFX/UI positioning

        public ResourceHarvestedMessage(GameObject harvester, GameObject harvestedObject, ResourceType resourceType, int amount, Vector3 harvestPosition)
        {
            Harvester = harvester;
            HarvestedObject = harvestedObject;
            ResourceType = resourceType;
            Amount = amount;
            HarvestPosition = harvestPosition;
        }
    }
}
