# Samurai Character Setup Guide

## Overview

The Samurai character system uses:
- **Sprite Atlas**: Single texture containing all animation frames
- **JSON Atlas Data**: Frame positions and animation metadata
- **Custom URP Shader**: UV-based frame rendering with sprite flipping and billboard effects
- **Entity System**: Extends base Entity class for player character functionality
- **Automatic Sprite Flipping**: Camera-relative sprite flipping based on movement direction
- **Billboard Effects**: Enhanced shader support for stretching and size preservation

## Files Created

```
Samurai/
├── generate_spritesheet.py      # Python script to generate atlas
├── SamuraiAtlas.png              # Generated sprite sheet (2048x512)
├── SamuraiAtlas.json             # Generated frame data
├── SamuraiAnimatedSprite.shader  # Custom URP shader
├── SpriteAtlasData.cs            # JSON data structures
├── Samurai.cs                    # Main player character class
└── SETUP.md                      # This file
```

## Unity Setup Instructions

### 1. Install Dependencies

Ensure Newtonsoft.Json is installed in your Unity project:
- Window → Package Manager
- Search for "Json.NET" or "Newtonsoft.Json"
- Install if not already present

### 2. Import the Sprite Sheet

1. Select `SamuraiAtlas.png` in Unity
2. In the Inspector:
   - **Texture Type**: Sprite (2D and UI)
   - **Sprite Mode**: Single
   - **Pixels Per Unit**: 128 (matches frame size)
   - **Filter Mode**: Point (no filter) for pixel art
   - **Compression**: None
   - **Max Size**: 2048
   - Click **Apply**

### 3. Create Material

1. Right-click in Project → Create → Material
2. Name it `SamuraiMaterial`
3. Set **Shader** to `BugWars/SamuraiAnimatedSprite`
4. Assign `SamuraiAtlas.png` to the **Sprite Sheet** slot
5. Set **Tint** to white (1,1,1,1)
6. Configure rendering:
   - Src Blend: SrcAlpha
   - Dst Blend: OneMinusSrcAlpha
   - Z Write: Off
   - Cull: Off (for billboards)

### 4. Import JSON Atlas

1. Select `SamuraiAtlas.json` in Unity
2. In the Inspector, ensure it's imported as a **TextAsset**

### 5. Create Samurai GameObject

#### Method A: From Scratch

1. Create Empty GameObject: `GameObject → Create Empty`
2. Name it `Samurai`
3. Add Components:
   - `Rigidbody` (required by Entity)
   - `CapsuleCollider` (required by Entity)
   - `Samurai` script
4. Create child GameObject: `GameObject → Create Empty`
   - Name it `SpriteRenderer`
   - Add `Sprite Renderer` component
   - Set parent to Samurai
   - Position: (0, 0.5, 0) - adjust to your needs

#### Method B: Prefab (Recommended)

Create a prefab in `Assets/BugWars/Prefabs/Character/Samurai/Samurai.prefab`

### 6. Configure Samurai Component

Select the Samurai GameObject and configure in Inspector:

**Entity Properties:**
- Entity Name: "Samurai"
- Health: 100
- Max Health: 100
- Auto Register With Manager: ✓ (automatically registers with EntityManager)

**Billboard Sprite:**
- Sprite Renderer: Drag the child SpriteRenderer
- Enable Billboard: ✓
- Sprite Offset: (0, 0.5, 0)
- Auto Flip Sprite: ✓ (automatically flips sprite based on movement)

**Physics:**
- Move Speed: 5
- Rotation Speed: 720
- (Other physics properties auto-configured)

**Samurai Animation:**
- **Atlas JSON**: Drag `SamuraiAtlas.json`
- **Sprite Material**: Drag `SamuraiMaterial`
- Debug Animation: ☐ (enable for debugging)

### 7. EntityManager Integration

The Samurai character **automatically registers** with the EntityManager on startup:

**Automatic Registration:**
- Entity.cs base class auto-registers all entities on Initialize()
- EntityManager automatically detects Samurai as the player entity
- Player reference is stored in `EntityManager.Instance.Player`
- No manual setup required!

**Accessing the Player:**
```csharp
// Get player entity from anywhere
Entity player = EntityManager.Instance.GetPlayer();

// Check if player is alive
bool isAlive = EntityManager.Instance.IsPlayerAlive();

// Get player position
Vector3 playerPos = EntityManager.Instance.GetPlayerPosition();

// Direct property access
Entity playerEntity = EntityManager.Instance.Player;
```

**EntityManager Features:**
- Tracks all entities in the scene
- Maintains special reference to player entity
- Find entities by name, type, or proximity
- Query alive/dead entities
- Gizmo visualization (player = cyan, entities = green/red)

**Disabling Auto-Registration:**
If you need to manually control registration:
```csharp
// In Inspector: uncheck "Auto Register With Manager"
// Then manually register when needed:
EntityManager.Instance.RegisterEntity(this);
```

### 8. Test in Scene

1. Drag Samurai prefab into scene
2. Ensure there's a Camera with tag "MainCamera"
3. Ensure EntityManager exists in scene (auto-creates if missing)
4. Enter Play mode
5. The character should display the Idle animation
6. Console should show: "[EntityManager] Player entity set: Samurai"
7. Use the Context Menu (right-click on Samurai component) to test:
   - "Debug: Next Animation" - cycles through animations
   - "Debug: Print Atlas Info" - shows loaded data

## Animation System

### Available Animations

The system includes 10 animations:
- **Idle**: 6 frames @ 8 fps
- **Walk**: 8 frames @ 12 fps
- **Run**: 8 frames @ 15 fps
- **Jump**: 12 frames @ 10 fps
- **Attack_1**: 6 frames @ 15 fps
- **Attack_2**: 4 frames @ 15 fps
- **Attack_3**: 3 frames @ 15 fps
- **Shield**: 2 frames @ 6 fps
- **Hurt**: 2 frames @ 12 fps
- **Dead**: 3 frames @ 8 fps

### Playing Animations from Code

```csharp
// Get the Samurai component
var samurai = GetComponent<Samurai>();

// Play an animation
samurai.PlayAnimation("Walk");

// Check current animation
string currentAnim = samurai.GetCurrentAnimation();

// Check if animation exists
if (samurai.HasAnimation("Run"))
{
    samurai.PlayAnimation("Run");
}

// Get all available animations
List<string> anims = samurai.GetAvailableAnimations();
```

## How It Works

### 1. Sprite Sheet Generation

The Python script (`generate_spritesheet.py`):
- Extracts frames from individual PNG strips
- Packs them into a 2048x512 atlas (16x4 grid)
- Generates JSON with frame positions and UV coordinates

### 2. Shader UV Mapping

The custom shader (`SamuraiAnimatedSprite.shader`):
- Receives UV min/max parameters from script
- Remaps mesh UVs (0-1) to specific frame region
- Supports URP lighting and transparency

### 3. Frame Animation

The Samurai class:
- Loads JSON atlas data
- Updates frame timer based on animation FPS
- Sets shader UV parameters via MaterialPropertyBlock
- No draw call overhead (uses property blocks)

### 4. Entity Integration

Samurai extends the Entity base class:
- Health and damage system
- Physics-based movement
- Billboard sprite support
- Collision detection

## Performance Notes

- **Single Draw Call**: All animations use one material
- **No Texture Swapping**: Frames selected via UV mapping
- **Property Blocks**: Per-instance material properties
- **Optimized Atlas**: 2048x512 fits most hardware limits

## Regenerating the Sprite Sheet

If you add/modify animation PNGs:

```bash
cd Assets/BugWars/Prefabs/Character/Samurai
python3 generate_spritesheet.py
```

This will regenerate `SamuraiAtlas.png` and `SamuraiAtlas.json`.

## Troubleshooting

### "Samurai: Atlas JSON not assigned!"
→ Assign `SamuraiAtlas.json` to the Atlas JSON field

### "Failed to parse atlas JSON"
→ Check that Newtonsoft.Json is installed

### Sprite not visible
→ Ensure SpriteRenderer has the SamuraiMaterial assigned
→ Check that the shader is set to BugWars/SamuraiAnimatedSprite

### Animation not playing
→ Enable "Debug Animation" to see frame updates in Console
→ Verify JSON loaded correctly with "Debug: Print Atlas Info"

### Billboard not working
→ Ensure Camera.main exists and is tagged "MainCamera"
→ Check "Enable Billboard" is enabled on Entity

### Wrong frame displayed
→ Check that UV coordinates in JSON are correct
→ Verify shader is receiving UV parameters (check MaterialPropertyBlock)

### Sprite not flipping
→ Ensure "Auto Flip Sprite" is enabled in Entity component
→ Check that shader has _FlipX and _FlipY parameters
→ Verify MaterialPropertyBlock is being used correctly

## Sprite Flipping & Billboard Effects

### Automatic Sprite Flipping

The Entity system includes automatic sprite flipping based on movement direction:

**How it works:**
- Tracks facing direction relative to camera
- Automatically flips sprite when moving left/right
- Uses shader-based flipping (no texture duplication)
- Smooth transitions without performance overhead

**Configuration:**
```csharp
// Enable/disable auto-flip in Inspector or code
entity.autoFlipSprite = true; // default

// Manually control facing direction
entity.SetFacingDirection(1);  // Face right
entity.SetFacingDirection(-1); // Face left

// Get current facing direction
int facing = entity.GetFacingDirection(); // 1 or -1
```

**Camera-Relative Flipping:**
The system uses the camera's right vector to determine left/right movement, ensuring sprites flip correctly regardless of camera angle.

### Manual Sprite Flipping

You can manually flip sprites via the Entity API:

```csharp
// Flip horizontally
entity.SetSpriteFlip(true, false);

// Flip vertically (e.g., for wall-climbing)
entity.SetSpriteFlip(false, true);

// Flip both axes
entity.SetSpriteFlip(true, true);

// No flip
entity.SetSpriteFlip(false, false);
```

### Billboard Effects in Shader

The shader supports additional billboard effects:

**Billboard Stretch:**
Adjust sprite proportions via material properties:
```csharp
Material mat = spriteRenderer.material;
mat.SetVector("_BillboardStretch", new Vector4(1.2f, 1.0f, 1.0f, 0)); // 20% wider
```

**Preserve Size with Distance:**
Keep sprite size constant regardless of camera distance:
```csharp
mat.SetFloat("_PreserveSize", 0.5f); // 0=perspective, 1=constant size
```

### Advanced: Custom Property Block Usage

For derived classes that need additional shader parameters:

```csharp
public class CustomEntity : Entity
{
    private static readonly int CustomParamID = Shader.PropertyToID("_CustomParam");

    protected void UpdateCustomShaderParam(float value)
    {
        // Get the shared property block (includes flip state, UV coords, etc.)
        MaterialPropertyBlock block = GetSpritePropertyBlock();

        // Add your custom parameter
        block.SetFloat(CustomParamID, value);

        // Apply all changes
        ApplySpritePropertyBlock();
    }
}
```

**Important:** Always use `GetSpritePropertyBlock()` and `ApplySpritePropertyBlock()` from the Entity base class to ensure flip parameters and other properties work together.

## Extending the System

### Adding New Animations

1. Add new PNG sprite strip to Samurai folder
2. Run `python3 generate_spritesheet.py`
3. Refresh Unity (Ctrl+R)
4. Animation is automatically available

### Custom Animation Logic

Override or extend animation behavior:

```csharp
public class CustomSamurai : Samurai
{
    private void Update()
    {
        base.Update(); // Keep base animation system

        // Custom logic
        if (isAttacking)
        {
            PlayAnimation("Attack_1");
        }
    }
}
```

### State Machine Integration

The Samurai class can be integrated with Unity's Animator or custom state machines by calling `PlayAnimation()` from state transitions.

## API Reference

### Public Methods

```csharp
// Play animation by name
void PlayAnimation(string animationName)

// Get current animation
string GetCurrentAnimation()

// Check if animation exists
bool HasAnimation(string animationName)

// Get all animations
List<string> GetAvailableAnimations()

// Inherited from Entity
void TakeDamage(float damage)
void Heal(float amount)
float GetHealth()
float GetMaxHealth()
bool IsAlive()

// Sprite flipping (inherited from Entity)
void SetSpriteFlip(bool flipX, bool flipY)
void SetFacingDirection(int direction) // 1 = right, -1 = left
int GetFacingDirection()
```

## Credits

- Character sprites: Samurai sprite pack
- System design: Frame-based UV animation for URP
- Architecture: Entity component system
