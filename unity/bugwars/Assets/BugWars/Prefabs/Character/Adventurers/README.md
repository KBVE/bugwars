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
├── Rig_Medium/                    # Animation rig files
│   ├── Rig_Medium_General.fbx     # General character rig
│   └── Rig_Medium_MovementBasic.fbx # Movement animations
├── Materials/                     # Character materials
│   ├── Knight_Material.mat
│   ├── Barbarian_Material.mat
│   ├── Mage_Material.mat
│   ├── Rogue_Material.mat
│   └── Ranger_Material.mat
├── Editor/                        # Editor utilities
│   └── AdventurerPrefabCreator.cs
└── AdventurerCharacter.cs         # Character controller component
```

## Quick Start

### Creating Character Prefabs

1. **Open Unity Editor**
2. **Use the Menu Command:**
   - Go to `BugWars > Create Adventurer Prefabs`
   - This will automatically create prefabs for all characters

3. **Create a Test Character:**
   - Go to `BugWars > Setup Single Character (Test)`
   - This will create a Knight character in your scene for testing

### Manual Setup

If you prefer to create characters manually:

1. **Drag an FBX model** from `fbx/` folder into the scene
2. **Add the AdventurerCharacter component** to the GameObject
3. **Assign the corresponding material** from the `Materials/` folder
4. **Set the character class** in the inspector (Knight, Mage, etc.)

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

## Next Steps: Pixel Shader

After setting up the characters, the next phase will be creating a custom pixel shader to give these 3D models a pixelated look that blends with the game's art style. This will involve:

1. Creating a custom shader that quantizes the model's position/normals
2. Applying pixelation effects to the texture sampling
3. Implementing outline/edge detection for a retro aesthetic
4. Optimizing the shader for performance

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

### Characters appear without textures
- Ensure materials are assigned in the AdventurerCharacter component
- Check that texture import settings are correct (Read/Write enabled if needed)

### Animation not playing
- Verify Animator component is present and has a controller assigned
- Check that the rig is properly configured in FBX import settings
- Ensure animation clips are imported from the Rig_Medium files

### Characters appear too large/small
- Adjust the FBX import scale in the model's import settings
- Default Unity scale should work, but may need tweaking based on game requirements

## License
See `License.txt` for model licensing information.
