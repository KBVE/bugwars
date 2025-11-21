use anyhow::Result;
use std::{net::SocketAddr, time::Duration};

use axum::{
    extract::{
        Query, State,
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
use tracing::{debug, error, info, warn, Level};

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

/// Query parameters for WebSocket authentication
/// Supports token via query string for browser WebSocket API
#[derive(Deserialize)]
struct WsQuery {
    token: Option<String>,
}

async fn ws_upgrade(
    ws: WebSocketUpgrade,
    State((bus, jwt_cache)): State<(AppBus, JwtCache)>,
    Query(query): Query<WsQuery>,
    req: Request<axum::body::Body>,
) -> impl IntoResponse {
    use crate::auth::jwt_cache::AuthCacheError;

    // Log incoming WebSocket upgrade request
    let (parts, _) = req.into_parts();
    info!(
        method = %parts.method,
        uri = %parts.uri,
        remote_addr = ?parts.extensions.get::<axum::extract::ConnectInfo<std::net::SocketAddr>>(),
        "WebSocket upgrade request received"
    );

    // Extract JWT token from Authorization header OR query parameter
    let token = match extract_token_from_header(&parts.headers) {
        Ok(t) => {
            debug!(token_len = t.len(), "JWT token extracted from Authorization header");
            t
        }
        Err(header_err) => {
            // Fallback: Try to extract from query parameter (for browser WebSocket API)
            if let Some(t) = query.token {
                debug!(token_len = t.len(), "JWT token extracted from query parameter");
                t
            } else {
                warn!(
                    header_error = %header_err,
                    "WebSocket connection rejected: no valid auth token in header or query"
                );
                return (StatusCode::UNAUTHORIZED, "Missing or invalid auth token").into_response();
            }
        }
    };

    // Verify JWT using cache (fast path) or Supabase API (slow path)
    debug!("Starting JWT verification for WebSocket connection");
    let verification_start = std::time::Instant::now();
    let token_info = match jwt_cache.verify_and_cache(&token).await {
        Ok(info) => {
            let verification_duration = verification_start.elapsed();
            info!(
                user_id = %info.user_id,
                email = ?info.email,
                role = %info.role,
                verification_ms = %verification_duration.as_millis(),
                expires_in_seconds = %(info.expires_at - chrono::Utc::now().timestamp()),
                "WebSocket connection authenticated successfully"
            );
            info
        }
        Err(AuthCacheError::InvalidToken(msg)) => {
            let verification_duration = verification_start.elapsed();
            warn!(
                error = %msg,
                verification_ms = %verification_duration.as_millis(),
                "WebSocket connection rejected: invalid token"
            );
            return (StatusCode::UNAUTHORIZED, format!("Invalid token: {}", msg)).into_response();
        }
        Err(e) => {
            let verification_duration = verification_start.elapsed();
            error!(
                error = %e,
                verification_ms = %verification_duration.as_millis(),
                "WebSocket JWT verification failed: internal error"
            );
            return (StatusCode::INTERNAL_SERVER_ERROR, "Authentication service error").into_response();
        }
    };

    // Check if token is expired
    if token_info.is_expired() {
        warn!(
            user_id = %token_info.user_id,
            expires_at = %token_info.expires_at,
            "WebSocket connection rejected: token expired"
        );
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
        email = ?auth_user.email(),
        "WebSocket upgrade successful, starting connection loop"
    );

    // Set sizes to defend allocations; tune to your needs
    ws.max_message_size(1 << 20) // 1 MiB per message
        .max_frame_size(1 << 20)
        .on_upgrade(move |socket| {
            debug!(user_id = %auth_user.user_id(), "WebSocket connection upgraded, entering message loop");
            ws_loop(socket, bus, auth_user)
        })
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

async fn ws_loop(mut socket: WebSocket, _bus: AppBus, auth_user: AuthUser) {
    use tokio::sync::oneshot;

    let user_id = auth_user.user_id();
    info!(user_id = %user_id, "WebSocket session starting, sending welcome message");

    // Send welcome message with user info
    let welcome_msg = format!(
        "{{\"type\":\"connected\",\"user_id\":\"{}\",\"role\":\"{}\"}}",
        user_id,
        auth_user.role()
    );
    if let Err(e) = socket.send(Message::Text(welcome_msg.into())).await {
        error!(user_id = %user_id, error = %e, "Failed to send welcome message");
        return;
    }

    info!(user_id = %user_id, "WebSocket session active, listening for messages");

    let mut message_count = 0u64;
    while let Some(result) = socket.next().await {
        match result {
            Ok(msg) => {
                message_count += 1;
                match msg {
                    Message::Text(text) => {
                        let text_str = text.to_string();
                        debug!(
                            user_id = %user_id,
                            message_num = message_count,
                            text_len = text_str.len(),
                            preview = %text_str.chars().take(50).collect::<String>(),
                            "Received text message"
                        );

                        // Check if this is an application-level ping/pong
                        if text_str.contains("\"type\":\"ping\"") {
                            debug!(user_id = %user_id, "Received application-level ping, sending pong");
                            let pong_response = format!("{{\"type\":\"pong\",\"timestamp\":{}}}",
                                std::time::SystemTime::now()
                                    .duration_since(std::time::UNIX_EPOCH)
                                    .unwrap_or_default()
                                    .as_secs()
                            );
                            if let Err(e) = socket.send(Message::Text(pong_response.into())).await {
                                error!(user_id = %user_id, error = %e, "Failed to send pong response");
                                break;
                            }
                            continue;
                        }

                        // For now, echo back with user context
                        let response = format!(
                            "{{\"type\":\"echo\",\"user_id\":\"{}\",\"message\":{}}}",
                            user_id,
                            serde_json::to_string(&text_str).unwrap_or_else(|_| "\"invalid\"".to_string())
                        );

                        if let Err(e) = socket.send(Message::Text(response.into())).await {
                            error!(user_id = %user_id, error = %e, "Failed to send text response");
                            break;
                        }

                        // Optional: Send to AppBus for processing
                        // let (tx, rx) = oneshot::channel();
                        // if bus.tx.send(AppCmd::Chat { room: user_id.to_string(), text: text_str }).await.is_err() {
                        //     let _ = socket.send(Message::Text("{\"type\":\"error\",\"message\":\"busy\"}".into())).await;
                        // }
                    }
                    Message::Binary(bytes) => {
                        debug!(
                            user_id = %user_id,
                            message_num = message_count,
                            bytes_len = bytes.len(),
                            "Received binary message"
                        );
                        // Zero-copy echo for binary data
                        if let Err(e) = socket.send(Message::Binary(bytes)).await {
                            error!(user_id = %user_id, error = %e, "Failed to send binary response");
                            break;
                        }
                    }
                    Message::Ping(p) => {
                        debug!(user_id = %user_id, "Received Ping, sending Pong");
                        if let Err(e) = socket.send(Message::Pong(p)).await {
                            error!(user_id = %user_id, error = %e, "Failed to send Pong response");
                            break;
                        }
                    }
                    Message::Close(frame) => {
                        let close_info = frame.as_ref().map(|f| {
                            (f.code, f.reason.to_string())
                        });
                        info!(
                            user_id = %user_id,
                            close_code = ?close_info.as_ref().map(|(code, _)| code),
                            close_reason = ?close_info.as_ref().map(|(_, reason)| reason),
                            messages_exchanged = message_count,
                            "WebSocket connection closed by client"
                        );
                        break;
                    }
                    _ => {
                        debug!(user_id = %user_id, "Received other WebSocket message type");
                    }
                }
            }
            Err(e) => {
                error!(
                    user_id = %user_id,
                    error = %e,
                    messages_exchanged = message_count,
                    "WebSocket error, closing connection"
                );
                break;
            }
        }
    }

    info!(
        user_id = %user_id,
        total_messages = message_count,
        "WebSocket session ended"
    );
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
