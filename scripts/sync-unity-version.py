#!/usr/bin/env python3
"""
Sync Unity version from Cargo.toml (source of truth)

This script:
1. Reads version from website/axum/Cargo.toml
2. Updates Unity's ProjectSettings.asset with the correct version
3. Updates companyName, productName, and productVersion for WebGL builds

Usage:
    python scripts/sync-unity-version.py
"""

import re
import sys
from pathlib import Path


def get_cargo_version(cargo_toml_path: Path) -> str | None:
    """Extract version from Cargo.toml"""
    if not cargo_toml_path.exists():
        print(f"ERROR: Cargo.toml not found at {cargo_toml_path}", file=sys.stderr)
        return None

    try:
        content = cargo_toml_path.read_text()
        match = re.search(r'^version\s*=\s*"([^"]+)"', content, re.MULTILINE)
        if match:
            return match.group(1)
        else:
            print("ERROR: Version field not found in Cargo.toml", file=sys.stderr)
            return None
    except Exception as e:
        print(f"ERROR: Failed to read Cargo.toml: {e}", file=sys.stderr)
        return None


def parse_version_code(version: str) -> int:
    """Convert semantic version to integer code (e.g., 1.2.3 -> 10203)"""
    match = re.match(r'^(\d+)\.(\d+)\.(\d+)', version)
    if not match:
        print(f"WARNING: Could not parse version components from: {version}", file=sys.stderr)
        return 0

    major, minor, patch = map(int, match.groups())
    return major * 10000 + minor * 100 + patch


def update_unity_project_settings(project_settings_path: Path, version: str) -> bool:
    """Update Unity ProjectSettings.asset with version info"""
    if not project_settings_path.exists():
        print(f"ERROR: ProjectSettings.asset not found at {project_settings_path}", file=sys.stderr)
        return False

    try:
        content = project_settings_path.read_text()

        # Update bundleVersion
        content = re.sub(
            r'bundleVersion:\s*[^\n]*',
            f'bundleVersion: {version}',
            content
        )

        # Update Android bundleVersionCode
        version_code = parse_version_code(version)
        content = re.sub(
            r'(AndroidBundleVersionCode:\s*)\d+',
            f'\\g<1>{version_code}',
            content
        )

        # Update iOS buildNumber
        content = re.sub(
            r'(buildNumber:\s*)[^\n]*',
            f'\\g<1>{version}',
            content
        )

        # Update company name to fix WebGL warnings
        content = re.sub(
            r'companyName:\s*[^\n]*',
            'companyName: KBVE',
            content
        )

        # Update product name to fix WebGL warnings
        content = re.sub(
            r'productName:\s*[^\n]*',
            'productName: BugWars',
            content
        )

        # Write back
        project_settings_path.write_text(content)

        print(f"✓ Unity version synced successfully: {version}")
        print(f"✓ Bundle version code: {version_code}")
        print(f"✓ Company: KBVE")
        print(f"✓ Product: BugWars")

        return True

    except Exception as e:
        print(f"ERROR: Failed to update ProjectSettings.asset: {e}", file=sys.stderr)
        return False


def main():
    """Main entry point"""
    # Determine project root (script is in scripts/ folder)
    script_dir = Path(__file__).parent
    project_root = script_dir.parent

    # Paths
    cargo_toml_path = project_root / "website" / "axum" / "Cargo.toml"
    project_settings_path = project_root / "unity" / "bugwars" / "ProjectSettings" / "ProjectSettings.asset"

    print("=" * 60)
    print("Unity Version Sync from Cargo.toml")
    print("=" * 60)

    # Get version from Cargo.toml
    version = get_cargo_version(cargo_toml_path)
    if not version:
        sys.exit(1)

    print(f"Source version (Cargo.toml): {version}")

    # Update Unity project settings
    if update_unity_project_settings(project_settings_path, version):
        print("\n✓ Version sync completed successfully!")
        sys.exit(0)
    else:
        print("\n✗ Version sync failed!", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
