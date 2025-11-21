// src/game/entity_state.rs
// Manages game entity state (players, NPCs, etc.)

use dashmap::DashMap;
use serde::{Deserialize, Serialize};
use std::sync::Arc;
use std::time::{Duration, Instant};
use tracing::{debug, info, warn};

/// 3D position in game world
#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct Position {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

impl Position {
    pub fn new(x: f32, y: f32, z: f32) -> Self {
        Self { x, y, z }
    }

    pub fn distance_to(&self, other: &Position) -> f32 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        let dz = self.z - other.z;
        (dx * dx + dy * dy + dz * dz).sqrt()
    }
}

impl Default for Position {
    fn default() -> Self {
        Self::new(0.0, 0.0, 0.0)
    }
}

/// Rotation in game world (quaternion or euler angles)
#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct Rotation {
    pub x: f32,
    pub y: f32,
    pub z: f32,
    pub w: f32, // For quaternion (optional, 1.0 for euler angles)
}

impl Default for Rotation {
    fn default() -> Self {
        Self {
            x: 0.0,
            y: 0.0,
            z: 0.0,
            w: 1.0, // Identity quaternion
        }
    }
}

/// Inventory item
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq, Hash)]
pub struct InventoryItem {
    pub item_id: String,      // Unique identifier for item type (e.g., "weapon_pistol", "health_potion")
    pub quantity: u32,         // Stack size
    pub metadata: Option<String>, // Optional JSON metadata (durability, enchantments, etc.)
}

impl InventoryItem {
    pub fn new(item_id: String, quantity: u32) -> Self {
        Self {
            item_id,
            quantity,
            metadata: None,
        }
    }

    pub fn with_metadata(item_id: String, quantity: u32, metadata: String) -> Self {
        Self {
            item_id,
            quantity,
            metadata: Some(metadata),
        }
    }
}

/// Player inventory (items keyed by item_id)
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Inventory {
    pub items: Vec<InventoryItem>,
    pub max_slots: u32, // Maximum inventory slots (0 = unlimited)
}

impl Inventory {
    pub fn new(max_slots: u32) -> Self {
        Self {
            items: Vec::new(),
            max_slots,
        }
    }

    pub fn add_item(&mut self, item_id: String, quantity: u32) -> bool {
        // Try to stack with existing item
        if let Some(existing) = self.items.iter_mut().find(|i| i.item_id == item_id && i.metadata.is_none()) {
            existing.quantity += quantity;
            return true;
        }

        // Check slot limit
        if self.max_slots > 0 && self.items.len() >= self.max_slots as usize {
            return false; // Inventory full
        }

        // Add new item
        self.items.push(InventoryItem::new(item_id, quantity));
        true
    }

    pub fn remove_item(&mut self, item_id: &str, quantity: u32) -> bool {
        if let Some(pos) = self.items.iter().position(|i| i.item_id == item_id) {
            let item = &mut self.items[pos];
            if item.quantity >= quantity {
                item.quantity -= quantity;
                if item.quantity == 0 {
                    self.items.remove(pos);
                }
                return true;
            }
        }
        false
    }

    pub fn has_item(&self, item_id: &str, quantity: u32) -> bool {
        self.items
            .iter()
            .find(|i| i.item_id == item_id)
            .map(|i| i.quantity >= quantity)
            .unwrap_or(false)
    }

    pub fn get_item_quantity(&self, item_id: &str) -> u32 {
        self.items
            .iter()
            .find(|i| i.item_id == item_id)
            .map(|i| i.quantity)
            .unwrap_or(0)
    }

    pub fn clear(&mut self) {
        self.items.clear();
    }
}

impl Default for Inventory {
    fn default() -> Self {
        Self::new(20) // Default 20 slots
    }
}

/// Entity type (Player, NPC, etc.)
#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "lowercase")]
pub enum EntityType {
    Player,
    Npc,
    Enemy,
    Boss,
}

/// Entity state tracked by the server
#[derive(Debug, Clone, Serialize)]
pub struct EntityState {
    pub entity_id: String,  // Unique entity ID (user_id for players, generated for NPCs)
    pub entity_type: EntityType,
    pub email: Option<String>, // Only for players
    pub position: Position,
    pub rotation: Rotation,
    pub health: f32,
    pub is_alive: bool,
    pub inventory: Inventory,
    pub last_update: i64, // Unix timestamp
    #[serde(skip)]
    pub last_seen: Instant, // Server-side tracking (not serialized)
}

impl EntityState {
    pub fn new_player(user_id: String, email: Option<String>) -> Self {
        Self {
            entity_id: user_id,
            entity_type: EntityType::Player,
            email,
            position: Position::default(),
            rotation: Rotation::default(),
            health: 100.0,
            is_alive: true,
            inventory: Inventory::default(),
            last_update: chrono::Utc::now().timestamp(),
            last_seen: Instant::now(),
        }
    }

    pub fn new_npc(npc_id: String) -> Self {
        Self {
            entity_id: npc_id,
            entity_type: EntityType::Npc,
            email: None,
            position: Position::default(),
            rotation: Rotation::default(),
            health: 100.0,
            is_alive: true,
            inventory: Inventory::default(),
            last_update: chrono::Utc::now().timestamp(),
            last_seen: Instant::now(),
        }
    }

    pub fn new_enemy(enemy_id: String) -> Self {
        Self {
            entity_id: enemy_id,
            entity_type: EntityType::Enemy,
            email: None,
            position: Position::default(),
            rotation: Rotation::default(),
            health: 100.0,
            is_alive: true,
            inventory: Inventory::default(),
            last_update: chrono::Utc::now().timestamp(),
            last_seen: Instant::now(),
        }
    }

    pub fn new_boss(boss_id: String, health: f32) -> Self {
        Self {
            entity_id: boss_id,
            entity_type: EntityType::Boss,
            email: None,
            position: Position::default(),
            rotation: Rotation::default(),
            health,
            is_alive: true,
            inventory: Inventory::default(),
            last_update: chrono::Utc::now().timestamp(),
            last_seen: Instant::now(),
        }
    }

    pub fn update_position(&mut self, position: Position, rotation: Option<Rotation>) {
        self.position = position;
        if let Some(rot) = rotation {
            self.rotation = rot;
        }
        self.last_update = chrono::Utc::now().timestamp();
        self.last_seen = Instant::now();
    }

    pub fn update_health(&mut self, health: f32) {
        self.health = health.clamp(0.0, 100.0);
        self.is_alive = self.health > 0.0;
        self.last_update = chrono::Utc::now().timestamp();
        self.last_seen = Instant::now();
    }

    pub fn is_stale(&self, timeout: Duration) -> bool {
        self.last_seen.elapsed() > timeout
    }
}

/// Messages from Unity clients
#[derive(Debug, Deserialize, Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum GameMessage {
    /// Player joins the game
    Join {
        position: Option<Position>,
    },
    /// Player updates position/rotation
    UpdatePosition {
        position: Position,
        rotation: Option<Rotation>,
    },
    /// Player takes damage or heals
    UpdateHealth {
        health: f32,
    },
    /// Add item to inventory
    AddItem {
        item_id: String,
        quantity: u32,
    },
    /// Remove item from inventory
    RemoveItem {
        item_id: String,
        quantity: u32,
    },
    /// Get full inventory
    GetInventory,
    /// Player leaves the game
    Leave,
    /// Request current game state
    GetState,
    /// Heartbeat/keepalive
    Ping,
}

/// Server response messages
#[derive(Debug, Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum ServerMessage {
    /// Welcome message when player connects
    Connected {
        user_id: String,
        role: String,
    },
    /// Player successfully joined
    Joined {
        user_id: String,
        position: Position,
    },
    /// Current game state (all players)
    GameState {
        players: Vec<EntityState>,
        timestamp: i64,
    },
    /// Another player joined
    PlayerJoined {
        player: EntityState,
    },
    /// Another player left
    PlayerLeft {
        user_id: String,
    },
    /// Player position updated
    PlayerMoved {
        user_id: String,
        position: Position,
        rotation: Rotation,
    },
    /// Player health changed
    PlayerHealthChanged {
        user_id: String,
        health: f32,
        is_alive: bool,
    },
    /// Inventory updated (item added/removed)
    InventoryUpdated {
        user_id: String,
        inventory: Inventory,
    },
    /// Item added successfully
    ItemAdded {
        item_id: String,
        quantity: u32,
        success: bool,
    },
    /// Item removed successfully
    ItemRemoved {
        item_id: String,
        quantity: u32,
        success: bool,
    },
    /// Pong response to ping
    Pong {
        timestamp: i64,
    },
    /// Error message
    Error {
        message: String,
    },
}

/// Global entity state manager (tracks players, NPCs, enemies, bosses, etc.)
#[derive(Clone)]
pub struct EntityStateManager {
    entities: Arc<DashMap<String, EntityState>>,
    stale_timeout: Duration,
}

impl EntityStateManager {
    pub fn new(stale_timeout_secs: u64) -> Self {
        Self {
            entities: Arc::new(DashMap::new()),
            stale_timeout: Duration::from_secs(stale_timeout_secs),
        }
    }

    /// Add or update a player entity
    pub fn add_player(&self, user_id: String, email: Option<String>) -> EntityState {
        let entity = EntityState::new_player(user_id.clone(), email);
        info!(
            entity_id = %user_id,
            entity_type = ?entity.entity_type,
            entity_count = self.entities.len() + 1,
            "Player entity added to game state"
        );
        self.entities.insert(user_id, entity.clone());
        entity
    }

    /// Add an NPC entity
    pub fn add_npc(&self, npc_id: String) -> EntityState {
        let entity = EntityState::new_npc(npc_id.clone());
        info!(
            entity_id = %npc_id,
            entity_type = ?entity.entity_type,
            entity_count = self.entities.len() + 1,
            "NPC entity added to game state"
        );
        self.entities.insert(npc_id, entity.clone());
        entity
    }

    /// Add an enemy entity
    pub fn add_enemy(&self, enemy_id: String) -> EntityState {
        let entity = EntityState::new_enemy(enemy_id.clone());
        info!(
            entity_id = %enemy_id,
            entity_type = ?entity.entity_type,
            entity_count = self.entities.len() + 1,
            "Enemy entity added to game state"
        );
        self.entities.insert(enemy_id, entity.clone());
        entity
    }

    /// Add a boss entity
    pub fn add_boss(&self, boss_id: String, health: f32) -> EntityState {
        let entity = EntityState::new_boss(boss_id.clone(), health);
        info!(
            entity_id = %boss_id,
            entity_type = ?entity.entity_type,
            health = %health,
            entity_count = self.entities.len() + 1,
            "Boss entity added to game state"
        );
        self.entities.insert(boss_id, entity.clone());
        entity
    }

    /// Remove an entity
    pub fn remove_entity(&self, entity_id: &str) -> Option<EntityState> {
        let removed = self.entities.remove(entity_id).map(|(_, entity)| entity);
        if let Some(ref entity) = removed {
            info!(
                entity_id = %entity_id,
                entity_type = ?entity.entity_type,
                entity_count = self.entities.len(),
                "Entity removed from game state"
            );
        }
        removed
    }

    /// Update entity position
    pub fn update_position(
        &self,
        entity_id: &str,
        position: Position,
        rotation: Option<Rotation>,
    ) -> Option<EntityState> {
        self.entities.get_mut(entity_id).map(|mut entity| {
            entity.update_position(position, rotation);
            debug!(
                entity_id = %entity_id,
                entity_type = ?entity.entity_type,
                x = %position.x,
                y = %position.y,
                z = %position.z,
                "Entity position updated"
            );
            entity.clone()
        })
    }

    /// Update entity health
    pub fn update_health(&self, entity_id: &str, health: f32) -> Option<EntityState> {
        self.entities.get_mut(entity_id).map(|mut entity| {
            let was_alive = entity.is_alive;
            entity.update_health(health);
            if was_alive && !entity.is_alive {
                warn!(
                    entity_id = %entity_id,
                    entity_type = ?entity.entity_type,
                    health = %entity.health,
                    "Entity died"
                );
            }
            debug!(
                entity_id = %entity_id,
                entity_type = ?entity.entity_type,
                health = %health,
                is_alive = entity.is_alive,
                "Entity health updated"
            );
            entity.clone()
        })
    }

    /// Get an entity's current state
    pub fn get_entity(&self, entity_id: &str) -> Option<EntityState> {
        self.entities.get(entity_id).map(|entity| entity.clone())
    }

    /// Get all entities
    pub fn get_all_entities(&self) -> Vec<EntityState> {
        self.entities.iter().map(|entry| entry.value().clone()).collect()
    }

    /// Get all player entities
    pub fn get_all_players(&self) -> Vec<EntityState> {
        self.entities
            .iter()
            .filter(|entry| entry.value().entity_type == EntityType::Player)
            .map(|entry| entry.value().clone())
            .collect()
    }

    /// Get entity count
    pub fn entity_count(&self) -> usize {
        self.entities.len()
    }

    /// Get player count
    pub fn player_count(&self) -> usize {
        self.entities
            .iter()
            .filter(|entry| entry.value().entity_type == EntityType::Player)
            .count()
    }

    /// Add item to entity's inventory
    pub fn add_item(&self, entity_id: &str, item_id: String, quantity: u32) -> Option<(bool, Inventory)> {
        self.entities.get_mut(entity_id).map(|mut entity| {
            let success = entity.inventory.add_item(item_id.clone(), quantity);
            if success {
                info!(
                    entity_id = %entity_id,
                    entity_type = ?entity.entity_type,
                    item_id = %item_id,
                    quantity = quantity,
                    "Item added to entity inventory"
                );
            } else {
                warn!(
                    entity_id = %entity_id,
                    entity_type = ?entity.entity_type,
                    item_id = %item_id,
                    quantity = quantity,
                    "Failed to add item (inventory full)"
                );
            }
            (success, entity.inventory.clone())
        })
    }

    /// Remove item from entity's inventory
    pub fn remove_item(&self, entity_id: &str, item_id: &str, quantity: u32) -> Option<(bool, Inventory)> {
        self.entities.get_mut(entity_id).map(|mut entity| {
            let success = entity.inventory.remove_item(item_id, quantity);
            if success {
                info!(
                    entity_id = %entity_id,
                    entity_type = ?entity.entity_type,
                    item_id = %item_id,
                    quantity = quantity,
                    "Item removed from entity inventory"
                );
            } else {
                warn!(
                    entity_id = %entity_id,
                    entity_type = ?entity.entity_type,
                    item_id = %item_id,
                    quantity = quantity,
                    "Failed to remove item (not enough quantity)"
                );
            }
            (success, entity.inventory.clone())
        })
    }

    /// Get entity's inventory
    pub fn get_inventory(&self, entity_id: &str) -> Option<Inventory> {
        self.entities.get(entity_id).map(|entity| entity.inventory.clone())
    }

    /// Clean up stale entities (haven't sent updates in a while)
    pub fn cleanup_stale_entities(&self) -> Vec<String> {
        let stale_entities: Vec<String> = self.entities
            .iter()
            .filter_map(|entry| {
                if entry.value().is_stale(self.stale_timeout) {
                    Some(entry.key().clone())
                } else {
                    None
                }
            })
            .collect();

        if !stale_entities.is_empty() {
            warn!(
                count = stale_entities.len(),
                timeout_secs = self.stale_timeout.as_secs(),
                "Cleaning up stale entities"
            );
            for entity_id in &stale_entities {
                self.remove_entity(entity_id);
            }
        }

        stale_entities
    }

    /// Run periodic cleanup task
    pub async fn run_cleanup_task(self, cleanup_interval_secs: u64) {
        use tokio::time;

        info!(
            cleanup_interval_secs = cleanup_interval_secs,
            stale_timeout_secs = self.stale_timeout.as_secs(),
            "Starting entity state cleanup task"
        );

        let mut interval = time::interval(Duration::from_secs(cleanup_interval_secs));

        loop {
            interval.tick().await;

            let stale_entities = self.cleanup_stale_entities();
            if !stale_entities.is_empty() {
                info!(
                    removed_count = stale_entities.len(),
                    remaining_entities = self.entity_count(),
                    remaining_players = self.player_count(),
                    "Cleaned up stale entities"
                );
            } else {
                debug!(
                    entity_count = self.entity_count(),
                    player_count = self.player_count(),
                    "Entity state cleanup: no stale entities"
                );
            }
        }
    }
}

impl Default for EntityStateManager {
    fn default() -> Self {
        Self::new(120) // 2 minute timeout by default
    }
}
