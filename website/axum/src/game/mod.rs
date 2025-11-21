// src/game/mod.rs
// Game state management for all entities (players, NPCs, etc.)

pub mod entity_state;

pub use entity_state::{
    EntityState, EntityStateManager, EntityType, Position, Rotation,
    Inventory, InventoryItem, GameMessage, ServerMessage
};
