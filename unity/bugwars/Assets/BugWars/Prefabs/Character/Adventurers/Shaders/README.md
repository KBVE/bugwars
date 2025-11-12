# PixelArtCharacter Shader

Custom Unity shader that transforms 3D character models into retro pixel art aesthetic.

## Overview

This shader gives 3D models a pixelated, retro look similar to classic pixel art games, while maintaining the flexibility and efficiency of 3D rendering. It uses texture pixelation, toon-style lighting, and Fresnel-based edge detection to create subtle outlines without overdoing it.

## Technical Details

### Rendering Features

#### Single-Pass Rendering (Forward Lit)
- **UV Pixelation**: Quantizes texture coordinates for retro texture look
- **Toon Lighting**: 4-step quantized lighting (0.6 ambient minimum)
- **Fresnel Edge Detection**: Detects edges where surface normals face away from camera
  - Inspired by Three.js `normalEdgeStrength` parameter
  - Configurable strength (default 0.3 for subtle effect)
  - Power function (^3) to sharpen edge detection
- **Lighting Support**: Fully lit with URP lighting system

### Key Techniques

#### UV Pixelation
Reduces texture resolution for pixel art look:
```hlsl
float2 pixelatedUV = floor(input.uv * _TexturePixelation) / _TexturePixelation;
```

#### Toon Lighting (4-Step Quantization)
Creates stepped lighting with discrete levels:
```hlsl
float lighting = max(0.6, NdotL);
lighting = floor(lighting * 4.0) / 4.0;
```

#### Fresnel Edge Detection
Detects edges based on view angle to surface normal:
```hlsl
float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
fresnel = pow(fresnel, 3.0);  // Sharpen edges
float edgeFactor = step(1.0 - _OutlineStrength, fresnel);
color = lerp(color, _OutlineColor.rgb, edgeFactor * _OutlineStrength);
```

## Shader Properties

| Property | Type | Range | Default | Description |
|----------|------|-------|---------|-------------|
| `_BaseMap` | Texture2D | - | white | Character texture (URP) |
| `_BaseColor` | Color | - | (1,1,1,1) | Tint color (URP) |
| `_TexturePixelation` | Float | 1 - 64 | 16 | Texture pixelation level |
| `_OutlineStrength` | Float | 0 - 1 | 0.3 | Edge detection strength (Fresnel-based) |
| `_OutlineColor` | Color | - | (0,0,0,1) | Outline color |

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
- Increase `_TexturePixelation` (e.g., 32 or 64)

**For smoother look:**
- Decrease `_TexturePixelation` (e.g., 8 or 4)

**For thicker outlines:**
- Increase `_OutlineStrength` (e.g., 0.5 or 0.7)
- Note: Uses Fresnel-based edge detection (inspired by Three.js normalEdgeStrength)

**For subtler outlines:**
- Decrease `_OutlineStrength` (e.g., 0.1 or 0.2)

**For different lighting styles:**
- Currently uses 4-step quantized lighting with 0.6 ambient
- Modify lighting steps in shader code for different toon shading levels

## Shader Tags

```glsl
Tags { "RenderType"="Opaque" "Queue"="Geometry" }
```

- **RenderType**: Opaque (no transparency)
- **Queue**: Geometry (standard rendering queue)
- **LOD**: 100 (standard quality level)

## Compatibility

- **Unity Version**: Requires URP (Universal Render Pipeline)
- **Lighting**: UniversalForward lighting mode
- **Platform**: Works on all platforms that support URP and HLSL shaders

## Performance Considerations

- Single-pass rendering (efficient)
- Simple mathematical operations (floor, pow, lerp)
- No expensive texture lookups beyond base map
- Suitable for mobile with moderate poly counts
- URP Forward rendering path

## Related Files

- **Shader**: `PixelArtCharacter.shader`
- **Materials**: `../Materials/*_Material.mat`
- **Editor Tool**: `../Editor/AdventurerPrefabCreator.cs`
- **Documentation**: `../README.md`, `../QUICK_REFERENCE.md`

## Inspiration

Based on retro pixel art aesthetics and Three.js WebGL postprocessing techniques (specifically the `normalEdgeStrength` parameter from RenderPixelatedPass), adapted for Unity's URP with real-time 3D character rendering using Fresnel-based edge detection.

## License

See project LICENSE file.
