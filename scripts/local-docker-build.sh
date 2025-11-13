#!/bin/bash
# Local Docker build helper - Ensures Unity WebGL is available before building

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "╔══════════════════════════════════════════════════════════╗"
echo "║  BugWars Local Docker Build Helper                      ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

# Get current version from Cargo.toml
VERSION=$("$SCRIPT_DIR/get-version.sh")
echo "→ Current version from Cargo.toml: $VERSION"
echo ""

# Check if Unity build exists locally
UNITY_BUILD_PATH="$PROJECT_ROOT/unity/bugwars/build/WebGL/WebGL"
LOCAL_WEBGL_ZIP="$PROJECT_ROOT/tmp-docker-context/webgl.zip"

mkdir -p "$PROJECT_ROOT/tmp-docker-context"

# Option 1: Use existing local Unity build
if [ -d "$UNITY_BUILD_PATH" ]; then
    echo "✓ Found local Unity WebGL build at:"
    echo "  $UNITY_BUILD_PATH"
    echo ""
    read -p "Use this build for Docker? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "→ Creating webgl.zip from local build..."
        cd "$UNITY_BUILD_PATH"
        zip -r "$LOCAL_WEBGL_ZIP" . > /dev/null
        echo "✓ Created $LOCAL_WEBGL_ZIP"
        echo ""
    fi
fi

# Option 2: Check if webgl.zip already exists in tmp-docker-context
if [ -f "$LOCAL_WEBGL_ZIP" ]; then
    echo "✓ Using webgl.zip from tmp-docker-context/"
    ls -lh "$LOCAL_WEBGL_ZIP"

    # Verify version in webgl.zip
    if unzip -l "$LOCAL_WEBGL_ZIP" | grep -q "StreamingAssets/version.json"; then
        BUILD_VERSION=$(unzip -p "$LOCAL_WEBGL_ZIP" StreamingAssets/version.json | grep -oP '"version":\s*"\K[^"]+' || echo "unknown")
        echo "→ Version in webgl.zip: $BUILD_VERSION"

        if [ "$BUILD_VERSION" != "$VERSION" ]; then
            echo ""
            echo "⚠️  WARNING: Version mismatch detected!"
            echo "   Cargo.toml version: $VERSION"
            echo "   WebGL build version: $BUILD_VERSION"
            echo ""
            echo "This means your Docker build will have the wrong version embedded."
            echo ""
            read -p "Continue anyway? (y/n): " -n 1 -r
            echo
            if [[ ! $REPLY =~ ^[Yy]$ ]]; then
                echo "Build cancelled."
                exit 1
            fi
        else
            echo "✓ Version matches Cargo.toml"
        fi
    else
        echo "⚠️  WARNING: version.json not found in webgl.zip"
        echo "   This build may not have version information embedded"
    fi
    echo ""
else
    # Option 3: Download from GitHub Pages
    echo "⚠️  No local Unity build found"
    echo ""
    echo "The Docker build will attempt to download from GitHub Pages:"
    echo "  https://unity-bw.kbve.com/webgl.zip"
    echo ""
    echo "⚠️  RACE CONDITION WARNING:"
    echo "If you just updated Cargo.toml version, the Unity build on GitHub"
    echo "Pages may not match yet. The CI pipeline needs to:"
    echo "  1. Build Unity WebGL (takes ~15-20 minutes)"
    echo "  2. Deploy to GitHub Pages (takes ~2-5 minutes)"
    echo ""
    echo "Options to avoid this:"
    echo "  1. Wait for CI pipeline to complete"
    echo "  2. Build Unity WebGL locally and run this script again"
    echo "  3. Use the CI pipeline instead of local builds"
    echo ""
    read -p "Continue with download from GitHub Pages? (y/n): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo ""
        echo "Build cancelled."
        echo ""
        echo "To build Unity WebGL locally:"
        echo "  1. Open unity/bugwars/ in Unity Editor"
        echo "  2. Run: scripts/generate-version-json.sh"
        echo "  3. Build WebGL (File > Build Settings > WebGL > Build)"
        echo "  4. Run this script again"
        echo ""
        exit 1
    fi
fi

# Build Docker image
echo "╔══════════════════════════════════════════════════════════╗"
echo "║  Building Docker Image                                   ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

cd "$PROJECT_ROOT"

echo "→ Building image: bugwars:$VERSION"
echo ""

if docker build \
    --build-arg EXPECTED_VERSION="$VERSION" \
    -t "bugwars:$VERSION" \
    -t "bugwars:latest" \
    .; then

    echo ""
    echo "╔══════════════════════════════════════════════════════════╗"
    echo "║  ✓ Docker Build Successful                              ║"
    echo "╚══════════════════════════════════════════════════════════╝"
    echo ""
    echo "Images created:"
    echo "  - bugwars:$VERSION"
    echo "  - bugwars:latest"
    echo ""
    echo "To run:"
    echo "  docker run -p 4321:4321 bugwars:$VERSION"
    echo ""
else
    echo ""
    echo "╔══════════════════════════════════════════════════════════╗"
    echo "║  ✗ Docker Build Failed                                  ║"
    echo "╚══════════════════════════════════════════════════════════╝"
    echo ""
    exit 1
fi
