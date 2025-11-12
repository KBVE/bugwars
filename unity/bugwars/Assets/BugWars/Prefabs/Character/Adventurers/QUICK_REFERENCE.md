# Adventurer Characters - Quick Reference

## The One Command You Need

### KBVE > Characters > Sync Adventurers [ALL-IN-ONE]

**This is the ONLY command you need to run!**

**What it does (in one click):**
1. **PHASE 1:** Loads all 7 animation rigs
   - Rig_Medium_General.fbx (general poses & idle)
   - Rig_Medium_MovementBasic.fbx (walk, run)
   - Rig_Medium_MovementAdvanced.fbx (jump, climb, etc.)
   - Rig_Medium_CombatMelee.fbx (melee attacks)
   - Rig_Medium_CombatRanged.fbx (ranged attacks)
   - Rig_Medium_Simulation.fbx (interactions)
   - Rig_Medium_Special.fbx (unique animations)

2. **PHASE 2:** Creates & Syncs all prefabs
   - Creates missing prefabs (preserves existing GUIDs - won't break references!)
   - Creates Animator Controllers with default Idle state and movement parameters
   - Syncs **all 7 rig references** to each character
   - Assigns materials
   - Links Animator components with controllers
   - Sets up SkinnedMeshRenderer references

3. **PHASE 3:** Verifies everything
   - Checks all components
   - Validates rig assignments
   - Confirms materials are applied

4. **PHASE 4:** Applies Pixel Art Shader
   - Loads PixelArtCharacter shader
   - Applies shader to all character materials
   - Sets default pixelation parameters
   - Creates retro pixel art aesthetic with outlines

**Expected Output:**
```
=== KBVE Adventurer Sync - All-In-One ===
╔════════════════════════════════════════════════════╗
║     KBVE ADVENTURER SYNC - COMPLETE REPORT        ║
╚════════════════════════════════════════════════════╝

PHASE 1: Loading Animation Rigs
--------------------------------
✓ Loaded: Rig_Medium_General
✓ Loaded: Rig_Medium_MovementBasic

PHASE 2: Creating & Syncing Prefabs
------------------------------------
✓ Knight: Synced successfully
✓ Barbarian: Synced successfully
✓ Mage: Synced successfully
✓ Rogue: Synced successfully
✓ Rogue_Hooded: Synced successfully

Sync Results: 5 succeeded, 0 failed

PHASE 3: Verification Check
---------------------------
Knight:
  ✓ AdventurerCharacter component present
  ✓ Material: Knight_Material
  ✓ Animator present
  ✓ General rig: Rig_Medium_General
  ✓ Movement rig: Rig_Medium_MovementBasic
  ✓ Mesh renderer present
[... similar for other characters ...]

PHASE 4: Applying Pixel Art Shader
-----------------------------------
✓ Loaded shader: BugWars/PixelArtCharacter
✓ Applied pixel shader to: Knight_Material.mat
✓ Applied pixel shader to: Barbarian_Material.mat
✓ Applied pixel shader to: Mage_Material.mat
✓ Applied pixel shader to: Rogue_Material.mat
✓ Applied pixel shader to: Ranger_Material.mat

Pixel Shader Applied: 5 material(s) updated

╔════════════════════════════════════════════════════╗
║  ✓ SUCCESS - All adventurers ready to use!        ║
╚════════════════════════════════════════════════════╝
```

**Plus:** You'll get a popup dialog confirming success!

---

## Secondary Commands

### KBVE > Characters > Configure FBX Import Settings
**Use this if Avatars are showing as "None"**
- Configures all character FBX files to generate humanoid Avatars
- Sets Animation Type to Humanoid
- Sets Avatar Definition to "Create From This Model"
- Auto-reimports FBX files
- After running, sync again to assign Avatars

### KBVE > Characters > Apply Pixel Shader to Materials
**Standalone command to apply pixel shader**
- Applies PixelArtCharacter shader to all character materials
- Sets default pixelation parameters
- Useful if you want to re-apply shader settings or if shader was updated
- Note: This is automatically done during "Sync Adventurers"

### KBVE > Characters > Setup Test Character
Creates a Knight character in the scene at (0, 0, 0) for testing.

## Character Files

### Prefabs
All located in: `Assets/BugWars/Prefabs/Character/Adventurers/`
- Knight_Prefab.prefab
- Barbarian_Prefab.prefab
- Mage_Prefab.prefab
- Rogue_Prefab.prefab
- Rogue_Hooded_Prefab.prefab

### Materials
Located in: `Assets/BugWars/Prefabs/Character/Adventurers/Materials/`
- Knight_Material.mat → knight_texture.png
- Barbarian_Material.mat → barbarian_texture.png
- Mage_Material.mat → mage_texture.png
- Rogue_Material.mat → rogue_texture.png
- Ranger_Material.mat → ranger_texture.png

### Animation Rigs
Located in: `Assets/BugWars/Prefabs/Character/Adventurers/Rig_Medium/`
- **Rig_Medium_General.fbx** - General character rig with poses
- **Rig_Medium_MovementBasic.fbx** - Movement animations

## Common Issues

### Issue: Avatar shows as "None" ⚠️ MOST COMMON
**This prevents animations from working!**

**Solution:**
1. Run `KBVE > Characters > Configure FBX Import Settings`
2. Wait for FBX files to reimport
3. Run `KBVE > Characters > Sync Adventurers`
4. Avatar will now be assigned

### Issue: "General rig not assigned"
**Solution:** Run `KBVE > Characters > Sync Adventurers`

### Issue: "Material not showing on character"
**Solution:**
1. Check Materials folder has the correct .mat files
2. Run `KBVE > Characters > Sync Adventurers`
3. Verify texture files are in fbx/ folder

### Issue: "Animator not present"
**Solution:** The FBX model may need to be re-imported with animation enabled. Run sync after reimporting.

### Issue: Prefab missing entirely
**Solution:** Run `KBVE > Characters > Sync Adventurers` - it will create missing prefabs automatically

## Workflow

### Initial Setup (First Time)
1. Open Unity project
2. Run `KBVE > Characters > Sync Adventurers`
3. Done! All 5 characters are ready

**That's it. Seriously.**

### After Making Changes to FBX Files
1. Unity will auto-reimport
2. Run `KBVE > Characters > Sync Adventurers`
3. Everything is re-synced and verified

### Adding Characters to Scene
1. Drag any prefab from Adventurers folder into scene
2. Position as needed
3. Character is ready to use!

### Quick Testing
1. Run `KBVE > Characters > Setup Test Character`
2. A Knight appears at (0, 0, 0)
3. Test animations, materials, etc.

## Pixel Art Shader

The characters use a custom **PixelArtCharacter** shader that transforms 3D models into retro pixel art:

### Features
- **Vertex Quantization** - Snaps vertices to a grid for blocky low-poly look
- **Texture Pixelation** - Reduces texture resolution for pixel art aesthetic
- **Outline/Edge Detection** - Black outlines around characters (two-pass rendering)
- **Toon Lighting** - Stepped lighting with 4 discrete levels
- **Color Quantization** - Reduces color palette to 16 levels per channel for retro look

### Shader Parameters (Adjustable in Material Inspector)
- **Pixel Size** (0.001 - 0.1) - Controls overall pixelation amount (default: 0.02)
- **Texture Pixelation** (1 - 64) - How pixelated textures appear (default: 8x)
- **Outline Width** (0 - 0.1) - Thickness of character outline (default: 0.01)
- **Outline Color** - Color of the outline (default: black)
- **Vertex Quantization** (0 - 1) - How much geometry is snapped to grid (default: 0.5)
- **Quantization Grid Size** (0.01 - 1.0) - Size of snap grid (default: 0.1)
- **Ambient Strength** (0 - 1) - Ambient lighting amount (default: 0.3)
- **Diffuse Strength** (0 - 1) - Directional lighting amount (default: 0.7)

### Location
`Assets/BugWars/Prefabs/Character/Adventurers/Shaders/PixelArtCharacter.shader`

### Auto-Applied
The shader is **automatically applied** during "Sync Adventurers" command (Phase 4).

## File Structure

```
Adventurers/
├── fbx/                      # Source FBX files and textures
│   ├── [Character].fbx
│   ├── [character]_texture.png
│   └── [weapons/props].fbx
├── Rig_Medium/              # Animation rigs
│   ├── Rig_Medium_General.fbx
│   └── Rig_Medium_MovementBasic.fbx
├── Materials/               # Character materials
│   └── [Character]_Material.mat
├── Editor/                  # Editor tools
│   └── AdventurerPrefabCreator.cs
├── [Character]_Prefab.prefab # Ready-to-use prefabs
├── AdventurerCharacter.cs   # Character controller
├── AdventurerCharacterData.cs # ScriptableObject data
└── README.md               # Full documentation
```

---

## TL;DR

**One Command Does Everything:**
```
KBVE > Characters > Sync Adventurers
```

- Creates prefabs (if needed)
- Syncs all rigs
- Assigns materials
- Verifies everything
- Won't break existing GUIDs

**You're done. Go make games.** 🎮
