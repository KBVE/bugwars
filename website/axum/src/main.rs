mod core;
mod astro;
mod auth;

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

    // Spawn cache manager task
    let cache_manager = {
        let cache = jwt_cache.clone();
        tokio::spawn(async move {
            cache.run_manager().await;
        })
    };

    // Tokio
    let http = tokio::spawn(transports::https::serve(bus.clone(), jwt_cache.clone()));

    // Print
    info!("BugWars v{}", env!("CARGO_PKG_VERSION"));

     tokio::select! {
        _ = http => {},
        //  _ = tcp  => {},
        //  _ = grpc => {},
        _ = cache_manager => {
            error!("JWT cache manager task terminated unexpectedly");
        },
        _ = tokio::signal::ctrl_c() => {
            tracing::info!("shutdown signal received");
        }
    }

    Ok(())

}