// src/game/environment.rs
// Server-authoritative environment object management
// Trees, rocks, bushes, grass - all managed by server for true multiplayer sync

use dashmap::DashMap;
use serde::{Deserialize, Serialize};
use std::collections::{HashMap, HashSet};
use std::sync::Arc;
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use tracing::{debug, info, warn, error};
use tokio::time;

// [AUDIT]: 11-22-2025 6:17AM - Added unix_time_secs() helper to reduce unwrap() calls
// [AUDIT]: 11-22-2025 6:25AM - Performance optimizations:
//   - Added get_objects_in_chunks_network() to avoid intermediate object clones
//   - Added get_respawnable_object_ids() to avoid cloning full objects in respawn task
//   - Updated send_initial_objects(), update_player_chunks(), start_respawn_task() to use optimized methods
//   - Reduces allocations in hot paths when handling thousands of objects

use super::entity_state::Position;

/// Helper function to get current Unix timestamp in seconds
/// Returns 0 if system time is before UNIX_EPOCH (should never happen)
/// Uses i64 for better compatibility with Postgres BIGINT/TIMESTAMPTZ
fn unix_time_secs() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_else(|_| Duration::from_secs(0))
        .as_secs() as i64
}

/// 3D scale vector
#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct Scale {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

impl Scale {
    pub fn uniform(value: f32) -> Self {
        Self { x: value, y: value, z: value }
    }
}

impl Default for Scale {
    fn default() -> Self {
        Self::uniform(1.0)
    }
}

/// Quaternion rotation
#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub struct Quaternion {
    pub x: f32,
    pub y: f32,
    pub z: f32,
    pub w: f32,
}

impl Default for Quaternion {
    fn default() -> Self {
        Self { x: 0.0, y: 0.0, z: 0.0, w: 1.0 } // Identity
    }
}

/// Environment object types (must match Unity enum)
#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq, Hash)]
#[serde(rename_all = "PascalCase")]
pub enum EnvironmentObjectType {
    Tree = 0,
    Rock = 1,
    Bush = 2,
    Grass = 3,
}

/// Resource types (must match Unity enum)
#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq, Hash)]
#[serde(rename_all = "PascalCase")]
pub enum ResourceType {
    Wood = 0,
    Stone = 1,
    Berries = 2,
    Herbs = 3,
    None = 4,
}

/// Environment object in the game world
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EnvironmentObject {
    pub object_id: String,              // e.g., "tree_chunk_12_45_idx_3"
    pub asset_name: String,             // e.g., "Tree_Oak_01"
    pub position: Position,
    pub rotation: Quaternion,
    pub scale: Scale,
    pub object_type: EnvironmentObjectType,
    pub resource_type: ResourceType,
    pub resource_amount: u32,
    pub harvest_time: f32,              // Seconds to harvest
    pub is_harvested: bool,
    pub harvested_at: Option<i64>,     // Unix timestamp in seconds (i64 for Postgres BIGINT compatibility)
    pub respawn_time_seconds: Option<u32>, // e.g., 300 (5 minutes)
}

impl EnvironmentObject {
    /// Check if this object should respawn
    pub fn should_respawn(&self) -> bool {
        if !self.is_harvested {
            return false;
        }

        if let (Some(harvested_at), Some(respawn_time)) = (self.harvested_at, self.respawn_time_seconds) {
            let now = unix_time_secs();
            let elapsed = now.saturating_sub(harvested_at);
            elapsed >= respawn_time as i64
        } else {
            false
        }
    }

    /// Mark as harvested
    pub fn mark_harvested(&mut self) {
        self.is_harvested = true;
        self.harvested_at = Some(unix_time_secs());
    }

    /// Respawn the object
    pub fn respawn(&mut self) {
        self.is_harvested = false;
        self.harvested_at = None;
    }

    /// Convert to network data (for sending to clients)
    pub fn to_network_data(&self) -> EnvironmentObjectData {
        EnvironmentObjectData {
            object_id: self.object_id.clone(),
            asset_name: self.asset_name.clone(),
            position: self.position,
            rotation: self.rotation,
            scale: self.scale,
            object_type: self.object_type,
            resource_type: self.resource_type,
            resource_amount: self.resource_amount,
            harvest_time: self.harvest_time,
        }
    }
}

/// Network data for environment objects (sent to clients)
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EnvironmentObjectData {
    pub object_id: String,
    pub asset_name: String,
    pub position: Position,
    pub rotation: Quaternion,
    pub scale: Scale,
    pub object_type: EnvironmentObjectType,
    pub resource_type: ResourceType,
    pub resource_amount: u32,
    pub harvest_time: f32,
}

/// Network messages
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EnvironmentObjectsSpawnMessage {
    pub objects: Vec<EnvironmentObjectData>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EnvironmentObjectsDespawnMessage {
    pub object_ids: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct HarvestObjectRequest {
    pub object_id: String,
    pub player_position: Position,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct HarvestObjectResponse {
    pub success: bool,
    pub object_id: String,
    pub player_id: String,
    pub resource_type: ResourceType,
    pub resource_amount: u32,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error_message: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EnvironmentObjectRespawnMessage {
    pub object_data: EnvironmentObjectData,
}

/// Chunk coordinate
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct ChunkCoord {
    pub x: i32,
    pub z: i32,
}

impl ChunkCoord {
    pub fn from_position(position: &Position, chunk_size: f32) -> Self {
        Self {
            x: (position.x / chunk_size).floor() as i32,
            z: (position.z / chunk_size).floor() as i32,
        }
    }

    /// Get neighboring chunks within radius
    pub fn neighbors(&self, radius: i32) -> Vec<ChunkCoord> {
        let mut neighbors = Vec::new();
        for dx in -radius..=radius {
            for dz in -radius..=radius {
                neighbors.push(ChunkCoord {
                    x: self.x + dx,
                    z: self.z + dz,
                });
            }
        }
        neighbors
    }
}

/// Environment manager - server-side authority for all environment objects
pub struct EnvironmentManager {
    /// All objects in the world (object_id -> object)
    objects: Arc<DashMap<String, EnvironmentObject>>,

    /// Chunk to object IDs mapping
    chunk_objects: Arc<DashMap<ChunkCoord, Vec<String>>>,

    /// Player to visible chunks mapping
    player_chunks: Arc<DashMap<String, HashSet<ChunkCoord>>>,

    /// Configuration
    chunk_size: f32,
    view_distance_chunks: i32,
    max_harvest_range: f32,
}

impl EnvironmentManager {
    pub fn new(chunk_size: f32, view_distance_chunks: i32, max_harvest_range: f32) -> Self {
        Self {
            objects: Arc::new(DashMap::new()),
            chunk_objects: Arc::new(DashMap::new()),
            player_chunks: Arc::new(DashMap::new()),
            chunk_size,
            view_distance_chunks,
            max_harvest_range,
        }
    }

    /// Add an object to the world
    pub fn add_object(&self, object: EnvironmentObject) {
        let chunk = ChunkCoord::from_position(&object.position, self.chunk_size);
        let object_id = object.object_id.clone();

        // Add to objects map
        self.objects.insert(object_id.clone(), object);

        // Add to chunk mapping
        self.chunk_objects
            .entry(chunk)
            .or_insert_with(Vec::new)
            .push(object_id);
    }

    /// Get objects in specific chunks
    pub fn get_objects_in_chunks(&self, chunks: &[ChunkCoord]) -> Vec<EnvironmentObject> {
        let mut objects = Vec::new();

        for chunk in chunks {
            if let Some(object_ids) = self.chunk_objects.get(chunk) {
                for object_id in object_ids.iter() {
                    if let Some(object) = self.objects.get(object_id) {
                        if !object.is_harvested {
                            objects.push(object.clone());
                        }
                    }
                }
            }
        }

        objects
    }

    /// Get objects in specific chunks as network data (optimized - avoids intermediate clone)
    /// This is more efficient than get_objects_in_chunks() followed by to_network_data()
    pub fn get_objects_in_chunks_network(&self, chunks: &[ChunkCoord]) -> Vec<EnvironmentObjectData> {
        let mut result = Vec::new();

        for chunk in chunks {
            if let Some(object_ids) = self.chunk_objects.get(chunk) {
                for object_id in object_ids.iter() {
                    if let Some(object) = self.objects.get(object_id) {
                        if !object.is_harvested {
                            result.push(object.to_network_data());
                        }
                    }
                }
            }
        }

        result
    }

    /// Get nearby chunks for a position
    pub fn get_nearby_chunks(&self, position: &Position) -> Vec<ChunkCoord> {
        let center_chunk = ChunkCoord::from_position(position, self.chunk_size);
        center_chunk.neighbors(self.view_distance_chunks)
    }

    /// Send initial objects to a player when they join
    pub fn send_initial_objects(&self, player_id: &str, player_position: &Position) -> EnvironmentObjectsSpawnMessage {
        let chunks = self.get_nearby_chunks(player_position);
        let objects = self.get_objects_in_chunks_network(&chunks);

        // Store player's visible chunks
        self.player_chunks.insert(
            player_id.to_string(),
            chunks.into_iter().collect()
        );

        info!("Sending {} objects to player {}", objects.len(), player_id);

        EnvironmentObjectsSpawnMessage { objects }
    }

    /// Update player's visible chunks (call when player moves)
    pub fn update_player_chunks(&self, player_id: &str, new_position: &Position) -> (Option<EnvironmentObjectsSpawnMessage>, Option<EnvironmentObjectsDespawnMessage>) {
        let new_chunks: HashSet<ChunkCoord> = self.get_nearby_chunks(new_position).into_iter().collect();

        let old_chunks = self.player_chunks
            .get(player_id)
            .map(|c| c.clone())
            .unwrap_or_default();

        let enter_chunks: Vec<_> = new_chunks.difference(&old_chunks).copied().collect();
        let exit_chunks: Vec<_> = old_chunks.difference(&new_chunks).copied().collect();

        // Update stored chunks
        self.player_chunks.insert(player_id.to_string(), new_chunks);

        let spawn_msg = if !enter_chunks.is_empty() {
            let objects = self.get_objects_in_chunks_network(&enter_chunks);
            Some(EnvironmentObjectsSpawnMessage { objects })
        } else {
            None
        };

        let despawn_msg = if !exit_chunks.is_empty() {
            let mut object_ids = Vec::new();
            for chunk in exit_chunks {
                if let Some(ids) = self.chunk_objects.get(&chunk) {
                    object_ids.extend(ids.iter().cloned());
                }
            }
            Some(EnvironmentObjectsDespawnMessage { object_ids })
        } else {
            None
        };

        (spawn_msg, despawn_msg)
    }

    /// Handle harvest request from player
    pub fn handle_harvest_request(&self, player_id: &str, request: HarvestObjectRequest) -> HarvestObjectResponse {
        // Get object
        let mut object = match self.objects.get_mut(&request.object_id) {
            Some(obj) => obj,
            None => {
                return HarvestObjectResponse {
                    success: false,
                    object_id: request.object_id,
                    player_id: player_id.to_string(),
                    resource_type: ResourceType::None,
                    resource_amount: 0,
                    error_message: Some("Object not found".to_string()),
                };
            }
        };

        // Check if already harvested
        if object.is_harvested {
            return HarvestObjectResponse {
                success: false,
                object_id: request.object_id,
                player_id: player_id.to_string(),
                resource_type: ResourceType::None,
                resource_amount: 0,
                error_message: Some("Already harvested".to_string()),
            };
        }

        // Validate range (anti-cheat)
        let distance = object.position.distance_to(&request.player_position);
        if distance > self.max_harvest_range {
            warn!("Player {} attempted to harvest from too far: {} > {}",
                  player_id, distance, self.max_harvest_range);
            return HarvestObjectResponse {
                success: false,
                object_id: request.object_id,
                player_id: player_id.to_string(),
                resource_type: ResourceType::None,
                resource_amount: 0,
                error_message: Some(format!("Too far: {:.1}m > {:.1}m", distance, self.max_harvest_range)),
            };
        }

        // SUCCESS: Mark as harvested
        let resource_type = object.resource_type;
        let resource_amount = object.resource_amount;
        object.mark_harvested();

        info!("Player {} harvested {} for {}x {:?}",
              player_id, request.object_id, resource_amount, resource_type);

        HarvestObjectResponse {
            success: true,
            object_id: request.object_id,
            player_id: player_id.to_string(),
            resource_type,
            resource_amount,
            error_message: None,
        }
    }

    /// Get objects that should respawn
    pub fn get_respawnable_objects(&self) -> Vec<EnvironmentObject> {
        self.objects
            .iter()
            .filter_map(|entry| {
                let object = entry.value();
                if object.should_respawn() {
                    Some(object.clone())
                } else {
                    None
                }
            })
            .collect()
    }

    /// Get IDs of objects that should respawn (optimized - avoids cloning full objects)
    /// More efficient than get_respawnable_objects() when you only need IDs
    pub fn get_respawnable_object_ids(&self) -> Vec<String> {
        self.objects
            .iter()
            .filter_map(|entry| {
                let object = entry.value();
                if object.should_respawn() {
                    Some(object.object_id.clone())
                } else {
                    None
                }
            })
            .collect()
    }

    /// Respawn an object
    pub fn respawn_object(&self, object_id: &str) -> Option<EnvironmentObjectRespawnMessage> {
        if let Some(mut object) = self.objects.get_mut(object_id) {
            object.respawn();
            info!("Respawned object: {}", object_id);
            Some(EnvironmentObjectRespawnMessage {
                object_data: object.to_network_data(),
            })
        } else {
            None
        }
    }

    /// Get all player IDs that can see a specific chunk
    /// Used for broadcasting respawn messages to relevant players
    pub fn get_players_in_chunk(&self, chunk: &ChunkCoord) -> Vec<String> {
        let mut players = Vec::new();

        for entry in self.player_chunks.iter() {
            if entry.value().contains(chunk) {
                players.push(entry.key().clone());
            }
        }

        players
    }

    /// Get chunk coordinate for an object ID
    pub fn get_object_chunk(&self, object_id: &str) -> Option<ChunkCoord> {
        self.objects.get(object_id).map(|obj| {
            ChunkCoord::from_position(&obj.position, self.chunk_size)
        })
    }

    /// Background task to handle respawns
    /// NOTE: This only handles server-side respawning. Broadcasting to clients must be implemented
    /// at the transport layer (WebSocket/HTTP) which has access to player connections.
    ///
    /// TODO: Implement broadcast mechanism
    /// Suggested approach:
    /// 1. Create a broadcast channel in main.rs
    /// 2. Pass sender to this task
    /// 3. Pass receiver to WebSocket handler
    /// 4. Send (object_id, respawn_msg, player_ids) through channel
    /// 5. WebSocket handler broadcasts to specific player connections
    ///
    /// For now, objects respawn server-side but clients only see them on reconnect or chunk reload
    pub async fn start_respawn_task(self: Arc<Self>) {
        let mut interval = time::interval(Duration::from_secs(10)); // Check every 10 seconds

        loop {
            interval.tick().await;

            let respawnable_ids = self.get_respawnable_object_ids();
            if !respawnable_ids.is_empty() {
                debug!("Found {} objects ready to respawn", respawnable_ids.len());

                for object_id in respawnable_ids {
                    if let Some(_respawn_msg) = self.respawn_object(&object_id) {
                        // Get chunk for this object
                        if let Some(chunk) = self.get_object_chunk(&object_id) {
                            // Get all players who can see this chunk
                            let player_ids = self.get_players_in_chunk(&chunk);

                            if !player_ids.is_empty() {
                                debug!(
                                    "Object {} respawned in chunk ({}, {}) - would broadcast to {} players: {:?}",
                                    object_id, chunk.x, chunk.z, player_ids.len(), player_ids
                                );
                                // TODO: Broadcast ServerMessage::ObjectRespawned to player_ids
                                // This requires access to WebSocket connections which are owned by the transport layer
                            } else {
                                debug!("Object {} respawned but no players in chunk ({}, {})", object_id, chunk.x, chunk.z);
                            }
                        }
                    }
                }
            }
        }
    }

    /// Remove player from tracking (call on disconnect)
    pub fn remove_player(&self, player_id: &str) {
        self.player_chunks.remove(player_id);
        debug!("Removed player {} from environment tracking", player_id);
    }

    /// Get statistics
    pub fn get_stats(&self) -> EnvironmentStats {
        let total_objects = self.objects.len();
        let harvested_objects = self.objects.iter().filter(|o| o.is_harvested).count();
        let active_objects = total_objects - harvested_objects;

        EnvironmentStats {
            total_objects,
            active_objects,
            harvested_objects,
            tracked_players: self.player_chunks.len(),
            loaded_chunks: self.chunk_objects.len(),
        }
    }
}

#[derive(Debug, Clone, Serialize)]
pub struct EnvironmentStats {
    pub total_objects: usize,
    pub active_objects: usize,
    pub harvested_objects: usize,
    pub tracked_players: usize,
    pub loaded_chunks: usize,
}
