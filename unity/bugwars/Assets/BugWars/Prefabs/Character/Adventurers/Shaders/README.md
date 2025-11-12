# PixelArtCharacter Shader

Custom Unity shader that transforms 3D character models into retro pixel art aesthetic.

## Overview

This shader gives 3D models a pixelated, retro look similar to classic pixel art games, while maintaining the flexibility and efficiency of 3D rendering. It uses a two-pass rendering system to create outlines and applies various quantization and pixelation effects.

## Technical Details

### Rendering Passes

#### Pass 1: Outline (Front-face culling)
- **Purpose**: Creates a solid outline around the character
- **Method**: Vertex expansion along normals with front-face culling
- **Customizable**: Outline width and color

#### Pass 2: Main Rendering (Back-face culling)
- **Purpose**: Renders the pixelated character with lighting
- **Includes**:
  - Vertex quantization for blocky geometry
  - UV pixelation for retro textures
  - Toon-style stepped lighting (4 levels)
  - Color palette quantization (16 levels per channel)
  - Shadow support via `AutoLight.cginc`

### Key Functions

#### `QuantizePosition(float3 pos, float gridSize)`
Snaps vertex positions to a grid, creating a blocky low-poly aesthetic:
```glsl
return floor(pos / gridSize) * gridSize;
```

#### `PixelateUV(float2 uv, float pixelation)`
Reduces texture resolution for pixel art look:
```glsl
return floor(uv * pixelation) / pixelation;
```

#### `ToonShading(float3 normal, float3 lightDir, int steps)`
Creates stepped lighting with discrete levels:
```glsl
float lighting = max(0.0, NdotL);
lighting = floor(lighting * steps) / steps;
```

## Shader Properties

| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `_MainTex` | Texture2D | - | white | Character texture |
| `_Color` | Color | - | (1,1,1,1) | Tint color |
| `_PixelSize` | Float | 0.001 - 0.1 | 0.02 | Overall pixelation amount |
| `_TexturePixelation` | Float | 1 - 64 | 8 | Texture pixelation level |
| `_OutlineWidth` | Float | 0 - 0.1 | 0.01 | Outline thickness |
| `_OutlineColor` | Color | - | (0,0,0,1) | Outline color |
| `_VertexQuantization` | Float | 0 - 1 | 0.5 | Amount of vertex snapping |
| `_QuantizationSize` | Float | 0.01 - 1.0 | 0.1 | Grid cell size for snapping |
| `_AmbientStrength` | Float | 0 - 1 | 0.3 | Ambient light contribution |
| `_DiffuseStrength` | Float | 0 - 1 | 0.7 | Directional light contribution |

## Usage

### Automatic Application
The shader is automatically applied to all character materials when running:
```
KBVE > Characters > Sync Adventurers
```

### Manual Application
1. Select a character material in the Project window
2. In the Inspector, change Shader dropdown to `BugWars/PixelArtCharacter`
3. Assign the character texture to `_MainTex`
4. Adjust parameters as needed

### Tuning Tips

**For more pixelated look:**
- Increase `_TexturePixelation` (e.g., 16 or 32)
- Increase `_VertexQuantization` (closer to 1.0)
- Increase `_QuantizationSize` (e.g., 0.2 or 0.3)

**For smoother look:**
- Decrease `_TexturePixelation` (e.g., 4 or 2)
- Decrease `_VertexQuantization` (closer to 0.0)
- Decrease `_QuantizationSize` (e.g., 0.05)

**For thicker outlines:**
- Increase `_OutlineWidth` (e.g., 0.02 or 0.03)

**For different lighting styles:**
- Adjust `_AmbientStrength` for base brightness
- Adjust `_DiffuseStrength` for light/shadow contrast
- Modify the `steps` parameter in `ToonShading()` function (currently 4)

## Shader Tags

```glsl
Tags { "RenderType"="Opaque" "Queue"="Geometry" }
```

- **RenderType**: Opaque (no transparency)
- **Queue**: Geometry (standard rendering queue)
- **LOD**: 100 (standard quality level)

## Compatibility

- **Unity Version**: Compatible with Unity's built-in render pipeline
- **Lighting**: ForwardBase lighting mode with shadow support
- **Platform**: Works on all platforms that support CG/HLSL shaders

## Performance Considerations

- Two-pass rendering (outline + main) has moderate performance cost
- Quantization operations are simple and efficient
- Shadow support included via `SHADOW_ATTENUATION`
- Suitable for mobile with moderate poly counts

## Related Files

- **Shader**: `PixelArtCharacter.shader`
- **Materials**: `../Materials/*_Material.mat`
- **Editor Tool**: `../Editor/AdventurerPrefabCreator.cs`
- **Documentation**: `../README.md`, `../QUICK_REFERENCE.md`

## Inspiration

Based on retro pixel art aesthetics and Three.js WebGL postprocessing techniques, adapted for Unity's built-in render pipeline with real-time 3D character rendering.

## License

See project LICENSE file.
