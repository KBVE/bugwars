// src/auth/jwt_cache.rs
// JWT cache using DashMap for concurrent access across HTTP/TCP/gRPC
use dashmap::DashMap;
use std::sync::Arc;
use std::time::{Duration, Instant};
use tokio::time;
use tracing::{debug, info, warn};

const MAX_CACHE_SIZE: usize = 10_000; // Maximum number of cached tokens
const CLEANUP_INTERVAL: Duration = Duration::from_secs(60); // Cleanup every 60 seconds
const TOKEN_GRACE_PERIOD: i64 = 300; // 5 minutes grace period before expiry

#[derive(Debug, Clone)]
pub struct TokenInfo {
    pub user_id: String,
    pub email: Option<String>,
    pub role: String,
    pub expires_at: i64, // Unix timestamp
    pub verified_at: Instant,
}

impl TokenInfo {
    pub fn is_expired(&self) -> bool {
        let now = chrono::Utc::now().timestamp();
        now >= self.expires_at
    }

    pub fn is_near_expiry(&self) -> bool {
        let now = chrono::Utc::now().timestamp();
        (self.expires_at - now) <= TOKEN_GRACE_PERIOD
    }
}

#[derive(Clone)]
pub struct JwtCache {
    tokens: Arc<DashMap<String, TokenInfo>>,
    supabase_url: String,
    http_client: reqwest::Client,
}

impl JwtCache {
    pub fn new(supabase_url: String) -> Self {
        info!("Initializing JWT cache with Supabase URL: {}", supabase_url);
        Self {
            tokens: Arc::new(DashMap::new()),
            supabase_url,
            http_client: reqwest::Client::builder()
                .timeout(Duration::from_secs(5))
                .build()
                .expect("Failed to create HTTP client"),
        }
    }

    /// Get a token from the cache if it exists and is not expired
    pub fn get(&self, token: &str) -> Option<TokenInfo> {
        if let Some(entry) = self.tokens.get(token) {
            let info = entry.value().clone();
            if !info.is_expired() {
                debug!(
                    user_id = %info.user_id,
                    expires_in = %(info.expires_at - chrono::Utc::now().timestamp()),
                    "JWT cache hit"
                );
                return Some(info);
            } else {
                debug!(user_id = %info.user_id, "JWT cache hit but token expired");
                // Remove expired token
                drop(entry);
                self.tokens.remove(token);
            }
        }
        debug!("JWT cache miss");
        None
    }

    /// Verify a token against Supabase API and cache the result
    pub async fn verify_and_cache(&self, token: &str) -> Result<TokenInfo, AuthCacheError> {
        // First check cache (fast path)
        if let Some(info) = self.get(token) {
            debug!(
                user_id = %info.user_id,
                cache_hit = true,
                "JWT verified from cache (fast path)"
            );
            return Ok(info);
        }

        // Cache miss - verify with Supabase API (slow path)
        info!(
            cache_hit = false,
            cache_size = self.tokens.len(),
            "JWT cache miss, verifying with Supabase API (slow path)"
        );
        let api_start = std::time::Instant::now();
        let token_info = self.verify_with_supabase(token).await?;
        let api_duration = api_start.elapsed();

        // Cache the verified token
        self.insert(token.to_string(), token_info.clone());

        info!(
            user_id = %token_info.user_id,
            supabase_api_ms = %api_duration.as_millis(),
            "JWT verified via Supabase API and cached"
        );

        Ok(token_info)
    }

    /// Verify token by calling Supabase /auth/v1/user endpoint
    async fn verify_with_supabase(&self, token: &str) -> Result<TokenInfo, AuthCacheError> {
        let url = format!("{}/auth/v1/user", self.supabase_url);

        debug!(
            url = %url,
            token_preview = %&token[..token.len().min(20)],
            "Calling Supabase user verification API"
        );

        let request_start = std::time::Instant::now();
        let response = self.http_client
            .get(&url)
            .bearer_auth(token)
            .send()
            .await
            .map_err(|e| {
                let request_duration = request_start.elapsed();
                warn!(
                    error = %e,
                    request_ms = %request_duration.as_millis(),
                    "Failed to call Supabase API (network/timeout error)"
                );
                AuthCacheError::SupabaseApiError(e.to_string())
            })?;

        let request_duration = request_start.elapsed();
        let status = response.status();

        if !status.is_success() {
            let body = response.text().await.unwrap_or_default();
            warn!(
                status = %status,
                body = %body,
                request_ms = %request_duration.as_millis(),
                "Supabase API returned error response"
            );
            return Err(AuthCacheError::InvalidToken(format!("Status: {}, Body: {}", status, body)));
        }

        debug!(
            status = %status,
            request_ms = %request_duration.as_millis(),
            "Supabase API responded successfully"
        );

        let user_data: serde_json::Value = response.json().await
            .map_err(|e| AuthCacheError::InvalidResponse(e.to_string()))?;

        // Extract user info from response
        let user_id = user_data["id"]
            .as_str()
            .ok_or_else(|| AuthCacheError::InvalidResponse("Missing user id".to_string()))?
            .to_string();

        let email = user_data["email"].as_str().map(|s| s.to_string());
        let role = user_data["role"].as_str().unwrap_or("authenticated").to_string();

        // Parse JWT to get expiry time (we still need this for cache management)
        use jsonwebtoken::{decode, Algorithm, DecodingKey, Validation};
        let mut validation = Validation::new(Algorithm::HS256);
        validation.validate_exp = false; // Don't validate expiry here, Supabase already did
        validation.insecure_disable_signature_validation(); // We trust Supabase's response

        let token_data = decode::<serde_json::Value>(
            token,
            &DecodingKey::from_secret(&[]), // Empty secret since we disabled validation
            &validation,
        ).map_err(|e| AuthCacheError::InvalidToken(e.to_string()))?;

        let expires_at = token_data.claims["exp"]
            .as_i64()
            .ok_or_else(|| AuthCacheError::InvalidToken("Missing exp claim".to_string()))?;

        let expires_in = expires_at - chrono::Utc::now().timestamp();
        info!(
            user_id = %user_id,
            email = ?email,
            role = %role,
            expires_at = %expires_at,
            expires_in_seconds = %expires_in,
            "JWT verified successfully via Supabase API, token parsed"
        );

        Ok(TokenInfo {
            user_id,
            email,
            role,
            expires_at,
            verified_at: Instant::now(),
        })
    }

    /// Insert a token into the cache
    fn insert(&self, token: String, info: TokenInfo) {
        let cache_size = self.tokens.len();

        // Check size limit before inserting
        if cache_size >= MAX_CACHE_SIZE {
            warn!(
                current_size = cache_size,
                max_size = MAX_CACHE_SIZE,
                user_id = %info.user_id,
                "JWT cache at max size, evicting oldest entries before insert"
            );
            self.evict_oldest(MAX_CACHE_SIZE / 10); // Evict 10% of cache
        }

        debug!(
            user_id = %info.user_id,
            cache_size_before = cache_size,
            cache_size_after = %(cache_size + 1).min(MAX_CACHE_SIZE),
            "Inserting JWT into cache"
        );
        self.tokens.insert(token, info);
    }

    /// Evict the oldest N entries from the cache (LRU)
    fn evict_oldest(&self, count: usize) {
        use rayon::prelude::*;

        let eviction_start = std::time::Instant::now();
        let cache_size_before = self.tokens.len();

        debug!(
            count_to_evict = count,
            cache_size = cache_size_before,
            "Starting LRU eviction"
        );

        // Parallel collect entries with their timestamps
        let mut entries: Vec<_> = self.tokens
            .par_iter()
            .map(|entry| (entry.key().clone(), entry.value().verified_at))
            .collect();

        // Sort by verified_at (oldest first)
        entries.sort_by_key(|(_, verified_at)| *verified_at);

        // Remove oldest N entries in parallel
        let removed: usize = entries
            .into_par_iter()
            .take(count)
            .map(|(token, _)| {
                if self.tokens.remove(&token).is_some() {
                    1
                } else {
                    0
                }
            })
            .sum();

        let eviction_duration = eviction_start.elapsed();
        info!(
            removed = removed,
            cache_size_before = cache_size_before,
            cache_size_after = self.tokens.len(),
            eviction_ms = %eviction_duration.as_millis(),
            "Evicted oldest JWT cache entries (LRU)"
        );
    }

    /// Remove expired tokens from the cache
    fn cleanup_expired(&self) {
        use rayon::prelude::*;

        let cleanup_start = std::time::Instant::now();
        let now = chrono::Utc::now().timestamp();
        let cache_size_before = self.tokens.len();

        debug!(
            cache_size = cache_size_before,
            "Starting expired token cleanup"
        );

        // Parallel identify expired tokens
        let expired_tokens: Vec<String> = self.tokens
            .par_iter()
            .filter_map(|entry| {
                if entry.value().expires_at <= now {
                    Some(entry.key().clone())
                } else {
                    None
                }
            })
            .collect();

        // Remove expired tokens (this is sequential but fast since we've already identified them)
        let removed = expired_tokens.len();
        for token in expired_tokens {
            self.tokens.remove(&token);
        }

        let cleanup_duration = cleanup_start.elapsed();

        if removed > 0 {
            info!(
                removed = removed,
                cache_size_before = cache_size_before,
                cache_size_after = self.tokens.len(),
                cleanup_ms = %cleanup_duration.as_millis(),
                "Cleaned up expired JWT cache entries"
            );
        } else {
            debug!(
                cache_size = self.tokens.len(),
                cleanup_ms = %cleanup_duration.as_millis(),
                "JWT cache cleanup: no expired entries found"
            );
        }
    }

    /// Get current cache size
    pub fn size(&self) -> usize {
        self.tokens.len()
    }

    /// Run the cache manager task
    /// This should be spawned in tokio::select! in main
    pub async fn run_manager(self) {
        info!("Starting JWT cache manager");
        let mut interval = time::interval(CLEANUP_INTERVAL);

        loop {
            interval.tick().await;

            // 1. Remove expired tokens
            self.cleanup_expired();

            // 2. Check size and evict if needed
            if self.tokens.len() > MAX_CACHE_SIZE {
                warn!(
                    current_size = self.tokens.len(),
                    max_size = MAX_CACHE_SIZE,
                    "JWT cache exceeded max size"
                );
                self.evict_oldest(self.tokens.len() - MAX_CACHE_SIZE);
            }

            debug!(
                cache_size = self.tokens.len(),
                max_size = MAX_CACHE_SIZE,
                "JWT cache manager tick"
            );
        }
    }
}

#[derive(Debug, thiserror::Error)]
pub enum AuthCacheError {
    #[error("Supabase API error: {0}")]
    SupabaseApiError(String),

    #[error("Invalid token: {0}")]
    InvalidToken(String),

    #[error("Invalid response from Supabase: {0}")]
    InvalidResponse(String),
}
