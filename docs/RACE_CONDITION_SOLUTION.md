# Race Condition Solution

## The Problem

When updating the version in `Cargo.toml` and pushing to GitHub, a race condition occurs:

```
Developer pushes Cargo.toml v1.0.11
    ↓
GitHub Actions starts CI pipeline
    ↓
    ├─→ Unity Build Job (15-20 minutes) → GitHub Pages Deploy (2-5 minutes)
    │   └─→ webgl.zip with v1.0.11 available at unity-bw.kbve.com
    │
    └─→ Docker Build Job (5-10 minutes) starts IMMEDIATELY
        └─→ Tries to download webgl.zip
        └─→ Gets OLD version (v1.0.10) ❌ RACE CONDITION!
```

**Result:** Docker image is built with v1.0.11 tag but contains v1.0.10 Unity build.

## Our Solution: Multi-Layered Defense

### Layer 1: CI Pipeline Uses Artifacts (Primary Solution)

The Docker build job now uses the Unity build artifact directly, avoiding the download entirely.

**Before:**
```yaml
docker-build:
  needs: [unity-build]
  steps:
    # Docker downloads from GitHub Pages ❌
    - docker build .
```

**After:**
```yaml
docker-build:
  needs: [unity-build]
  steps:
    - download-artifact: webgl-zip  # ✓ Use artifact
    - copy webgl.zip to tmp-docker-context/
    - docker build .  # Uses local file, no download!
```

**Dockerfile change:**
```dockerfile
# Copy webgl.zip if available (from CI artifact)
COPY tmp-docker-context/webgl.zi[p] /tmp/ || true

# Use local file if exists, otherwise download
RUN if [ -f "/tmp/webgl.zip" ]; then
    echo "Using CI artifact"
else
    echo "Downloading from GitHub Pages"
    ./verify-webgl-build.sh
fi
```

**Status:** ✅ **SOLVED** - CI builds never have race condition

---

### Layer 2: Version Verification (Fail Fast)

If Docker must download (local builds), verify version matches and fail fast.

**Script:** `scripts/verify-webgl-build.sh`

```bash
# Download webgl.zip from GitHub Pages
# Extract version.json
# Compare versions

if [ "$BUILD_VERSION" != "$EXPECTED_VERSION" ]; then
    echo "ERROR: Race condition detected!"
    echo "Expected: v1.0.11"
    echo "Got: v1.0.10"
    echo ""
    echo "Solutions:"
    echo "1. Wait for CI to complete (~20 min)"
    echo "2. Build Unity locally"
    echo "3. Use CI pipeline instead"
    exit 1  # Fail the build
fi
```

**Status:** ✅ **PREVENTS** - Catches race condition early with clear error

---

### Layer 3: Local Build Helper (Developer Experience)

Interactive script that guides developers through the build process.

**Script:** `scripts/local-docker-build.sh`

```bash
./scripts/local-docker-build.sh
```

**Features:**
- Detects version from Cargo.toml
- Checks for local Unity build
- Verifies version.json matches
- Warns about race conditions
- Offers solutions:
  - Use local Unity build
  - Wait for CI
  - Bypass check (with warning)

**Status:** ✅ **GUIDES** - Prevents user error, provides clear options

---

### Layer 4: Documentation (Prevention)

Clear documentation explaining the race condition and best practices.

**Files:**
- `docs/VERSION_MANAGEMENT.md` - Complete system overview
- `docs/RACE_CONDITION_SOLUTION.md` - This document
- Updated `README.md` with build instructions

**Status:** ✅ **EDUCATES** - Developers understand the issue

---

## Usage Scenarios

### Scenario 1: CI Pipeline (Recommended) ✅

```bash
# 1. Update version
vim website/axum/Cargo.toml  # version = "1.0.11"

# 2. Commit and push
git add website/axum/Cargo.toml
git commit -m "chore: bump version to 1.0.11"
git push origin main

# 3. CI automatically:
#    - Generates version.json
#    - Builds Unity with v1.0.11
#    - Uses artifact for Docker (NO RACE CONDITION)
#    - Pushes to GHCR as v1.0.11
```

**Race condition:** ❌ None - Uses artifact

---

### Scenario 2: Local Build with Unity Already Built ✅

```bash
# 1. Update version
vim website/axum/Cargo.toml  # version = "1.0.11"

# 2. Generate version.json
./scripts/generate-version-json.sh

# 3. Build Unity WebGL in Unity Editor
#    File > Build Settings > WebGL > Build

# 4. Build Docker with helper script
./scripts/local-docker-build.sh
# ✓ Detects local Unity build
# ✓ Verifies version matches
# ✓ Builds Docker successfully
```

**Race condition:** ❌ None - Uses local build

---

### Scenario 3: Local Build Without Unity (Will Fail) ⚠️

```bash
# 1. Update version
vim website/axum/Cargo.toml  # version = "1.0.11"

# 2. Try to build Docker immediately
docker build .

# Result: ❌ FAILS with clear error:
# "ERROR: Version Mismatch - Race Condition Detected"
# "Expected: 1.0.11, Got: 1.0.10"
# "Solutions: Wait for CI, build Unity locally, or use CI pipeline"
```

**Race condition:** ✅ Detected and prevented - Build fails with guidance

---

### Scenario 4: Emergency Bypass (Not Recommended) ⚠️

```bash
# If you REALLY need to bypass the check:
SKIP_VERSION_CHECK=1 docker build \
  --build-arg EXPECTED_VERSION=1.0.11 .

# Warning: This builds Docker with mismatched versions!
```

**Race condition:** ⚠️ Allowed but warned - User explicitly bypassed

---

## Technical Details

### Version Flow in CI

```
1. extract-version job
   └─→ Outputs: version=1.0.11

2. unity-build job
   ├─→ Runs: generate-version-json.sh
   │   ├─→ Creates: unity/.../StreamingAssets/version.json
   │   └─→ Creates: website/astro/public/version.json
   ├─→ Builds Unity WebGL (includes version.json)
   └─→ Uploads artifact: webgl.zip

3. docker-build job
   ├─→ Downloads artifact: webgl.zip
   ├─→ Copies to: tmp-docker-context/webgl.zip
   └─→ Builds Docker (uses local file, no download!)

4. gh-pages-deploy job (runs in parallel)
   └─→ Deploys webgl.zip to unity-bw.kbve.com
```

**Key insight:** Docker build downloads artifact (step 3) while GitHub Pages deploys (step 4). No dependency on GitHub Pages = No race condition.

---

### Dockerfile Logic

```dockerfile
# 1. Define build arg
ARG EXPECTED_VERSION=""

# 2. Try to copy local file (from CI artifact or local build)
COPY tmp-docker-context/webgl.zi[p] /tmp/ || true
#     └─→ [p] makes it optional (COPY succeeds even if missing)

# 3. Check if local file exists
RUN if [ -f "/tmp/webgl.zip" ]; then
    # Local file exists - use it (CI artifact or local build)
    echo "Using pre-downloaded build"
else
    # No local file - download and verify
    echo "Downloading from GitHub Pages"
    verify-webgl-build.sh "$URL" "$EXPECTED_VERSION"
    # └─→ This will fail if version mismatch!
fi

# 4. Extract and use
RUN unzip /tmp/webgl.zip -d /app/astro/dist/assets/game
```

---

## Testing the Solution

### Test 1: CI Pipeline (Should Succeed)

```bash
# Push version bump to GitHub
git add website/axum/Cargo.toml
git commit -m "test: bump version to test CI"
git push origin main

# Check GitHub Actions
# ✓ Unity build completes
# ✓ Docker build uses artifact
# ✓ No race condition
# ✓ Version matches everywhere
```

### Test 2: Local Build Without Unity (Should Fail)

```bash
# Update version
echo 'version = "1.0.99"' >> website/axum/Cargo.toml

# Try to build
docker build --build-arg EXPECTED_VERSION=1.0.99 .

# Expected result:
# ❌ Fails with: "ERROR: Version Mismatch - Race Condition Detected"
```

### Test 3: Local Build With Unity (Should Succeed)

```bash
# Update version
vim website/axum/Cargo.toml

# Generate version.json
./scripts/generate-version-json.sh

# Build Unity in Editor
# (File > Build Settings > WebGL > Build)

# Build Docker with helper
./scripts/local-docker-build.sh

# Expected result:
# ✓ Detects local build
# ✓ Verifies version
# ✓ Builds successfully
```

---

## Monitoring Version Consistency

### Check Version in Running Container

```bash
docker run -d -p 4321:4321 bugwars:latest

# Check version endpoint
curl http://localhost:4321/version.json
# Should return current Cargo.toml version

# Check Unity game version
# (In game menu or console)
```

### Check Version in webgl.zip

```bash
# Download from GitHub Pages
wget https://unity-bw.kbve.com/webgl.zip

# Check version
unzip -p webgl.zip StreamingAssets/version.json | jq .version
```

### Check Version in GHCR Image

```bash
# Pull image
docker pull ghcr.io/kbve/bugwars:1.0.11

# Run and check
docker run -d -p 4321:4321 ghcr.io/kbve/bugwars:1.0.11
curl http://localhost:4321/version.json
```

---

## Best Practices

### ✅ DO:
- Use CI pipeline for production builds
- Run `generate-version-json.sh` before Unity builds
- Use `local-docker-build.sh` for local development
- Wait for CI to complete before pulling images
- Verify version consistency after deployment

### ❌ DON'T:
- Build Docker locally immediately after version bump
- Skip version verification checks
- Ignore version mismatch warnings
- Use `SKIP_VERSION_CHECK=1` in production
- Manually edit version.json files

---

## Future Improvements

1. **Semantic Release Integration**
   - Auto-bump version based on commit messages
   - Generate changelogs automatically

2. **Version Validation in Axum**
   - Add Rust build-time version check
   - Ensure binary version matches Cargo.toml

3. **Client-Side Version Check**
   - Unity checks `/version.json` on startup
   - Warns if client/server version mismatch

4. **Dashboard**
   - Web UI showing version consistency across all components
   - Alert if versions don't match

---

## Summary

**Race Condition:** ❌ SOLVED

**How:**
1. ✅ CI uses artifacts (no download race)
2. ✅ Version verification (fail fast on mismatch)
3. ✅ Local build helper (guides developers)
4. ✅ Documentation (prevents user error)

**Result:**
- CI builds: **Always safe**
- Local builds: **Protected with clear errors**
- Developers: **Guided to correct approach**

---

**Last Updated:** 2025-11-13
**Version:** 1.0.10
