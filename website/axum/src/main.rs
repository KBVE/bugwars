mod core;
mod astro;
mod auth;
mod game;

mod transports {
    pub mod https;
    pub mod tcp;
    pub mod graph;
}

use std::sync::Arc;
use std::time::Duration;
use core::{new_bus, run_app};
use axum::{
    response::IntoResponse,
    routing::get,
    Json, Router,
};
use tokio::net::TcpListener;
use tower::ServiceBuilder;
use tracing::{info, warn, error};
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};

#[cfg(feature = "jemalloc")]
mod allocator {
    #[cfg(not(target_env = "msvc"))]
    use tikv_jemallocator::Jemalloc;
    #[cfg(not(target_env = "msvc"))]
    #[global_allocator]
    static GLOBAL: Jemalloc = Jemalloc;
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {

    // ================
    //      Tracing
    // ================
    tracing_subscriber::registry()
        .with(
            tracing_subscriber::EnvFilter::try_from_default_env()
                .unwrap_or_else(|_| {
                    format!("{}=info,tower_http=debug", env!("CARGO_CRATE_NAME")).into()
                })
        )
        .with(tracing_subscriber::fmt::layer())
        .init();

    // Bus
    let (bus, rx) = new_bus(1024);
    tokio::spawn(run_app(rx));

    // JWT Cache - uses Supabase URL and anon key from environment
    let supabase_url = std::env::var("SUPABASE_URL")
        .unwrap_or_else(|_| {
            warn!("SUPABASE_URL not set, using local default (for development only)");
            "http://localhost:8000".to_string()
        });
    let supabase_anon_key = std::env::var("SUPABASE_ANON_KEY")
        .expect("SUPABASE_ANON_KEY must be set in environment");

    let jwt_cache = auth::jwt_cache::JwtCache::new(supabase_url, supabase_anon_key);
    info!("JWT cache initialized with Supabase verification");

    // Service role key initialization - validate at startup (kills app if invalid)
    if let Ok(service_key) = std::env::var("SUPABASE_SERVICE_ROLE_KEY") {
        auth::jwt_cache::init_service_role_key(service_key)
            .expect("CRITICAL: Failed to initialize service role key - this should never happen (key was already set). Terminating application for safety.");
        info!("Service role key initialized successfully - admin operations enabled (bypasses RLS)");
    } else {
        warn!("SUPABASE_SERVICE_ROLE_KEY not configured - admin operations will be disabled");
    }

    // Entity state manager for Unity game clients (players, NPCs, enemies, bosses)
    let entity_state = game::EntityStateManager::new(120); // 2 minute stale timeout
    info!("Entity state manager initialized for Unity clients");

    // Environment manager for server-authoritative environment objects (trees, rocks, bushes)
    let environment_manager = Arc::new(game::EnvironmentManager::new(
        50.0,  // chunk_size (matches Unity terrain chunks)
        3,     // view_distance_chunks (3 = 7x7 grid)
        10.0,  // max_harvest_range (anti-cheat validation)
    ));
    info!("Environment manager initialized");

    // Generate initial world environment objects
    let generator = game::EnvironmentGenerator::new(
        12345, // world seed (deterministic generation)
        50.0,  // chunk_size (must match environment_manager)
    );

    // Generate starting area around spawn (0, 0)
    let spawn_chunk = game::ChunkCoord { x: 0, z: 0 };
    let initial_objects = generator.generate_area(&spawn_chunk, 5); // 11x11 chunks
    info!("Generated {} initial environment objects", initial_objects.len());

    // Add objects to manager
    for object in initial_objects {
        environment_manager.add_object(object);
    }

    // Start respawn background task
    let env_manager_clone = environment_manager.clone();
    tokio::spawn(async move {
        info!("Starting environment respawn task");
        env_manager_clone.start_respawn_task().await;
    });

    // Spawn cache manager task
    let cache_manager = {
        let cache = jwt_cache.clone();
        tokio::spawn(async move {
            cache.run_manager().await;
        })
    };

    // Spawn entity state cleanup task
    let entity_cleanup = {
        let entity_mgr = entity_state.clone();
        tokio::spawn(async move {
            entity_mgr.run_cleanup_task(60).await; // Cleanup every 60 seconds
        })
    };

    // Tokio
    let http = tokio::spawn(transports::https::serve(
        bus.clone(),
        jwt_cache.clone(),
        entity_state.clone(),
        environment_manager.clone(),
    ));

    // Print
    info!("BugWars v{}", env!("CARGO_PKG_VERSION"));

     tokio::select! {
        _ = http => {},
        //  _ = tcp  => {},
        //  _ = grpc => {},
        _ = cache_manager => {
            error!("JWT cache manager task terminated unexpectedly");
        },
        _ = entity_cleanup => {
            error!("Entity state cleanup task terminated unexpectedly");
        },
        _ = tokio::signal::ctrl_c() => {
            tracing::info!("shutdown signal received");
        }
    }

    Ok(())

}