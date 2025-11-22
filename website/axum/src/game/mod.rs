// src/game/mod.rs
// Game state management for all entities (players, NPCs, etc.) and environment

pub mod entity_state;
pub mod environment;
pub mod environment_gen;

pub use entity_state::{
    EntityState, EntityStateManager, EntityType, Position, Rotation,
    Inventory, InventoryItem, GameMessage, ServerMessage
};

pub use environment::{
    EnvironmentManager, EnvironmentObject, EnvironmentObjectType, ResourceType,
    EnvironmentObjectData, EnvironmentObjectsSpawnMessage, EnvironmentObjectsDespawnMessage,
    HarvestObjectRequest, HarvestObjectResponse, EnvironmentObjectRespawnMessage,
    ChunkCoord, EnvironmentStats
};

pub use environment_gen::EnvironmentGenerator;
