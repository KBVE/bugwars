# Version Management System

This document describes the version management and build verification system for BugWars.

## Overview

BugWars uses **Cargo.toml** (`website/axum/Cargo.toml`) as the single source of truth for versioning. All other components (Unity, Astro, Docker) derive their version from this file.

## Architecture

```
Cargo.toml (v1.0.10)
    ↓
scripts/get-version.sh → Extract version
    ↓
scripts/generate-version-json.sh → Generate version.json files
    ↓
    ├─→ website/astro/public/version.json (static file served by Astro)
    └─→ unity/bugwars/Assets/StreamingAssets/version.json (embedded in Unity build)
        ↓
        Unity WebGL Build (includes version.json)
        ↓
        Docker Build (with verification)
        ↓
        Deployment
```

## Components

### 1. Version Extraction Script

**File:** `scripts/get-version.sh`

Extracts the version from Cargo.toml.

```bash
./scripts/get-version.sh
# Output: 1.0.10
```

### 2. Version JSON Generator

**File:** `scripts/generate-version-json.sh`

Generates `version.json` files for both Astro and Unity:

```bash
./scripts/generate-version-json.sh
```

**Generated files:**
- `website/astro/public/version.json` - Served as static file
- `unity/bugwars/Assets/StreamingAssets/version.json` - Embedded in Unity build

**JSON Structure:**
```json
{
  "version": "1.0.10",
  "package": "kbve-bugwars",
  "source": "cargo.toml",
  "timestamp": "2025-11-13T19:34:31Z",
  "build": {
    "unity": "6000.2.8f1",
    "node": "22",
    "rust": "1.85"
  }
}
```

### 3. Unity Version Manager

**File:** `unity/bugwars/Assets/Scripts/Core/VersionManager.cs`

Unity C# component that reads and displays version information.

**Features:**
- Loads version.json from StreamingAssets
- WebGL-compatible using UnityWebRequest
- Validates version format
- Provides version display methods

**Usage in Unity:**
```csharp
using BugWars.Core;

// Get version manager instance
VersionManager versionManager = GetComponent<VersionManager>();

// Access version info
string version = versionManager.Version;  // "1.0.10"
string fullVersion = versionManager.GetFullVersionString();  // "kbve-bugwars v1.0.10"

// Log detailed info
versionManager.LogVersionInfo();
```

### 4. Docker Build Verification

**File:** `scripts/verify-webgl-build.sh`

Verifies Unity WebGL build availability and integrity before Docker build proceeds.

**Features:**
- Downloads with retry (3 attempts, exponential backoff)
- Validates ZIP integrity
- Checks file size (minimum 10MB)
- Verifies required Unity WebGL structure
- Optional version matching

**Verification Steps:**
1. Download Unity WebGL build (with retries)
2. Verify ZIP file integrity
3. Check file size threshold
4. Extract and validate contents (Build/, TemplateData/)
5. Verify version.json if available

**Error Handling:**
- Clear error messages with troubleshooting steps
- Fails fast if build unavailable
- Suggests solutions (wait for CI, check URL, etc.)

### 5. Dockerfile Integration

**File:** `Dockerfile`

The Dockerfile has two modes for obtaining Unity WebGL build:

**Mode 1: CI Build (Preferred)**
- Uses pre-downloaded artifact from CI
- Faster, more reliable
- No network dependency during build

**Mode 2: Local Build (Fallback)**
- Downloads from GitHub Pages
- Uses verification script
- Includes retry logic

**Build Arguments:**
```bash
# With version verification
docker build --build-arg EXPECTED_VERSION=1.0.10 .

# In CI (uses artifact)
docker build --build-arg EXPECTED_VERSION=1.0.10 .
```

### 6. CI/CD Integration

**File:** `.github/workflows/ci-main.yaml`

**Workflow Steps:**

1. **Extract Version** → Read from Cargo.toml
2. **Generate Version JSON** → Create version.json files
3. **Unity Build** → Build WebGL with embedded version.json
4. **Upload Artifacts** → Store webgl.zip
5. **Docker Build** → Use artifact (no download race condition)
6. **GitHub Pages Deploy** → Deploy webgl.zip for future use

**Key Improvements:**
- Version JSON generated before Unity build
- Docker build uses CI artifact (no race condition)
- Version passed as build arg for verification
- Explicit job dependencies ensure proper ordering

## Usage

### Local Development

```bash
# Extract current version
./scripts/get-version.sh

# Generate version.json files
./scripts/generate-version-json.sh

# Build Docker image locally (downloads from GitHub Pages)
docker build -t bugwars:local .

# Build with version verification
docker build --build-arg EXPECTED_VERSION=$(./scripts/get-version.sh) -t bugwars:local .
```

### CI/CD Pipeline

The CI pipeline automatically:
1. Extracts version from Cargo.toml
2. Generates version.json files
3. Builds Unity with embedded version
4. Uses artifact for Docker build (no download)
5. Deploys to GHCR, Itch.io, and GitHub Pages

### Updating Version

To update the version:

1. Edit `website/axum/Cargo.toml`:
   ```toml
   [package]
   version = "1.0.11"
   ```

2. Commit and push to main:
   ```bash
   git add website/axum/Cargo.toml
   git commit -m "chore: bump version to 1.0.11"
   git push origin main
   ```

3. CI pipeline automatically:
   - Generates new version.json files
   - Builds with new version
   - Tags Docker image as `ghcr.io/kbve/bugwars:1.0.11`
   - Updates deployment manifest

## Verification

### Verify Version in Unity Build

```bash
# Check version.json in webgl.zip
unzip -p webgl.zip StreamingAssets/version.json | jq .
```

### Verify Version in Docker Image

```bash
# Run container and check version endpoint
docker run -d -p 4321:4321 ghcr.io/kbve/bugwars:latest
curl http://localhost:4321/version.json
```

### Verify Version in Deployed Site

```bash
# Check GitHub Pages
curl https://unity-bw.kbve.com/version.json

# Check main site
curl https://bugwars.kbve.com/version.json
```

## Troubleshooting

### Docker Build Fails: "WebGL Build Not Available"

**Cause:** GitHub Pages deployment hasn't completed yet.

**Solution:**
- Wait for GitHub Pages deployment to complete
- Check CI workflow status
- Verify webgl.zip exists at `https://unity-bw.kbve.com/webgl.zip`

### Version Mismatch Warning

**Cause:** Unity build version doesn't match Cargo.toml version.

**Solution:**
- Regenerate version.json: `./scripts/generate-version-json.sh`
- Rebuild Unity WebGL
- Ensure CI generates version.json before Unity build

### Unity Can't Load version.json

**Cause:** version.json not included in Unity build.

**Solution:**
- Verify file exists: `unity/bugwars/Assets/StreamingAssets/version.json`
- Check Unity meta file exists: `version.json.meta`
- Regenerate: `./scripts/generate-version-json.sh`
- Rebuild Unity project

## Best Practices

1. **Never manually edit version.json** - Always regenerate from Cargo.toml
2. **Always run generate-version-json.sh before Unity builds** - Ensures version is embedded
3. **Use semantic versioning** - MAJOR.MINOR.PATCH format
4. **Test locally before pushing** - Verify Docker build works
5. **Check CI logs** - Ensure version propagation is correct

## Files Modified/Created

### Scripts
- `scripts/get-version.sh` - Extract version from Cargo.toml
- `scripts/generate-version-json.sh` - Generate version.json files
- `scripts/verify-webgl-build.sh` - Verify Unity WebGL build

### Unity
- `unity/bugwars/Assets/Scripts/Core/VersionManager.cs` - Version manager component
- `unity/bugwars/Assets/Scripts/Core/VersionManager.cs.meta` - Unity meta file
- `unity/bugwars/Assets/StreamingAssets/version.json` - Version data (generated)
- `unity/bugwars/Assets/StreamingAssets/version.json.meta` - Unity meta file

### Astro
- `website/astro/public/version.json` - Static version file (generated)

### Docker & CI
- `Dockerfile` - Updated with verification and artifact support
- `.github/workflows/ci-main.yaml` - Updated with version generation step

### Documentation
- `docs/VERSION_MANAGEMENT.md` - This document

## API Endpoints

### GET /version.json

Returns version information as JSON.

**Response:**
```json
{
  "version": "1.0.10",
  "package": "kbve-bugwars",
  "source": "cargo.toml",
  "timestamp": "2025-11-13T19:34:31Z",
  "build": {
    "unity": "6000.2.8f1",
    "node": "22",
    "rust": "1.85"
  }
}
```

**Availability:**
- Static file: `https://bugwars.kbve.com/version.json`
- GitHub Pages: `https://unity-bw.kbve.com/version.json`
- Local: `http://localhost:4321/version.json`

## Future Enhancements

1. **Axum API Endpoint** - Dynamic version endpoint from Rust backend
2. **Version Comparison** - Client-side version compatibility checks
3. **Changelog Integration** - Auto-generate changelog from version bumps
4. **Semantic Release** - Automated version bumping based on commits
5. **Version Display in UI** - Show version in game menu/footer

---

**Last Updated:** 2025-11-13
**Version:** 1.0.10
