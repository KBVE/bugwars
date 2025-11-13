# Camera System Upgrade - Character-Aware Camera Configuration

## Overview

The camera system has been upgraded to support multiple character types with different rendering approaches:
- **3D Characters** (**PRIMARY: AdventurerCharacter**) - Full 3D models without billboarding
- **Billboard Sprites** (LEGACY: Samurai, BlankPlayer) - 2D sprites that always face the camera

**AdventurerCharacter is now the primary player character** for the game, with full 3D rendering and third-person camera controls. Legacy billboard characters (Samurai, BlankPlayer) are still supported for backward compatibility.

## What Was Implemented

### Option 1: Interface-Based (Primary) ✅
Characters implement `ICameraPreference` interface to specify their preferred camera configuration.

### Option 2: Tag-Based (Fallback) ✅
Characters can be tagged with `Camera3D` or `CameraBillboard` for automatic camera selection.

### Option 3: Validation & Warnings ✅
The system validates tags and warns developers about misconfigurations.

## Files Changed

### New Files Created

1. **[Assets/Scripts/Core/CameraManager.cs](unity/bugwars/Assets/Scripts/Core/CameraManager.cs)** (Updated)
   - **Lines 1890-1944**: Added `ICameraPreference` interface
   - **Lines 1923-1941**: Added `CameraTags` static class with constants
   - Consolidates all camera-related types in one location

2. **[Assets/Editor/KBVE/CameraManagerEditor.cs](unity/bugwars/Assets/Editor/KBVE/CameraManagerEditor.cs)** (NEW)
   - Editor utilities for CameraManager and ICameraPreference system
   - Auto-adds required tags on Unity startup
   - Provides menu items for validation and auto-fixing:
     - `KBVE/Camera/Ensure Camera Tags` - Add tags to project
     - `KBVE/Camera/Validate Player Tags` - Check all player prefabs
     - `KBVE/Camera/Auto-Fix Player Tags` - Automatically fix mismatched tags

### Files Removed (Consolidation & Legacy Cleanup)

1. **Assets/Scripts/Core/ICameraPreference.cs** ❌ REMOVED → **Moved to CameraManager.cs**
   - Interface consolidated into CameraManager.cs (lines 1897-1917)

2. **Assets/Scripts/Core/README_ICameraPreference.md** ❌ REMOVED
   - Quick reference guide (outdated with consolidation)

3. **Assets/Editor/KBVE/CameraTagSetup.cs** ❌ REMOVED → **Replaced by CameraManagerEditor.cs**
   - Functionality moved to CameraManagerEditor.cs for better organization

4. **Assets/Editor/KBVE/SamuraiTools.cs** ❌ REMOVED
   - Legacy editor sync tool for Samurai billboard character
   - No longer needed with new ICameraPreference system

5. **Assets/Editor/KBVE/BlankPlayerTools.cs** ❌ REMOVED
   - Legacy editor sync tool for BlankPlayer billboard character
   - No longer needed with new ICameraPreference system

**Note**: All camera-related code is now centralized in:
- **Runtime**: `CameraManager.cs` (interface, tags, camera logic)
- **Editor**: `CameraManagerEditor.cs` (validation, auto-fix tools)

### Modified Files

1. **[Assets/BugWars/Prefabs/Character/Adventurers/AdventurerCharacter.cs](unity/bugwars/Assets/BugWars/Prefabs/Character/Adventurers/AdventurerCharacter.cs)**
   - Implements `ICameraPreference`
   - Returns `FreeLookOrbit` camera config (3D third-person)
   - Expected tag: `Camera3D`
   - `UsesBillboarding` = `false`

2. **[Assets/BugWars/Prefabs/Character/Samurai/Samurai.cs](unity/bugwars/Assets/BugWars/Prefabs/Character/Samurai/Samurai.cs)**
   - Implements `ICameraPreference`
   - Returns `CinematicFollow` camera config (billboard optimized)
   - Expected tag: `CameraBillboard`
   - `UsesBillboarding` = `true`

3. **[Assets/BugWars/Prefabs/Character/BlankPlayer/BlankPlayer.cs](unity/bugwars/Assets/BugWars/Prefabs/Character/BlankPlayer/BlankPlayer.cs)**
   - Implements `ICameraPreference`
   - Returns `CinematicFollow` camera config (4-directional sprites)
   - Expected tag: `CameraBillboard`
   - `UsesBillboarding` = `true`

4. **[Assets/Scripts/Entity/EntityManager.cs](unity/bugwars/Assets/Scripts/Entity/EntityManager.cs)**
   - Updated `RequestCameraFollow()` to check for `ICameraPreference`
   - Added `ValidateCameraTags()` for tag validation with warnings
   - Added `DetermineCameraConfigFromTags()` for fallback tag-based detection
   - Provides detailed logging for debugging camera setup

## How It Works

### Player Detection Flow

When an entity is registered, EntityManager checks if it's a player using:

```csharp
// EntityManager.RegisterEntity() - Line 270-275
bool isPlayer = entity.CompareTag("Player") ||
              entity.CompareTag("Camera3D") ||      // 3D characters like AdventurerCharacter
              entity.CompareTag("CameraBillboard") || // Billboard sprites like Samurai
              entity is Player.Player ||             // AdventurerCharacter inherits from Player
              entity is Character.Samurai ||         // Samurai extends Entity directly
              entity is Character.BlankPlayer;       // BlankPlayer extends Player
```

**This means AdventurerCharacter is automatically detected as the player** because it inherits from `Player`.

### Camera Selection Flow

```
Player Spawns (AdventurerCharacter, Samurai, or BlankPlayer)
    ↓
EntityManager.RegisterEntity() detects it's a player
    ↓
EntityManager.SetPlayer() marks it as the active player
    ↓
EntityManager.RequestCameraFollow(Transform) is called
    ↓
Check: Does player implement ICameraPreference?
    ↓
YES                                    NO
↓                                      ↓
Use ICameraPreference                  Check GameObject tags
GetPreferredCameraConfig()             ↓
    ↓                                  Camera3D → FreeLookOrbit
ValidateCameraTags()                   CameraBillboard → CinematicFollow
(warns if mismatch)                    No tag → Default (FreeLookOrbit + warning)
    ↓                                  ↓
Fire CameraManager event ← ← ← ← ← ← ←
    ↓
Camera follows the active player (AdventurerCharacter)
```

### Example: AdventurerCharacter Flow

```
1. AdventurerCharacter spawns in scene
   ↓
2. AdventurerCharacter.Awake() sets enableBillboard = false
   ↓
3. EntityManager.RegisterEntity() is called
   ↓
4. EntityManager detects: "entity is BugWars.Entity.Player.Player" → TRUE
   ↓
5. EntityManager.SetPlayer(adventurerCharacter)
   ↓
6. EntityManager.RequestCameraFollow(adventurerCharacter.transform)
   ↓
7. Check: adventurerCharacter implements ICameraPreference? → YES
   ↓
8. adventurerCharacter.GetPreferredCameraConfig() returns FreeLookOrbit
   ↓
9. ValidateCameraTags() checks if tag = "Camera3D" → warns if mismatch
   ↓
10. CameraManager receives FreeLookOrbit config
    ↓
11. Camera follows AdventurerCharacter with 3D controls (no billboarding!)
```

### Camera Configurations

#### FreeLookOrbit (3D Characters)
- Mouse-driven yaw/pitch controls
- Collision detection with environment
- Adjustable camera distance
- Smooth follow with damping
- Perfect for 3D models

#### CinematicFollow (Billboard Sprites)
- Auto-follow with velocity lookahead
- Fixed 25-35° downward viewing angle
- Locked rotation (critical for 2D sprites)
- Orthographic projection option
- HD-2D style (like Octopath Traveler)

## Unity Editor Setup

### When Unity Starts

The `CameraManagerEditor` script automatically:
1. Adds `Camera3D` and `CameraBillboard` tags to the project
2. Logs confirmation when tags are added

### Manual Validation

All camera tools are now under the **`KBVE → Camera`** menu:

1. **Ensure Camera Tags**
   - Menu: `KBVE → Camera → Ensure Camera Tags`
   - Adds required camera tags to the project
   - Safe to run multiple times (idempotent)

2. **Validate Player Tags**
   - Menu: `KBVE → Camera → Validate Player Tags`
   - Checks all character prefabs in `Assets/BugWars/Prefabs/Character`
   - Reports mismatches and missing tags
   - Shows which prefabs implement `ICameraPreference`

3. **Auto-Fix Player Tags**
   - Menu: `KBVE → Camera → Auto-Fix Player Tags`
   - Automatically updates prefab tags based on `ICameraPreference.GetExpectedCameraTag()`
   - Only affects prefabs that implement `ICameraPreference`
   - Creates automatic backup before modifying prefabs

## Required Unity Tags

These tags will be automatically added when Unity opens:

- `Camera3D` - For 3D characters (AdventurerCharacter)
- `CameraBillboard` - For billboard sprites (Samurai, BlankPlayer)
- `Player` - Standard Unity tag for player identification

## Updating Prefabs

### For Existing Prefabs

1. **Automatic (Recommended)**
   - Unity Menu: `KBVE → Setup → Auto-Fix Player Tags`
   - This will automatically tag all prefabs correctly

2. **Manual**
   - Select the prefab in Unity
   - In Inspector, set Tag dropdown to:
     - `Camera3D` for 3D characters
     - `CameraBillboard` for billboard sprites

### For New Characters

When creating a new character:

1. **Inherit from appropriate base class**
   - `Player` or `Entity`

2. **Implement ICameraPreference**
   ```csharp
   public class MyCharacter : Player, ICameraPreference
   {
       public CameraFollowConfig GetPreferredCameraConfig(Transform target)
       {
           // For 3D characters
           return CameraFollowConfig.FreeLookOrbit(target);

           // OR for billboard sprites
           return CameraFollowConfig.CinematicFollow(target);
       }

       public string GetExpectedCameraTag()
       {
           return CameraTags.Camera3D; // or CameraTags.CameraBillboard
       }

       public bool UsesBillboarding => false; // or true for billboards
   }
   ```

3. **Set the prefab tag**
   - Use `KBVE → Setup → Auto-Fix Player Tags` menu item
   - OR manually set in Inspector

## Console Warnings & Debugging

### Expected Warnings (informational)

When spawning a character, you'll see logs like:
```
[EntityManager] Using custom camera config from ICameraPreference for AdventurerCharacter (Billboard: False, Expected Tag: Camera3D)
```

### Tag Mismatch Warning

If prefab tag doesn't match `GetExpectedCameraTag()`:
```
[EntityManager] Tag Mismatch: Player 'AdventurerCharacter' expects tag 'Camera3D' but has tag 'Player'.
Please update the prefab tags in the Unity Editor.
[EntityManager] To fix: Select 'AdventurerCharacter' prefab and set Tag to 'Camera3D'
```

**Fix**: Run `KBVE → Setup → Auto-Fix Player Tags`

### Missing ICameraPreference Warning

If character doesn't implement the interface:
```
[EntityManager] Player 'OldCharacter' does not implement ICameraPreference.
Using tag-based fallback. Consider implementing ICameraPreference for explicit camera control.
```

**Fix**: Implement `ICameraPreference` on the character class

### No Camera Tag Warning

If character has neither interface nor appropriate tags:
```
[EntityManager] No camera-specific tag found on 'OldCharacter' (Current tag: 'Player').
Using default FreeLookOrbit camera. Consider adding 'Camera3D' or 'CameraBillboard' tag, or implement ICameraPreference.
```

**Fix**: Add appropriate tag to prefab or implement `ICameraPreference`

## Testing

1. **Test 3D Character (AdventurerCharacter)**
   - Camera should use perspective projection
   - Mouse controls camera rotation
   - Collision detection prevents clipping through walls
   - No billboarding applied

2. **Test Billboard Character (Samurai/BlankPlayer)**
   - Camera should use orthographic projection (optional)
   - Fixed camera angle (25-35° down)
   - Sprite always faces camera
   - No mouse camera control

3. **Test Tag Validation**
   - Spawn character with wrong tag → should see warning
   - Run Auto-Fix → warnings should disappear

## Benefits

### For Developers

1. **Type Safety**: Interface ensures all required methods are implemented
2. **Intellisense**: IDE autocomplete helps implement camera preferences
3. **Validation**: Automatic tag validation catches configuration errors
4. **Flexibility**: Each character controls its own camera behavior

### For the Game

1. **Performance**: Characters specify optimal camera settings upfront
2. **Polish**: Each character type gets appropriate camera feel
3. **Maintainability**: Clear separation between 3D and billboard rendering
4. **Extensibility**: Easy to add new character types with custom cameras

## Future Enhancements

Potential additions:

1. **Per-Class Camera Overrides**
   - Different camera distances for melee vs ranged characters
   - Custom FOV for specific character abilities

2. **Dynamic Camera Switching**
   - Switch between camera modes during gameplay (e.g., aim mode)
   - Context-sensitive camera adjustments

3. **Camera Presets**
   - More preset configurations (TopDown, Isometric, etc.)
   - Save custom presets in ScriptableObjects

4. **Advanced Validation**
   - Build-time validation to prevent shipping with warnings
   - Unit tests for camera configuration

## Questions?

If you encounter issues:

1. Check Console for warning messages
2. Run `KBVE → Validation → Validate Player Tags`
3. Use `KBVE → Setup → Auto-Fix Player Tags` to auto-correct
4. Verify character implements `ICameraPreference`
5. Check prefab tag matches `GetExpectedCameraTag()`

---

**Implementation Date**: 2025-11-13
**Author**: Claude (Sonnet 4.5)
**Status**: ✅ Complete & Ready for Testing
