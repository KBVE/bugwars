#!/bin/bash
# Extract version from Cargo.toml (source of truth)

set -euo pipefail

CARGO_TOML="${1:-website/axum/Cargo.toml}"

if [[ ! -f "$CARGO_TOML" ]]; then
    echo "ERROR: Cargo.toml not found at $CARGO_TOML" >&2
    exit 1
fi

# Extract version using grep and sed
VERSION=$(grep -m1 '^version = ' "$CARGO_TOML" | sed 's/version = "\(.*\)"/\1/')

if [[ -z "$VERSION" ]]; then
    echo "ERROR: Failed to extract version from $CARGO_TOML" >&2
    exit 1
fi

echo "$VERSION"
