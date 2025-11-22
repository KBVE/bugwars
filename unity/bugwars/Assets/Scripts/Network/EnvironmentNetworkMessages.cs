using UnityEngine;
using BugWars.Interaction;

namespace BugWars.Network
{
    /// <summary>
    /// Network messages for server-authoritative environment object management
    /// Server is source of truth for all environment objects (trees, rocks, bushes)
    /// Client receives spawn/despawn commands and renders accordingly
    /// </summary>

    /// <summary>
    /// Server -> Client: Spawn multiple environment objects
    /// Sent when player enters new chunk or logs in
    /// </summary>
    [System.Serializable]
    public struct EnvironmentObjectsSpawnMessage
    {
        public EnvironmentObjectData[] objects;
    }

    /// <summary>
    /// Server -> Client: Despawn environment objects by ID
    /// Sent when objects are harvested or player moves away from chunk
    /// </summary>
    [System.Serializable]
    public struct EnvironmentObjectsDespawnMessage
    {
        public string[] objectIds; // Server-assigned unique IDs
    }

    /// <summary>
    /// Client -> Server: Request to harvest an environment object
    /// Server validates (range, existence, not already harvested) and responds
    /// </summary>
    [System.Serializable]
    public struct HarvestObjectRequest
    {
        public string objectId;
        public Vector3 playerPosition; // For server-side range validation
    }

    /// <summary>
    /// Server -> Client: Harvest request result
    /// Sent to harvester AND nearby players (for sync)
    /// </summary>
    [System.Serializable]
    public struct HarvestObjectResponse
    {
        public bool success;
        public string objectId;
        public string playerId; // Who harvested it
        public ResourceType resourceType;
        public int resourceAmount;
        public string errorMessage; // If success=false
    }

    /// <summary>
    /// Server -> Client: Object respawned
    /// Sent to all players in chunk when object respawns after timer
    /// </summary>
    [System.Serializable]
    public struct EnvironmentObjectRespawnMessage
    {
        public EnvironmentObjectData objectData;
    }

    /// <summary>
    /// Data structure for environment object synchronization
    /// Matches server-side representation
    /// </summary>
    [System.Serializable]
    public struct EnvironmentObjectData
    {
        public string objectId; // Server-assigned unique ID (e.g., "tree_chunk_12_45_idx_3")
        public string assetName; // Prefab name (e.g., "Tree_Oak_01")
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public EnvironmentObjectType objectType; // Tree, Rock, Bush, Grass
        public ResourceType resourceType; // Wood, Stone, Berries, Herbs
        public int resourceAmount; // How much resource it gives
        public float harvestTime; // How long to harvest
    }

    /// <summary>
    /// Environment object type enum (must match server)
    /// </summary>
    public enum EnvironmentObjectType
    {
        Tree = 0,
        Rock = 1,
        Bush = 2,
        Grass = 3
    }
}
