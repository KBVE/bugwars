// src/auth/mod.rs
//! JWT authentication module for Supabase tokens
//!
//! This module provides JWT validation for Supabase-issued tokens.
//! Tokens are validated using HS256 (HMAC with SHA-256) algorithm.

pub mod jwt_cache;

use axum::{
    extract::{Request, FromRequestParts},
    http::{StatusCode, header::{AUTHORIZATION, HeaderValue}},
    response::{IntoResponse, Response},
    RequestPartsExt,
};
use axum::body::Body;
use axum::middleware::Next;
use async_trait::async_trait;
use http::request::Parts;
use jsonwebtoken::{decode, decode_header, DecodingKey, Validation, Algorithm, TokenData};
use serde::{Deserialize, Serialize};
use std::fmt;
use tracing::{debug, warn};

/// Supabase JWT configuration
/// These values should match your Supabase instance
pub struct SupabaseConfig {
    pub jwt_secret: String,
    pub issuer: String,
}

impl Default for SupabaseConfig {
    fn default() -> Self {
        Self {
            // This is the SUPABASE_ANON_KEY from your frontend
            // In production, load this from environment variables
            jwt_secret: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoiYW5vbiIsImlzcyI6InN1cGFiYXNlIiwiaWF0IjoxNzU1NDAzMjAwLCJleHAiOjE5MTMxNjk2MDB9.oietJI22ZytbghFywvdYMSJp7rcsBdBYbcciJxeGWrg".to_string(),
            issuer: "supabase".to_string(),
        }
    }
}

/// Standard JWT claims from Supabase
/// Reference: https://supabase.com/docs/guides/auth/jwts
#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct Claims {
    /// Subject (user ID)
    pub sub: String,

    /// Issued at (Unix timestamp)
    pub iat: i64,

    /// Expiration time (Unix timestamp)
    pub exp: i64,

    /// Issuer (should be "supabase")
    pub iss: String,

    /// Role (e.g., "authenticated", "anon")
    pub role: String,

    /// Email (optional)
    pub email: Option<String>,

    /// Phone (optional)
    pub phone: Option<String>,

    /// App metadata (optional)
    pub app_metadata: Option<serde_json::Value>,

    /// User metadata (optional)
    pub user_metadata: Option<serde_json::Value>,
}

impl fmt::Display for Claims {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "User(id={}, role={}, email={})",
            self.sub,
            self.role,
            self.email.as_deref().unwrap_or("none")
        )
    }
}

/// Authenticated user extracted from JWT
#[derive(Debug, Clone)]
pub struct AuthUser {
    pub claims: Claims,
    pub token: String,
}

impl AuthUser {
    /// Get the user ID
    pub fn user_id(&self) -> &str {
        &self.claims.sub
    }

    /// Get the user's role
    pub fn role(&self) -> &str {
        &self.claims.role
    }

    /// Get the user's email (if available)
    pub fn email(&self) -> Option<&str> {
        self.claims.email.as_deref()
    }

    /// Check if the token is expired
    pub fn is_expired(&self) -> bool {
        let now = chrono::Utc::now().timestamp();
        self.claims.exp < now
    }
}

/// Error types for authentication
#[derive(Debug)]
pub enum AuthError {
    InvalidToken,
    MissingToken,
    ExpiredToken,
    InvalidIssuer,
    DecodeError(String),
}

impl fmt::Display for AuthError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AuthError::InvalidToken => write!(f, "Invalid token"),
            AuthError::MissingToken => write!(f, "Missing authorization token"),
            AuthError::ExpiredToken => write!(f, "Token has expired"),
            AuthError::InvalidIssuer => write!(f, "Invalid token issuer"),
            AuthError::DecodeError(msg) => write!(f, "Token decode error: {}", msg),
        }
    }
}

impl IntoResponse for AuthError {
    fn into_response(self) -> Response {
        let (status, message) = match self {
            AuthError::MissingToken => (StatusCode::UNAUTHORIZED, "Missing authorization token"),
            AuthError::InvalidToken => (StatusCode::UNAUTHORIZED, "Invalid token"),
            AuthError::ExpiredToken => (StatusCode::UNAUTHORIZED, "Token expired"),
            AuthError::InvalidIssuer => (StatusCode::UNAUTHORIZED, "Invalid issuer"),
            AuthError::DecodeError(_) => (StatusCode::UNAUTHORIZED, "Authentication failed"),
        };

        (status, message).into_response()
    }
}

/// Validate a Supabase JWT token
pub fn validate_token(token: &str, config: &SupabaseConfig) -> Result<TokenData<Claims>, AuthError> {
    // Decode the header first to check algorithm
    let header = decode_header(token)
        .map_err(|e| {
            warn!("Failed to decode JWT header: {}", e);
            AuthError::InvalidToken
        })?;

    debug!("JWT algorithm: {:?}", header.alg);

    // Supabase uses HS256 for JWT signing
    // The secret is the JWT_SECRET from your Supabase project settings
    let mut validation = Validation::new(Algorithm::HS256);
    validation.set_issuer(&[&config.issuer]);
    validation.validate_exp = true;

    // For Supabase, the JWT_SECRET is actually the ANON_KEY itself
    // We need to extract the signature portion (after the last dot)
    let secret = extract_jwt_secret(&config.jwt_secret);

    let decoding_key = DecodingKey::from_secret(secret.as_bytes());

    decode::<Claims>(token, &decoding_key, &validation)
        .map_err(|e| {
            warn!("JWT validation failed: {}", e);
            match e.kind() {
                jsonwebtoken::errors::ErrorKind::ExpiredSignature => AuthError::ExpiredToken,
                jsonwebtoken::errors::ErrorKind::InvalidIssuer => AuthError::InvalidIssuer,
                _ => AuthError::DecodeError(e.to_string()),
            }
        })
}

/// Extract the JWT secret from the Supabase ANON_KEY
/// The ANON_KEY is actually a JWT itself, but we need the signing secret
/// For Supabase, the JWT_SECRET is the key used to sign tokens
fn extract_jwt_secret(_anon_key: &str) -> String {
    // In production, this should come from environment variable SUPABASE_JWT_SECRET
    // For now, we'll use a placeholder that matches the Supabase configuration
    //
    // IMPORTANT: In a real deployment, you must set the JWT_SECRET environment variable
    // to match your Supabase project's JWT secret (found in project settings > API)

    // TODO: Replace with environment variable
    std::env::var("SUPABASE_JWT_SECRET")
        .unwrap_or_else(|_| {
            warn!("SUPABASE_JWT_SECRET not set, using default (INSECURE)");
            "your-super-secret-jwt-token-with-at-least-32-characters-long".to_string()
        })
}

/// Helper function to extract and validate auth user from request parts
/// This is used by the middleware and can be called manually
pub fn extract_auth_user_from_parts(parts: &Parts) -> Result<AuthUser, AuthError> {
    // Extract token from Authorization header
    let token = extract_token_from_headers(&parts.headers)?;

    // Validate the token
    let config = SupabaseConfig::default();
    let token_data = validate_token(&token, &config)?;

    debug!("Authenticated user: {}", token_data.claims);

    Ok(AuthUser {
        claims: token_data.claims,
        token,
    })
}

/// Extract Bearer token from Authorization header
fn extract_token_from_headers(headers: &http::HeaderMap) -> Result<String, AuthError> {
    let auth_header = headers
        .get(AUTHORIZATION)
        .ok_or(AuthError::MissingToken)?;

    let auth_str = auth_header
        .to_str()
        .map_err(|_| AuthError::InvalidToken)?;

    if !auth_str.starts_with("Bearer ") {
        return Err(AuthError::InvalidToken);
    }

    Ok(auth_str[7..].to_string())
}

/// Middleware for JWT authentication
/// This can be applied to routes that require authentication
pub async fn auth_middleware(
    req: Request<Body>,
    next: Next,
) -> Result<Response, AuthError> {
    // Extract and validate token
    let (mut parts, body) = req.into_parts();
    let auth_user = extract_auth_user_from_parts(&parts)?;

    // Check if token is expired
    if auth_user.is_expired() {
        return Err(AuthError::ExpiredToken);
    }

    // Add auth user to request extensions for downstream handlers
    parts.extensions.insert(auth_user);

    // Reconstruct request and continue
    let req = Request::from_parts(parts, body);
    Ok(next.run(req).await)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_extract_bearer_token() {
        let mut headers = http::HeaderMap::new();
        headers.insert(
            AUTHORIZATION,
            HeaderValue::from_static("Bearer test_token_123")
        );

        let result = extract_token_from_headers(&headers);
        assert!(result.is_ok());
        assert_eq!(result.unwrap(), "test_token_123");
    }

    #[test]
    fn test_missing_token() {
        let headers = http::HeaderMap::new();
        let result = extract_token_from_headers(&headers);
        assert!(matches!(result, Err(AuthError::MissingToken)));
    }

    #[test]
    fn test_invalid_token_format() {
        let mut headers = http::HeaderMap::new();
        headers.insert(
            AUTHORIZATION,
            HeaderValue::from_static("Invalid format")
        );

        let result = extract_token_from_headers(&headers);
        assert!(matches!(result, Err(AuthError::InvalidToken)));
    }
}
