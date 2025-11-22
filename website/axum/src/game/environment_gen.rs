// src/game/environment_gen.rs
// Procedural generation for environment objects
// Generates trees, rocks, bushes using noise for natural distribution
//
// [AUDIT]: 11-22-2025 6:30AM - Improved chunk seed mixing:
//   - Added mix_seed() helper with better hash mixing for negative coordinates
//   - Prevents clustering patterns in negative chunks
//   - Deterministic but better per-chunk separation
//
// [AUDIT]: 11-22-2025 6:35AM - Integrated fastnoise-lite for organic world generation:
//   - Uses Perlin noise for biome-like density variation (forests vs plains)
//   - Separate noise layers for tree density, rock placement, bush clustering
//   - Creates more natural, organic distributions instead of pure random

use fastnoise_lite::{FastNoiseLite, NoiseType, FractalType};
use rand::{Rng, SeedableRng};
use rand_chacha::ChaCha8Rng;

use super::environment::*;
use super::entity_state::Position;

/// Mix seed with chunk coordinates for better RNG distribution
/// Handles negative coordinates properly and provides better per-chunk separation
fn mix_seed(base: u64, x: i32, z: i32) -> u64 {
    // Map to i64 first so negatives are handled predictably
    let x = x as i64 as u64;
    let z = z as i64 as u64;

    // Simple 2D hash mix (nothing fancy, just better than linear)
    let mut h = base ^ 0x9E3779B97F4A7C15;
    h ^= x.wrapping_mul(0xBF58476D1CE4E5B9);
    h = h.rotate_left(27);
    h ^= z.wrapping_mul(0x94D049BB133111EB);
    h
}

/// Noise-based procedural generation for environment objects
pub struct EnvironmentGenerator {
    seed: u64,
    chunk_size: f32,
    // Noise generators for different aspects of world generation
    tree_density_noise: FastNoiseLite,    // Controls where forests vs plains are
    tree_type_noise: FastNoiseLite,       // Controls oak vs pine distribution
    rock_density_noise: FastNoiseLite,    // Controls rocky areas
    bush_cluster_noise: FastNoiseLite,    // Controls bush clustering
}

impl EnvironmentGenerator {
    pub fn new(seed: u64, chunk_size: f32) -> Self {
        // Tree density noise - Large scale for biomes (forests vs plains)
        let mut tree_density_noise = FastNoiseLite::with_seed(seed as i32);
        tree_density_noise.set_noise_type(Some(NoiseType::Perlin));
        tree_density_noise.set_fractal_type(Some(FractalType::FBm));
        tree_density_noise.set_fractal_octaves(Some(3));
        tree_density_noise.set_frequency(Some(0.02)); // Low frequency = large features

        // Tree type noise - Medium scale for oak/pine variation
        let mut tree_type_noise = FastNoiseLite::with_seed((seed.wrapping_add(1000)) as i32);
        tree_type_noise.set_noise_type(Some(NoiseType::Perlin));
        tree_type_noise.set_frequency(Some(0.05));

        // Rock density noise - Medium scale for rocky areas
        let mut rock_density_noise = FastNoiseLite::with_seed((seed.wrapping_add(2000)) as i32);
        rock_density_noise.set_noise_type(Some(NoiseType::Perlin));
        rock_density_noise.set_fractal_type(Some(FractalType::FBm));
        rock_density_noise.set_fractal_octaves(Some(2));
        rock_density_noise.set_frequency(Some(0.03));

        // Bush cluster noise - Higher frequency for small clusters
        let mut bush_cluster_noise = FastNoiseLite::with_seed((seed.wrapping_add(3000)) as i32);
        bush_cluster_noise.set_noise_type(Some(NoiseType::Perlin));
        bush_cluster_noise.set_frequency(Some(0.08)); // Higher frequency = smaller clusters

        Self {
            seed,
            chunk_size,
            tree_density_noise,
            tree_type_noise,
            rock_density_noise,
            bush_cluster_noise,
        }
    }

    /// Generate objects for a specific chunk
    /// Uses deterministic RNG based on seed + chunk coords for consistency
    /// Uses noise for natural biome-like density variation
    pub fn generate_chunk(&self, chunk_coord: &ChunkCoord) -> Vec<EnvironmentObject> {
        // Create deterministic RNG from seed and chunk coords
        // Uses improved mixing for better distribution with negative coordinates
        let chunk_seed = mix_seed(self.seed, chunk_coord.x, chunk_coord.z);

        let mut rng = ChaCha8Rng::seed_from_u64(chunk_seed);
        let mut objects = Vec::new();

        // Calculate chunk world position (center of chunk for noise sampling)
        let chunk_x = chunk_coord.x as f32 * self.chunk_size;
        let chunk_z = chunk_coord.z as f32 * self.chunk_size;
        let chunk_center_x = chunk_x + self.chunk_size * 0.5;
        let chunk_center_z = chunk_z + self.chunk_size * 0.5;

        // Sample noise at chunk center to determine biome characteristics
        // Noise returns values in range [-1, 1], we map to [0, 1]
        let tree_density = (self.tree_density_noise.get_noise_2d(chunk_center_x, chunk_center_z) + 1.0) * 0.5;
        let rock_density = (self.rock_density_noise.get_noise_2d(chunk_center_x, chunk_center_z) + 1.0) * 0.5;
        let bush_density = (self.bush_cluster_noise.get_noise_2d(chunk_center_x, chunk_center_z) + 1.0) * 0.5;

        // Use noise to modulate object counts
        // Dense forest: 10-20 trees, Plains: 2-6 trees
        let tree_count = (2.0 + tree_density * 18.0) as u32;
        for i in 0..tree_count {
            let object = self.generate_tree(&mut rng, chunk_coord, i, chunk_x, chunk_z);
            objects.push(object);
        }

        // Rocky areas: 6-12 rocks, Normal: 0-3 rocks
        let rock_count = (rock_density * 12.0) as u32;
        for i in 0..rock_count {
            let object = self.generate_rock(&mut rng, chunk_coord, i, chunk_x, chunk_z);
            objects.push(object);
        }

        // Bush clusters: 15-25 bushes, Sparse: 3-8 bushes
        let bush_count = (3.0 + bush_density * 22.0) as u32;
        for i in 0..bush_count {
            let object = self.generate_bush(&mut rng, chunk_coord, i, chunk_x, chunk_z);
            objects.push(object);
        }

        // Grass is fairly uniform across all areas (10-30 per chunk)
        let grass_count = rng.gen_range(10..=30);
        for i in 0..grass_count {
            let object = self.generate_grass(&mut rng, chunk_coord, i, chunk_x, chunk_z);
            objects.push(object);
        }

        objects
    }

    fn generate_tree(&self, rng: &mut ChaCha8Rng, chunk: &ChunkCoord, index: u32, chunk_x: f32, chunk_z: f32) -> EnvironmentObject {
        let position = Position {
            x: chunk_x + rng.gen_range(0.0..self.chunk_size),
            y: 0.0, // Will be adjusted by terrain height on client
            z: chunk_z + rng.gen_range(0.0..self.chunk_size),
        };

        // Use noise to determine tree type (oak vs pine biomes)
        let tree_type_value = self.tree_type_noise.get_noise_2d(position.x, position.z);
        let asset_name = if tree_type_value > 0.0 {
            // Pine forest (higher noise values)
            if rng.gen_bool(0.5) { "Tree_Pine_01" } else { "Tree_Pine_02" }
        } else {
            // Oak forest (lower noise values)
            if rng.gen_bool(0.5) { "Tree_Oak_01" } else { "Tree_Oak_02" }
        }.to_string();

        EnvironmentObject {
            object_id: format!("tree_{}_{}_idx_{}", chunk.x, chunk.z, index),
            asset_name,
            position,
            rotation: Quaternion {
                x: 0.0,
                y: rng.gen_range(0.0..360.0),
                z: 0.0,
                w: 1.0,
            },
            scale: Scale::uniform(rng.gen_range(0.8..1.2)),
            object_type: EnvironmentObjectType::Tree,
            resource_type: ResourceType::Wood,
            resource_amount: rng.gen_range(3..=8),
            harvest_time: 3.0,
            is_harvested: false,
            harvested_at: None,
            respawn_time_seconds: Some(300), // 5 minutes
        }
    }

    fn generate_rock(&self, rng: &mut ChaCha8Rng, chunk: &ChunkCoord, index: u32, chunk_x: f32, chunk_z: f32) -> EnvironmentObject {
        let position = Position {
            x: chunk_x + rng.gen_range(0.0..self.chunk_size),
            y: 0.0,
            z: chunk_z + rng.gen_range(0.0..self.chunk_size),
        };

        let rock_variants = ["Rock_01", "Rock_02", "Rock_03"];
        let asset_name = rock_variants[rng.gen_range(0..rock_variants.len())].to_string();

        EnvironmentObject {
            object_id: format!("rock_{}_{}_idx_{}", chunk.x, chunk.z, index),
            asset_name,
            position,
            rotation: Quaternion {
                x: 0.0,
                y: rng.gen_range(0.0..360.0),
                z: 0.0,
                w: 1.0,
            },
            scale: Scale::uniform(rng.gen_range(0.9..1.3)),
            object_type: EnvironmentObjectType::Rock,
            resource_type: ResourceType::Stone,
            resource_amount: rng.gen_range(2..=6),
            harvest_time: 4.0,
            is_harvested: false,
            harvested_at: None,
            respawn_time_seconds: Some(600), // 10 minutes
        }
    }

    fn generate_bush(&self, rng: &mut ChaCha8Rng, chunk: &ChunkCoord, index: u32, chunk_x: f32, chunk_z: f32) -> EnvironmentObject {
        let position = Position {
            x: chunk_x + rng.gen_range(0.0..self.chunk_size),
            y: 0.0,
            z: chunk_z + rng.gen_range(0.0..self.chunk_size),
        };

        let bush_variants = ["Bush_01", "Bush_02"];
        let asset_name = bush_variants[rng.gen_range(0..bush_variants.len())].to_string();

        EnvironmentObject {
            object_id: format!("bush_{}_{}_idx_{}", chunk.x, chunk.z, index),
            asset_name,
            position,
            rotation: Quaternion {
                x: 0.0,
                y: rng.gen_range(0.0..360.0),
                z: 0.0,
                w: 1.0,
            },
            scale: Scale::uniform(rng.gen_range(0.7..1.1)),
            object_type: EnvironmentObjectType::Bush,
            resource_type: ResourceType::Berries,
            resource_amount: rng.gen_range(1..=4),
            harvest_time: 1.5,
            is_harvested: false,
            harvested_at: None,
            respawn_time_seconds: Some(180), // 3 minutes
        }
    }

    fn generate_grass(&self, rng: &mut ChaCha8Rng, chunk: &ChunkCoord, index: u32, chunk_x: f32, chunk_z: f32) -> EnvironmentObject {
        let position = Position {
            x: chunk_x + rng.gen_range(0.0..self.chunk_size),
            y: 0.0,
            z: chunk_z + rng.gen_range(0.0..self.chunk_size),
        };

        EnvironmentObject {
            object_id: format!("grass_{}_{}_idx_{}", chunk.x, chunk.z, index),
            asset_name: "Grass_Patch_01".to_string(),
            position,
            rotation: Quaternion::default(),
            scale: Scale::uniform(1.0),
            object_type: EnvironmentObjectType::Grass,
            resource_type: ResourceType::Herbs,
            resource_amount: 1,
            harvest_time: 0.5,
            is_harvested: false,
            harvested_at: None,
            respawn_time_seconds: Some(120), // 2 minutes
        }
    }

    /// Generate objects for all chunks in a radius around center
    pub fn generate_area(&self, center: &ChunkCoord, radius: i32) -> Vec<EnvironmentObject> {
        let mut all_objects = Vec::new();

        for dx in -radius..=radius {
            for dz in -radius..=radius {
                let chunk = ChunkCoord {
                    x: center.x + dx,
                    z: center.z + dz,
                };
                let objects = self.generate_chunk(&chunk);
                all_objects.extend(objects);
            }
        }

        all_objects
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_deterministic_generation() {
        let gen = EnvironmentGenerator::new(12345, 50.0);
        let chunk = ChunkCoord { x: 0, z: 0 };

        let objects1 = gen.generate_chunk(&chunk);
        let objects2 = gen.generate_chunk(&chunk);

        assert_eq!(objects1.len(), objects2.len());
        assert_eq!(objects1[0].object_id, objects2[0].object_id);
        assert_eq!(objects1[0].position.x, objects2[0].position.x);
    }

    #[test]
    fn test_different_chunks_different_objects() {
        let gen = EnvironmentGenerator::new(12345, 50.0);
        let chunk1 = ChunkCoord { x: 0, z: 0 };
        let chunk2 = ChunkCoord { x: 1, z: 0 };

        let objects1 = gen.generate_chunk(&chunk1);
        let objects2 = gen.generate_chunk(&chunk2);

        assert_ne!(objects1[0].object_id, objects2[0].object_id);
    }
}
