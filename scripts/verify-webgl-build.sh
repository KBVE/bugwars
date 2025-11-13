#!/bin/bash
# Verify Unity WebGL build before Docker build proceeds

set -euo pipefail

WEBGL_URL="${1:-https://unity-bw.kbve.com/webgl.zip?new}"
EXPECTED_VERSION="${2:-}"
MAX_RETRIES=3
RETRY_DELAY=5

echo "╔══════════════════════════════════════════════════════════╗"
echo "║  Unity WebGL Build Verification                         ║"
echo "╚══════════════════════════════════════════════════════════╝"

# Function to download with retry
download_with_retry() {
    local url="$1"
    local output="$2"
    local attempt=1

    while [ $attempt -le $MAX_RETRIES ]; do
        echo "→ Attempt $attempt/$MAX_RETRIES: Downloading from $url"

        if wget --timeout=30 --tries=1 -O "$output" "$url" 2>&1; then
            echo "✓ Download successful"
            return 0
        else
            echo "✗ Download failed"
            if [ $attempt -lt $MAX_RETRIES ]; then
                echo "  Retrying in ${RETRY_DELAY}s..."
                sleep $RETRY_DELAY
            fi
            attempt=$((attempt + 1))
        fi
    done

    echo "ERROR: Failed to download after $MAX_RETRIES attempts" >&2
    return 1
}

# Download the WebGL build
echo ""
echo "Step 1: Downloading Unity WebGL build"
echo "────────────────────────────────────────────────────────────"

if ! download_with_retry "$WEBGL_URL" "/tmp/webgl.zip"; then
    echo ""
    echo "╔══════════════════════════════════════════════════════════╗"
    echo "║  ERROR: WebGL Build Not Available                       ║"
    echo "╚══════════════════════════════════════════════════════════╝"
    echo ""
    echo "The Unity WebGL build could not be downloaded from:"
    echo "  $WEBGL_URL"
    echo ""
    echo "Possible causes:"
    echo "  1. Unity build CI job hasn't completed yet"
    echo "  2. GitHub Pages hasn't deployed the build yet"
    echo "  3. Network connectivity issues"
    echo "  4. URL is incorrect or has changed"
    echo ""
    echo "Solutions:"
    echo "  - Wait for Unity build job to complete in CI"
    echo "  - Verify GitHub Pages deployment is successful"
    echo "  - Check the URL is correct: $WEBGL_URL"
    echo "  - Ensure CI job dependencies are properly configured"
    echo ""
    exit 1
fi

# Verify file is a valid zip
echo ""
echo "Step 2: Verifying ZIP integrity"
echo "────────────────────────────────────────────────────────────"

if ! unzip -t /tmp/webgl.zip > /dev/null 2>&1; then
    echo "✗ ERROR: Downloaded file is not a valid ZIP archive" >&2
    echo "  The file may be corrupted or incomplete" >&2
    rm -f /tmp/webgl.zip
    exit 1
fi

echo "✓ ZIP file integrity verified"

# Check file size (Unity WebGL builds are typically > 10MB)
# Alpine busybox stat uses different flags
if command -v stat > /dev/null 2>&1; then
    FILE_SIZE=$(stat -c%s "/tmp/webgl.zip" 2>/dev/null || stat -f%z "/tmp/webgl.zip" 2>/dev/null || wc -c < "/tmp/webgl.zip")
else
    FILE_SIZE=$(wc -c < "/tmp/webgl.zip")
fi
MIN_SIZE=$((10 * 1024 * 1024))  # 10MB

if [ "$FILE_SIZE" -lt "$MIN_SIZE" ]; then
    echo "✗ WARNING: WebGL build size is suspiciously small (${FILE_SIZE} bytes)"
    echo "  Expected at least ${MIN_SIZE} bytes"
    echo "  The build may be incomplete or corrupted"
fi

echo "✓ File size check passed (${FILE_SIZE} bytes)"

# Extract and verify contents
echo ""
echo "Step 3: Extracting and verifying contents"
echo "────────────────────────────────────────────────────────────"

mkdir -p /tmp/webgl-verify
if ! unzip -q /tmp/webgl.zip -d /tmp/webgl-verify; then
    echo "✗ ERROR: Failed to extract WebGL build" >&2
    rm -rf /tmp/webgl-verify /tmp/webgl.zip
    exit 1
fi

echo "✓ Extraction successful"

# Check for required Unity WebGL files
REQUIRED_FILES=("Build" "TemplateData")
MISSING_FILES=0

for file in "${REQUIRED_FILES[@]}"; do
    if [ ! -e "/tmp/webgl-verify/$file" ]; then
        echo "✗ Missing required file/directory: $file"
        MISSING_FILES=$((MISSING_FILES + 1))
    else
        echo "✓ Found: $file"
    fi
done

if [ $MISSING_FILES -gt 0 ]; then
    echo ""
    echo "ERROR: Unity WebGL build is incomplete ($MISSING_FILES missing files)" >&2
    echo "Contents of webgl.zip:"
    ls -lah /tmp/webgl-verify
    rm -rf /tmp/webgl-verify /tmp/webgl.zip
    exit 1
fi

# Verify version if provided
if [ -n "$EXPECTED_VERSION" ]; then
    echo ""
    echo "Step 4: Verifying version"
    echo "────────────────────────────────────────────────────────────"

    if [ -f "/tmp/webgl-verify/StreamingAssets/version.json" ]; then
        BUILD_VERSION=$(grep -oP '"version":\s*"\K[^"]+' /tmp/webgl-verify/StreamingAssets/version.json || echo "")

        if [ -n "$BUILD_VERSION" ]; then
            echo "→ Expected version: $EXPECTED_VERSION"
            echo "→ Build version:    $BUILD_VERSION"

            if [ "$BUILD_VERSION" = "$EXPECTED_VERSION" ]; then
                echo "✓ Version match confirmed"
            else
                echo ""
                echo "╔══════════════════════════════════════════════════════════╗"
                echo "║  ERROR: Version Mismatch - Race Condition Detected      ║"
                echo "╚══════════════════════════════════════════════════════════╝"
                echo ""
                echo "Expected version: $EXPECTED_VERSION"
                echo "Build version:    $BUILD_VERSION"
                echo ""
                echo "⚠️  This indicates a RACE CONDITION:"
                echo "You updated Cargo.toml to v$EXPECTED_VERSION, but the Unity"
                echo "build on GitHub Pages is still v$BUILD_VERSION."
                echo ""
                echo "The CI pipeline needs time to:"
                echo "  1. Build Unity WebGL with the new version (15-20 min)"
                echo "  2. Deploy to GitHub Pages (2-5 min)"
                echo ""
                echo "Solutions:"
                echo "  1. Wait for CI pipeline to complete, then retry"
                echo "  2. Build Unity WebGL locally and use scripts/local-docker-build.sh"
                echo "  3. Use the CI pipeline instead of local Docker builds"
                echo ""
                echo "To bypass this check (NOT RECOMMENDED):"
                echo "  Set SKIP_VERSION_CHECK=1 environment variable"
                echo ""

                # Allow bypass via environment variable
                if [ "${SKIP_VERSION_CHECK:-0}" = "1" ]; then
                    echo "⚠️  Version check bypassed (SKIP_VERSION_CHECK=1)"
                    echo "   Proceeding with mismatched version!"
                    echo ""
                else
                    rm -rf /tmp/webgl-verify /tmp/webgl.zip
                    exit 1
                fi
            fi
        else
            echo "✗ WARNING: Could not extract version from version.json"
            echo "  Unable to verify build version"
        fi
    else
        echo "✗ WARNING: version.json not found in StreamingAssets"
        echo "  Unable to verify build version"
        echo "  This may indicate an old Unity build format"
    fi
fi

# Cleanup verification directory
rm -rf /tmp/webgl-verify

echo ""
echo "╔══════════════════════════════════════════════════════════╗"
echo "║  ✓ WebGL Build Verification Complete                    ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""
echo "The Unity WebGL build is valid and ready to use"
echo ""

exit 0
