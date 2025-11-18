#!/bin/bash
# Sync Unity version from Cargo.toml (source of truth)
#
# This script ensures Cargo.toml is the single source of truth for version numbers
# It reads the version from Cargo.toml and updates Unity's ProjectSettings.asset
#
# Usage:
#   ./scripts/sync-unity-version.sh           # Sync version
#   ./scripts/sync-unity-version.sh --verify  # Verify version is in sync

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Paths (relative to project root)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CARGO_TOML="$PROJECT_ROOT/website/axum/Cargo.toml"
PROJECT_SETTINGS="$PROJECT_ROOT/unity/bugwars/ProjectSettings/ProjectSettings.asset"

# Functions
print_header() {
    echo -e "${BLUE}============================================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}============================================================${NC}"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1" >&2
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

# Extract version from Cargo.toml
get_cargo_version() {
    if [[ ! -f "$CARGO_TOML" ]]; then
        print_error "Cargo.toml not found at: $CARGO_TOML"
        exit 1
    fi

    local version
    version=$(grep -m1 '^version = ' "$CARGO_TOML" | sed 's/version = "\(.*\)"/\1/')

    if [[ -z "$version" ]]; then
        print_error "Failed to extract version from Cargo.toml"
        exit 1
    fi

    echo "$version"
}

# Extract version from Unity ProjectSettings.asset
get_unity_version() {
    if [[ ! -f "$PROJECT_SETTINGS" ]]; then
        print_error "ProjectSettings.asset not found at: $PROJECT_SETTINGS"
        exit 1
    fi

    local version
    version=$(grep -m1 'bundleVersion:' "$PROJECT_SETTINGS" | sed 's/.*bundleVersion: //' | tr -d '\r')

    echo "$version"
}

# Parse semantic version into integer code (1.2.3 -> 10203)
parse_version_code() {
    local version="$1"

    if [[ $version =~ ^([0-9]+)\.([0-9]+)\.([0-9]+) ]]; then
        local major="${BASH_REMATCH[1]}"
        local minor="${BASH_REMATCH[2]}"
        local patch="${BASH_REMATCH[3]}"
        echo $((major * 10000 + minor * 100 + patch))
    else
        print_warning "Could not parse version components from: $version"
        echo "0"
    fi
}

# Update Unity ProjectSettings.asset
update_unity_version() {
    local version="$1"
    local version_code
    version_code=$(parse_version_code "$version")

    if [[ ! -f "$PROJECT_SETTINGS" ]]; then
        print_error "ProjectSettings.asset not found at: $PROJECT_SETTINGS"
        exit 1
    fi

    # Create backup
    cp "$PROJECT_SETTINGS" "$PROJECT_SETTINGS.backup"

    # Update bundleVersion
    sed -i "s/bundleVersion:.*/bundleVersion: $version/" "$PROJECT_SETTINGS"

    # Update Android bundleVersionCode
    sed -i "s/AndroidBundleVersionCode:.*/AndroidBundleVersionCode: $version_code/" "$PROJECT_SETTINGS"

    # Update iOS buildNumber
    sed -i "s/buildNumber:.*/buildNumber: $version/" "$PROJECT_SETTINGS"

    # Update company name (fixes WebGL warning)
    sed -i "s/companyName:.*/companyName: KBVE/" "$PROJECT_SETTINGS"

    # Update product name (fixes WebGL warning)
    sed -i "s/productName:.*/productName: BugWars/" "$PROJECT_SETTINGS"

    print_success "Unity version synced successfully: $version"
    print_success "Bundle version code: $version_code"
    print_success "Company: KBVE"
    print_success "Product: BugWars"

    # Remove backup if successful
    rm -f "$PROJECT_SETTINGS.backup"
}

# Verify versions are in sync
verify_sync() {
    local cargo_version
    local unity_version

    cargo_version=$(get_cargo_version)
    unity_version=$(get_unity_version)

    print_info "Cargo.toml version: $cargo_version"
    print_info "Unity version:      $unity_version"

    if [[ "$cargo_version" == "$unity_version" ]]; then
        print_success "Versions are in sync!"
        return 0
    else
        print_error "Versions are OUT OF SYNC!"
        print_warning "Run this script without --verify to sync Unity with Cargo.toml"
        return 1
    fi
}

# Main logic
main() {
    print_header "Unity Version Sync from Cargo.toml"

    # Check for verify flag
    if [[ "${1:-}" == "--verify" ]] || [[ "${1:-}" == "-v" ]]; then
        verify_sync
        exit $?
    fi

    # Get version from Cargo.toml (source of truth)
    local version
    version=$(get_cargo_version)

    print_info "Source version (Cargo.toml): $version"

    # Check if already in sync
    local unity_version
    unity_version=$(get_unity_version)

    if [[ "$version" == "$unity_version" ]]; then
        print_success "Unity is already in sync with Cargo.toml version: $version"
        exit 0
    fi

    print_warning "Unity version ($unity_version) differs from Cargo.toml ($version)"
    print_info "Updating Unity project settings..."

    # Update Unity version
    update_unity_version "$version"

    echo ""
    print_success "Version sync completed successfully!"
    echo ""
    print_info "You may need to refresh Unity Editor to see the changes."
}

# Run main function
main "$@"
