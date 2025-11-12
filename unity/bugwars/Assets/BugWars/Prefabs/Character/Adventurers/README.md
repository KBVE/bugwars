# Adventurer Characters Setup

## Overview
This folder contains 3D character models for the BugWars adventurer characters, including:
- **Knight** - Armored melee warrior
- **Barbarian** - Heavy-hitting melee fighter
- **Mage** - Magic-wielding spellcaster
- **Rogue** - Agile stealth character
- **Ranger** - Ranged bow specialist

## Folder Structure

```
Adventurers/
├── fbx/                           # FBX models and textures
│   ├── Knight.fbx
│   ├── Barbarian.fbx
│   ├── Mage.fbx
│   ├── Rogue.fbx
│   ├── Rogue_Hooded.fbx
│   ├── knight_texture.png
│   ├── barbarian_texture.png
│   ├── mage_texture.png
│   ├── rogue_texture.png
│   ├── ranger_texture.png
│   └── [weapons and props].fbx
├── Rig_Medium/                    # Animation rig files (7 total)
│   ├── Rig_Medium_General.fbx     # General character poses & idle
│   ├── Rig_Medium_MovementBasic.fbx # Basic movement (walk, run)
│   ├── Rig_Medium_MovementAdvanced.fbx # Advanced movement (jump, climb, etc.)
│   ├── Rig_Medium_CombatMelee.fbx # Melee combat animations
│   ├── Rig_Medium_CombatRanged.fbx # Ranged combat animations
│   ├── Rig_Medium_Simulation.fbx  # Simulation/interaction animations
│   └── Rig_Medium_Special.fbx     # Special/unique animations
├── Materials/                     # Character materials
│   ├── Knight_Material.mat
│   ├── Barbarian_Material.mat
│   ├── Mage_Material.mat
│   ├── Rogue_Material.mat
│   └── Ranger_Material.mat
├── Animators/                     # Animator Controllers (auto-generated)
│   ├── Knight_Controller.controller
│   ├── Barbarian_Controller.controller
│   ├── Mage_Controller.controller
│   └── Rogue_Controller.controller
├── Editor/                        # Editor utilities
│   └── AdventurerPrefabCreator.cs
├── AdventurerCharacter.cs         # Character controller component
└── [Character]_Prefab.prefab      # Ready-to-use prefabs
```

## Quick Start

### One-Click Setup (All-In-One)

1. **Open Unity Editor**
2. **Run the Sync Command:**
   - Go to `KBVE > Characters > Sync Adventurers`
   - This **single command** handles everything:
     - ✓ Creates any missing prefabs (preserves existing GUIDs)
     - ✓ Creates Animator Controllers with default states
     - ✓ Syncs **all 7 rig references**:
       - General, Movement (Basic & Advanced)
       - Combat (Melee & Ranged)
       - Simulation, Special
     - ✓ Assigns materials and components
     - ✓ Links Animator components with controllers
     - ✓ Verifies all configurations
     - ✓ Provides detailed console report
     - ✓ Shows success/failure dialog
3. **Done!** All 5 characters are ready to use

### Testing Characters

- **Create a Test Character:**
  - Go to `KBVE > Characters > Setup Test Character`
  - Creates a Knight at (0, 0, 0) for immediate testing

## KBVE Menu Commands

All character management is located under `KBVE > Characters`:

| Command | Description |
|---------|-------------|
| **Sync Adventurers** | **[ALL-IN-ONE]** Creates, syncs, and verifies all character prefabs. Applies pixel art shader. Handles everything in one click! |
| **Configure FBX Import Settings** | Configures all character FBX files to generate humanoid Avatars. Run this if Avatars are missing. |
| **Apply Pixel Shader to Materials** | Applies the PixelArtCharacter shader to all character materials. Auto-done during sync. |
| **Setup Test Character** | Creates a test Knight character in the current scene at (0, 0, 0). |

### Manual Setup

If you prefer to create characters manually:

1. **Drag an FBX model** from `fbx/` folder into the scene
2. **Add the AdventurerCharacter component** to the GameObject
3. **Assign the corresponding material** from the `Materials/` folder
4. **Set the character class** in the inspector (Knight, Mage, etc.)
5. **Run `KBVE > Characters > Sync Adventurers`** to automatically assign rig references

## Character Component

The `AdventurerCharacter.cs` component provides:

### Properties
- **Character Class** - The character type (Knight, Mage, etc.)
- **Move Speed** - Movement speed multiplier (default: 5)
- **Rotation Speed** - How fast the character rotates (default: 10)

### Animation Support
- **Animator** - Reference to the Animator component
- **General Rig Reference** - Link to Rig_Medium_General
- **Movement Rig Reference** - Link to Rig_Medium_MovementBasic

### Rendering
- **Mesh Renderer** - Automatically detected SkinnedMeshRenderer
- **Character Material** - Applied material (auto-assigned via prefab creator)

### Public Methods
```csharp
void SetMoveDirection(Vector3 direction)  // Set movement direction
string GetCharacterClass()                 // Get character class name
void SetMaterial(Material material)        // Assign new material
void SetAnimator(RuntimeAnimatorController controller) // Set animator
```

## Animation Setup

The characters use two rig files:
- **Rig_Medium_General.fbx** - Base character rig with idle/combat poses
- **Rig_Medium_MovementBasic.fbx** - Movement animations (walk, run, etc.)

To use animations:
1. Create an Animator Controller
2. Import animation clips from the rig FBX files
3. Set up state machines and transitions
4. Assign the controller to the character's Animator component

## Materials and Textures

Each character has a dedicated texture file in the `fbx/` folder:
- `knight_texture.png`
- `barbarian_texture.png`
- `mage_texture.png`
- `rogue_texture.png`
- `ranger_texture.png`

Materials are created using Unity's Standard shader by default, located in the `Materials/` folder.

## Pixel Art Shader

The characters use a custom **PixelArtCharacter** shader (`Shaders/PixelArtCharacter.shader`) that transforms 3D models into retro pixel art. The shader is **automatically applied** during the "Sync Adventurers" command.

### Features
- **Vertex Quantization** - Snaps vertices to a grid for blocky low-poly look
- **Texture Pixelation** - Reduces texture resolution (default: 8x pixelation)
- **Two-Pass Outline Rendering** - Black outlines around characters for crisp edges
- **Toon Lighting** - 4-level stepped lighting for retro aesthetic
- **Color Quantization** - 16-level color palette reduction per channel

### Adjustable Parameters
All parameters can be tweaked in the Material Inspector:

| Parameter | Range | Default | Description |
|-----------|-------|---------|-------------|
| Pixel Size | 0.001 - 0.1 | 0.02 | Overall pixelation amount |
| Texture Pixelation | 1 - 64 | 8 | Texture pixelation level |
| Outline Width | 0 - 0.1 | 0.01 | Outline thickness |
| Outline Color | Color | Black | Outline color |
| Vertex Quantization | 0 - 1 | 0.5 | Geometry snap to grid amount |
| Quantization Grid Size | 0.01 - 1.0 | 0.1 | Grid cell size |
| Ambient Strength | 0 - 1 | 0.3 | Ambient lighting |
| Diffuse Strength | 0 - 1 | 0.7 | Directional lighting |

### Shader Location
`Assets/BugWars/Prefabs/Character/Adventurers/Shaders/PixelArtCharacter.shader`

### Manual Application
If you need to manually apply or re-apply the shader:
- Run `KBVE > Characters > Apply Pixel Shader to Materials`
- Or assign the shader directly in the Material Inspector

## Weapons and Props

The `fbx/` folder also contains various weapons and props:
- Swords (1-handed and 2-handed)
- Axes (1-handed and 2-handed)
- Bows and crossbows
- Staffs
- Shields (various types)
- Arrows and projectiles
- Misc items (mugs, smoke bombs, etc.)

These can be attached to characters as child objects or held items.

## Troubleshooting

### Avatar shows as "None" in Animator
**This is the most common issue!**

**Solution:**
1. Run `KBVE > Characters > Configure FBX Import Settings`
2. This will configure all FBX files to generate humanoid Avatars
3. Then run `KBVE > Characters > Sync Adventurers` again
4. Avatars will be automatically assigned

**What this does:** Sets FBX import settings to `Animation Type: Humanoid` and `Avatar Definition: Create From This Model`

### Characters appear without textures
- Ensure materials are assigned in the AdventurerCharacter component
- Check that texture import settings are correct (Read/Write enabled if needed)

### Animation not playing
- Verify Animator component is present and has a controller assigned
- **Check that Avatar is assigned** (see above if None)
- Check that the rig is properly configured in FBX import settings
- Ensure animation clips are imported from the Rig_Medium files

### Characters appear too large/small
- Adjust the FBX import scale in the model's import settings
- Default Unity scale should work, but may need tweaking based on game requirements

## License
See `License.txt` for model licensing information.
