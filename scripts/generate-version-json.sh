#!/bin/bash
# Generate version.json from Cargo.toml for Unity and client verification

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Get version using our extraction script
VERSION=$("$SCRIPT_DIR/get-version.sh" "$PROJECT_ROOT/website/axum/Cargo.toml")

# Output paths
ASTRO_PUBLIC="$PROJECT_ROOT/website/astro/public"
UNITY_STREAMING_ASSETS="$PROJECT_ROOT/unity/bugwars/Assets/StreamingAssets"

# Create directories if they don't exist
mkdir -p "$ASTRO_PUBLIC"
mkdir -p "$UNITY_STREAMING_ASSETS"

# Generate version.json
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

VERSION_JSON=$(cat <<EOF
{
  "version": "$VERSION",
  "package": "kbve-bugwars",
  "source": "cargo.toml",
  "timestamp": "$TIMESTAMP",
  "build": {
    "unity": "6000.2.8f1",
    "node": "22",
    "rust": "1.85"
  }
}
EOF
)

# Write to Astro public directory (will be served as static file)
echo "$VERSION_JSON" > "$ASTRO_PUBLIC/version.json"
echo "✓ Generated $ASTRO_PUBLIC/version.json (v$VERSION)"

# Write to Unity StreamingAssets (will be included in WebGL build)
echo "$VERSION_JSON" > "$UNITY_STREAMING_ASSETS/version.json"
echo "✓ Generated $UNITY_STREAMING_ASSETS/version.json (v$VERSION)"

# Also output to stdout for CI/CD use
echo "$VERSION_JSON"
