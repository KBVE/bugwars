use anyhow::Result;
use std::{net::SocketAddr, time::Duration};

use axum::{
    extract::{
        State,
        ws::{Message, WebSocket, WebSocketUpgrade},
    },
    http::{Request, StatusCode},
    response::IntoResponse,
    routing::{get, post},
    Json, Router,
};
use futures_util::StreamExt;
use serde::{Deserialize, Serialize};
use tokio::net::TcpListener;
use tower::ServiceBuilder;
use tower_http::{
    compression::CompressionLayer,
    cors::CorsLayer,
    limit::RequestBodyLimitLayer,
    trace::{DefaultMakeSpan, TraceLayer},
};
use tracing::{error, info, Level};

use crate::core::{AppBus, AppCmd};
use crate::auth::{extract_auth_user_from_parts, AuthUser, jwt_cache::JwtCache};

/* ------------------------------- serve() -------------------------------- */

pub async fn serve(bus: AppBus, jwt_cache: JwtCache) -> Result<()> {
    // Env-configurable bind
    let host = std::env::var("HTTP_HOST").unwrap_or_else(|_| "0.0.0.0".into());
    let port: u16 = std::env::var("HTTP_PORT").ok().and_then(|s| s.parse().ok()).unwrap_or(4321);
    let addr: SocketAddr = format!("{host}:{port}").parse()?;

    // Socket tuning (nodelay, keepalive, reuseaddr)
    let listener = tuned_listener(addr)?;

    info!("HTTP/WS listening on http://{addr}");

    // Build app
    let app = router(bus, jwt_cache);

    // Axum/Hyper tuning
    axum::serve(listener, app)
        .with_graceful_shutdown(shutdown_signal())
        .await?;

    Ok(())
}


/* ------------------------------- router() ------------------------------- */

fn router(bus: crate::core::AppBus, jwt_cache: JwtCache) -> axum::Router {
    // bring trait for .and() on compression predicates
    use tower_http::compression::Predicate as _;

    let max_inflight: usize = (num_cpus::get().max(1) * 1024) as usize;

    // Static asset configuration
    let static_config = crate::astro::StaticConfig::default();

    // Compress only when default rules allow AND body > 1 KiB
    // NOTE: Static assets are pre-compressed, so this only applies to dynamic routes
    let compression = tower_http::compression::CompressionLayer::new().compress_when(
        tower_http::compression::predicate::DefaultPredicate::new()
            .and(tower_http::compression::predicate::SizeAbove::new(1024)),
    );

    // Build middleware stack with HandleErrorLayer for fallible services
    let middleware = tower::ServiceBuilder::new()
        // Trace layer (outermost) - must be last to avoid Default bound issues
        .layer(
            tower_http::trace::TraceLayer::new_for_http().make_span_with(
                tower_http::trace::DefaultMakeSpan::new().level(tracing::Level::INFO),
            ),
        )
        // CORS layer
        .layer(tower_http::cors::CorsLayer::permissive())
        // Compression layer (skipped for precompressed static assets)
        .layer(compression)
        // Handle errors from fallible middleware BEFORE applying them
        .layer(axum::error_handling::HandleErrorLayer::new(
            |err: tower::BoxError| async move {
                if err.is::<tower::timeout::error::Elapsed>() {
                    (axum::http::StatusCode::REQUEST_TIMEOUT, "request timed out")
                } else if err.is::<tower::load_shed::error::Overloaded>() {
                    (axum::http::StatusCode::SERVICE_UNAVAILABLE, "service overloaded")
                } else {
                    tracing::warn!(error = %err, "middleware error");
                    (axum::http::StatusCode::INTERNAL_SERVER_ERROR, "internal server error")
                }
            },
        ))
        // Fallible middleware layers (innermost)
        .timeout(std::time::Duration::from_secs(10))
        .concurrency_limit(max_inflight)
        .load_shed()
        // Request body limit (after fallible layers so it doesn't need Default)
        .layer(tower_http::limit::RequestBodyLimitLayer::new(1 * 1024 * 1024));

    // Build router with priority:
    // 1. Static assets (highest priority, no state, precompressed)
    // 2. Dynamic API routes (with state)
    // 3. Fallback to Askama templates (future)
    let static_router = crate::astro::build_static_router(&static_config);

    // Dynamic routes with state
    // Note: "/" is handled by static index.html from Astro
    let dynamic_router = axum::Router::new()
        .route("/health", axum::routing::get(health))
        .route("/echo", axum::routing::post(echo))
        .route("/ws", axum::routing::get(ws_upgrade))
        // Optional: Add dynamic Askama routes
        // .route("/dashboard", axum::routing::get(crate::astro::askama::private_dashboard))
        // .route("/page/*path", axum::routing::get(crate::astro::askama::dynamic_page_handler))
        .with_state((bus, jwt_cache));

    // Merge static and dynamic routers, then apply middleware
    static_router
        .merge(dynamic_router)
        // Optional: Add fallback for 404s or catch-all dynamic rendering
        // .fallback(crate::astro::askama::fallback_handler)
        .layer(middleware)
}



/* ------------------------------- Handlers ------------------------------- */

async fn root() -> impl IntoResponse {
    "kbve-staryo online"
}

async fn health() -> impl IntoResponse {
    "OK"
}

#[derive(Deserialize)]
struct EchoIn {
    name: String,
}

#[derive(Serialize)]
struct EchoOut {
    message: String,
}

async fn echo(State((bus, _)): State<(AppBus, JwtCache)>, Json(input): Json<EchoIn>) -> impl IntoResponse {
    use tokio::sync::oneshot;
    let (tx, rx) = oneshot::channel();
    let _ = bus.tx.send(AppCmd::Hello { name: input.name, reply: tx }).await;
    let message = rx.await.unwrap_or_else(|_| "unavailable".into());
    Json(EchoOut { message })
}

/* ---------------------------- WebSocket path ---------------------------- */

async fn ws_upgrade(
    ws: WebSocketUpgrade,
    State((bus, jwt_cache)): State<(AppBus, JwtCache)>,
    req: Request<axum::body::Body>,
) -> impl IntoResponse {
    use crate::auth::jwt_cache::AuthCacheError;

    // Extract JWT token from Authorization header
    let (parts, _) = req.into_parts();
    let token = match extract_token_from_header(&parts.headers) {
        Ok(t) => t,
        Err(e) => {
            info!("WebSocket connection rejected: {}", e);
            return (StatusCode::UNAUTHORIZED, format!("Missing or invalid auth token: {}", e)).into_response();
        }
    };

    // Verify JWT using cache (fast path) or Supabase API (slow path)
    let token_info = match jwt_cache.verify_and_cache(&token).await {
        Ok(info) => {
            info!(
                user_id = %info.user_id,
                email = ?info.email,
                role = %info.role,
                "WebSocket connection authenticated via JWT cache"
            );
            info
        }
        Err(AuthCacheError::InvalidToken(msg)) => {
            info!(error = %msg, "WebSocket connection rejected: invalid token");
            return (StatusCode::UNAUTHORIZED, format!("Invalid token: {}", msg)).into_response();
        }
        Err(e) => {
            error!(error = %e, "WebSocket JWT verification failed");
            return (StatusCode::INTERNAL_SERVER_ERROR, "Authentication service error").into_response();
        }
    };

    // Check if token is expired
    if token_info.is_expired() {
        info!(user_id = %token_info.user_id, "WebSocket connection rejected: token expired");
        return (StatusCode::UNAUTHORIZED, "Token expired").into_response();
    }

    // Create AuthUser from token info
    let auth_user = AuthUser {
        claims: crate::auth::Claims {
            sub: token_info.user_id.clone(),
            iat: 0, // Not needed for WebSocket session
            exp: token_info.expires_at,
            iss: "supabase".to_string(),
            role: token_info.role.clone(),
            email: token_info.email.clone(),
            phone: None,
            app_metadata: None,
            user_metadata: None,
        },
        token: token.clone(),
    };

    info!(
        user_id = %auth_user.user_id(),
        role = %auth_user.role(),
        "WebSocket upgrade successful"
    );

    // Set sizes to defend allocations; tune to your needs
    ws.max_message_size(1 << 20) // 1 MiB per message
        .max_frame_size(1 << 20)
        .on_upgrade(move |socket| ws_loop(socket, bus, auth_user))
}

fn extract_token_from_header(headers: &http::HeaderMap) -> Result<String, String> {
    let auth_header = headers
        .get(http::header::AUTHORIZATION)
        .ok_or_else(|| "Missing Authorization header".to_string())?;

    let auth_str = auth_header
        .to_str()
        .map_err(|_| "Invalid Authorization header".to_string())?;

    if !auth_str.starts_with("Bearer ") {
        return Err("Authorization header must start with 'Bearer '".to_string());
    }

    Ok(auth_str[7..].to_string())
}

async fn ws_loop(mut socket: WebSocket, bus: AppBus, auth_user: AuthUser) {
    use tokio::sync::oneshot;

    // Send welcome message with user info
    let welcome_msg = format!(
        "{{\"type\":\"connected\",\"user_id\":\"{}\",\"role\":\"{}\"}}",
        auth_user.user_id(),
        auth_user.role()
    );
    let _ = socket.send(Message::Text(welcome_msg.into())).await;

    info!("WebSocket session started for user: {}", auth_user.user_id());

    while let Some(Ok(msg)) = socket.next().await {
        match msg {
            Message::Text(text) => {
                // Parse incoming JSON messages
                // Expected format: {"type": "command", "data": "..."}
                let text_str = text.to_string();

                // For now, echo back with user context
                let response = format!(
                    "{{\"type\":\"echo\",\"user_id\":\"{}\",\"message\":{}}}",
                    auth_user.user_id(),
                    serde_json::to_string(&text_str).unwrap_or_else(|_| "\"invalid\"".to_string())
                );
                let _ = socket.send(Message::Text(response.into())).await;

                // Optional: Send to AppBus for processing
                // let (tx, rx) = oneshot::channel();
                // if bus.tx.send(AppCmd::Chat { room: auth_user.user_id().to_string(), text: text_str }).await.is_err() {
                //     let _ = socket.send(Message::Text("{\"type\":\"error\",\"message\":\"busy\"}".into())).await;
                // }
            }
            Message::Binary(bytes) => {
                // Zero-copy echo for binary data
                let _ = socket.send(Message::Binary(bytes)).await;
            }
            Message::Ping(p) => { let _ = socket.send(Message::Pong(p)).await; }
            Message::Close(_) => {
                info!("WebSocket connection closed for user: {}", auth_user.user_id());
                break;
            }
            _ => {}
        }
    }

    info!("WebSocket session ended for user: {}", auth_user.user_id());
}

/* ----------------------------- Socket tuning ---------------------------- */

fn tuned_listener(addr: SocketAddr) -> Result<TcpListener> {
    use socket2::{Socket, Domain, Type, Protocol};
    let domain = match addr { SocketAddr::V4(_) => Domain::IPV4, SocketAddr::V6(_) => Domain::IPV6 };
    let socket = Socket::new(domain, Type::STREAM, Some(Protocol::TCP))?;

    // Reuseaddr to speed restarts & readiness flips
    socket.set_reuse_address(true)?;
    // Keep connections alive (helps load balancers too)
    socket.set_keepalive(true)?;
    // Linux/Unix keepalive intervals (best effort)
    #[cfg(any(target_os = "linux", target_os = "android"))]
    {
        use socket2::TcpKeepalive;
        let ka = TcpKeepalive::new().with_time(Duration::from_secs(30)).with_interval(Duration::from_secs(10));
        let _ = socket.set_tcp_keepalive(&ka);
    }

    // Bind + listen
    socket.bind(&addr.into())?;
    socket.listen(1024)?;

    // Convert to Tokio listener
    let std_listener = std::net::TcpListener::from(socket);
    std_listener.set_nonblocking(true)?;
    // Note: TCP_NODELAY is set per-connection by hyper/axum automatically
    Ok(TcpListener::from_std(std_listener)?)
}

/* ----------------------------- Shutdown hook ---------------------------- */

async fn shutdown_signal() {
    let _ = tokio::signal::ctrl_c().await;
}
